#if NET48
// Só compila na perna net48 — PXToolBoxDev.Integrations só existe lá. Testa
// Integrations/OrderContextService.cs (primeira recepção do contrato OrderContext — ver
// docs/integrations/nexor-order-context.md). Só a validação/parse é testada aqui (lógica pura);
// a janela de exibição (Production/OrderContextWindow) é UI, fora do escopo de teste automatizado
// deste projeto, como as demais janelas WPF.
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    public class OrderContextServiceTests
    {
        private const string ValidExample = @"{
            ""formatId"": ""NexorOrderContext"",
            ""schemaVersion"": 1,
            ""exportedAtUtc"": ""2026-10-29T21:00:00Z"",
            ""source"": { ""rollId"": 482, ""rollName"": ""M1_29-10-2026_0013"", ""machine"": ""M1"", ""installationId"": ""abc123def456"", ""databaseId"": ""db7890fedcba"" },
            ""job"": {
                ""jobHash"": ""9f1c2a7e4b3d"",
                ""pedidoLabel"": ""Dryfit - João Silva"",
                ""pedidoManual"": false,
                ""fabric"": ""Dryfit"",
                ""tipo"": ""Camiseta"",
                ""sourceFileReference"": ""D:\\Impressao\\M1\\16-10 - Dryfit - Joao Silva.pdf""
            }
        }";

        [Fact]
        public void Validate_ValidExample_RecognizesAllFields()
        {
            var result = OrderContextService.Validate(ValidExample);

            Assert.True(result.IsValid);
            Assert.Equal(482, result.Context!.RollId);
            Assert.Equal("M1_29-10-2026_0013", result.Context.RollName);
            Assert.Equal("M1", result.Context.Machine);
            Assert.Equal("abc123def456", result.Context.InstallationId);
            Assert.Equal("db7890fedcba", result.Context.DatabaseId);
            Assert.Equal("9f1c2a7e4b3d", result.Context.JobHash);
            Assert.Equal("Dryfit - João Silva", result.Context.PedidoLabel);
            Assert.False(result.Context.PedidoManual);
            Assert.Equal("Dryfit", result.Context.Fabric);
            Assert.Equal("Camiseta", result.Context.Tipo);
            Assert.Equal(@"D:\Impressao\M1\16-10 - Dryfit - Joao Silva.pdf", result.Context.SourceFileReference);
        }

        [Fact]
        public void Validate_OptionalFieldsAbsent_StillValid()
        {
            var json = @"{
                ""formatId"": ""NexorOrderContext"",
                ""schemaVersion"": 1,
                ""exportedAtUtc"": ""2026-10-29T21:00:00Z"",
                ""source"": { ""rollId"": 1 },
                ""job"": { ""jobHash"": ""abc"", ""pedidoLabel"": ""Qualquer"" }
            }";

            var result = OrderContextService.Validate(json);

            Assert.True(result.IsValid);
            Assert.Null(result.Context!.Fabric);
            Assert.Null(result.Context.Tipo);
            Assert.Null(result.Context.SourceFileReference);
            Assert.Equal("", result.Context.RollName);
            Assert.False(result.Context.PedidoManual);
            // installationId/databaseId são opcionais — um arquivo de uma versão anterior do
            // exportador do Nexor (sem esses campos) precisa continuar sendo aceito.
            Assert.Null(result.Context.InstallationId);
            Assert.Null(result.Context.DatabaseId);
        }

        [Fact]
        public void Validate_InvalidJson_FailsWithoutThrowing()
        {
            var result = OrderContextService.Validate("isto nao e json {{{");

            Assert.False(result.IsValid);
            Assert.NotNull(result.FailureReason);
        }

        [Fact]
        public void Validate_MissingFormatId_Fails()
        {
            var result = OrderContextService.Validate(@"{""schemaVersion"":1}");

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_WrongFormatId_Fails()
        {
            var result = OrderContextService.Validate(@"{""formatId"":""OutroFormato"",""schemaVersion"":1}");

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_IncompatibleSchemaVersion_Fails()
        {
            var result = OrderContextService.Validate(
                $@"{{""formatId"":""{OrderContextService.FormatId}"",""schemaVersion"":999}}");

            Assert.False(result.IsValid);
            Assert.Contains("esquema", result.FailureReason!, System.StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(@"{""formatId"":""NexorOrderContext"",""schemaVersion"":1}")] // sem exportedAtUtc/source/job
        [InlineData(@"{""formatId"":""NexorOrderContext"",""schemaVersion"":1,""exportedAtUtc"":""2026-10-29T21:00:00Z""}")] // sem source/job
        [InlineData(@"{""formatId"":""NexorOrderContext"",""schemaVersion"":1,""exportedAtUtc"":""2026-10-29T21:00:00Z"",""source"":{""rollId"":1}}")] // sem job
        [InlineData(@"{""formatId"":""NexorOrderContext"",""schemaVersion"":1,""exportedAtUtc"":""2026-10-29T21:00:00Z"",""source"":{""rollId"":1},""job"":{}}")] // job sem jobHash/pedidoLabel
        public void Validate_MissingRequiredFields_Fails(string json)
        {
            var result = OrderContextService.Validate(json);

            Assert.False(result.IsValid);
            Assert.NotNull(result.FailureReason);
        }

        [Fact]
        public void Validate_RollIdWrongType_Fails()
        {
            var json = @"{
                ""formatId"": ""NexorOrderContext"",
                ""schemaVersion"": 1,
                ""exportedAtUtc"": ""2026-10-29T21:00:00Z"",
                ""source"": { ""rollId"": ""nao-e-numero"" },
                ""job"": { ""jobHash"": ""abc"", ""pedidoLabel"": ""Qualquer"" }
            }";

            var result = OrderContextService.Validate(json);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_PedidoManualTrue_IsRecognized()
        {
            var json = @"{
                ""formatId"": ""NexorOrderContext"",
                ""schemaVersion"": 1,
                ""exportedAtUtc"": ""2026-10-29T21:00:00Z"",
                ""source"": { ""rollId"": 1 },
                ""job"": { ""jobHash"": ""abc"", ""pedidoLabel"": ""Qualquer"", ""pedidoManual"": true }
            }";

            var result = OrderContextService.Validate(json);

            Assert.True(result.IsValid);
            Assert.True(result.Context!.PedidoManual);
        }

        [Fact]
        public void Validate_SameRollIdAndJobHash_DifferentSource_KeepsDistinctIdentifiers()
        {
            // Cenário explícito pedido na tarefa: dois contextos com o MESMO (rollId, jobHash)
            // — plausível entre bases Nexor diferentes, já que esse par só é único DENTRO de
            // uma base (ver docs/ARCHITECTURE.md, seções 22-23) — vindos de instalações/bases
            // DIFERENTES. O importador nunca deve fundir/igualar esses contextos: cada um
            // mantém seu próprio installationId/databaseId, nunca presumidos iguais.
            string JsonFor(string installationId, string databaseId) => $@"{{
                ""formatId"": ""NexorOrderContext"",
                ""schemaVersion"": 1,
                ""exportedAtUtc"": ""2026-10-29T21:00:00Z"",
                ""source"": {{ ""rollId"": 1, ""installationId"": ""{installationId}"", ""databaseId"": ""{databaseId}"" }},
                ""job"": {{ ""jobHash"": ""mesmo-hash"", ""pedidoLabel"": ""Qualquer"" }}
            }}";

            var fromMachineA = OrderContextService.Validate(JsonFor("maquina-a", "base-a"));
            var fromMachineB = OrderContextService.Validate(JsonFor("maquina-b", "base-b"));

            Assert.True(fromMachineA.IsValid);
            Assert.True(fromMachineB.IsValid);
            Assert.Equal(fromMachineA.Context!.RollId, fromMachineB.Context!.RollId);
            Assert.Equal(fromMachineA.Context.JobHash, fromMachineB.Context.JobHash);
            Assert.NotEqual(fromMachineA.Context.InstallationId, fromMachineB.Context.InstallationId);
            Assert.NotEqual(fromMachineA.Context.DatabaseId, fromMachineB.Context.DatabaseId);
        }
    }
}
#endif
