using System;
using System.IO;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Persiste o caminho do executável do ListForge standalone como um dado local DA MÁQUINA
    /// (<see cref="Environment.SpecialFolder.LocalApplicationData"/> — NÃO acompanha perfis
    /// móveis/roaming, ao contrário de ApplicationData). Fica num arquivo próprio, separado
    /// desde já de futuras preferências portáveis (layout, favoritos) — ver
    /// docs/ARCHITECTURE.md, seção "Perfis e configuração".
    ///
    /// Pasta exclusiva da distribuição DEV ("PXToolBox\Dev\config"): a distribuição de Produção
    /// (futura) usará sua própria pasta de configuração, nunca esta — mesma regra de identidade
    /// exclusiva por distribuição já aplicada a GUIDs de docker, logs e canal de atualização
    /// (ver AGENTS.md).
    /// </summary>
    public static class ListForgeConfigStore
    {
        private static string? _filePathOverride;

        private static string DefaultFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "config", "listforge-path.txt");

        private static string FilePath => _filePathOverride ?? DefaultFilePath;

        /// <summary>Só para testes — aponta esta classe para um arquivo temporário isolado,
        /// nunca o real do usuário. Mesmo padrão já usado por
        /// <see cref="PxLogger.SetLogDirectoryForTesting"/>.</summary>
        public static void SetFilePathForTesting(string? path) => _filePathOverride = path;

        /// <summary>Só para teste do caminho de produção em si (ver ListForgeLauncherTests) —
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
