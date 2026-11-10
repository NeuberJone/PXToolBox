using System;
using System.IO;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Pasta de saída padrão de Preparar Escudo/Bandeira/Faixa de Capitão nesta máquina
    /// (ROADMAP, "Tela de configuração no host": substitui o caminho cravado
    /// <c>Z:\Neuber\Saídas</c> da macro original quando definida). Dado local DA MÁQUINA — mesmo
    /// padrão de <see cref="NexorConfigStore"/>/<see cref="ListForgeConfigStore"/>, num arquivo
    /// próprio ("output-folder.txt"), nunca nas preferências portáteis: uma pasta de rede ou de
    /// disco de uma máquina não vale para outra.
    /// </summary>
    public static class OutputFolderConfigStore
    {
        private static string? _filePathOverride;

        private static string DefaultFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "config", "output-folder.txt");

        private static string FilePath => _filePathOverride ?? DefaultFilePath;

        /// <summary>Só para testes — aponta esta classe para um arquivo temporário isolado,
        /// nunca o real do usuário.</summary>
        public static void SetFilePathForTesting(string? path) => _filePathOverride = path;

        /// <summary>null quando nunca definida, apagada ou ilegível (vale o padrão original).</summary>
        public static string? ReadConfiguredPath()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var text = File.ReadAllText(FilePath).Trim();
                return text.Length == 0 ? null : text;
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

            File.WriteAllText(FilePath, path.Trim());
        }

        /// <summary>Volta ao padrão original (apaga só o arquivo desta configuração).</summary>
        public static void ClearConfiguredPath()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
