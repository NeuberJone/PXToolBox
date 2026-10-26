using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class LayerOrganizationServiceTests
    {
        [Fact]
        public void OrganizeLayersByPieceAndCategory_MatchesShortLayers_UsingCorrectedSho1Prefix()
        {
            // Regressão da correção CHO1 -> SHO1 (confirmada com o usuário, ver docs/migrated-tools.md):
            // uma camada de Short real (prefixo SHO1) precisa ser encontrada e reordenada.
            var page = new FakePage();
            var shoC = (FakeLayer)page.CreateLayer("SHO1.PP.C");
            var ma = (FakeLayer)page.CreateLayer("CAM1.PP.MA");

            var service = new LayerOrganizationService();
            var result = service.OrganizeLayersByPieceAndCategory(page);

            Assert.True(result.IsSuccess);
            Assert.NotNull(shoC.MovedAboveOf); // foi encontrada e movida pela ordenação
        }

        [Fact]
        public void DeleteEmptyLayers_RemovesOnlyEmptyNonSpecialLayers()
        {
            var page = new FakePage();
            var empty = (FakeLayer)page.CreateLayer("Vazia");
            var special = (FakeLayer)page.CreateLayer("Guias");
            special.IsSpecialLayer = true;
            var withShape = (FakeLayer)page.CreateLayer("ComConteudo");
            withShape.AddShape(new FakeShape { Name = "algo" });

            var service = new LayerOrganizationService();
            var result = service.DeleteEmptyLayers(page);

            Assert.Equal(1, result.Value);
            Assert.True(empty.IsDeleted);
            Assert.False(special.IsDeleted); // especial nunca é apagada, mesmo vazia
            Assert.False(withShape.IsDeleted);
        }

        [Fact]
        public void RenameShapesWithLayerName_RenamesEveryShapeRegardlessOfVisibility()
        {
            var page = new FakePage();
            var hiddenLayer = (FakeLayer)page.CreateLayer("CAM1.PP.FE");
            hiddenLayer.Visible = false;
            var shape = hiddenLayer.AddShape(new FakeShape { Name = "qualquer", Visible = false });

            var service = new LayerOrganizationService();
            service.RenameShapesWithLayerName(page);

            Assert.Equal("CAM1.PP.FE", shape.Name);
        }

        [Fact]
        public void RenameShapesAfterFirstDot_UsesSecondSegment()
        {
            var page = new FakePage();
            var layer = (FakeLayer)page.CreateLayer("CAM1.PP.X.MA");
            var shape = layer.AddShape(new FakeShape());

            var service = new LayerOrganizationService();
            service.RenameShapesAfterFirstDot(page);

            Assert.Equal("PP", shape.Name);
        }

        [Fact]
        public void TrimLayerNamesToThreeSegments_OnlyTrimsFourOrMoreSegments()
        {
            var document = new FakeDocument();
            var page = document.ActivePageFake;
            var four = (FakeLayer)page.CreateLayer("CAM1.PP.X.MA");
            var three = (FakeLayer)page.CreateLayer("CAM1.PP.X");

            var service = new LayerOrganizationService();
            var result = service.TrimLayerNamesToThreeSegments(document);

            Assert.Equal(1, result.Value);
            Assert.Equal("CAM1.PP.X", four.Name);
            Assert.Equal("CAM1.PP.X", three.Name); // já tinha 3, não contado mas inalterado
        }

        [Fact]
        public void DeleteGridHelperShapes_DeletesNamedXOrTag_AcrossAllPages()
        {
            var document = new FakeDocument();
            var layer = (FakeLayer)document.ActivePageFake.CreateLayer("L1");
            var x = layer.AddShape(new FakeShape { Name = "x" });
            var tag = layer.AddShape(new FakeShape { Name = "tag" });
            var keep = layer.AddShape(new FakeShape { Name = "mantem" });

            var service = new LayerOrganizationService();
            var result = service.DeleteGridHelperShapes(document);

            Assert.Equal(2, result.Value);
            Assert.True(x.IsDeleted);
            Assert.True(tag.IsDeleted);
            Assert.False(keep.IsDeleted);
        }
    }
}
