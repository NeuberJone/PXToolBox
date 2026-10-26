using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class RenameSelectedObjectsServiceTests
    {
        [Fact]
        public void Rename_EmptySelection_Fails()
        {
            var service = new RenameSelectedObjectsService();
            var result = service.Rename(new FakeSelection(), "Novo");
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void Rename_EmptyName_Cancels()
        {
            var selection = new FakeSelection();
            selection.ShapesList.Add(new FakeShape());
            var service = new RenameSelectedObjectsService();

            var result = service.Rename(selection, "");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void Rename_AppliesToAllSelected()
        {
            var selection = new FakeSelection();
            var a = new FakeShape();
            var b = new FakeShape();
            selection.ShapesList.Add(a);
            selection.ShapesList.Add(b);
            var service = new RenameSelectedObjectsService();

            var result = service.Rename(selection, "Peça X");

            Assert.True(result.IsSuccess);
            Assert.Equal("Peça X", a.Name);
            Assert.Equal("Peça X", b.Name);
        }
    }
}
