#if NET48
// Só na perna net48 — PXToolBoxDev (host) só existe lá. Manual de uso: toda ferramenta do
// catálogo tem texto, nenhum texto sobra de ferramenta removida, e a janela mostra e busca.
using System;
using System.Linq;
using System.Threading;
using PXToolBoxDev.Favorites;
using PXToolBoxDev.Manual;
using PXToolBoxDev.PanelLayout;
using Xunit;

namespace PXToolBox.Tests
{
    [Collection("WPF created in code — no parallel WPF initialization")]
    public class UserManualTests
    {
        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        // Ferramenta nova, removida ou alterada → atualize Manual/user-manual.json (AGENTS.md).
        [Fact]
        public void EveryCatalogTool_HasPurposeAndSteps_AndNoTextIsLeftForRemovedTools()
        {
            var catalog = PanelToolCatalog.All.Select(t => t.Id).ToList();

            Assert.Empty(catalog.Except(UserManual.DocumentedToolIds));
            Assert.Empty(UserManual.DocumentedToolIds.Except(catalog));
            Assert.Equal(UserManual.DocumentedToolIds.Count, UserManual.DocumentedToolIds.Distinct().Count());
            Assert.All(catalog.Select(id => UserManual.Find(id)!), tool =>
            {
                Assert.False(string.IsNullOrWhiteSpace(tool.Purpose), tool.Id);
                Assert.NotEmpty(tool.Steps);
                Assert.All(tool.Steps.Concat(tool.Notes), line => Assert.False(string.IsNullOrWhiteSpace(line), tool.Id));
            });
        }

        [Fact]
        public void EverySectionWithTools_HasAnIntro_AndNoIntroForUnknownSections()
        {
            var withTools = PanelToolCatalog.All.Select(t => t.Section).Distinct().ToList();

            Assert.Empty(withTools.Except(UserManual.DocumentedSections));
            Assert.Empty(UserManual.DocumentedSections.Except(PanelSectionCatalog.All.Select(s => s.Title)));
            Assert.Equal(withTools.Count, UserManual.Sections.Count);
            Assert.All(UserManual.Sections, s => Assert.False(string.IsNullOrWhiteSpace(s.Intro), s.Title));
            Assert.Equal(PanelToolCatalog.All.Count, UserManual.Sections.Sum(s => s.Tools.Count));
        }

        // Regra do usuário: nomes de produtos de origem não aparecem em texto visível do PX.
        [Fact]
        public void Text_NeverNamesTheSourceProducts()
        {
            var all = string.Join("\n", UserManual.Topics.SelectMany(t => new[] { t.Title }.Concat(t.Paragraphs))
                .Concat(UserManual.Sections.SelectMany(s => new[] { s.Intro }.Concat(s.Tools.SelectMany(t => new[] { t.Purpose }.Concat(t.Steps).Concat(t.Notes))))));

            Assert.DoesNotContain("sisbolt", all, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("moldmotor", all, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Search_IgnoresCaseAndAccents_AndNeedsEveryWord()
        {
            Assert.Contains(UserManual.Search("LASER"), t => t.Id == "molds.define");
            Assert.Contains(UserManual.Search("camadas"), t => t.Id == "layers.hide-selected");
            Assert.Contains(UserManual.Search("saida faixa capitao"), t => t.Id == "output.prepare-captain-sash");
            Assert.DoesNotContain(UserManual.Search("saida faixa capitao"), t => t.Id == "output.prepare-shield");
            Assert.Empty(UserManual.Search("   "));
            Assert.Empty(UserManual.Search("xyzzy"));
        }

        [Fact]
        public void Window_ShowsTopicsAndSections_ToolsInCatalogOrder_AndSearches()
        {
            RunSta(() =>
            {
                var window = new UserManualWindow();
                Assert.Equal(UserManual.Topics.Select(t => t.Title).Concat(UserManual.Sections.Select(s => s.Title)), window.NavigationTitles);
                Assert.Contains("O PX Tool Box é um painel", window.DisplayedText);

                window.ShowPage("Cadastro de moldes");
                Assert.Equal(PanelToolCatalog.All.Where(t => t.Section == "Cadastro de moldes").Select(t => t.Id), window.DisplayedToolIds);
                Assert.Contains("Definir objetos", window.DisplayedText);
                Assert.Contains("COMO USAR", window.DisplayedText);

                // Filtro: a lista só mantém o que tem as palavras; a página da seção, só as ferramentas que combinam.
                window.SearchText = "laser";
                Assert.Equal(UserManual.Search("laser").Select(t => t.Id), window.DisplayedToolIds);
                Assert.Contains("Seção Cadastro de moldes", window.DisplayedText);
                Assert.Contains("Cadastro de moldes", window.NavigationTitles);
                Assert.DoesNotContain("Medidas", window.NavigationTitles);
                Assert.DoesNotContain("Favoritos", window.NavigationTitles);
                Assert.Null(window.SelectedNavigationTitle);

                window.ShowPage("Cadastro de moldes");
                var moldsWithLaser = UserManual.Search("laser").Where(t => t.Section == "Cadastro de moldes").Select(t => t.Id).ToList();
                Assert.Equal(moldsWithLaser, window.DisplayedToolIds);
                Assert.True(moldsWithLaser.Count < PanelToolCatalog.All.Count(t => t.Section == "Cadastro de moldes"));

                window.SearchText = "xyzzy";
                Assert.Empty(window.DisplayedToolIds);
                Assert.Empty(window.NavigationTitles);
                Assert.Contains("Nada encontrado", window.DisplayedText);

                // Limpar o filtro volta à página de antes dele, com tudo.
                window.SearchText = "";
                Assert.Equal(UserManual.Topics.Select(t => t.Title).Concat(UserManual.Sections.Select(s => s.Title)), window.NavigationTitles);
                Assert.Equal(PanelToolCatalog.All.Where(t => t.Section == "Cadastro de moldes").Select(t => t.Id), window.DisplayedToolIds);
                window.Close();
            });
        }
    }
}
#endif
