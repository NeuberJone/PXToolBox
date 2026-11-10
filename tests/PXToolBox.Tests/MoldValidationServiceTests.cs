using System.Linq;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;
using Severity = PXToolBox.Outputs.Tools.MoldValidationService.Severity;

namespace PXToolBox.Tests
{
    public class MoldValidationServiceTests
    {
        private readonly FakePage _page = new FakePage();
        private readonly FakeLayer _layer = new FakeLayer { Name = "MOLDE.N.MA.FRENTE" };

        public MoldValidationServiceTests()
        {
            _page.LayersList.Add(_layer);
        }

        /// <summary>Grupo de tamanho montado (TAG, logos, peça, x), com a peça de centro (x, y).
        /// Logos: posição em relação ao centro da peça, em alturas da peça.</summary>
        private static FakeShape SizeGroup(string size, double x, double y, double height, string piece = "Frente", bool tag = true,
            params (string Name, double DX, double DY)[] logos)
        {
            var group = new FakeShape { Name = size, Kind = ShapeKind.Group };
            void Add(string name, double cx, double cy, double w, double h) =>
                group.ShapesList.Add(new FakeShape { Name = name, Kind = ShapeKind.Curve, CenterX = cx, CenterY = cy, Width = w, Height = h, ParentGroup = group });
            if (tag) Add("TAG", x, y - height / 2 + 5, 20, 10);
            foreach (var logo in logos) Add(logo.Name, x + logo.DX * height, y + logo.DY * height, 5, 5);
            Add(piece, x, y, height / 2, height);
            Add("x", x, y, height / 2, height);
            return group;
        }

        private FakeShape OnLayer(FakeShape shape) => _layer.AddShape(shape);

        private MoldValidationService.Report Validate() => new MoldValidationService().Validate(_page);

        [Fact]
        public void ConsistentMold_HasNoIssues_AndSizeGroupsInsideOtherGroupsCount()
        {
            OnLayer(SizeGroup("G", 0, 0, 200, "Frente", true, ("L1", -0.2, 0.3), ("L2", 0.1, -0.2)));
            OnLayer(SizeGroup("M", 500, 0, 100, "Frente", true, ("L1", -0.2, 0.3), ("L2", 0.1, -0.2)));
            var box = OnLayer(new FakeShape { Name = "Caixa", Kind = ShapeKind.Group });
            box.ShapesList.Add(SizeGroup("P", 900, 0, 80, "Frente", true, ("L1", -0.21, 0.31), ("L2", 0.1, -0.2)));
            OnLayer(SizeGroup("G", 0, 600, 200, "Costas", true, ("L3", 0, 0.2)));

            var report = Validate();

            Assert.Empty(report.Issues);
            Assert.Equal(4, report.GroupCount);
            Assert.Equal(new[] { "Frente", "Costas" }, report.Pieces);
            Assert.Equal("4 grupo(s) de tamanho de 2 peça(s): nenhum problema encontrado. Nada foi alterado.", report.Summary);
        }

        [Fact]
        public void LogoInThePlaceOfAnotherNumber_IsAnError_MisplacedAndMissingAreWarnings()
        {
            OnLayer(SizeGroup("G", 0, 0, 200, "Frente", true, ("L1", -0.2, 0.3), ("L2", 0.1, -0.2), ("L3", 0.2, 0.2)));
            // M: L1 e L2 trocados de lugar; sem L3.
            OnLayer(SizeGroup("M", 500, 0, 100, "Frente", true, ("L2", -0.2, 0.3), ("L1", 0.1, -0.2)));
            // P: L1 longe do lugar, L2 no lugar, sem L3.
            OnLayer(SizeGroup("P", 900, 0, 80, "Frente", true, ("L1", 0.4, -0.4), ("L2", 0.1, -0.2)));

            var report = Validate();

            Assert.Equal(2, report.ErrorCount);
            var errors = report.Issues.Where(i => i.Severity == Severity.Error).Select(i => i.Message).ToList();
            Assert.Contains("Grupo \"M\" da peça Frente: L1 está no lugar do L2 do \"G\" (a referência). O mesmo logo precisa do mesmo número em todos os tamanhos.", errors);
            Assert.Contains("Grupo \"M\" da peça Frente: L2 está no lugar do L1 do \"G\" (a referência). O mesmo logo precisa do mesmo número em todos os tamanhos.", errors);
            var warnings = report.Issues.Where(i => i.Severity == Severity.Warning).Select(i => i.Message).ToList();
            // Na ordem de tamanhos das configurações (P antes de M).
            Assert.Equal(new[]
            {
                "Grupo \"P\" da peça Frente: L1 não está no mesmo lugar que no \"G\" (a referência). Propagar logos acerta a posição.",
                "Grupo \"P\" da peça Frente: falta L3, que o \"G\" (a referência) tem. Propagar logos cria o que falta.",
                "Grupo \"M\" da peça Frente: falta L3, que o \"G\" (a referência) tem. Propagar logos cria o que falta.",
            }, warnings);
            Assert.Equal(Severity.Error, report.Issues[0].Severity);
            Assert.Contains("2 erro(s) e 3 aviso(s)", report.Summary);
        }

        [Fact]
        public void SameNumberInTwoPieces_RepeatedLogo_MissingTag_RepeatedSize_AndUnrecognizedPiece()
        {
            OnLayer(SizeGroup("G", 0, 0, 200, "Frente", true, ("L1", -0.2, 0.3), ("L1", 0.1, -0.2)));
            OnLayer(SizeGroup("M", 500, 0, 100, "Frente", false, ("L1", -0.2, 0.3)));
            OnLayer(SizeGroup("M", 700, 0, 100, "Frente", true, ("L1", -0.2, 0.3)));
            OnLayer(SizeGroup("G", 0, 600, 200, "Costas", true, ("L1", 0, 0.2)));
            var broken = OnLayer(new FakeShape { Name = "P", Kind = ShapeKind.Group });
            broken.ShapesList.Add(new FakeShape { Name = "Frente", Kind = ShapeKind.Curve, Width = 10, Height = 10, ParentGroup = broken });
            broken.ShapesList.Add(new FakeShape { Name = "Outro", Kind = ShapeKind.Curve, Width = 10, Height = 10, ParentGroup = broken });

            var report = Validate();
            var messages = report.Issues.Select(i => (i.Severity, i.Message)).ToList();

            Assert.Contains((Severity.Error, "Grupo \"G\" da peça Frente: L1 aparece 2 vezes."), messages);
            Assert.Contains((Severity.Error, "L1 é usado pelas peças Frente, Costas. Cada peça precisa de números próprios (Duplicar peça já faz assim)."), messages);
            Assert.Contains((Severity.Error, "Grupo \"P\" (camada MOLDE.N.MA.FRENTE): há mais de um objeto candidato a peça dentro do grupo. Redefina o grupo com Definir objetos."), messages);
            Assert.Contains((Severity.Warning, "Grupo \"M\" da peça Frente: sem TAG (TAG)."), messages);
            Assert.Contains((Severity.Warning, "Há 2 grupos \"M\" da peça Frente na página (camadas: MOLDE.N.MA.FRENTE). Propagar logos e Criar Grid esperam um por tamanho."), messages);
            Assert.Equal(5, report.GroupCount);
            Assert.True(report.Issues.TakeWhile(i => i.Severity == Severity.Error).Count() == report.ErrorCount);
        }

        [Fact]
        public void EmptyPage_SaysThereIsNothingToValidate_AndNothingIsChanged()
        {
            var loose = OnLayer(new FakeShape { Name = "Frente", Kind = ShapeKind.Curve, CenterX = 3, CenterY = 4, Width = 10, Height = 20 });
            var special = new FakeLayer { Name = "Guias", IsSpecialLayer = true };
            special.AddShape(SizeGroup("G", 0, 0, 200));
            _page.LayersList.Add(special);

            var report = Validate();

            Assert.Equal(0, report.GroupCount);
            Assert.Empty(report.Issues);
            Assert.StartsWith("Nenhum grupo de tamanho", report.Summary);
            Assert.Equal(("Frente", 3.0, 4.0, 10.0, 20.0), (loose.Name, loose.CenterX, loose.CenterY, loose.Width, loose.Height));
        }
    }
}
