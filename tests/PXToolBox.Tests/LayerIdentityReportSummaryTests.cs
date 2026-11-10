#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Resumo curto do relatório do
// teste de identidade (Diagnostics/LayerIdentityReportSummary.cs): só lê os marcadores.
using PXToolBoxDev.Diagnostics;
using Xunit;

namespace PXToolBox.Tests
{
    public class LayerIdentityReportSummaryTests
    {
        private const string Header = "Diagnóstico de identidade de sessão — PX Tool Box v0.1.0-dev.52, CorelDRAW 27.1.0.129\n";

        [Fact]
        public void NoFailures_NoObservations()
        {
            var report = Header + "  [OK] encontrado exatamente 1 vez\n\nResultado: nenhuma falha nesta execução.\n";

            Assert.Equal("Diagnóstico concluído: nenhuma falha.", LayerIdentityReportSummary.Summarize(report));
        }

        [Fact]
        public void NoFailures_WithObservations_MentionsThem()
        {
            var report = Header + "  [OK] a\r\n  [OBS] b: sim\r\n  [OBS] c: não\r\n\r\nResultado: nenhuma falha nesta execução.\r\n";

            Assert.Equal("Diagnóstico concluído: nenhuma falha (2 observações no relatório).", LayerIdentityReportSummary.Summarize(report));
        }

        [Fact]
        public void SingleItems_UseSingular()
        {
            Assert.Equal("Diagnóstico concluído: nenhuma falha (1 observação no relatório).",
                LayerIdentityReportSummary.Summarize(Header + "  [OBS] b: sim\nResultado: nenhuma falha.\n"));
            Assert.Equal("Diagnóstico concluído: 1 falha — veja o relatório.",
                LayerIdentityReportSummary.Summarize(Header + "  [FALHOU] x\nResultado: 1 falha(s).\n"));
        }

        [Fact]
        public void FailuresAndErrors_AreCountedTogether()
        {
            var report = Header + "  [FALHOU] x\n  [OK] y\n[ERRO] interrompido: COMException\n\nResultado: 2 falha(s).\n";

            Assert.Equal("Diagnóstico concluído: 2 falhas — veja o relatório.", LayerIdentityReportSummary.Summarize(report));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("  [OK] a\n")]
        public void WithoutFinalResultLine_NeverClaimsSuccess(string? report)
        {
            Assert.DoesNotContain("nenhuma falha", LayerIdentityReportSummary.Summarize(report!));
        }
    }
}
#endif
