using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Mecanismo de roteamento de teclado compartilhado entre <see cref="ClassicWorkspaceWindow"/>
    /// e a janela mínima de diagnóstico (<see cref="KeyboardDiagMiniWindow"/>) — mesmo código nas
    /// duas, para que uma diferença de comportamento entre elas só possa vir do contexto de
    /// hospedagem (como cada janela é aberta/ativada), nunca de uma diferença no mecanismo de
    /// interceptação em si. Ver o comentário no topo de ClassicWorkspaceWindow.xaml.cs para a
    /// causa completa do conflito original com os atalhos do Corel.
    ///
    /// Investigação desta rodada (diagnóstico do usuário mostrou 0 eventos observados mesmo
    /// depois da correção anterior com WH_GETMESSAGE): a correção anterior nunca verificava se
    /// SetWindowsHookEx teve sucesso, nem confirmava que esta janela chegava a receber o foco
    /// nativo do Win32 (GetFocus()) — só o foco lógico do WPF. Duas causas candidatas, ambas
    /// tratadas aqui:
    ///   1. hMod inválido: Marshal.GetHINSTANCE(...) sobre um assembly carregado por reflexão
    ///      (não via LoadLibrary a partir de um arquivo em disco) pode retornar um handle que o
    ///      SetWindowsHookEx recusa. Trocado para IntPtr.Zero, o valor documentado como válido
    ///      para hook de THREAD quando a thread de dwThreadId pertence ao processo atual.
    ///   2. Falta de foco nativo: Window.Show() numa Window WPF hospedada por reflexão dentro do
    ///      processo do Corel não garante que GetFocus() passe a apontar para o HWND desta
    ///      janela — se GetFocus() continuar apontando para a janela do Corel, toda WM_KEYDOWN
    ///      chega ao hook com msg.hwnd = hwnd do Corel, nunca o desta janela. Como o segundo
    ///      passo do mecanismo (TranslateMessage/DispatchMessage sobre uma cópia da mensagem)
    ///      entrega a mensagem ao WndProc de QUEM QUER QUE SEJA msg.hwnd, forçar isso sobre uma
    ///      mensagem com hwnd do Corel não produziria WM_CHAR nenhum neste editor — explicaria
    ///      exatamente o sintoma relatado (nada digitado, contadores em zero). Por isso Install()
    ///      chama Window.Activate() e, em seguida, SetFocus(OwnHwnd) nativamente, e
    ///      ReassertFocus() pode ser chamado de novo a cada Window.Activated (o Corel pode
    ///      retomar o foco sozinho entre uma ativação e outra).
    ///
    /// Os contadores abaixo existem para CONFIRMAR, dentro do Corel real, qual das duas causas
    /// (ou nenhuma) é a real — nunca tratam zero como prova por si só (ver DescribeState()).
    /// Nunca registram o conteúdo digitado.
    /// </summary>
    internal sealed class ThreadKeyboardRouter : IDisposable
    {
        private const int WH_GETMESSAGE = 3;
        private const int PM_REMOVE = 0x0001;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_NULL = 0x0000;

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Msg
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref Win32Msg lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref Win32Msg lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        private readonly Window _window;

        // Mantido num campo para não ser coletado pelo GC enquanto o hook estiver instalado —
        // SetWindowsHookEx não mantém referência gerenciada nenhuma por conta própria.
        private HookProc? _hookProc;
        private IntPtr _hook = IntPtr.Zero;

        public IntPtr OwnHwnd { get; private set; }
        public uint ThreadId { get; private set; }
        public bool InstallOk { get; private set; }
        public int InstallError { get; private set; }
        public bool FocusMatchedOwnHwndAfterActivate { get; private set; }

        public int AnyKeydownOnThread { get; private set; }
        public int MatchedOwnHwnd { get; private set; }
        public int ForcedRoutes { get; private set; }
        public IntPtr LastSeenHwnd { get; private set; }
        public IntPtr LastFocusHwnd { get; private set; }

        /// <summary>Disparado (de dentro do próprio hook, já na thread de UI) sempre que um dos
        /// contadores muda — quem usa isto só precisa atualizar o texto de diagnóstico.</summary>
        public event Action? CountersChanged;

        public ThreadKeyboardRouter(Window window)
        {
            _window = window;
        }

        public void Install()
        {
            OwnHwnd = (PresentationSource.FromVisual(_window) as HwndSource)?.Handle ?? IntPtr.Zero;
            ThreadId = GetCurrentThreadId();

            _hookProc = HookCallback;
            _hook = SetWindowsHookEx(WH_GETMESSAGE, _hookProc, IntPtr.Zero, ThreadId);
            InstallOk = _hook != IntPtr.Zero;
            InstallError = InstallOk ? 0 : Marshal.GetLastWin32Error();

            ReassertFocus();
        }

        /// <summary>Candidato a correção do foco nativo (ver comentário da classe). Seguro
        /// chamar de novo a qualquer momento (ex.: Window.Activated) — só afeta o foco desta
        /// janela, nunca outra.</summary>
        public void ReassertFocus()
        {
            _window.Activate();
            if (OwnHwnd != IntPtr.Zero)
            {
                SetFocus(OwnHwnd);
            }
            FocusMatchedOwnHwndAfterActivate = OwnHwnd != IntPtr.Zero && GetFocus() == OwnHwnd;
        }

        public void Uninstall()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
            _hookProc = null;
        }

        public void Dispose() => Uninstall();

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0 || lParam == IntPtr.Zero)
                return CallNextHookEx(_hook, nCode, wParam, lParam);

            try
            {
                // Só mensagens efetivamente retiradas da fila (GetMessage se comporta como
                // PeekMessage com PM_REMOVE) — nunca ao só espiar, para não mutar a mesma
                // mensagem mais de uma vez.
                if (wParam.ToInt32() == PM_REMOVE)
                {
                    var msg = Marshal.PtrToStructure<Win32Msg>(lParam);
                    if (msg.message == WM_KEYDOWN)
                    {
                        // Contado para TODA WM_KEYDOWN desta thread, não só a desta janela — é
                        // isso que permite distinguir "hook nunca é atingido" de "hook é atingido,
                        // mas a tecla chega com outro hwnd" (sintoma de falta de foco nativo).
                        AnyKeydownOnThread++;
                        LastSeenHwnd = msg.hwnd;
                        LastFocusHwnd = GetFocus();

                        if (msg.hwnd == OwnHwnd)
                        {
                            MatchedOwnHwnd++;

                            if (ShouldForceRouteToEditor((int)msg.wParam.ToInt64()))
                            {
                                var copy = msg;
                                TranslateMessage(ref copy);
                                DispatchMessage(ref copy);
                                ForcedRoutes++;

                                // Neutraliza a mensagem original: o loop do Corel ainda vai rodar
                                // TranslateAccelerator/TranslateMessage/DispatchMessage sobre ela
                                // depois que este hook retornar — WM_NULL garante que isso não faz
                                // nada (nem ativa ferramenta, nem duplica a digitação).
                                msg.message = WM_NULL;
                                Marshal.StructureToPtr(msg, lParam, false);
                            }
                        }

                        CountersChanged?.Invoke();
                    }
                }
            }
            catch
            {
                // Diagnóstico/roteamento não pode travar a digitação nem o Corel: se algo
                // inesperado falhar aqui, a mensagem segue o caminho normal (sem forçar nada).
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private bool ShouldForceRouteToEditor(int vkCode)
        {
            // Regra de decisão pura (testável sem janela real) em KeyboardRoutingPolicy — esta
            // função só apura os fatos que dependem de WPF/Dispatcher ao vivo (janela ativa, o
            // que tem foco) e entrega para a regra decidir.
            var key = KeyInterop.KeyFromVirtualKey(vkCode);
            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
            var focusInEditableControl = Keyboard.FocusedElement is TextBoxBase or ComboBox;

            return KeyboardRoutingPolicy.ShouldForceRouteToEditor(key, ctrl, alt, _window.IsActive, focusInEditableControl);
        }

        /// <summary>Texto de diagnóstico completo — só identificadores de janela/thread e
        /// contadores, nunca conteúdo digitado. Cada campo isola um elo possível da cadeia, para
        /// não tratar "zero eventos" como prova automática de uma causa só.</summary>
        public string DescribeState() =>
            $"Hook: {(InstallOk ? "instalado" : $"FALHOU (erro Win32 {InstallError})")} | " +
            $"hwnd desta janela: 0x{OwnHwnd.ToInt64():X} | thread do hook: {ThreadId} | " +
            $"foco nativo == hwnd desta janela (após Activate()+SetFocus()): {FocusMatchedOwnHwndAfterActivate}\n" +
            $"WM_KEYDOWN vistas nesta thread: {AnyKeydownOnThread} | " +
            $"com hwnd igual ao desta janela: {MatchedOwnHwnd} | " +
            $"roteadas manualmente para o editor: {ForcedRoutes}\n" +
            $"último hwnd observado numa tecla: 0x{LastSeenHwnd.ToInt64():X} | " +
            $"foco nativo no momento dessa tecla: 0x{LastFocusHwnd.ToInt64():X}";
    }
}
