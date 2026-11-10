using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// RESULTADO DE EXECUÇÃO (PX Tool Box → Nexor) — o caminho de volta do contexto de produção
    /// (ver docs/integrations/px-execution-result.md). O PX registra, só em memória, as saídas
    /// salvas (Escudo/Bandeira/Faixa) enquanto um contexto de produção está carregado e, quando o
    /// usuário pede, grava um arquivo .json com elas e com a identidade do trabalho de impressão
    /// copiada do contexto. Nada mais: nenhum status, nenhuma transição — o Nexor continua dono
    /// do status e decide o que fazer com o arquivo. O PX nunca escreve em nexor.db nem chama o
    /// Nexor.
    /// </summary>
    public static class ExecutionResultService
    {
        public const string FormatId = "PxExecutionResult";
        public const int CurrentSchemaVersion = 1;

        /// <summary>Uma saída salva. <see cref="Kind"/> é o identificador técnico estável
        /// (escudo/bandeira/faixa-de-capitao); <see cref="Label"/> é o nome exibido.</summary>
        public sealed class OutputRecord
        {
            public OutputRecord(string kind, string label, string? layoutName, string filePath, DateTimeOffset savedAtUtc)
            {
                Kind = kind;
                Label = label;
                LayoutName = layoutName;
                FilePath = filePath;
                SavedAtUtc = savedAtUtc;
            }

            public string Kind { get; }
            public string Label { get; }
            public string? LayoutName { get; }
            public string FilePath { get; }
            public DateTimeOffset SavedAtUtc { get; }
        }

        /// <summary>
        /// Saídas registradas para o contexto carregado nesta sessão do painel. Carregar de novo
        /// o MESMO trabalho de impressão (mesmo rolo, jobHash e base) mantém o registro; outro
        /// trabalho começa vazio. Salvar de novo no mesmo arquivo substitui o registro anterior.
        /// </summary>
        public sealed class Session
        {
            private readonly List<OutputRecord> _outputs = new List<OutputRecord>();

            public OrderContextService.OrderContext? Context { get; private set; }
            public IReadOnlyList<OutputRecord> Outputs => _outputs;

            public void SetContext(OrderContextService.OrderContext? context)
            {
                if (context == null || Context == null || !SameJob(Context, context))
                {
                    _outputs.Clear();
                }

                Context = context;
            }

            public void Record(OutputRecord output)
            {
                if (Context == null) return;
                _outputs.RemoveAll(o => string.Equals(o.FilePath, output.FilePath, StringComparison.OrdinalIgnoreCase));
                _outputs.Add(output);
            }

            private static bool SameJob(OrderContextService.OrderContext a, OrderContextService.OrderContext b) =>
                a.RollId == b.RollId
                && a.JobHash == b.JobHash
                && string.Equals(a.DatabaseId ?? "", b.DatabaseId ?? "", StringComparison.Ordinal);
        }

        /// <summary>Identificador técnico de cada saída — nunca muda, mesmo se o rótulo mudar.</summary>
        public static string KindFor(string outputName) => outputName switch
        {
            "Escudo" => "escudo",
            "Bandeira" => "bandeira",
            "Faixa De Capitão" => "faixa-de-capitao",
            _ => outputName.ToLowerInvariant().Replace(' ', '-'),
        };

        public static string Build(OrderContextService.OrderContext context, IEnumerable<OutputRecord> outputs, DateTimeOffset producedAtUtc, string pxVersion)
        {
            var contextJson = new JObject
            {
                ["formatId"] = OrderContextService.FormatId,
                ["exportedAtUtc"] = Utc(context.ExportedAtUtc),
                ["rollId"] = context.RollId,
                ["rollName"] = context.RollName,
                ["machine"] = context.Machine,
            };
            if (!string.IsNullOrWhiteSpace(context.InstallationId)) contextJson["installationId"] = context.InstallationId;
            if (!string.IsNullOrWhiteSpace(context.DatabaseId)) contextJson["databaseId"] = context.DatabaseId;
            contextJson["jobHash"] = context.JobHash;
            contextJson["pedidoLabel"] = context.PedidoLabel;

            var root = new JObject
            {
                ["formatId"] = FormatId,
                ["schemaVersion"] = CurrentSchemaVersion,
                ["producedAtUtc"] = Utc(producedAtUtc),
                ["producer"] = new JObject { ["app"] = "PX Tool Box DEV", ["version"] = pxVersion },
                ["context"] = contextJson,
                ["outputs"] = new JArray(outputs.Select(o => new JObject
                {
                    ["kind"] = o.Kind,
                    ["label"] = o.Label,
                    ["layoutName"] = o.LayoutName,
                    ["filePath"] = o.FilePath,
                    ["savedAtUtc"] = Utc(o.SavedAtUtc),
                })),
            };
            return root.ToString(Formatting.Indented);
        }

        /// <summary>Nome sugerido no "Salvar como": px-resultado-&lt;rolo&gt;-&lt;jobHash&gt;.json, só com
        /// caracteres válidos em nome de arquivo.</summary>
        public static string SuggestFileName(OrderContextService.OrderContext context)
        {
            var roll = string.IsNullOrWhiteSpace(context.RollName) ? context.RollId.ToString() : context.RollName;
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var raw = $"px-resultado-{roll}-{context.JobHash}";
            return new string(raw.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray()) + ".json";
        }

        // Sempre em UTC com "Z", como exportedAtUtc no contrato de ida.
        private static string Utc(DateTimeOffset value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }
}
