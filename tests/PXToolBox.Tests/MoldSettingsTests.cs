using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Core.Naming;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class MoldSettingsTests
    {
        private static MoldSettings With(MoldSizeCategory? male = null, MoldSizeCategory? female = null, MoldSizeCategory? kids = null,
            string grid = "G", string[]? laser = null, string tag = "TAG", string identifier = "") =>
            new MoldSettings(male ?? MoldSettings.Default.Male, female ?? MoldSettings.Default.Female, kids ?? MoldSettings.Default.Kids,
                grid, laser ?? new[] { "x" }, tag, identifier);

        [Fact]
        public void Default_GeneratesTheFactorySizes()
        {
            var settings = MoldSettings.Default;
            Assert.Equal(new[] { "PP", "P", "M", "G", "GG", "XGG", "XXGG" }, settings.Male.Sizes);
            Assert.Equal("BLPP", settings.Female.Sizes[0]);
            Assert.Equal(new[] { "2A", "4A", "6A", "8A", "10A", "12A" }, settings.Kids.Sizes);
            Assert.Empty(settings.Validate());
            Assert.Equal(new[] { "G" }, settings.GridSizesOf("MA"));
            Assert.Equal(new[] { "BLG" }, settings.GridSizesOf("fe"));
            Assert.Equal(new[] { "12A" }, settings.GridSizesOf("C"));
            Assert.Empty(settings.GridSizesOf("XX"));
        }

        [Fact]
        public void Combination_IsPrefixBaseSuffix_InOrder_WithoutRepeats()
        {
            var category = new MoldSizeCategory("fe", new[] { "p, m", "P" }, new[] { "bl;bf" }, new[] { "a b" });
            Assert.Equal("FE", category.Code);
            Assert.Equal(new[] { "BLPA", "BLPB", "BLMA", "BLMB", "BFPA", "BFPB", "BFMA", "BFMB" }, category.Sizes);
            Assert.True(category.Contains(" blmb "));
        }

        [Fact]
        public void Validate_FindsEveryProblem()
        {
            var bad = With(
                male: new MoldSizeCategory("MA", new[] { "P", "G" }),
                female: new MoldSizeCategory("MA", new[] { "X" }),
                kids: new MoldSizeCategory("C.X", new string[0]),
                grid: "", laser: new string[0], tag: "");
            var repeated = With(female: new MoldSizeCategory("FE", new[] { "G", "BLG" }));

            var problems = string.Join(" | ", bad.Validate()) + " | " + string.Join(" | ", repeated.Validate());

            Assert.Contains("ponto nem espaço", problems);
            Assert.Contains("Infantil: informe pelo menos um tamanho base", problems);
            Assert.Contains("códigos de categoria precisam ser diferentes", problems);
            Assert.Contains("Tamanho em mais de uma categoria: G", problems);
            Assert.Contains("laser", problems);
            Assert.Contains("TAG", problems);
            Assert.Contains("tamanho base do Grid", problems);
        }

        [Fact]
        public void Tools_UseTheConfiguredSizesAndNames()
        {
            var settings = With(
                female: new MoldSizeCategory("FEM", new[] { "P", "G" }, new[] { "BABY" }),
                kids: new MoldSizeCategory("INF", new[] { "4", "16" }, null, new[] { "ANOS" }),
                laser: new[] { "CORTE" }, tag: "ETIQUETA");
            var service = new MoldToolsService(() => settings);
            var page = new FakePage();
            var source = new FakeLayer { Name = "Camada 1" };
            page.LayersList.Add(source);
            var group = source.AddShape(new FakeShape { Name = "BABYG", Kind = ShapeKind.Group });
            foreach (var name in new[] { "ETIQUETA", "Frente", "CORTE" }) group.ShapesList.Add(new FakeShape { Name = name, ParentGroup = group });
            var selection = new FakeSelection();
            selection.ShapesList.Add(group);

            var result = service.CreateLayersForSelection(selection, page, PieceTypeCode.Cam1);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("CAM1.MOLDE.FEM.Frente", group.CurrentLayer!.Name);
            Assert.Equal(new[] { "16ANOS" }, settings.GridSizesOf("INF"));
            Assert.Equal(new[] { "BABYG" }, settings.GridSizesOf("FEM"));
        }
    }
}
