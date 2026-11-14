#if NET48
// Só na perna net48 — PXToolBoxDev (host) só existe lá. Busca de ferramentas do topo do painel.
using System.Linq;
using PXToolBoxDev.PanelLayout;
using Xunit;
using Candidate = PXToolBoxDev.PanelLayout.PanelToolFilter.Candidate;

namespace PXToolBox.Tests
{
    public class PanelToolFilterTests
    {
        private static readonly Candidate[] Tools =
        {
            new Candidate("tools.measures.ruler-width", "Régua da largura", "measures", "Medidas", "Cria embaixo da seleção uma régua em tamanho real."),
            new Candidate("tools.measures.sum-width", "Somar Largura", "measures", "Medidas", "Soma a largura dos objetos selecionados, em metros."),
            new Candidate("layers.hide-selected", "Ocultar camadas dos objetos selecionados", "layers", "Minhas camadas", "Oculta as camadas."),
            new Candidate("molds.propagate-logos", "Propagar logos", "molds", "Cadastro de moldes", "Os logos (L1, L2…) do grupo com mais logos vão para os outros."),
        };

        private static string[] Ids(string query) => PanelToolFilter.Filter(Tools, query).Select(c => c.Id).ToArray();

        [Fact]
        public void Matches_NameSectionOrHelp_IgnoringCaseAndAccents()
        {
            Assert.Equal(new[] { "tools.measures.ruler-width" }, Ids("regua"));
            Assert.Equal(new[] { "tools.measures.ruler-width", "tools.measures.sum-width" }, Ids("LARGURA"));
            Assert.Equal(new[] { "molds.propagate-logos" }, Ids("l2"));
        }

        [Fact]
        public void SectionTitle_IsTheCurrentCustomName()
        {
            Assert.Equal(new[] { "layers.hide-selected" }, Ids("minhas"));
        }

        [Fact]
        public void EveryWordMustMatch_KeepingTheGivenOrder()
        {
            Assert.Equal(new[] { "tools.measures.sum-width" }, Ids("largura metros"));
            Assert.Equal(new[] { "tools.measures.ruler-width", "tools.measures.sum-width", "layers.hide-selected" }, Ids("selec"));
            Assert.Empty(Ids("régua camada"));
        }

        [Fact]
        public void EmptyQuery_ShowsNothing()
        {
            Assert.Empty(Ids(""));
            Assert.Empty(Ids("   "));
            Assert.Empty(PanelToolFilter.Filter(Tools, null));
        }
    }
}
#endif
