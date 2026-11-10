using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class MoldToolsServiceTests
    {
        private readonly MoldToolsService _service = new MoldToolsService();
        private readonly FakeArrangement _arrangement = new FakeArrangement();
        private readonly FakeDocument _document = new FakeDocument();
        private FakePage Page => _document.ActivePageFake;

        private FakeLayer Layer(string name)
        {
            var layer = new FakeLayer { Name = name };
            Page.LayersList.Add(layer);
            return layer;
        }

        private static FakeShape SizeGroup(FakeLayer layer, string size, params string[] children)
        {
            var group = layer.AddShape(new FakeShape { Name = size, Kind = ShapeKind.Group });
            foreach (var child in children)
            {
                group.ShapesList.Add(new FakeShape { Name = child, Kind = ShapeKind.Curve, ParentGroup = group, CurrentLayer = layer, Width = 30, Height = 40, CenterX = 10, CenterY = 20 });
            }

            return group;
        }

        private static FakeSelection Select(params ICorelShape[] shapes)
        {
            var selection = new FakeSelection();
            selection.ShapesList.AddRange(shapes);
            return selection;
        }

        [Fact]
        public void Recognizer_PieceIsWhatRemains_AfterTagLaserLogosAndIdentifier()
        {
            var group = SizeGroup(new FakeLayer(), "G", "Tag", "Frente", "x", "L1", "L2", "IDENTIFICADOR_TAMANHO");
            var settings = MoldSettings.Default;
            Assert.Equal("Frente", MoldPieceRecognizer.Recognize(group, settings).Piece!.Name);

            var ambiguous = SizeGroup(new FakeLayer(), "G", "Frente", "Costa");
            Assert.Null(MoldPieceRecognizer.Recognize(ambiguous, settings).Piece);
            Assert.Equal("Costa", MoldPieceRecognizer.Recognize(ambiguous, settings, "costa").Piece!.Name);

            var laserOnly = SizeGroup(new FakeLayer(), "G", "Tag", "x");
            Assert.Equal("x", MoldPieceRecognizer.Recognize(laserOnly, settings).Piece!.Name);
        }

        [Theory]
        [InlineData("G", "MA")]
        [InlineData("blgg", "FE")]
        [InlineData("10A", "C")]
        [InlineData("XL", "")]
        public void SizeCategories(string size, string category) => Assert.Equal(category, MoldSettings.Default.CategoryOf(size));

        [Fact]
        public void CreateLayers_MovesEachGroupToItsPieceLayer_NewStandard()
        {
            var source = Layer("Camada 1");
            var g = SizeGroup(source, "G", "Tag", "Frente", "x");
            var blg = SizeGroup(source, "BLG", "Tag", "Frente", "x");
            var costa = SizeGroup(source, "M", "Costa", "x");
            var existing = Layer("CAM1.MOLDE.MA.Costa");

            var result = _service.CreateLayersForSelection(Select(g, blg, costa), Page, PieceTypeCode.Cam1);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("CAM1.MOLDE.MA.Frente", g.CurrentLayer!.Name);
            Assert.Equal("CAM1.MOLDE.FE.Frente", blg.CurrentLayer!.Name);
            Assert.Same(existing, costa.CurrentLayer);
            Assert.Equal(2, Page.Layers.Count(l => l.Name.StartsWith("CAM1.MOLDE.MA.")));
        }

        [Fact]
        public void CreateLayers_SkipsUnknownSizes_AmbiguousGroups_AndNamesWithSpaces()
        {
            var source = Layer("Camada 1");
            var unknown = SizeGroup(source, "XL", "Frente");
            var ambiguous = SizeGroup(source, "G", "Frente", "Costa");
            var spaced = SizeGroup(source, "P", "Manga D");

            var result = _service.CreateLayersForSelection(Select(unknown, ambiguous, spaced), Page, PieceTypeCode.Cam2);

            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
            Assert.All(new[] { unknown, ambiguous, spaced }, g => Assert.Same(source, g.CurrentLayer));
            Assert.Contains("MA, FE, C", result.Message);
            Assert.Contains("mais de um objeto candidato", result.Message);
            Assert.Contains("tem espaço", result.Message);
        }

        [Fact]
        public void Uppercase_PieceAndNewStandardLayer_LaserUntouched()
        {
            var layer = Layer("cam1.MOLDE.ma.frente");
            var group = SizeGroup(layer, "G", "Tag", "frente", "x");
            var oldLayer = Layer("CAM1.costa.MA.costa");
            var oldGroup = SizeGroup(oldLayer, "G", "costa", "x");

            var result = _service.UppercasePieceNames(Select(group, oldGroup));

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { "Tag", "FRENTE", "x" }, group.ShapesList.Select(s => s.Name));
            Assert.Equal("CAM1.MOLDE.MA.FRENTE", layer.Name);
            Assert.Equal("CAM1.costa.MA.costa", oldLayer.Name);
            Assert.Equal("COSTA", oldGroup.ShapesList[0].Name);
        }

        [Fact]
        public void Distribute_Vertical_LargestFirst_StacksFromTheTopWithAutomaticGap()
        {
            var layer = Layer("L");
            var small = layer.AddShape(new FakeShape { Name = "pequeno", Width = 10, Height = 10, CenterX = 50, CenterY = 0 });
            var big = layer.AddShape(new FakeShape { Name = "grande", Width = 20, Height = 30, CenterX = 0, CenterY = 100 });

            var result = _service.Distribute(Select(small, big), MoldToolsService.SortOrder.LargestFirst, vertical: true, gap: null);

            Assert.True(result.IsSuccess);
            // Topo = 115 (o mais alto); grande ocupa 115..85; espaço automático = menor altura (10).
            Assert.Equal((0, 100), (big.CenterX, big.CenterY));
            Assert.Equal((0, 70), (small.CenterX, small.CenterY));
            Assert.Equal(new ICorelShape[] { big, small }, layer.ShapesList);
        }

        [Fact]
        public void Distribute_Horizontal_SmallestFirst_WithGap()
        {
            var layer = Layer("L");
            var big = layer.AddShape(new FakeShape { Width = 20, Height = 20, CenterX = 10, CenterY = 5 });
            var small = layer.AddShape(new FakeShape { Width = 10, Height = 10, CenterX = 100, CenterY = 50 });

            _service.Distribute(Select(big, small), MoldToolsService.SortOrder.SmallestFirst, vertical: false, gap: 2);

            // Começa na esquerda do conjunto (0): pequeno 0..10, espaço 2, grande 12..32; Y do primeiro.
            Assert.Equal((5, 50), (small.CenterX, small.CenterY));
            Assert.Equal((22, 50), (big.CenterX, big.CenterY));
        }

        [Fact]
        public void Distribute_RejectsNegativeGap() =>
            Assert.Equal(OperationStatus.InvalidSelection,
                _service.Distribute(Select(new FakeShape()), MoldToolsService.SortOrder.LargestFirst, true, -1).Status);

        [Fact]
        public void SelectRectangles_FindsRectsInLayersAndGroups()
        {
            var layer = Layer("CAM1.MOLDE.MA");
            var rect = layer.AddShape(new FakeShape { Name = "Frente_RECT" });
            layer.AddShape(new FakeShape { Name = "Frente" });
            var group = SizeGroup(layer, "G", "Costa_RECT", "Costa");

            var result = _service.SelectRectangles(Page, _arrangement);

            Assert.True(result.IsSuccess);
            Assert.Equal(new[] { "Frente_RECT", "Costa_RECT" }, _arrangement.Selected.Select(s => s.Name));
            Assert.Same(rect, _arrangement.Selected[0]);
        }

        [Fact]
        public void CreateGrid_CopiesTheGridSizeOfEachPiece_ToANewFile_AndLeavesTheMoldUntouched()
        {
            var frente = Layer("CAM1.MOLDE.MA.Frente");
            SizeGroup(frente, "M", "Tag", "Frente", "x");
            var frenteG = SizeGroup(frente, "G", "Tag", "Frente", "x", "L1");
            var costaOld = Layer("CAM1.Costa.MA.Costa");
            SizeGroup(costaOld, "G", "Tag", "Costa", "x");
            var feminina = Layer("CAM1.MOLDE.FE.Frente");
            SizeGroup(feminina, "BLG", "Frente");
            var semG = Layer("CAM1.MOLDE.C.Frente");
            SizeGroup(semG, "8A", "Frente");
            _document.SelectionForCopy = () => _arrangement.Selected;
            var before = Page.Layers.Select(l => (l.Name, ((FakeLayer)l).ShapesList.Count)).ToList();

            var result = _service.CreateGrid(_document, _arrangement, @"C:\x\MOLDE_GRID.cdr", out var saved);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(@"C:\x\MOLDE_GRID.cdr", saved);
            Assert.Equal(new[]
            {
                ("CAM1.MOLDE.FE", "Frente"), ("CAM1.MOLDE.FE", "Frente_RECT"),
                ("CAM1.MOLDE.MA", "Costa"), ("CAM1.MOLDE.MA", "Costa_RECT"),
                ("CAM1.MOLDE.MA", "Frente"), ("CAM1.MOLDE.MA", "Frente_RECT"), ("CAM1.MOLDE.MA", "L1"),
            }, _document.SavedCopy.OrderBy(c => c.Layer).ThenBy(c => c.Name));
            Assert.Contains("12A não encontrado", result.Message);
            // O documento de moldes termina como estava: cópias e camadas temporárias apagadas.
            Assert.Equal(before, Page.Layers.Select(l => (l.Name, ((FakeLayer)l).ShapesList.Count)).ToList());
            Assert.Equal(new[] { "Tag", "Frente", "x", "L1" }, frenteG.ShapesList.Select(s => s.Name));
        }

        [Fact]
        public void CreateGrid_NothingToCopy_SavesNothing()
        {
            SizeGroup(Layer("CAM1.MOLDE.MA.Frente"), "M", "Frente");

            var result = _service.CreateGrid(_document, _arrangement, @"C:\x\g.cdr", out var saved);

            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
            Assert.Null(saved);
            Assert.Null(_document.SavedCopyPath);
        }
    }
}
