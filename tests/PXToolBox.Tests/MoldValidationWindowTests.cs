#if NET48
// Só na perna net48 — PXToolBoxDev (host) só existe lá. Janela do "Validar molde": mostra o
// relatório do serviço e o copia como texto, erros primeiro.
using System;
using System.Threading;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using PXToolBoxDev.Molds;
using Xunit;

namespace PXToolBox.Tests
{
    [Collection("WPF created in code — no parallel WPF initialization")]
    public class MoldValidationWindowTests
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

        private static FakeShape SizeGroup(string size, bool tag, params string[] logos)
        {
            var group = new FakeShape { Name = size, Kind = ShapeKind.Group };
            void Add(string name, double cx) =>
                group.ShapesList.Add(new FakeShape { Name = name, Kind = ShapeKind.Curve, CenterX = cx, Width = 10, Height = 100, ParentGroup = group });
            if (tag) Add("TAG", 0);
            for (var i = 0; i < logos.Length; i++) Add(logos[i], 20 * (i + 1));
            Add("Frente", 0);
            Add("x", 0);
            return group;
        }

        [Fact]
        public void Window_ShowsSummaryAndIssues_AndCopiesThemAsText()
        {
            var layer = new FakeLayer { Name = "Camada 1" };
            layer.AddShape(SizeGroup("G", true, "L1", "L1"));
            layer.AddShape(SizeGroup("M", false));
            var page = new FakePage();
            page.LayersList.Add(layer);
            var report = new MoldValidationService().Validate(page);

            RunSta(() =>
            {
                var window = new MoldValidationWindow();
                window.Display(report);

                var lines = window.ReportText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                Assert.Equal(report.Summary, lines[0]);
                Assert.Equal("Peças: Frente.", lines[1]);
                Assert.Equal("ERRO: Grupo \"G\" da peça Frente: L1 aparece 2 vezes.", lines[2]);
                Assert.Contains("AVISO: Grupo \"M\" da peça Frente: sem TAG (TAG).", lines);
                window.Close();
            });
        }
    }
}
#endif
