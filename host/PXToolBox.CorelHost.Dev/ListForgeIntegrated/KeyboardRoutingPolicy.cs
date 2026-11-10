using System.Collections.Generic;
using System.Windows.Input;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Decisão pura de "esta tecla deve ser roteada para o editor em foco, em vez de deixada
    /// para o Corel?" — extraída de <see cref="ThreadKeyboardRouter"/> para ser testável sem
    /// uma janela/Dispatcher real (o resto do mecanismo, hook Win32 e foco nativo, depende de um
    /// host real e continua fora do alcance de teste automatizado — ver
    /// docs/ARCHITECTURE.md, seção 10). Esta classe não decide SE a janela está ativa nem O QUE
    /// tem foco — só recebe esses fatos já apurados e aplica a mesma regra usada em produção.
    /// Pública (não internal) para ser exercitada diretamente pelos testes de regressão em
    /// PXToolBox.Tests, sem precisar de InternalsVisibleTo.
    /// </summary>
    public static class KeyboardRoutingPolicy
    {
        public static readonly HashSet<Key> NeverIntercept = new()
        {
            Key.Tab, Key.LeftShift, Key.RightShift, Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
            Key.LWin, Key.RWin, Key.Apps, Key.System,
            Key.CapsLock, Key.NumLock, Key.Scroll, Key.PrintScreen, Key.Pause,
        };

        /// <param name="key">Tecla (já convertida do código virtual nativo).</param>
        /// <param name="ctrl">Ctrl estava pressionado.</param>
        /// <param name="alt">Alt estava pressionado.</param>
        /// <param name="windowActive">A janela do ListForge Integrado está ativa (Win.IsActive).</param>
        /// <param name="focusInEditableControl">O foco lógico do WPF está num TextBox/ComboBox
        /// desta janela (Keyboard.FocusedElement).</param>
        public static bool ShouldForceRouteToEditor(Key key, bool ctrl, bool alt, bool windowActive, bool focusInEditableControl)
        {
            if (!windowActive) return false;
            if (!focusInEditableControl) return false;

            // Alt sempre fica com o Corel (combinações de menu/ferramenta).
            if (alt) return false;

            if (ctrl)
            {
                // Só as combinações de edição padrão são roteadas para o campo em foco — qualquer
                // outro Ctrl+tecla continua sendo um atalho do Corel, mesmo com o campo em foco.
                return key is Key.A or Key.C or Key.V or Key.X or Key.Z;
            }

            if (key >= Key.F1 && key <= Key.F24) return false;
            return !NeverIntercept.Contains(key);

            // Sem Ctrl/Alt e fora da lista de exclusão: letras, números, acentuação (teclas
            // mortas do layout ABNT, inclusive), Enter, Espaço, Backspace, Delete, Insert, setas,
            // Home/End/PageUp/PageDown, Escape — tudo isso é roteado para o editor em foco.
        }
    }
}
