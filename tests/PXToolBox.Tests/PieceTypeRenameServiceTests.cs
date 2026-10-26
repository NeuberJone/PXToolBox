using PXToolBox.Core.Naming;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class PieceTypeRenameServiceTests
    {
        [Fact]
        public void Rename_SpecificType_OnlyRenamesMatchingLayers()
        {
            var document = new FakeDocument();
            var page = document.ActivePageFake;
            page.CreateLayer("CAM1.PP.X.MA");
            page.CreateLayer("REG1.PP.X.MA");
            page.CreateLayer("SemPonto");

            var service = new PieceTypeRenameService();
            var result = service.Rename(document, PieceTypeCode.Cam1, PieceTypeCode.Sho1);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value);
            Assert.Equal("SHO1.PP.X.MA", page.Layers[0].Name);
            Assert.Equal("REG1.PP.X.MA", page.Layers[1].Name); // não mudou
            Assert.Equal("SemPonto", page.Layers[2].Name); // sem ponto, ignorada
        }

        [Fact]
        public void Rename_Tudo_RenamesAnyValidPieceType()
        {
            var document = new FakeDocument();
            var page = document.ActivePageFake;
            page.CreateLayer("CAM1.PP.X.MA");
            page.CreateLayer("REG1.PP.X.MA");
            page.CreateLayer("Outro.PP.X.MA"); // "Outro" não é um código válido

            var service = new PieceTypeRenameService();
            var result = service.Rename(document, fromCode: null, toCode: PieceTypeCode.Col1);

            Assert.Equal(2, result.Value);
            Assert.Equal("COL1.PP.X.MA", page.Layers[0].Name);
            Assert.Equal("COL1.PP.X.MA", page.Layers[1].Name);
            Assert.Equal("Outro.PP.X.MA", page.Layers[2].Name);
        }

        [Fact]
        public void RenameSelectedObjects_RecognizesTypeFromObjectName_IgnoresIncompatibleFormat()
        {
            var selection = new FakeSelection();
            var match = new FakeShape { Name = "CAM1.Frente.MA.Frente" };
            var noType = new FakeShape { Name = "Outro.Frente.MA.Frente" }; // "Outro" não é tipo válido
            var noDot = new FakeShape { Name = "Retangulo1" }; // sem ponto
            selection.ShapesList.Add(match);
            selection.ShapesList.Add(noType);
            selection.ShapesList.Add(noDot);

            var service = new PieceTypeRenameService();
            var result = service.RenameSelectedObjects(selection, PieceTypeCode.Sho1);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("SHO1.Frente.MA.Frente", match.Name); // só o 1º segmento mudou, resto preservado
            Assert.Equal("Outro.Frente.MA.Frente", noType.Name); // ignorado
            Assert.Equal("Retangulo1", noDot.Name); // ignorado
            Assert.Contains("1 objeto(s) alterado(s)", result.Message);
            Assert.Contains("2 ignorado(s)", result.Message);
        }

        [Fact]
        public void RenameSelectedObjects_EmptySelection_Fails()
        {
            var service = new PieceTypeRenameService();
            var result = service.RenameSelectedObjects(new FakeSelection(), PieceTypeCode.Sho1);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void RenameActiveLayer_ValidName_RenamesOnlyThatLayer()
        {
            var layer = new FakeLayer { Name = "CAM1.PP.MA" };
            var service = new PieceTypeRenameService();

            var result = service.RenameActiveLayer(layer, PieceTypeCode.Sho1);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("SHO1.PP.MA", layer.Name);
        }

        [Fact]
        public void RenameActiveLayer_NoActiveLayer_Fails()
        {
            var service = new PieceTypeRenameService();
            var result = service.RenameActiveLayer(null, PieceTypeCode.Sho1);

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void RenameActiveLayer_IncompatibleName_FailsWithoutRenaming()
        {
            var layer = new FakeLayer { Name = "SemPonto" };
            var service = new PieceTypeRenameService();

            var result = service.RenameActiveLayer(layer, PieceTypeCode.Sho1);

            Assert.False(result.IsSuccess);
            Assert.Equal("SemPonto", layer.Name);
        }
    }
}
