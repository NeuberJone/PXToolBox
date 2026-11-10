using System;
using System.IO;

namespace PXToolBoxDev.Integrations
{
    public enum NexorLaunchStatus
    {
        Launched,
        NeedsPathSelection,
        LaunchFailed
    }

    public sealed class NexorLaunchResult
    {
        public NexorLaunchStatus Status { get; }
        public string Message { get; }

        private NexorLaunchResult(NexorLaunchStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public static NexorLaunchResult Launched(int processId) => new NexorLaunchResult(
            NexorLaunchStatus.Launched,
            $"Processo do Nexor iniciado (PID {processId}). Não é possível confirmar, só por isso, que a janela abriu ou que o Nexor está pronto para uso.");

        public static NexorLaunchResult NeedsSelection(string reason) =>
            new NexorLaunchResult(NexorLaunchStatus.NeedsPathSelection, reason);

        public static NexorLaunchResult Failed(string reason) =>
            new NexorLaunchResult(NexorLaunchStatus.LaunchFailed, reason);
    }

    /// <summary>
    /// Decide como abrir o Nexor standalone — primeira etapa da integração com Produção (ver
    /// AGENTS.md, "Responsabilidades do ecossistema": Nexor é dono de pedidos/planejamento/
    /// filas/produção/status; esta classe só decide SE/COMO abrir o processo já existente, nunca
    /// incorpora a interface dele nem lê/altera `nexor.db` ou qualquer dado de pedido).
    ///
    /// Mesmo formato de <see cref="ListForgeLauncher"/> (decisão pura, toda E/S real injetada
    /// via delegate — testável sem abrir processo nem tocar na configuração real da máquina, ver
    /// NexorLauncherTests), mas uma classe própria, independente: a configuração salva (caminho
    /// do executável), as mensagens e o estado de cada instância nunca são compartilhados entre
    /// os dois launchers — ver <see cref="NexorConfigStore"/> (arquivo próprio) vs.
    /// <see cref="ListForgeConfigStore"/>.
    ///
    /// Distribuição real localizada no ambiente nesta tarefa: o Nexor foi, em algum momento,
    /// renomeado a partir do codinome "Projeto Jocasta"/JocastaHub (ver CHANGELOG do próprio
    /// projeto) — o executável empacotado real (PyInstaller, sem argumentos de linha de
    /// comando) é o que o usuário já fixa na barra de tarefas; nenhum caminho, nome de
    /// executável ou mecanismo de inicialização foi suposto aqui.
    /// </summary>
    public sealed class NexorLauncher
    {
        private readonly Func<string?> _readConfiguredPath;
        private readonly Action<string> _saveConfiguredPath;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, string, int> _startProcess;

        public NexorLauncher(
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
        public NexorLaunchResult TryOpenConfigured()
        {
            var path = _readConfiguredPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return NexorLaunchResult.NeedsSelection("Nenhum caminho do Nexor configurado ainda.");
            }

            // path não é mais nulo aqui (IsNullOrWhiteSpace já retornou acima) — as anotações de
            // nulidade do BCL de referência do net48 não expressam isso, por isso o "!".
            if (!_fileExists(path!))
            {
                return NexorLaunchResult.NeedsSelection(
                    $"O executável configurado não foi encontrado (pode ter sido movido ou removido): {path}");
            }

            return StartAndSave(path!);
        }

        /// <summary>
        /// Usa um caminho recém-escolhido pelo usuário no seletor de arquivo do host. Salva o
        /// caminho ANTES de tentar abrir — uma falha ao iniciar o processo não desfaz a
        /// configuração que o usuário escolheu de propósito.
        /// </summary>
        public NexorLaunchResult OpenWithSelectedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !_fileExists(path))
            {
                return NexorLaunchResult.Failed($"Caminho inválido ou inexistente: {path}");
            }

            _saveConfiguredPath(path);
            return StartAndSave(path, alreadySaved: true);
        }

        private NexorLaunchResult StartAndSave(string path, bool alreadySaved = false)
        {
            string workingDirectory;
            try
            {
                workingDirectory = Path.GetDirectoryName(path) ?? string.Empty;
            }
            catch (ArgumentException ex)
            {
                return NexorLaunchResult.Failed($"Caminho inválido: {ex.Message}");
            }

            int processId;
            try
            {
                processId = _startProcess(path, workingDirectory);
            }
            catch (Exception ex)
            {
                return NexorLaunchResult.Failed(alreadySaved
                    ? $"Caminho salvo, mas não foi possível iniciar o Nexor agora: {ex.Message}"
                    : $"Não foi possível iniciar o Nexor: {ex.Message}");
            }

            if (!alreadySaved)
            {
                _saveConfiguredPath(path);
            }

            return NexorLaunchResult.Launched(processId);
        }
    }
}
