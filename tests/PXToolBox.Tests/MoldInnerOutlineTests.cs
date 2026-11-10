using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class MoldInnerOutlineTests
    {
        private static MoldSettings With(MoldInnerOutlineOptions inner, bool laserCopy = true)
        {
            var d = MoldSettings.Default;
            var define = new MoldDefineOptions(laserCopy, "#FF0000", 0.2, true, 50, 2);
            return new MoldSettings(d.Male, d.Female, d.Kids, d.GridBaseSize, d.LaserNames, d.TagName, d.IdentifierName, define, inner);
        }

        [Fact]
        public void ColorAndStyle_CadastradaOuSugeridaPelaPosicao_EstiloDaCategoria()
        {
            var settings = With(new MoldInnerOutlineOptions(true, 0.5, sizeColors: MoldInnerOutlineOptions.ParseColors("g=00ff00\nblp: #0000FF")));

            Assert.Equal("#00FF00", settings.InnerOutlineColorOf("G"));
            Assert.Equal("#0000FF", settings.InnerOutlineColorOf("BLP"));
            Assert.Equal(MoldInnerOutlineOptions.Palette[0], settings.InnerOutlineColorOf("PP"));
            Assert.Equal(MoldInnerOutlineOptions.Palette[4], settings.InnerOutlineColorOf("BLGG"));
            Assert.Equal(MoldInnerOutlineOptions.Palette[2], settings.InnerOutlineColorOf("6A"));
            Assert.Equal(string.Empty, settings.InnerOutlineColorOf("ZZ"));
            Assert.Equal((MoldLineStyle.Solid, MoldLineStyle.Dashed, MoldLineStyle.Dotted),
                (settings.InnerOutlineStyleOf("M"), settings.InnerOutlineStyleOf("BLM"), settings.InnerOutlineStyleOf("8A")));
            Assert.Empty(settings.Validate());
        }

        [Fact]
        public void Validate_Refuses_BadColor_UnknownSize_Width_AndNoLaserCopy()
        {
            var bad = With(new MoldInnerOutlineOptions(true, 0, sizeColors: MoldInnerOutlineOptions.ParseColors("G vermelho\nZZ #000000\nM")), laserCopy: false).Validate();

            Assert.Contains("Espessura do contorno interno: entre 0 e 10 mm.", bad);
            Assert.Contains("Cor do contorno interno: use #RRGGBB (G, M).", bad);
            Assert.Contains("Cor para tamanho que não está nas categorias: ZZ.", bad);
            Assert.Contains(bad, p => p.StartsWith("Contorno interno: ligue"));
            // Desligado, sem cópia do laser: tudo bem (é a configuração de fábrica do laser na peça).
            Assert.Empty(With(MoldInnerOutlineOptions.Default, laserCopy: false).Validate());
        }

        [Fact]
        public void Define_PieceGetsInsideOutline_ColorOfSize_StyleOfCategory_LaserCopyKeepsLaser()
        {
            var layer = new FakeLayer { Name = "Camada 1" };
            var page = new FakePage();
            page.LayersList.Add(layer);
            var piece = layer.AddShape(new FakeShape { Name = "Curva", Kind = ShapeKind.Curve, Width = 100, Height = 200, CenterX = 50, CenterY = 100 });
            var selection = new FakeSelection();
            selection.ShapesList.Add(piece);
            var settings = With(new MoldInnerOutlineOptions(true, 0.7, sizeColors: MoldInnerOutlineOptions.ParseColors("BLG #112233")));

            var result = new MoldDefineService(() => settings).Define(selection, page, new FakeArrangement(), "Frente", "BLG");

            Assert.True(result.IsSuccess, result.Message);
            var group = (FakeShape)layer.ShapesList.Single();
            var inner = Child(group, "Frente").Outline!;
            var laser = Child(group, "x").Outline!;
            Assert.Equal((0x11, 0x22, 0x33, 0.7, ShapeOutlineDash.Dashed, true), (inner.Red, inner.Green, inner.Blue, inner.WidthMillimeters, inner.Dash, inner.Inside));
            Assert.Equal((255, 0, 0, ShapeOutlineDash.Solid, false), (laser.Red, laser.Green, laser.Blue, laser.Dash, laser.Inside));
            Assert.Contains("contorno interno #112233 (tracejado)", result.Message);
        }

        [Fact]
        public void Define_Disabled_LeavesThePieceOutlineAlone()
        {
            var layer = new FakeLayer { Name = "Camada 1" };
            var page = new FakePage();
            page.LayersList.Add(layer);
            var piece = layer.AddShape(new FakeShape { Name = "Curva", Kind = ShapeKind.Curve, Width = 100, Height = 200, CenterX = 50, CenterY = 100 });
            var selection = new FakeSelection();
            selection.ShapesList.Add(piece);

            var result = new MoldDefineService().Define(selection, page, new FakeArrangement(), "Frente", "G");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Null(piece.Outline);
            Assert.DoesNotContain("contorno interno", result.Message);
        }

        private static FakeShape Child(FakeShape group, string name) => (FakeShape)group.Shapes.Single(s => s.Name == name);
    }
}
