using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Importador do CONTEXTO DE PRODUÇÃO exportado pelo Nexor (ver
    /// docs/integrations/nexor-order-context.md) — só leitura. O nome técnico desta classe e
    /// os nomes de campo (RollId/JobHash/PedidoLabel...) permanecem por compatibilidade com o
    /// contrato já em uso — mas isto NÃO é um pedido comercial: o Nexor não tem essa entidade
    /// hoje (sem número/cliente/status formais). O texto exibido ao usuário nunca chama isto
    /// de "pedido" como identificador nem de "Job" — ver <see cref="Production.OrderContextWindow"/>.
    ///
    /// <para><b>Identidade real de RollId/JobHash</b> (ver docs/ARCHITECTURE.md, seções 22–23):
    /// os dois só são garantidamente únicos DENTRO da mesma base de dados (printlogs.db) —
    /// nunca globalmente. Dois campos opcionais ajudam a desambiguar, mas com garantias
    /// DIFERENTES (confirmado com testes reais nos dois lados, nunca presumido):
    /// <see cref="OrderContext.InstallationId"/> identifica a INSTALAÇÃO/máquina que fez a
    /// exportação (config local) — pode ser IGUAL para bases diferentes (a mesma instalação
    /// trocando de base) ou DUPLICADO entre máquinas (config copiada sem copiar a base), então
    /// nunca prova "mesma base" por si só. <see cref="OrderContext.DatabaseId"/> é gravado
    /// DENTRO do próprio arquivo printlogs.db — permanece igual numa cópia/restauração do
    /// arquivo ou quando várias instalações acessam a mesma base compartilhada (pasta de
    /// rede), e é o campo que de fato identifica "mesma base de dados". Nenhum dos dois é uma
    /// identificação global formal; ambos ausentes em arquivos de versões anteriores do
    /// exportador, sem que isso seja um erro.</para>
    ///
    /// Esta classe NUNCA escreve em <c>nexor.db</c>, nunca chama o Nexor, nunca modifica
    /// documentos do Corel nem a sessão do ListForge, e nunca executa nada a partir dos
    /// valores do JSON (inclusive <see cref="OrderContext.SourceFileReference"/>, que é só
    /// texto exibido — nunca aberto automaticamente). Mantido só em memória nesta entrega —
    /// nenhuma persistência em disco foi adicionada por suposição.
    /// </summary>
    public static class OrderContextService
    {
        public const string FormatId = "NexorOrderContext";
        public const int CurrentSchemaVersion = 1;

        public sealed class OrderContext
        {
            public DateTimeOffset ExportedAtUtc { get; set; }
            public long RollId { get; set; }
            public string RollName { get; set; } = "";
            public string Machine { get; set; } = "";

            /// <summary>Opcional — ausente em arquivos de versões anteriores do exportador do
            /// Nexor, ou se a instalação de origem não conseguiu gerar/persistir o id (nunca
            /// tratado como erro). Nunca uma garantia de unicidade global por si só — ver
            /// comentário da classe.</summary>
            public string? InstallationId { get; set; }

            /// <summary>Opcional — ausente em arquivos de versões anteriores a esta. Gravado
            /// DENTRO do arquivo printlogs.db de origem (ver core/printlogs_db.get_database_id
            /// no Nexor): permanece igual numa cópia/restauração do arquivo e entre
            /// instalações diferentes que acessem a MESMA base compartilhada — por isso é mais
            /// confiável que <see cref="InstallationId"/> para saber se dois contextos vêm da
            /// mesma base de dados.</summary>
            public string? DatabaseId { get; set; }

            public string JobHash { get; set; } = "";

            /// <summary>Vazio = o arquivo não informa pedido (o exportador grava "" nesse caso).</summary>
            public string PedidoLabel { get; set; } = "";

            public bool HasPedidoLabel => !string.IsNullOrWhiteSpace(PedidoLabel);
            public bool PedidoManual { get; set; }
            public string? Fabric { get; set; }
            public string? Tipo { get; set; }
            public string? SourceFileReference { get; set; }
        }

        public sealed class ValidationResult
        {
            public bool IsValid { get; }
            public string? FailureReason { get; }
            public OrderContext? Context { get; }

            private ValidationResult(bool isValid, string? failureReason, OrderContext? context)
            {
                IsValid = isValid;
                FailureReason = failureReason;
                Context = context;
            }

            public static ValidationResult Fail(string reason) => new(false, reason, null);
            public static ValidationResult Ok(OrderContext context) => new(true, null, context);
        }

        /// <summary>Nunca lança: todo problema (JSON inválido, formato/esquema desconhecido,
        /// campo obrigatório ausente/com tipo errado) resulta num resultado inválido com motivo
        /// claro — nunca uma tentativa de "adivinhar" um contexto parcial.</summary>
        public static ValidationResult Validate(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException)
            {
                return ValidationResult.Fail("O arquivo não é um JSON válido.");
            }

            var formatId = root.Value<string>("formatId");
            if (!string.Equals(formatId, FormatId, StringComparison.Ordinal))
            {
                return ValidationResult.Fail(
                    string.IsNullOrWhiteSpace(formatId)
                        ? "Arquivo sem identificador de formato — não é um contexto de produção do Nexor."
                        : $"Identificador de formato desconhecido (\"{formatId}\") — não é um contexto de produção do Nexor.");
            }

            var schemaVersion = root.Value<int?>("schemaVersion") ?? 0;
            if (schemaVersion != CurrentSchemaVersion)
            {
                return ValidationResult.Fail(
                    $"Versão do esquema não suportada ({schemaVersion}) — esta instalação entende só a versão {CurrentSchemaVersion}.");
            }

            // Nunca lê como string e tenta re-parsear: JObject.Parse já converte um valor com
            // formato de data (DateParseHandling padrão do Newtonsoft) para um JValue do tipo
            // Date (como System.DateTime, não DateTimeOffset) — pedir string de volta desse
            // token produz um formato diferente do ISO original e pode falhar o TryParse; pedir
            // DateTimeOffset via Value<T> (conversão rápida/Convert.ChangeType) lança
            // InvalidCastException, porque DateTime não converte direto para DateTimeOffset por
            // esse caminho. ToObject<T> passa pelo JsonSerializer completo, que sabe fazer essa
            // conversão corretamente.
            var exportedAtToken = root["exportedAtUtc"];
            DateTimeOffset? exportedAt;
            try
            {
                exportedAt = exportedAtToken?.ToObject<DateTimeOffset?>();
            }
            catch
            {
                exportedAt = null;
            }

            if (exportedAt == null)
                return ValidationResult.Fail("Campo obrigatório ausente ou inválido: exportedAtUtc.");

            var source = root["source"] as JObject;
            var rollIdToken = source?["rollId"];
            if (rollIdToken == null || (rollIdToken.Type != JTokenType.Integer))
                return ValidationResult.Fail("Campo obrigatório ausente ou inválido: source.rollId.");

            var job = root["job"] as JObject;
            var jobHash = job?.Value<string>("jobHash");
            if (string.IsNullOrWhiteSpace(jobHash))
                return ValidationResult.Fail("Campo obrigatório ausente: job.jobHash.");

            // job.pedidoLabel: o exportador real do Nexor grava `row.get("pedido") or ""` — texto
            // VAZIO quando o trabalho não tem pedido informado. Exigir valor recusava esses
            // arquivos inteiros; vazio ou ausente agora vale "não informado" (PedidoLabel = "").
            var pedidoLabel = job?["pedidoLabel"]?.Type == JTokenType.String ? job!.Value<string>("pedidoLabel") : null;

            var context = new OrderContext
            {
                ExportedAtUtc = exportedAt.Value,
                RollId = rollIdToken.Value<long>(),
                RollName = source?.Value<string>("rollName") ?? "",
                Machine = source?.Value<string>("machine") ?? "",
                InstallationId = source?.Value<string>("installationId"),
                DatabaseId = source?.Value<string>("databaseId"),
                JobHash = jobHash!,
                PedidoLabel = pedidoLabel?.Trim() ?? "",
                PedidoManual = job?.Value<bool?>("pedidoManual") ?? false,
                Fabric = job?.Value<string>("fabric"),
                Tipo = job?.Value<string>("tipo"),
                SourceFileReference = job?.Value<string>("sourceFileReference"),
            };

            return ValidationResult.Ok(context);
        }
    }
}
