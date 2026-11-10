using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ListForge.Core;
using ListForge.Services;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Pacote de suporte do modo Clássico do ListForge Integrado — ver
    /// docs/listforge-classic-parity.md, grupo "Configurações aplicáveis ao integrado" ("Gerar
    /// pacote de suporte"), porta de <c>ListForge.Services.SupportPackageService</c> do
    /// standalone, reduzida ao que o integrado realmente tem (sem a entrada/saída atuais — ver
    /// a nota de privacidade abaixo sobre por que esta versão nunca inclui a lista).
    ///
    /// <para>Nunca inclui: conteúdo de listas (entrada/saída/JSON), nomes/pastas/nomes de lista
    /// configurados em um perfil de trabalho (podem ser nomes de cliente/pedido reais — mesma
    /// cautela do standalone, que também exclui `OutputDir`/`DefaultListName` do seu resumo de
    /// configurações), documentos do Corel, arquivos de produção, credenciais/tokens/licenças.
    /// Só inclui: versões/ambiente, um resumo de configurações EXPLICITAMENTE permitido (campo a
    /// campo, nunca um objeto inteiro serializado) e os logs recentes deste módulo dentro de um
    /// limite de arquivos/tamanho próprio (menor que a retenção em disco de
    /// <see cref="PxLogger"/> — ver <see cref="SupportPackageOptions"/>).</para>
    ///
    /// <para>Nunca envia nada automaticamente — só gera o arquivo no diretório escolhido pelo
    /// usuário (ver <see cref="ListForgeIntegrated.ClassicWorkspaceWindow.OnGenerateSupportPackage"/>,
    /// que mostra o conteúdo antes de pedir o destino).</para>
    /// </summary>
    public sealed class SupportPackageOptions
    {
        public SupportPackageOptions(int maxLogFiles = 5, long maxTotalLogSizeBytes = 1_048_576)
        {
            MaxLogFiles = maxLogFiles;
            MaxTotalLogSizeBytes = maxTotalLogSizeBytes;
        }

        public int MaxLogFiles { get; }
        public long MaxTotalLogSizeBytes { get; }
    }

    public sealed class SupportPackageService
    {
        private static readonly SupportPackageOptions DefaultOptions = new();

        public OperationResult<string> Generate(string outputDirectory, ListForgeIntegratedSettings settings, string? corelVersion) =>
            Generate(outputDirectory, settings, corelVersion, DefaultOptions);

        public OperationResult<string> Generate(
            string outputDirectory,
            ListForgeIntegratedSettings settings,
            string? corelVersion,
            SupportPackageOptions options)
        {
            try
            {
                var (path, warnings) = GeneratePackage(outputDirectory, settings, corelVersion, NormalizeOptions(options));
                PxLogger.Info("SupportPackage", "Pacote de suporte gerado.", path);

                var message = warnings.Count == 0
                    ? "Pacote de suporte gerado com sucesso."
                    : "Pacote de suporte gerado, com ressalvas: " + string.Join(" ", warnings);
                return OperationResult<string>.Ok(path, message);
            }
            catch (Exception ex)
            {
                PxLogger.Error("SupportPackage", "Falha ao gerar pacote de suporte.", ex);
                return OperationResult<string>.Fail(
                    "Falha ao gerar pacote de suporte.",
                    "Falha ao gerar pacote de suporte.",
                    ex,
                    "SupportPackageFailed");
            }
        }

        private static SupportPackageOptions NormalizeOptions(SupportPackageOptions? options)
        {
            options ??= DefaultOptions;
            return new SupportPackageOptions(
                Math.Max(0, Math.Min(options.MaxLogFiles, 10)),
                Math.Max(0L, Math.Min(options.MaxTotalLogSizeBytes, 5 * 1_048_576L)));
        }

        private (string PackagePath, List<string> Warnings) GeneratePackage(
            string outputDirectory,
            ListForgeIntegratedSettings settings,
            string? corelVersion,
            SupportPackageOptions options)
        {
            Directory.CreateDirectory(outputDirectory);
            var packagePath = CreatePackagePath(outputDirectory);
            var warnings = new List<string>();

            using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
                AddTextEntry(archive, "support-info.txt", BuildSupportInfo(corelVersion, options));
                AddTextEntry(archive, "settings-summary.txt", BuildSettingsSummary(settings));
                AddRecentLogs(archive, options, warnings);
            }

            return (packagePath, warnings);
        }

        private static string CreatePackagePath(string outputDirectory)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var packagePath = Path.Combine(outputDirectory, $"pxtoolbox-listforge-support-{timestamp}.zip");

            if (!File.Exists(packagePath))
                return packagePath;

            for (var index = 2; ; index++)
            {
                var candidate = Path.Combine(outputDirectory, $"pxtoolbox-listforge-support-{timestamp}-{index}.zip");
                if (!File.Exists(candidate))
                    return candidate;
            }
        }

        private static string BuildSupportInfo(string? corelVersion, SupportPackageOptions options)
        {
            var lines = new[]
            {
                "PX Tool Box DEV — ListForge Integrado (modo Clássico)",
                $"Versao do PX Tool Box DEV: {PxVersionInfo.PxToolBoxVersion}",
                $"Versao do nucleo ListForge (ListForge.Core): {PxVersionInfo.ListForgeCoreVersion}",
                $"Versao do CorelDRAW: {(string.IsNullOrWhiteSpace(corelVersion) ? "nao disponivel (sem conexao com o Corel neste momento)" : corelVersion)}",
                $"Runtime: {PxVersionInfo.RuntimeDescription}",
                $"Gerado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"Pasta de configuracao: {ListForgeIntegratedSettingsStore.ConfigDirectoryForDisplay}",
                $"Pasta de logs: {PxLogger.LogDirectory}",
                $"Politica de retencao de logs: maximo {PxLogger.MaxLogAgeDays} dias, {PxLogger.MaxTotalLogBytes} bytes no total",
                $"Limite de arquivos de log incluidos neste pacote: {options.MaxLogFiles}",
                $"Limite total de logs incluidos neste pacote: {options.MaxTotalLogSizeBytes} bytes",
                "",
                "Privacidade: este pacote inclui versao/ambiente tecnico, um resumo seguro das " +
                "configuracoes (campo a campo, nunca o arquivo de configuracao inteiro — nomes de " +
                "perfil, pasta e nome de lista configurados NAO sao incluidos, pois podem ser " +
                "nomes de cliente/pedido reais) e os logs recentes deste modulo. NUNCA inclui o " +
                "conteudo de listas, nomes/numeros de jogadores, area de transferencia, " +
                "credenciais, tokens, licencas, documentos do Corel ou arquivos de producao. " +
                "Revise o conteudo antes de enviar a qualquer pessoa.",
            };

            return string.Join(Environment.NewLine, lines);
        }

        // Campo a campo, nunca JsonConvert.SerializeObject(settings) inteiro — mesma cautela do
        // BuildConfigSummary do standalone, que também exclui OutputDir/DefaultListName (podem
        // ser nomes de cliente/pedido reais) do seu próprio resumo "seguro".
        private static string BuildSettingsSummary(ListForgeIntegratedSettings settings)
        {
            var service = new WorkProfileService();
            var active = service.GetActiveProfile(settings);
            var activeSettings = active?.Settings ?? WorkProfileService.CaptureOfficialDefaultSettings();

            var lines = new[]
            {
                $"SchemaVersion: {settings.SchemaVersion}",
                $"EditorFontSize: {settings.EditorFontSize}",
                $"WorkProfiles.Count: {settings.WorkProfiles.Count}",
                $"ActiveWorkProfile.IsDefault: {active?.IsDefault ?? true}",
                $"ActiveWorkProfile.Settings.DefaultInputSeparator: {activeSettings.DefaultInputSeparator}",
                $"ActiveWorkProfile.Settings.DefaultCaseMode: {activeSettings.DefaultCaseMode}",
                $"ActiveWorkProfile.Settings.EditorSortMode: {activeSettings.EditorSortMode}",
                $"ActiveWorkProfile.Settings.UseAdvancedJsonPieceMapping: {activeSettings.UseAdvancedJsonPieceMapping}",
                $"ActiveWorkProfile.Settings.AdvancedJsonPieceOrder: {string.Join(", ", activeSettings.AdvancedJsonPieceOrder)}",
                $"ActiveWorkProfile.Settings.AdvancedSaveMode: {activeSettings.AdvancedSaveMode}",
                $"ActiveWorkProfile.Settings.UseDefaultOutputDir: {activeSettings.UseDefaultOutputDir}",
                $"ActiveWorkProfile.Settings.UseDefaultListName: {activeSettings.UseDefaultListName}",
                "",
                "Tamanhos customizados (resumo seguro — tamanhos nao sao dado pessoal):",
                ListForge.Core.SizeHelper.BuildSizeSummary(settings.Sizes),
            };

            return string.Join(Environment.NewLine, lines);
        }

        private static void AddRecentLogs(ZipArchive archive, SupportPackageOptions options, List<string> warnings)
        {
            if (!Directory.Exists(PxLogger.LogDirectory) || options.MaxLogFiles <= 0 || options.MaxTotalLogSizeBytes <= 0)
                return;

            var remainingBytes = options.MaxTotalLogSizeBytes;
            var logs = Directory
                .GetFiles(PxLogger.LogDirectory, "pxtoolbox-integrated-*.log")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTime)
                .Take(options.MaxLogFiles);

            foreach (var log in logs)
            {
                if (log.Length > remainingBytes)
                    continue;

                try
                {
                    archive.CreateEntryFromFile(log.FullName, $"logs/{log.Name}");
                    remainingBytes -= log.Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Arquivo de log em uso por outro processo/sem permissão — não aborta o
                    // pacote por isso; registra a ressalva para o chamador informar ao usuário
                    // exatamente o que ficou de fora (nunca falha silenciosamente).
                    warnings.Add($"Não foi possível incluir o log \"{log.Name}\" ({ex.GetType().Name}).");
                }
            }
        }

        private static void AddTextEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
        }
    }
}
