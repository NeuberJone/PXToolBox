using System;
using System.IO;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public sealed class SliceThumbnailServiceTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "px-slice-thumb-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static FakeSelection Art(double width, double height)
        {
            var layer = new FakeLayer { Name = "Camada 1" };
            var selection = new FakeSelection();
            selection.ShapesList.Add(layer.AddShape(new FakeShape { Kind = ShapeKind.Group, CenterX = 500, CenterY = 500, Width = width, Height = height }));
            return selection;
        }

        [Fact]
        public void Read_ExportsTheSelection_LongestSide600Pixels_ReadsTheBytes_AndDeletesTheFile()
        {
            var document = new FakeDocument { ExportBytes = new byte[] { 1, 2, 3 } };

            // 1 mm = 2 unidades do documento: 400 × 100 unidades = 200 × 50 mm.
            var result = new SliceThumbnailService().Read(document, Art(400, 100), 2, _folder);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(new byte[] { 1, 2, 3 }, result.Value!.Png);
            Assert.Equal((200.0, 50.0), (result.Value.WidthMm, result.Value.HeightMm));
            var (path, width, height) = Assert.Single(document.Exported);
            Assert.Equal((600, 150), (width, height));
            Assert.Equal(_folder, Path.GetDirectoryName(path));
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void NothingExported_OrNoSelection_Explains()
        {
            var document = new FakeDocument { ExportBytes = null };

            var missing = new SliceThumbnailService().Read(document, Art(100, 100), 1, _folder);
            Assert.False(missing.IsSuccess);
            Assert.Equal("O Corel não gerou a miniatura da arte.", missing.Message);

            var none = new SliceThumbnailService().Read(document, new FakeSelection(), 1, _folder);
            Assert.False(none.IsSuccess);
            Assert.Single(document.Exported);
        }
    }
}
