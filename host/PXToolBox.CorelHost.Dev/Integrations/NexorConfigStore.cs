using System;
using System.IO;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Persiste o caminho do executável do Nexor (centro de controle de produção — ver
    /// AGENTS.md) como um dado local DA MÁQUINA (<see cref="Environment.SpecialFolder.LocalApplicationData"/>
    /// — nunca ApplicationData/roaming), exatamente no mesmo padrão de
    /// <see cref="ListForgeConfigStore"/>, mas num arquivo PRÓPRIO ("nexor-path.txt") — a
    /// configuração dos dois aplicativos externos é sempre independente, nunca compartilhada,
    /// mesmo morando na mesma pasta de configuração deste módulo (ver AGENTS.md, "MoldMotor" e
    /// "Responsabilidades do ecossistema": nenhum produto compartilha configuração com outro).
    ///
    /// Pasta exclusiva da distribuição DEV ("PXToolBox\Dev\config") — mesma regra de identidade
    /// exclusiva por distribuição já aplicada a GUIDs de docker, logs e canal de atualização.
    /// </summary>
    public static class NexorConfigStore
    {
        private static string? _filePathOverride;

        private static string DefaultFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "config", "nexor-path.txt");

        private static string FilePath => _filePathOverride ?? DefaultFilePath;

        /// <summary>Só para testes — aponta esta classe para um arquivo temporário isolado,
        /// nunca o real do usuário. Mesmo padrão já usado por
        /// <see cref="PxLogger.SetLogDirectoryForTesting"/>.</summary>
        public static void SetFilePathForTesting(string? path) => _filePathOverride = path;

        /// <summary>Só para teste do caminho de produção em si (ver NexorLauncherTests) —
        /// nunca lê/grava nada; só expõe onde o arquivo real estaria, ignorando qualquer
        /// override ativo.</summary>
        public static string DefaultFilePathForTesting => DefaultFilePath;

        public static string? ReadConfiguredPath()
        {
            try
            {
                return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static void SaveConfiguredPath(string path)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, path);
        }
    }
}
