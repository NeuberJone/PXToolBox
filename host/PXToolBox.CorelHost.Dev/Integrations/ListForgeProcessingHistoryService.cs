using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ListForge.Core;
using Newtonsoft.Json;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Histórico de processamentos do modo Clássico do ListForge Integrado — ver
    /// docs/listforge-classic-parity.md, grupo "Revisão e resumos", porta de
    /// <c>ListForge.Services.ProcessingHistoryService</c> (standalone-only, lido por completo,
    /// junto com <c>HistoryView.xaml</c>/<c>MainViewModel</c>, antes desta porta).
    ///
    /// <para><b>Quando um registro é criado</b> (mesma regra do standalone, confirmada lendo
    /// <c>MainViewModel.SaveOutput/GenerateJson/AdvancedSave</c>): só quando um ARQUIVO é de fato
    /// gravado em disco por "Salvar saída", "Gerar JSON" ou "Salvar avançado" — nunca só por
    /// clicar em "Processar"/"Processar com prévia". Ver
    /// <c>ListForgeIntegrated.ClassicWorkspaceWindow.RegisterProcessingHistory</c>.</para>
    ///
    /// <para><b>Dados armazenados</b>: data/hora, um nome de origem SEGURO (nome de arquivo sem
    /// caminho/URL completa — nunca o caminho de entrada real nem conteúdo da lista), tipo de
    /// origem, quantidade de linhas processadas e o caminho do arquivo de saída gerado. Nunca
    /// grava o texto de entrada, saída ou JSON em si.</para>
    ///
    /// <para><b>Ações do standalone reproduzidas</b> (confirmado lendo <c>HistoryView.xaml</c>/
    /// <c>MainViewModel</c> por completo): "Abrir pasta da saída" (seleciona o arquivo exportado
    /// no Explorer) e "Limpar histórico" (apaga tudo, com confirmação Sim/Não).
    /// <b>O standalone não tem</b> exclusão individual de registro nem "restaurar" um registro
    /// para reabrir a lista processada — a única ação por linha é abrir a pasta do arquivo já
    /// exportado. Esta porta não inventa nenhuma das duas, por decisão explícita de não divergir
    /// da referência real (ver nota correspondente em docs/listforge-classic-parity.md).</para>
    ///
    /// <para><b>Limite e retenção</b>: <see cref="MaxEntries"/> = 100, mesmo valor do standalone
    /// (<c>ProcessingHistoryService.MaxEntries</c>) — ao ultrapassar, os registros mais ANTIGOS
    /// são descartados silenciosamente (nunca os mais recentes). O standalone não tem nenhuma
    /// política de expiração por idade (confirmado lendo a classe por completo) — esta porta
    /// também não introduz uma, mesma razão já documentada em
    /// <see cref="ListForgeInputBackupService"/> (não inventar uma política que não existe na
    /// referência).</para>
    ///
    /// <para><b>Persistência isolada</b>: grava em
    /// <c>%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-history.json</c> — mesma pasta
    /// de <see cref="ListForgeIntegratedSettingsStore"/>, nunca o <c>AppDir</c> do ListForge
    /// Standalone. Nunca lido/alterado automaticamente a partir do histórico do standalone (são
    /// arquivos diferentes, em pastas diferentes, de produtos diferentes — AGENTS.md). Pode
    /// conter nomes de arquivo/lista reais (dado potencialmente sensível) — por isso
    /// <see cref="SupportPackageService"/> nunca lê este arquivo nem a pasta de configuração
    /// inteira: só grava um resumo campo a campo explicitamente permitido, mais os logs, nunca
    /// o histórico.</para>
    ///
    /// <para>Sem relação com perfis de trabalho — é um único arquivo por instalação do PX DEV,
    /// igual ao standalone (cujo histórico também não depende de perfil).</para>
    /// </summary>
    public sealed class ListForgeProcessingHistoryService
    {
        public const int MaxEntries = 100;
        public const int SchemaVersion = 1;

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
        private readonly string _path;
        private readonly Func<DateTimeOffset> _clock;

        public ListForgeProcessingHistoryService()
            : this(DefaultPath, null)
        {
        }

        public ListForgeProcessingHistoryService(string path, Func<DateTimeOffset>? clock = null)
        {
            _path = path;
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        public static string DefaultPath => Path.Combine(
            ListForgeIntegratedSettingsStore.ConfigDirectoryForDisplay,
            "listforge-integrated-history.json");

        public IReadOnlyList<ListForgeProcessingHistoryEntry> Load()
        {
            var document = LoadDocument();
            return NormalizeEntries(document.Entries);
        }

        public OperationResult<ListForgeProcessingHistoryEntry> Add(ListForgeProcessingHistoryEntry entry)
        {
            try
            {
                var stored = new ListForgeProcessingHistoryEntry
                {
                    Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
                    ProcessedAt = entry.ProcessedAt == default ? _clock() : entry.ProcessedAt.ToUniversalTime(),
                    SourceDisplayName = SanitizeSourceDisplayName(entry.SourceDisplayName, entry.SourceType),
                    SourceType = SanitizeSourceType(entry.SourceType),
                    ProcessedLineCount = Math.Max(0, entry.ProcessedLineCount),
                    OutputPath = entry.OutputPath ?? "",
                };

                if (string.IsNullOrWhiteSpace(stored.OutputPath))
                    return OperationResult<ListForgeProcessingHistoryEntry>.Fail(
                        "O processamento foi concluído, mas não foi possível atualizar o histórico.",
                        "Histórico sem caminho de saída.",
                        errorCode: "HistoryMissingOutputPath");

                var document = LoadDocument();
                var entries = document.Entries;
                entries.Insert(0, stored);
                entries = NormalizeEntries(entries).Take(MaxEntries).ToList();
                SaveDocument(new ListForgeProcessingHistoryDocument { Entries = entries });
                return OperationResult<ListForgeProcessingHistoryEntry>.Ok(stored);
            }
            catch (Exception ex)
            {
                return OperationResult<ListForgeProcessingHistoryEntry>.Fail(
                    "O processamento foi concluído, mas não foi possível atualizar o histórico.",
                    "Falha ao gravar histórico de processamentos.",
                    ex,
                    "ProcessingHistoryWriteFailed");
            }
        }

        public OperationResult Clear()
        {
            try
            {
                SaveDocument(new ListForgeProcessingHistoryDocument { Entries = new List<ListForgeProcessingHistoryEntry>() });
                return OperationResult.Ok("Histórico limpo.", "Histórico de processamentos limpo.");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(
                    "Não foi possível limpar o histórico.",
                    "Falha ao limpar histórico de processamentos.",
                    ex,
                    "ProcessingHistoryClearFailed");
            }
        }

        public OperationResult OpenOutputFolder(ListForgeProcessingHistoryEntry? entry)
        {
            try
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.OutputPath) || !File.Exists(entry.OutputPath))
                    return OperationResult.Fail(
                        "O arquivo de saída não foi encontrado.",
                        "Arquivo de saída do histórico não encontrado.",
                        errorCode: "HistoryOutputMissing");

                var argument = $"/select,\"{entry.OutputPath}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
                return OperationResult.Ok("Pasta aberta.", "Pasta da saída do histórico aberta.");
            }
            catch (Exception ex)
            {
                return OperationResult.Fail(
                    "Não foi possível abrir a pasta da saída.",
                    "Falha ao abrir pasta da saída do histórico.",
                    ex,
                    "HistoryOpenOutputFailed");
            }
        }

        /// <summary>Resolve nome/tipo de origem seguro a partir do que
        /// <c>ClassicWorkspaceWindow</c> já rastreia explicitamente no momento da ação (abrir
        /// arquivo/extrair link/aplicar edição) — sem a heurística de <c>CurrentFileLabel</c> do
        /// standalone (<c>BuildSafeSource</c>), que lá existe só para inferir o tipo quando ele
        /// não foi informado explicitamente; aqui o tipo é sempre conhecido no momento da
        /// chamada.</summary>
        public static ListForgeProcessingHistorySource BuildSource(string sourceType, string? currentInputFile)
        {
            switch (sourceType)
            {
                case ListForgeProcessingHistorySourceTypes.File:
                    var name = string.IsNullOrWhiteSpace(currentInputFile) ? null : Path.GetFileName(currentInputFile);
                    return new ListForgeProcessingHistorySource(
                        string.IsNullOrWhiteSpace(name) ? "Arquivo sem nome" : name!,
                        ListForgeProcessingHistorySourceTypes.File);
                case ListForgeProcessingHistorySourceTypes.Link:
                    return new ListForgeProcessingHistorySource("Lista extraída de link", ListForgeProcessingHistorySourceTypes.Link);
                case ListForgeProcessingHistorySourceTypes.EditedJson:
                    return new ListForgeProcessingHistorySource("JSON editado", ListForgeProcessingHistorySourceTypes.EditedJson);
                case ListForgeProcessingHistorySourceTypes.EditedOutput:
                    return new ListForgeProcessingHistorySource("Lista de saída editada", ListForgeProcessingHistorySourceTypes.EditedOutput);
                default:
                    return new ListForgeProcessingHistorySource("Texto colado", ListForgeProcessingHistorySourceTypes.PastedText);
            }
        }

        private ListForgeProcessingHistoryDocument LoadDocument()
        {
            if (!File.Exists(_path))
                return new ListForgeProcessingHistoryDocument();

            string json;
            try
            {
                json = File.ReadAllText(_path, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Mesma cautela de ListForgeIntegratedSettingsStore.Load: não tenta mover/
                // sobrescrever um arquivo que nem conseguiu abrir (pode só ter um lock
                // temporário de outro processo) — usa a lista vazia só nesta chamada.
                return new ListForgeProcessingHistoryDocument();
            }

            ListForgeProcessingHistoryDocument? document;
            try
            {
                document = JsonConvert.DeserializeObject<ListForgeProcessingHistoryDocument>(json);
            }
            catch (JsonException)
            {
                document = null;
            }

            if (document == null)
            {
                PreserveInvalidFile();
                return new ListForgeProcessingHistoryDocument();
            }

            return document;
        }

        /// <summary>Grava de forma segura e tolerante a bloqueio transitório no destino — ver
        /// <see cref="AtomicFileWriter"/> (infraestrutura compartilhada com
        /// <see cref="ListForgeIntegratedSettingsStore"/>, que tinha uma cópia quase idêntica
        /// desta mesma lógica antes da correção do erro "Não foi possível remover o arquivo a
        /// ser substituído" ao salvar perfis de trabalho).</summary>
        private void SaveDocument(ListForgeProcessingHistoryDocument document)
        {
            document.SchemaVersion = SchemaVersion;
            document.Entries = NormalizeEntries(document.Entries).Take(MaxEntries).ToList();

            var json = JsonConvert.SerializeObject(document, Formatting.Indented);
            AtomicFileWriter.WriteTextSafely(_path, json, Utf8NoBom);
        }

        private void PreserveInvalidFile()
        {
            try
            {
                if (!File.Exists(_path))
                    return;

                var invalidPath = $"{_path}.invalid-{DateTime.Now:yyyyMMddHHmmss}";
                File.Move(_path, invalidPath);
            }
            catch
            {
                // Melhor esforço — mesma postura de ListForgeIntegratedSettingsStore: sem
                // conseguir preservar o arquivo corrompido, ainda assim segue com a lista vazia
                // nesta sessão, em vez de travar o histórico.
            }
        }

        private static List<ListForgeProcessingHistoryEntry> NormalizeEntries(IEnumerable<ListForgeProcessingHistoryEntry>? entries) =>
            (entries ?? Enumerable.Empty<ListForgeProcessingHistoryEntry>())
                .Where(entry => entry != null)
                .Select(entry => new ListForgeProcessingHistoryEntry
                {
                    Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
                    ProcessedAt = entry.ProcessedAt == default ? DateTimeOffset.MinValue : entry.ProcessedAt.ToUniversalTime(),
                    SourceDisplayName = SanitizeSourceDisplayName(entry.SourceDisplayName, entry.SourceType),
                    SourceType = SanitizeSourceType(entry.SourceType),
                    ProcessedLineCount = Math.Max(0, entry.ProcessedLineCount),
                    OutputPath = entry.OutputPath ?? "",
                })
                .OrderByDescending(entry => entry.ProcessedAt)
                .ToList();

        private static string SanitizeSourceType(string? sourceType)
        {
            var value = (sourceType ?? "").Trim();
            switch (value)
            {
                case ListForgeProcessingHistorySourceTypes.File: return ListForgeProcessingHistorySourceTypes.File;
                case ListForgeProcessingHistorySourceTypes.ImportedFile: return ListForgeProcessingHistorySourceTypes.ImportedFile;
                case ListForgeProcessingHistorySourceTypes.PastedText: return ListForgeProcessingHistorySourceTypes.PastedText;
                case ListForgeProcessingHistorySourceTypes.Link: return ListForgeProcessingHistorySourceTypes.Link;
                case ListForgeProcessingHistorySourceTypes.EditedJson: return ListForgeProcessingHistorySourceTypes.EditedJson;
                case ListForgeProcessingHistorySourceTypes.EditedOutput: return ListForgeProcessingHistorySourceTypes.EditedOutput;
                default: return ListForgeProcessingHistorySourceTypes.Unknown;
            }
        }

        private static string SanitizeSourceDisplayName(string? sourceDisplayName, string? sourceType)
        {
            var type = SanitizeSourceType(sourceType);
            string fallback;
            switch (type)
            {
                case ListForgeProcessingHistorySourceTypes.Link: fallback = "Lista extraída de link"; break;
                case ListForgeProcessingHistorySourceTypes.EditedJson: fallback = "JSON editado"; break;
                case ListForgeProcessingHistorySourceTypes.EditedOutput: fallback = "Lista de saída editada"; break;
                case ListForgeProcessingHistorySourceTypes.Unknown: fallback = "Arquivo sem nome"; break;
                default: fallback = "Texto colado"; break;
            }

            var text = (sourceDisplayName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            text = text.Replace('\r', ' ').Replace('\n', ' ');

            if (type == ListForgeProcessingHistorySourceTypes.File || type == ListForgeProcessingHistorySourceTypes.ImportedFile)
            {
                var fileName = Path.GetFileName(text);
                return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
            }

            return Uri.TryCreate(text, UriKind.Absolute, out _) ? fallback : text;
        }
    }
}
