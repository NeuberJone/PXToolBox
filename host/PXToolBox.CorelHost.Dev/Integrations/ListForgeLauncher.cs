using System;
using System.IO;

namespace PXToolBoxDev.Integrations
{
    public enum ListForgeLaunchStatus
    {
        Launched,
        NeedsPathSelection,
        LaunchFailed
    }

    public sealed class ListForgeLaunchResult
    {
        public ListForgeLaunchStatus Status { get; }
        public string Message { get; }

        private ListForgeLaunchResult(ListForgeLaunchStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public static ListForgeLaunchResult Launched(int processId) => new ListForgeLaunchResult(
            ListForgeLaunchStatus.Launched,
            $"Processo do ListForge iniciado (PID {processId}). Não é possível confirmar, só por isso, que a janela abriu ou que o ListForge está pronto para uso.");

        public static ListForgeLaunchResult NeedsSelection(string reason) =>
            new ListForgeLaunchResult(ListForgeLaunchStatus.NeedsPathSelection, reason);

        public static ListForgeLaunchResult Failed(string reason) =>
            new ListForgeLaunchResult(ListForgeLaunchStatus.LaunchFailed, reason);
    }

    /// <summary>
    /// Decide como abrir o ListForge standalone — nunca embute a interface ou o processamento
    /// do ListForge, só decide SE/COMO iniciar o processo configurado. Toda E/S real (arquivo e
    /// processo) entra via delegates injetados no construtor, para que a lógica de decisão seja
    /// testável sem abrir processos reais nem tocar na configuração real da máquina (ver
    /// ListForgeLauncherTests). O host (ControlUI) injeta as implementações reais.
    ///
    /// Não implementa nenhum mecanismo de ativação de instância existente (mutex, named pipe,
    /// argumento de linha de comando) — o ListForge standalone (C:\Projetos\ListForge,
    /// App.xaml.cs) não documenta nem implementa suporte a isso hoje; inventar um mecanismo aqui
    /// seria exatamente o que a tarefa pediu para não fazer. Cada clique inicia um processo novo
    /// via Process.Start, sem Arguments e sem UseShellExecute (ver ControlUI.xaml.cs).
    /// </summary>
    public sealed class ListForgeLauncher
    {
        private readonly Func<string?> _readConfiguredPath;
        private readonly Action<string> _saveConfiguredPath;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, string, int> _startProcess;

        public ListForgeLauncher(
            Func<string?> readConfiguredPath,
            Action<string> saveConfiguredPath,
            Func<string, bool> fileExists,
            Func<string, string, int> startProcess)
        {
            _readConfiguredPath = readConfiguredPath;
            _saveConfiguredPath = saveConfiguredPath;
            _fileExists = fileExists;
            _startProcess = startProcess;
        }

        /// <summary>
        /// Usa o caminho já configurado. Nunca abre um seletor sozinho — só informa que é
        /// preciso selecionar um caminho (NeedsPathSelection), distinguindo "nunca configurado"
        /// de "configurado, mas o arquivo não existe mais" (movido/removido).
        /// </summary>
        public ListForgeLaunchResult TryOpenConfigured()
        {
            var path = _readConfiguredPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return ListForgeLaunchResult.NeedsSelection("Nenhum caminho do ListForge configurado ainda.");
            }

            // path não é mais nulo aqui (IsNullOrWhiteSpace já retornou acima) — as anotações de
            // nulidade do BCL de referência do net48 não expressam isso, por isso o "!".
            if (!_fileExists(path!))
            {
                return ListForgeLaunchResult.NeedsSelection(
                    $"O executável configurado não foi encontrado (pode ter sido movido ou removido): {path}");
            }

            return StartAndSave(path!);
        }

        /// <summary>
        /// Usa um caminho recém-escolhido pelo usuário no seletor de arquivo do host. Salva o
        /// caminho ANTES de tentar abrir (passo 3 antes do passo 4 do escopo pedido) — uma falha
        /// ao iniciar o processo não desfaz a configuração que o usuário escolheu de propósito.
        /// </summary>
        public ListForgeLaunchResult OpenWithSelectedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !_fileExists(path))
            {
                return ListForgeLaunchResult.Failed($"Caminho inválido ou inexistente: {path}");
            }

            _saveConfiguredPath(path);
            return StartAndSave(path, alreadySaved: true);
        }

        private ListForgeLaunchResult StartAndSave(string path, bool alreadySaved = false)
        {
            string workingDirectory;
            try
            {
                workingDirectory = Path.GetDirectoryName(path) ?? string.Empty;
            }
            catch (ArgumentException ex)
            {
                return ListForgeLaunchResult.Failed($"Caminho inválido: {ex.Message}");
            }

            int processId;
            try
            {
                processId = _startProcess(path, workingDirectory);
            }
            catch (Exception ex)
            {
                return ListForgeLaunchResult.Failed(alreadySaved
                    ? $"Caminho salvo, mas não foi possível iniciar o ListForge agora: {ex.Message}"
                    : $"Não foi possível iniciar o ListForge: {ex.Message}");
            }

            if (!alreadySaved)
            {
                _saveConfiguredPath(path);
            }

            return ListForgeLaunchResult.Launched(processId);
        }
    }
}
