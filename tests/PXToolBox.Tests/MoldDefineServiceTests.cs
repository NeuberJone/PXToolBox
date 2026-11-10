using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class MoldDefineServiceTests
    {
        private readonly FakeArrangement _arrangement = new FakeArrangement();
        private readonly FakePage _page = new FakePage();
        private readonly FakeLayer _layer = new FakeLayer { Name = "Camada 1" };

        public MoldDefineServiceTests() => _page.LayersList.Add(_layer);

        /// <summary>Peça 100×200 com o canto inferior esquerdo em (0, 0).</summary>
        private FakeShape Piece(string name = "Curva", double centerX = 50, double centerY = 100, double width = 100, double height = 200) =>
            _layer.AddShape(new FakeShape { Name = name, Kind = ShapeKind.Curve, Width = width, Height = height, CenterX = centerX, CenterY = centerY });

        private FakeShape Logo(double centerX, double centerY, string name = "Curva") =>
            _layer.AddShape(new FakeShape { Name = name, Kind = ShapeKind.Curve, Width = 10, Height = 10, CenterX = centerX, CenterY = centerY });

        private static FakeSelection Select(params ICorelShape[] shapes)
        {
            var selection = new FakeSelection();
            selection.ShapesList.AddRange(shapes);
            return selection;
        }

        private static FakeShape Child(FakeShape group, string name) => (FakeShape)group.Shapes.Single(s => s.Name == name);

        [Fact]
        public void LooseSelection_BecomesSizeGroup_TagLogosPieceLaser()
        {
            var lower = Logo(70, 50);
            var piece = Piece();
            var upper = Logo(30, 150);

            var result = new MoldDefineService().Define(Select(lower, piece, upper), _page, _arrangement, " Frente ", "g");

            Assert.True(result.IsSuccess, result.Message);
            var group = (FakeShape)Assert.Single(_layer.ShapesList);
            Assert.Equal("G", group.Name);
            Assert.Equal(new[] { "TAG", "L2", "L1", "Frente", "x" }, group.Shapes.Select(s => s.Name));
            Assert.Same(upper, Child(group, "L1"));
            Assert.Same(lower, Child(group, "L2"));
            Assert.Same(group, Assert.Single(_arrangement.Selected));

            var laser = Child(group, "x");
            Assert.Null(piece.Outline);
            Assert.Equal((255, 0, 0, 0.2), (laser.Outline!.Red, laser.Outline.Green, laser.Outline.Blue, laser.Outline.WidthMillimeters));
            Assert.False(laser.Outline.RoundCorners);
            Assert.Equal((piece.CenterX, piece.CenterY, piece.Width, piece.Height), (laser.CenterX, laser.CenterY, laser.Width, laser.Height));

            var tag = Child(group, "TAG");
            Assert.Equal(("G", MoldDefineService.TagFont, 50.0), (tag.Text, tag.FontFamily, tag.FontSizePoints));
            Assert.Equal((0, 0, 0), tag.Fill);
            Assert.Equal((255, 255, 255, 2.0, true, true), (tag.Outline!.Red, tag.Outline.Green, tag.Outline.Blue, tag.Outline.WidthMillimeters, tag.Outline.BehindFill, tag.Outline.RoundCorners));
            Assert.Equal(50, tag.CenterX, 6);
            Assert.Equal(0, tag.CenterY - tag.Height / 2, 6);
            Assert.Contains("Grupo \"G\" criado", result.Message);
        }

        [Fact]
        public void NewLogos_FollowTheNumberingOfAnotherSize_ByPositionInThePiece()
        {
            // Tamanho M já definido: peça 80×160 com canto em (200, 0); L5 em cima à esquerda, L6 embaixo à direita.
            var m = _layer.AddShape(new FakeShape { Name = "M", Kind = ShapeKind.Group });
            foreach (var (name, x, y, w, h) in new[] { ("Frente", 240.0, 80.0, 80.0, 160.0), ("L5", 216.0, 128.0, 8.0, 8.0), ("L6", 264.0, 32.0, 8.0, 8.0) })
            {
                m.ShapesList.Add(new FakeShape { Name = name, Kind = ShapeKind.Curve, CenterX = x, CenterY = y, Width = w, Height = h, ParentGroup = m, CurrentLayer = _layer });
            }

            var piece = Piece();
            var bottomRight = Logo(80, 40);
            var topLeft = Logo(20, 160);
            var middle = Logo(50, 100);

            var result = new MoldDefineService().Define(Select(piece, bottomRight, topLeft, middle), _page, _arrangement, "Frente", "G");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("L5", topLeft.Name);
            Assert.Equal("L6", bottomRight.Name);
            Assert.Equal("L7", middle.Name);
            Assert.Contains("2 logo(s) com a numeração do grupo \"M\"", result.Message);
        }

        [Fact]
        public void WithoutReference_NumbersContinueFromTheHighestOnThePage()
        {
            var other = _layer.AddShape(new FakeShape { Name = "G", Kind = ShapeKind.Group });
            other.ShapesList.Add(new FakeShape { Name = "Costa", ParentGroup = other });
            other.ShapesList.Add(new FakeShape { Name = "L4", ParentGroup = other });
            var piece = Piece();
            var named = Logo(20, 20, "L9");
            var unnamed = Logo(80, 80);

            var result = new MoldDefineService().Define(Select(piece, named, unnamed), _page, _arrangement, "Frente", "P");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("L9", named.Name);
            Assert.Equal("L10", unnamed.Name);
        }

        [Fact]
        public void SelectedGroup_IsRedefined_OldTagAndLaserReplaced_LogoNamesAndIdentifierKept()
        {
            var group = _layer.AddShape(new FakeShape { Name = "P", Kind = ShapeKind.Group });
            FakeShape Add(string name, ShapeKind kind, double w, double h)
            {
                var shape = new FakeShape { Name = name, Kind = kind, Width = w, Height = h, CenterX = 50, CenterY = 100, ParentGroup = group, CurrentLayer = _layer };
                group.ShapesList.Add(shape);
                return shape;
            }

            var oldTag = Add("Tag", ShapeKind.Text, 30, 20);
            var logo = Add("L3", ShapeKind.Curve, 10, 10);
            var identifier = Add("IDENTIFICADOR_TAMANHO", ShapeKind.Curve, 5, 5);
            var piece = Add("Frente", ShapeKind.Curve, 100, 200);
            var oldLaser = Add("x", ShapeKind.Curve, 100, 200);

            var result = new MoldDefineService().Define(Select(group), _page, _arrangement, "FRENTE", "G");

            Assert.True(result.IsSuccess, result.Message);
            Assert.True(oldTag.IsDeleted);
            Assert.True(oldLaser.IsDeleted);
            var rebuilt = (FakeShape)Assert.Single(_layer.ShapesList);
            Assert.Equal("G", rebuilt.Name);
            Assert.Equal(new[] { "TAG", "L3", "IDENTIFICADOR_TAMANHO", "FRENTE", "x" }, rebuilt.Shapes.Select(s => s.Name));
            Assert.Same(logo, Child(rebuilt, "L3"));
            Assert.Same(identifier, Child(rebuilt, "IDENTIFICADOR_TAMANHO"));
            Assert.Same(piece, Child(rebuilt, "FRENTE"));
            Assert.Contains("redefinido", result.Message);
        }

        [Fact]
        public void WithoutLaserCopyAndTag_ThePieceGetsTheLaserOutline()
        {
            var settings = new MoldSettings(MoldSettings.Default.Male, MoldSettings.Default.Female, MoldSettings.Default.Kids, "G",
                new[] { "x" }, "TAG", "", new MoldDefineOptions(false, "00ff00", 0.5, false, 50, 2));
            var piece = Piece();
            var logo = Logo(50, 50);

            var result = new MoldDefineService(() => settings).Define(Select(piece, logo), _page, _arrangement, "Costa", "8A");

            Assert.True(result.IsSuccess, result.Message);
            var group = (FakeShape)Assert.Single(_layer.ShapesList);
            Assert.Equal("8A", group.Name);
            Assert.Equal(new[] { "L1", "Costa" }, group.Shapes.Select(s => s.Name));
            Assert.Equal((0, 255, 0, 0.5), (piece.Outline!.Red, piece.Outline.Green, piece.Outline.Blue, piece.Outline.WidthMillimeters));
        }

        [Theory]
        [InlineData("Frente", "ZZ", "não é um tamanho")]
        [InlineData("", "G", "Informe o nome da peça")]
        [InlineData("Frente Costa", "G", "ponto nem espaço")]
        [InlineData("Frente.1", "G", "ponto nem espaço")]
        public void InvalidInput_RefusesWithoutChanges(string pieceName, string size, string expected)
        {
            var piece = Piece();
            var logo = Logo(50, 50);

            var result = new MoldDefineService().Define(Select(piece, logo), _page, _arrangement, pieceName, size);

            Assert.False(result.IsSuccess);
            Assert.Contains(expected, result.Message);
            Assert.Contains("Nada foi alterado", result.Message);
            Assert.Equal(("Curva", "Curva", 2), (piece.Name, logo.Name, _layer.ShapesList.Count));
        }

        [Fact]
        public void AmbiguousOrUnsuitablePiece_RefusesWithoutChanges()
        {
            var first = Piece();
            var twin = Piece(centerX: 300, width: 99.5);
            var ambiguous = new MoldDefineService().Define(Select(first, twin), _page, _arrangement, "Frente", "G");
            Assert.False(ambiguous.IsSuccess);
            Assert.Contains("quase do mesmo tamanho", ambiguous.Message);

            var bigGroup = _layer.AddShape(new FakeShape { Name = "Grupo", Kind = ShapeKind.Group, Width = 500, Height = 500 });
            var grouped = new MoldDefineService().Define(Select(bigGroup, first), _page, _arrangement, "Frente", "G");
            Assert.False(grouped.IsSuccess);
            Assert.Contains("curva ou forma", grouped.Message);

            Assert.Equal(new[] { "Curva", "Curva", "Grupo" }, _layer.ShapesList.Select(s => s.Name));
            Assert.Empty(_arrangement.Selected);
        }

        // ---- Detectar objetos em lote ----

        private static readonly MoldSettings ThreeKidsSizes = new MoldSettings(MoldSettings.Default.Male, MoldSettings.Default.Female,
            new MoldSizeCategory("C", new[] { "4", "8", "12" }, null, new[] { "A" }), "G", new[] { "x" }, "TAG", "");

        /// <summary>Peça quadrada de lado <paramref name="side"/> com canto em (x, 0) e um logo no canto
        /// superior esquerdo dela (20%, 80%).</summary>
        private (FakeShape Piece, FakeShape Logo) Set(double x, double side) =>
            (Piece(centerX: x + side / 2, centerY: side / 2, width: side, height: side), Logo(x + side * 0.2, side * 0.8));

        [Fact]
        public void Batch_FullGradeWithoutGroups_SizesFollowPieceArea_LogosShareNumbering()
        {
            var big = Set(0, 300);
            var small = Set(400, 100);
            var medium = Set(600, 200);

            var result = new MoldDefineService(() => ThreeKidsSizes).DefineBatch(
                Select(medium.Logo, big.Piece, small.Logo, small.Piece, big.Logo, medium.Piece), _page, _arrangement, "Frente", "C");

            Assert.True(result.IsSuccess, result.Message);
            var groups = _layer.ShapesList.Cast<FakeShape>().ToList();
            Assert.Equal(new[] { "12A", "4A", "8A" }, groups.Select(g => g.Name).OrderBy(n => n));
            foreach (var (set, size) in new[] { (small, "4A"), (medium, "8A"), (big, "12A") })
            {
                var group = groups.Single(g => g.Name == size);
                Assert.Equal(new[] { "TAG", "L1", "Frente", "x" }, group.Shapes.Select(s => s.Name));
                Assert.Same(set.Piece, Child(group, "Frente"));
                Assert.Same(set.Logo, Child(group, "L1"));
                Assert.Equal(size, ((FakeShape)Child(group, "TAG")).Text);
            }

            Assert.Equal(new[] { "4A", "8A", "12A" }, _arrangement.Selected.Select(s => s.Name));
            Assert.StartsWith("3 tamanho(s) de Frente: 4A —", result.Message);
        }

        [Fact]
        public void Batch_WithAnAlreadyDefinedGroup_OtherSetsAreSizedFromIt()
        {
            var definedG = Set(0, 300);
            var service = new MoldDefineService();
            Assert.True(service.Define(Select(definedG.Piece, definedG.Logo), _page, _arrangement, "Frente", "G").IsSuccess);
            var groupG = (FakeShape)_layer.ShapesList.Single();
            var p = Set(400, 100);
            var m = Set(600, 200);

            var result = service.DefineBatch(Select(groupG, p.Piece, p.Logo, m.Piece, m.Logo), _page, _arrangement, "Frente", "ma");

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new[] { "P", "M", "G" }, _arrangement.Selected.Select(s => s.Name));
            Assert.Equal("L1", p.Logo.Name);
            Assert.Equal("L1", m.Logo.Name);
            Assert.Equal(3, _layer.ShapesList.Count);
        }

        [Fact]
        public void Batch_RefusesWithoutChanges_WhenSizesCannotBeKnownOrASetIsAmbiguous()
        {
            var a = Set(0, 300);
            var b = Set(400, 100);
            var service = new MoldDefineService(() => ThreeKidsSizes);

            var partial = service.DefineBatch(Select(a.Piece, a.Logo, b.Piece, b.Logo), _page, _arrangement, "Frente", "C");
            Assert.False(partial.IsSuccess);
            Assert.Contains("um conjunto para cada tamanho da categoria (3: 4A, 8A, 12A); foram encontrados 2", partial.Message);

            var twin = Piece(centerX: 650, centerY: 100, width: 200, height: 200);
            var twinInside = _layer.AddShape(new FakeShape { Name = "Curva", Kind = ShapeKind.Curve, Width = 199, Height = 199, CenterX = 650, CenterY = 100 });
            var ambiguous = service.DefineBatch(Select(a.Piece, a.Logo, b.Piece, b.Logo, twin, twinInside), _page, _arrangement, "Frente", "C");
            Assert.False(ambiguous.IsSuccess);
            Assert.Contains("quase do mesmo tamanho", ambiguous.Message);

            Assert.All(_layer.ShapesList, s => Assert.Equal("Curva", s.Name));
            Assert.Equal(6, _layer.ShapesList.Count);
        }

        [Fact]
        public void DefineOptions_Validate()
        {
            Assert.Empty(MoldDefineOptions.Default.Validate());
            Assert.True(new MoldDefineOptions(true, "ff8000", 1, true, 10, 0).TryGetLaserRgb(out var r, out var g, out var b));
            Assert.Equal((255, 128, 0), (r, g, b));

            var problems = string.Join(" | ", new MoldDefineOptions(true, "#GG0000", 0, true, double.NaN, -1).Validate());
            Assert.Contains("Cor do laser", problems);
            Assert.Contains("Espessura do laser", problems);
            Assert.Contains("Fonte da TAG", problems);
            Assert.Contains("Contorno da TAG", problems);
        }
    }
}
