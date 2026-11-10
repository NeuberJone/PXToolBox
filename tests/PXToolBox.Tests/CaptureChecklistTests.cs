#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Roteiro das capturas antes/depois
// pendentes (dev.79): itens, pasta dos relatórios e as duas janelas. Pasta temporária isolada;
// nada aqui abre o CorelDRAW.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using PXToolBoxDev.Diagnostics;
using Xunit;

namespace PXToolBox.Tests
{
    public class CaptureChecklistModelTests
    {
        [Fact]
        public void Items_HaveUniqueIds_AndAllTexts()
        {
            var items = CaptureChecklist.All;

            Assert.True(items.Count >= 20);
            Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
            Assert.All(items, i =>
            {
                Assert.Matches("^[a-z0-9.-]+$", i.Id);
                Assert.False(string.IsNullOrWhiteSpace(i.Tool));
                Assert.False(string.IsNullOrWhiteSpace(i.Variant));
                Assert.False(string.IsNullOrWhiteSpace(i.Prepare));
                Assert.False(string.IsNullOrWhiteSpace(i.Action));
            });
        }

        // Regra do usuário: o nome do produto de origem não aparece em nenhum texto visível do PX.
        [Fact]
        public void VisibleTexts_NeverNameTheOriginalProduct()
        {
            Assert.All(CaptureChecklist.All, i =>
                Assert.DoesNotContain("sisbolt", (i.Tool + i.Variant + i.Prepare + i.Action).ToLowerInvariant()));
        }

        // Cobre a coluna "Captura pendente" de docs/sisbolt-tools-investigation.md.
        [Fact]
        public void CoversEveryPendingTool_AndAllEightOtherAnchors()
        {
            var tools = CaptureChecklist.All.Select(i => i.Tool).Distinct().ToList();
            foreach (var tool in new[] { "Esticar no Tamanho do Molde", "Criar grupo com item único", "Ignorar métricas da variável",
                "Alinhamento", "Trocar de Posição", "Replicar PowerClip", "Formato de exportação de imagens", "Alinhamento horizontal automático", "Mão Livre" })
            {
                Assert.Contains(tool, tools);
            }

            Assert.Equal(8, CaptureChecklist.All.Count(i => i.Id.StartsWith("alignment.anchor.")));
        }
    }

    // Ver WpfCodeCreatedControlsCollection (TestCollections.cs). Também serializa o uso da pasta
    // estática de CaptureReportStore.
    [Collection("WPF created in code — no parallel WPF initialization")]
    public class CaptureReportStoreAndWindowsTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "px-capturas-" + Guid.NewGuid().ToString("N"));

        public CaptureReportStoreAndWindowsTests()
        {
            CaptureReportStore.SetDirectoryForTesting(_dir);
            CaptureChecklistWindow.CurrentItemId = null;
        }

        public void Dispose()
        {
            CaptureReportStore.SetDirectoryForTesting(null);
            CaptureChecklistWindow.CurrentItemId = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

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

        private static PageCapture Page() => new PageCapture
        {
            CapturedAt = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero),
            DocumentName = "teste.cdr", PageIndex = 1, ToolVersion = "0.1.0-dev.79",
            RulerUnit = "milímetro (cdrMillimeter)", RulerUnitCode = "cdrMillimeter", GeometryUnitCode = "cdrInch",
            Shapes = Array.Empty<CapturedShape>(),
        };

        [Fact]
        public void Save_NamesByItem_NeverOverwrites_UnknownIsFree()
        {
            var now = new DateTime(2026, 11, 7, 9, 30, 0);
            var first = CaptureReportStore.Save("stretch.off", "a", now);
            var second = CaptureReportStore.Save("stretch.off", "b", now);
            var free = CaptureReportStore.Save("nao.existe", "c", now);

            Assert.Equal("stretch.off__20261107-093000.txt", Path.GetFileName(first));
            Assert.Equal("stretch.off__20261107-093000-2.txt", Path.GetFileName(second));
            Assert.Equal("livre__20261107-093000.txt", Path.GetFileName(free));
            Assert.Equal("a", File.ReadAllText(first));
            var saved = CaptureReportStore.SavedItems();
            Assert.Equal(new[] { "stretch.off" }, saved.Keys);
            Assert.Equal(now, saved["stretch.off"]);
        }

        [Fact]
        public void SavedItems_MissingFolderOrStrayFiles_NeverThrow()
        {
            Assert.Empty(CaptureReportStore.SavedItems());
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "anotacoes.txt"), "x");
            File.WriteAllText(Path.Combine(_dir, "stretch.off__ontem.txt"), "x");
            File.WriteAllText(Path.Combine(_dir, "futuro.item__20261107-093000.txt"), "x");

            Assert.Empty(CaptureReportStore.SavedItems());
        }

        [Fact]
        public void ReportWindow_StartsWithCurrentItem_SavesAndAdvancesToNextPending()
        {
            RunSta(() =>
            {
                CaptureReportStore.Save("single-group.other-name", "já feito");
                CaptureChecklistWindow.CurrentItemId = "stretch.off";
                var window = new PageCaptureReportWindow(Page(), Page());

                Assert.Equal("stretch.off", window.SelectedItemId);
                Assert.Contains("Ferramenta testada: Esticar no Tamanho do Molde — desligar", window.ReportText);

                var path = window.SaveToFolder();

                Assert.Equal("stretch.off", Path.GetFileName(path)!.Split(new[] { "__" }, StringSplitOptions.None)[0]);
                Assert.Contains("desligar", File.ReadAllText(path!));
                // O seguinte (single-group.other-name) já está salvo: pula para o próximo pendente.
                Assert.Equal("single-group.rename", CaptureChecklistWindow.CurrentItemId);

                window.ChooseItem(null);
                Assert.Equal("Nenhum (relatório livre)", window.ChecklistButtonText);
                Assert.DoesNotContain("Ferramenta testada:", window.ReportText);
                window.Close();
            });
        }

        [Fact]
        public void ReportWindow_WithoutCurrentItem_IsFree_UntilAnItemIsChosen()
        {
            RunSta(() =>
            {
                var window = new PageCaptureReportWindow(Page(), Page());
                Assert.Null(window.SelectedItemId);

                window.ChooseItem("free-hand.off");
                Assert.Contains("Ferramenta testada: Mão Livre — desligar", window.ReportText);
                window.Close();
            });
        }

        [Fact]
        public void ChecklistWindow_MarksSavedItems_AndCountsProgress()
        {
            RunSta(() =>
            {
                CaptureReportStore.Save("swap.heights", "x");
                var window = new CaptureChecklistWindow();

                Assert.Equal(CaptureChecklist.All.Count, window.DisplayedItems.Count);
                Assert.True(window.DisplayedItems.Single(i => i.Id == "swap.heights").Done);
                Assert.StartsWith("✓", window.DisplayedItems.Single(i => i.Id == "swap.heights").Text);
                Assert.Equal($"1 de {CaptureChecklist.All.Count} capturas salvas", window.ProgressLine);
                // Sem item atual: começa no primeiro pendente.
                Assert.Equal(CaptureChecklist.All[0].Id, CaptureChecklistWindow.CurrentItemId);

                window.Select("powerclip.replicate");
                Assert.Equal("powerclip.replicate", CaptureChecklistWindow.CurrentItemId);
                window.Close();
            });
        }
    }
}
#endif
