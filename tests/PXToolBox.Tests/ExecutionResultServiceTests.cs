#if NET48
// Só compila na perna net48 — PXToolBoxDev só existe lá. Resultado de execução PX → Nexor
// (Integrations/ExecutionResultService.cs, docs/integrations/px-execution-result.md).
using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    public class ExecutionResultServiceTests
    {
        private static OrderContextService.OrderContext Context(long rollId = 482, string jobHash = "9f1c2a7e4b3d", string? databaseId = "73d0ce708f00", string pedido = "Dryfit - João Silva") =>
            new OrderContextService.OrderContext
            {
                ExportedAtUtc = new DateTimeOffset(2026, 10, 29, 21, 0, 0, TimeSpan.Zero),
                RollId = rollId,
                RollName = "M1_29-10-2026_0013",
                Machine = "M1",
                InstallationId = "2678dca3c653",
                DatabaseId = databaseId,
                JobHash = jobHash,
                PedidoLabel = pedido,
            };

        private static ExecutionResultService.OutputRecord Output(string name, string path, int minute = 0) =>
            new ExecutionResultService.OutputRecord(ExecutionResultService.KindFor(name), name, "Dryfit - João Silva", path,
                new DateTimeOffset(2026, 11, 5, 19, minute, 0, TimeSpan.Zero));

        // Datas lidas como texto, como um leitor qualquer do arquivo (o Nexor é Python).
        private static JObject Parse(string json) =>
            JsonConvert.DeserializeObject<JObject>(json, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None })!;

        [Fact]
        public void Build_WritesContractV1()
        {
            var json = ExecutionResultService.Build(Context(), new[] { Output("Faixa De Capitão", @"Z:\S\M1\Faixa De Capitão - X.cdr") },
                new DateTimeOffset(2026, 11, 5, 16, 30, 0, TimeSpan.FromHours(-3)), "0.1.0-dev.65");

            var root = Parse(json);
            Assert.Equal("PxExecutionResult", (string?)root["formatId"]);
            Assert.Equal(1, (int)root["schemaVersion"]!);
            Assert.Equal("2026-11-05T19:30:00Z", (string?)root["producedAtUtc"]);
            Assert.Equal("0.1.0-dev.65", (string?)root["producer"]!["version"]);

            var context = root["context"]!;
            Assert.Equal("NexorOrderContext", (string?)context["formatId"]);
            Assert.Equal("2026-10-29T21:00:00Z", (string?)context["exportedAtUtc"]);
            Assert.Equal(482, (long)context["rollId"]!);
            Assert.Equal("9f1c2a7e4b3d", (string?)context["jobHash"]);
            Assert.Equal("73d0ce708f00", (string?)context["databaseId"]);
            Assert.Equal("Dryfit - João Silva", (string?)context["pedidoLabel"]);

            var output = Assert.Single(root["outputs"]!);
            Assert.Equal("faixa-de-capitao", (string?)output["kind"]);
            Assert.Equal("Faixa De Capitão", (string?)output["label"]);
            Assert.Equal(@"Z:\S\M1\Faixa De Capitão - X.cdr", (string?)output["filePath"]);
            Assert.Equal("2026-11-05T19:00:00Z", (string?)output["savedAtUtc"]);
            Assert.Null(root["status"]);
        }

        [Fact]
        public void Build_OmitsMissingOptionalIds()
        {
            var json = ExecutionResultService.Build(Context(databaseId: null), new[] { Output("Escudo", @"Z:\a.cdr") }, DateTimeOffset.UtcNow, "x");

            Assert.Null(Parse(json)["context"]!["databaseId"]);
        }

        [Fact]
        public void Session_RecordsOnlyWithContext_AndReplacesSameFile()
        {
            var session = new ExecutionResultService.Session();
            session.Record(Output("Escudo", @"Z:\a.cdr"));
            Assert.Empty(session.Outputs);

            session.SetContext(Context());
            session.Record(Output("Escudo", @"Z:\a.cdr", 1));
            session.Record(Output("Bandeira", @"Z:\b.cdr", 2));
            session.Record(Output("Escudo", @"z:\A.CDR", 3));

            Assert.Equal(new[] { "Bandeira", "Escudo" }, session.Outputs.Select(o => o.Label));
            Assert.Equal(3, session.Outputs[1].SavedAtUtc.Minute);
        }

        [Fact]
        public void Session_SameJobKeepsOutputs_OtherJobOrClearResets()
        {
            var session = new ExecutionResultService.Session();
            session.SetContext(Context());
            session.Record(Output("Escudo", @"Z:\a.cdr"));

            session.SetContext(Context(pedido: "reclassificado"));
            Assert.Single(session.Outputs);

            session.SetContext(Context(jobHash: "outro"));
            Assert.Empty(session.Outputs);

            session.Record(Output("Escudo", @"Z:\a.cdr"));
            session.SetContext(Context(jobHash: "outro", databaseId: "outra-base"));
            Assert.Empty(session.Outputs);

            session.Record(Output("Escudo", @"Z:\a.cdr"));
            session.SetContext(null);
            Assert.Empty(session.Outputs);
            Assert.Null(session.Context);
        }

        [Fact]
        public void SuggestFileName_IsSafe()
        {
            var context = Context();
            context.RollName = "M1/29:10 0013";

            Assert.Equal("px-resultado-M1_29_10_0013-9f1c2a7e4b3d.json", ExecutionResultService.SuggestFileName(context));
        }
    }
}
#endif
