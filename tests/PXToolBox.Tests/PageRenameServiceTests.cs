using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class PageRenameServiceTests
    {
        [Fact]
        public void Rename_SetsNameAndReportsPrevious()
        {
            var page = new FakePage { Name = "Página 1" };

            var result = new PageRenameService().Rename(page, "  Dryfit - João Silva ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Dryfit - João Silva", page.Name);
            Assert.Equal("Página ativa renomeada de \"Página 1\" para \"Dryfit - João Silva\".", result.Message);
        }

        [Fact]
        public void Rename_SameName_IsSuccessWithoutChange()
        {
            var page = new FakePage { Name = "Time X" };

            var result = new PageRenameService().Rename(page, "Time X");

            Assert.True(result.IsSuccess);
            Assert.Equal("A página ativa já se chama \"Time X\".", result.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Rename_EmptyName_LeavesPageUnchanged(string? name)
        {
            var page = new FakePage { Name = "Página 1" };

            var result = new PageRenameService().Rename(page, name);

            Assert.False(result.IsSuccess);
            Assert.Equal("Página 1", page.Name);
        }
    }
}
