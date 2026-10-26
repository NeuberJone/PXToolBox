using PXToolBox.Core.Naming;
using Xunit;

namespace PXToolBox.Tests
{
    public class LayerNameTests
    {
        [Theory]
        [InlineData("CAM1.PP.X.MA", LayerCategory.MA)]
        [InlineData("CAM1.PP.X.FE", LayerCategory.FE)]
        [InlineData("CAM1.PP.X.C", LayerCategory.C)]
        [InlineData("CAM1.PP.X.ma", LayerCategory.MA)] // case-insensitive
        public void Category_MatchesLastSegment(string name, LayerCategory expected)
        {
            Assert.Equal(expected, new LayerName(name).Category);
        }

        [Fact]
        public void Category_DoesNotMatchSubstringInsideAWord()
        {
            // "Estampa" contém "MA" no meio da palavra, mas não é um sufixo pontuado ".MA".
            Assert.Equal(LayerCategory.Unknown, new LayerName("Estampa").Category);
            Assert.Equal(LayerCategory.Unknown, new LayerName("CAM1.Estampa").Category);
        }

        [Fact]
        public void Category_SingleSegmentNeverMatches()
        {
            Assert.Equal(LayerCategory.Unknown, new LayerName("MA").Category);
            Assert.Equal(LayerCategory.Unknown, new LayerName("C").Category);
        }

        [Fact]
        public void WithFirstSegment_ReplacesOnlyFirstSegment()
        {
            var layerName = new LayerName("CAM1.PP.X.MA");
            Assert.Equal("SHO1.PP.X.MA", layerName.WithFirstSegment("SHO1"));
        }
    }
}
