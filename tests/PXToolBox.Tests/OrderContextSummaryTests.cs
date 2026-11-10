#if NET48
// Só compila na perna net48 — PXToolBoxDev só existe lá. Resumo do contexto de produção na seção
// Produção (Production/OrderContextSummary.cs) e a aceitação de "pedido não informado" pelo
// leitor (o exportador real do Nexor grava pedidoLabel vazio nesse caso).
using System;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.Production;
using Xunit;

namespace PXToolBox.Tests
{
    public class OrderContextSummaryTests
    {
        private static string Json(string job, string source = @"""rollId"": 482, ""rollName"": ""M1_29-10-2026_0013"", ""machine"": ""M1""") =>
            @"{ ""formatId"": ""NexorOrderContext"", ""schemaVersion"": 1, ""exportedAtUtc"": ""2026-10-29T21:00:00Z"", " +
            @"""source"": { " + source + @" }, ""job"": { " + job + @" } }";

        private static OrderContextService.OrderContext Load(string json)
        {
            var result = OrderContextService.Validate(json);
            Assert.True(result.IsValid, result.FailureReason);
            return result.Context!;
        }

        [Fact]
        public void FullFile_ShowsRollJobPedidoAndExportTime()
        {
            var context = Load(Json(@"""jobHash"": ""9f1c2a7e4b3d"", ""pedidoLabel"": ""Dryfit - João Silva"", ""fabric"": ""Dryfit"", ""tipo"": ""Camiseta"", " +
                                    @"""sourceFileReference"": ""D:\\Impressao\\M1\\16-10 - Dryfit - Joao Silva.pdf"""));

            var summary = OrderContextSummary.Build(context);

            Assert.Equal("Rolo M1_29-10-2026_0013 (#482) · máquina M1", summary.RollLine);
            Assert.Equal("Trabalho de impressão: 16-10 - Dryfit - Joao Silva.pdf (Camiseta · Dryfit)", summary.JobLine);
            Assert.Equal("Pedido informado: Dryfit - João Silva", summary.PedidoLine);
            Assert.Equal($"Exportado do Nexor em {new DateTimeOffset(2026, 10, 29, 21, 0, 0, TimeSpan.Zero).ToLocalTime():dd/MM/yyyy HH:mm}", summary.ExportedLine);
        }

        // O exportador real grava `row.get("pedido") or ""` — antes, esse arquivo era recusado.
        [Theory]
        [InlineData(@"""jobHash"": ""abc"", ""pedidoLabel"": """"")]
        [InlineData(@"""jobHash"": ""abc"", ""pedidoLabel"": ""   """)]
        [InlineData(@"""jobHash"": ""abc""")]
        [InlineData(@"""jobHash"": ""abc"", ""pedidoLabel"": null")]
        public void PedidoNotInformed_IsAccepted_AndHiddenFromSummary(string job)
        {
            var context = Load(Json(job));

            Assert.False(context.HasPedidoLabel);
            Assert.Equal("", context.PedidoLabel);
            Assert.Null(OrderContextSummary.Build(context).PedidoLine);
        }

        [Fact]
        public void MinimalOldFile_UsesJobCodeAndRollNumber()
        {
            var context = Load(Json(@"""jobHash"": ""9f1c2a7e4b3d"", ""pedidoLabel"": ""Qualquer""", @"""rollId"": 7"));

            var summary = OrderContextSummary.Build(context);

            Assert.Equal("Rolo #7", summary.RollLine);
            Assert.Equal("Trabalho de impressão: código 9f1c2a7e4b3d", summary.JobLine);
            Assert.Equal("Pedido informado: Qualquer", summary.PedidoLine);
        }

        // "Nome do layout" vai para o nome do arquivo salvo ("Escudo - <layout>.cdr").
        [Theory]
        [InlineData(@"""pedidoLabel"": ""Dryfit - João Silva"", ""sourceFileReference"": ""D:\\x\\outro.pdf""", "Dryfit - João Silva")]
        [InlineData(@"""pedidoLabel"": """", ""sourceFileReference"": ""D:\\Impressao\\M1\\16-10 - Dryfit.pdf""", "16-10 - Dryfit")]
        [InlineData(@"""sourceFileReference"": ""/mnt/rede/arquivo.sem.ponto.final.pdf""", "arquivo.sem.ponto.final")]
        [InlineData(@"""pedidoLabel"": ""Time A/B: final?  <2026> """, "Time A B final 2026")]
        [InlineData(@"""pedidoLabel"": """"", null)]
        [InlineData(@"""pedidoLabel"": ""///""", null)]
        public void SuggestLayoutName_PedidoThenFileName_SafeForFileNames(string extraJob, string? expected)
        {
            var context = Load(Json(@"""jobHash"": ""abc"", " + extraJob));

            Assert.Equal(expected, OrderContextSummary.SuggestLayoutName(context));
        }

        // Subpasta do rolo para Escudo/Bandeira/Faixa: nome do rolo limpo; sem nome, o número.
        [Theory]
        [InlineData(@"""rollId"": 482, ""rollName"": ""M1_29-10-2026_0013""", "M1_29-10-2026_0013")]
        [InlineData(@"""rollId"": 482, ""rollName"": ""M1/29:10 ""","M1 29 10")]
        [InlineData(@"""rollId"": 7", "Rolo 7")]
        [InlineData(@"""rollId"": 7, ""rollName"": ""??""", "Rolo 7")]
        public void RollFolderName_RollNameOrNumber(string source, string expected)
        {
            var context = Load(Json(@"""jobHash"": ""abc""", source));

            Assert.Equal(expected, OrderContextSummary.RollFolderName(context));
        }

        [Fact]
        public void OutputNote_NamesRollAndJob()
        {
            var withFile = Load(Json(@"""jobHash"": ""abc"", ""sourceFileReference"": ""D:\\x\\16-10 - Dryfit.pdf"""));
            var withoutFile = Load(Json(@"""jobHash"": ""abc""", @"""rollId"": 7"));

            Assert.Equal("Contexto: rolo M1_29-10-2026_0013 · trabalho 16-10 - Dryfit.pdf", OrderContextSummary.OutputNote(withFile));
            Assert.Equal("Contexto: rolo #7 · trabalho código abc", OrderContextSummary.OutputNote(withoutFile));
        }

        [Fact]
        public void JobHashStillRequired()
        {
            Assert.False(OrderContextService.Validate(Json(@"""pedidoLabel"": ""Qualquer""")).IsValid);
        }
    }
}
#endif
