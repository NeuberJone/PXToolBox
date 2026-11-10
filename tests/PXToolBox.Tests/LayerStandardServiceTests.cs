using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class LayerStandardServiceTests
    {
        private readonly LayerStandardService _service = new LayerStandardService();
        private readonly FakeDocument _document = new FakeDocument();
        private FakePage Page => _document.ActivePageFake;

        private FakeLayer Layer(string name)
        {
            var layer = new FakeLayer { Name = name };
            Page.LayersList.Add(layer);
            return layer;
        }

        private static FakeShape Shape(FakeLayer layer, string name, double x, double y, double w = 10, double h = 10) =>
            layer.AddShape(new FakeShape { Name = name, Kind = ShapeKind.Curve, CenterX = x, CenterY = y, Width = w, Height = h });

        private static FakeShape SizeGroup(FakeLayer layer, string size, params string[] children)
        {
            var group = layer.AddShape(new FakeShape { Name = size, Kind = ShapeKind.Group });
            foreach (var child in children) group.ShapesList.Add(new FakeShape { Name = child, ParentGroup = group });
            return group;
        }

        [Theory]
        [InlineData("CAM1.Frente.MA.Frente", "CAM1.MOLDE.MA.Frente")]
        [InlineData("REG1.Costa.FE.Costa", "REG1.MOLDE.FE.Costa")]
        public void MoldLayer_RoundTrips(string old, string @new)
        {
            Assert.Equal(@new, LayerStandardService.MoldLayerToNew(old));
            Assert.Equal(old, LayerStandardService.MoldLayerToOld(@new));
            Assert.Null(LayerStandardService.MoldLayerToNew(@new));
            Assert.Null(LayerStandardService.MoldLayerToOld(old));
            Assert.Null(LayerStandardService.MoldLayerToNew("CAM1.Frente.MA"));
        }

        [Fact]
        public void Mold_ConvertsOnlyLayerNames_ChildrenUntouched_AndBack()
        {
            var layer = Layer("CAM1.Frente.MA.Frente");
            var group = SizeGroup(layer, "G", "Tag", "Frente", "x");
            Layer("Guias");

            var result = _service.ConvertDocument(_document);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Contains("padrão novo", result.Message);
            Assert.Equal("CAM1.MOLDE.MA.Frente", layer.Name);
            Assert.Equal(new[] { "Tag", "Frente", "x" }, group.ShapesList.Select(s => s.Name));
            Assert.Equal("Guias", Page.Layers[1].Name);

            Assert.True(_service.ConvertDocument(_document).IsSuccess);
            Assert.Equal("CAM1.Frente.MA.Frente", layer.Name);
        }

        [Fact]
        public void Grid_OldToNew_OneLayerPerCategory_WithRectanglesAtTheBack()
        {
            var frente = Layer("CAM1.Frente.MA");
            var frentePiece = Shape(frente, "Frente", 10, 10, 30, 40);
            var costa = Layer("CAM1.Costa.MA");
            var costaPiece = Shape(costa, "Costa", 60, 10, 30, 40);
            var feminina = Layer("CAM1.Frente.FE");
            Shape(feminina, "Frente", 10, 80);

            var result = _service.ConvertDocument(_document);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new[] { "CAM1.MOLDE.MA", "CAM1.MOLDE.FE" }, Page.Layers.Select(l => l.Name));
            var male = (FakeLayer)Page.Layers[0];
            Assert.Equal(new[] { "Frente", "Costa", "Frente_RECT", "Costa_RECT" }.OrderBy(n => n), male.ShapesList.Select(s => s.Name).OrderBy(n => n));
            // Retângulos no fundo da camada, com a caixa da peça.
            Assert.EndsWith("_RECT", male.ShapesList[male.ShapesList.Count - 1].Name);
            Assert.EndsWith("_RECT", male.ShapesList[male.ShapesList.Count - 2].Name);
            var rect = (FakeShape)male.ShapesList.Single(s => s.Name == "Frente_RECT");
            Assert.Equal((frentePiece.CenterX, frentePiece.CenterY, frentePiece.Width, frentePiece.Height), (rect.CenterX, rect.CenterY, rect.Width, rect.Height));
            Assert.Same(male, costaPiece.CurrentLayer);
            Assert.True(frente.IsDeleted && costa.IsDeleted);
        }

        [Fact]
        public void Grid_NewToOld_SplitsByPiece_LogosGoWithTheirPiece_RectanglesRemoved()
        {
            var male = Layer("CAM1.MOLDE.MA");
            var frente = Shape(male, "Frente", 10, 10, 30, 40);
            var costa = Shape(male, "Costa", 60, 10, 30, 40);
            var logo = Shape(male, "L1", 62, 15, 4, 4);
            var frenteRect = Shape(male, "Frente_RECT", 10, 10, 30, 40);
            Shape(male, "Costa_RECT", 60, 10, 30, 40);

            var result = _service.ConvertDocument(_document);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Contains("padrão antigo", result.Message);
            Assert.True(male.IsDeleted);
            var frenteLayer = Page.Layers.Cast<FakeLayer>().Single(l => l.Name == "CAM1.Frente.MA");
            var costaLayer = Page.Layers.Cast<FakeLayer>().Single(l => l.Name == "CAM1.Costa.MA");
            Assert.Equal(new ICorelShape[] { frente }, frenteLayer.ShapesList);
            Assert.Equal(new ICorelShape[] { costa, logo }, costaLayer.ShapesList);
            Assert.Equal("L1", logo.Name);
            Assert.True(frenteRect.IsDeleted);
        }

        [Fact]
        public void Grid_RoundTrip_ComesBackToTheSameLayers()
        {
            Shape(Layer("CAM1.Frente.MA"), "Frente", 10, 10);
            Shape(Layer("CAM1.Costa.MA"), "Costa", 60, 10);

            _service.ConvertDocument(_document);
            _service.ConvertDocument(_document);

            Assert.Equal(new[] { "CAM1.Costa.MA", "CAM1.Frente.MA" }, Page.Layers.Select(l => l.Name).OrderBy(n => n));
            Assert.All(Page.Layers.Cast<FakeLayer>(), l => Assert.Single(l.ShapesList));
        }

        [Fact]
        public void UnconfiguredMoldLayers_AreNeverTreatedAsGrid_WhenTheDocumentHasMoldLayers()
        {
            var mold = Layer("CAM1.Frente.MA.Frente");
            var raw = Layer("CAM1.Costa.MA");
            SizeGroup(raw, "G", "Costa");

            Assert.True(_service.ConvertDocument(_document).IsSuccess);

            Assert.Equal("CAM1.MOLDE.MA.Frente", mold.Name);
            Assert.Equal("CAM1.Costa.MA", raw.Name);
            Assert.False(raw.IsDeleted);
        }

        [Fact]
        public void NoKnownLayers_Fails()
        {
            Layer("Camada 1");
            Assert.Equal(OperationStatus.InvalidSelection, _service.ConvertDocument(_document).Status);
        }

        [Fact]
        public void PrepareNewMold_NewStandard_NamesLayersWithMolde_AndKeepsLogoNames()
        {
            var layer = Layer("CAM1.Frente.MA");
            var group = SizeGroup(layer, "G", "Tag", "peça", "x", "L1");

            var result = new MoldPreparationService().PrepareNewMold(Page, LayerStandardService.Standard.New);

            Assert.True(result.IsSuccess);
            Assert.Equal("CAM1.MOLDE.MA.Frente", layer.Name);
            Assert.Equal(new[] { "Tag", "Frente", "x", "L1" }, group.ShapesList.Select(s => s.Name));
        }

        [Fact]
        public void PrepareNewMold_NewStandard_KeepsALayerThatIsAlreadyNew()
        {
            var layer = Layer("CAM1.MOLDE.MA.Frente");
            SizeGroup(layer, "G", "Frente");

            new MoldPreparationService().PrepareNewMold(Page, LayerStandardService.Standard.New);

            Assert.Equal("CAM1.MOLDE.MA.Frente", layer.Name);
        }

        [Fact]
        public void PrepareNewMold_DefaultIsTheOldStandard()
        {
            var layer = Layer("CAM1.Frente.MA");
            SizeGroup(layer, "G", "Frente");

            new MoldPreparationService().PrepareNewMold(Page);

            Assert.Equal("CAM1.Frente.MA.Frente", layer.Name);
        }

        [Fact]
        public void RenameAfterFirstDot_SkipsNewStandardLayers()
        {
            var layer = Layer("CAM1.MOLDE.MA");
            var piece = Shape(layer, "Frente", 0, 0);

            new LayerOrganizationService().RenameShapesAfterFirstDot(Page);

            Assert.Equal("Frente", piece.Name);
        }

        [Fact]
        public void PrepareNewGrid_NewStandard_FromNewMold_EndsInCategoryLayersWithRectangles()
        {
            Layer("CAM1.MOLDE.MA.Frente").AddShape(new FakeShape { Name = "Frente", Kind = ShapeKind.Curve, CenterX = 10, CenterY = 10, Width = 30, Height = 40 });
            Layer("CAM1.MOLDE.MA.Costa").AddShape(new FakeShape { Name = "Costa", Kind = ShapeKind.Curve, CenterX = 60, CenterY = 10, Width = 30, Height = 40 });

            var result = new GridPreparationService().PrepareNewGrid(_document, LayerStandardService.Standard.New);

            Assert.True(result.IsSuccess, result.Message);
            var male = Assert.Single(Page.Layers.Cast<FakeLayer>());
            Assert.Equal("CAM1.MOLDE.MA", male.Name);
            Assert.Equal(new[] { "Costa", "Costa_RECT", "Frente", "Frente_RECT" }, male.ShapesList.Select(s => s.Name).OrderBy(n => n));
        }

        [Fact]
        public void PrepareNewGrid_OldStandard_FromNewMold_OneLayerPerPiece()
        {
            Layer("CAM1.MOLDE.MA.Frente").AddShape(new FakeShape { Name = "Frente", Kind = ShapeKind.Curve, Width = 30, Height = 40 });
            Layer("CAM1.MOLDE.MA.Costa").AddShape(new FakeShape { Name = "Costa", Kind = ShapeKind.Curve, Width = 30, Height = 40 });

            var result = new GridPreparationService().PrepareNewGrid(_document);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new[] { "CAM1.Costa.MA", "CAM1.Frente.MA" }, Page.Layers.Select(l => l.Name).OrderBy(n => n));
        }
    }
}
