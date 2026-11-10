using System;
using System.Collections.Generic;
using System.Globalization;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Tipos de origem de uma entrada do histórico — porta de
    /// <c>ListForge.Models.ProcessingHistorySourceTypes</c> (standalone-only, não compartilhado
    /// por <c>ListForge.Core</c>). <see cref="ImportedFile"/> existe só para compatibilidade com
    /// uma futura importação de documentos no integrado (ver
    /// docs/listforge-classic-parity.md) — hoje nada aqui produz esse valor.
    /// </summary>
    public static class ListForgeProcessingHistorySourceTypes
    {
        public const string File = "File";
        public const string ImportedFile = "ImportedFile";
        public const string PastedText = "PastedText";
        public const string Link = "Link";
        public const string EditedJson = "EditedJson";
        public const string EditedOutput = "EditedOutput";
        public const string Unknown = "Unknown";
    }

    /// <summary>
    /// Uma linha do histórico de processamentos — só metadados seguros (nunca conteúdo de
    /// lista/entrada/saída/JSON). Porta de <c>ListForge.Models.ProcessingHistoryEntry</c>.
    /// Classe mutável (não record): net48 não tem <c>IsExternalInit</c>, mesma decisão já tomada
    /// em outras portas deste módulo (ver <c>ComparisonWindow</c>).
    /// </summary>
    public sealed class ListForgeProcessingHistoryEntry
    {
        public string Id { get; set; } = "";
        public DateTimeOffset ProcessedAt { get; set; }
        public string SourceDisplayName { get; set; } = "";
        public string SourceType { get; set; } = ListForgeProcessingHistorySourceTypes.Unknown;
        public int ProcessedLineCount { get; set; }
        public string OutputPath { get; set; } = "";

        public string ProcessedAtDisplay =>
            ProcessedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
    }

    public sealed class ListForgeProcessingHistoryDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public List<ListForgeProcessingHistoryEntry> Entries { get; set; } = new List<ListForgeProcessingHistoryEntry>();
    }

    /// <summary>Nome/tipo de origem já resolvidos de forma segura (sem caminho/URL completa) —
    /// porta de <c>ListForge.Models.ProcessingHistorySource</c> (lá um <c>record</c>; aqui classe
    /// simples pela mesma razão de net48 citada acima).</summary>
    public sealed class ListForgeProcessingHistorySource
    {
        public ListForgeProcessingHistorySource(string displayName, string sourceType)
        {
            DisplayName = displayName;
            SourceType = sourceType;
        }

        public string DisplayName { get; }
        public string SourceType { get; }
    }
}
