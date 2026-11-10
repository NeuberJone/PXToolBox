using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class MoldSaveServiceTests
    {
        [Theory]
        [InlineData(" Camisa Polo ", "Camisa Polo.cdr")]
        [InlineData("Camisa.CDR", "Camisa.cdr")]
        [InlineData("Regata.2026", "Regata.2026.cdr")]
        public void FileName_FromTheTypedName(string typed, string expected)
        {
            Assert.True(MoldSaveService.TryCreateFileName(typed, out var fileName, out var error), error);
            Assert.Equal(expected, fileName);
        }

        [Theory]
        [InlineData("", "Informe o nome do molde.")]
        [InlineData("   ", "Informe o nome do molde.")]
        [InlineData(".cdr", "não pode ter")]
        [InlineData("Camisa/Polo", "não pode ter")]
        [InlineData("Camisa?", "não pode ter")]
        public void FileName_Refused(string typed, string expectedError)
        {
            Assert.False(MoldSaveService.TryCreateFileName(typed, out var fileName, out var error));
            Assert.Equal(string.Empty, fileName);
            Assert.Contains(expectedError, error);
        }

        [Fact]
        public void InitialFolder_DocumentFolder_ThenMoldsFolder_ThenNone()
        {
            Assert.Equal(@"D:\Moldes", MoldSaveService.InitialFolder(@"D:\Moldes\Camisa.cdr", _ => true));
            Assert.Equal(MoldSaveService.DefaultFolder, MoldSaveService.InitialFolder(string.Empty, _ => true));
            Assert.Equal(MoldSaveService.DefaultFolder, MoldSaveService.InitialFolder(@"D:\Sumiu\Camisa.cdr", f => f == MoldSaveService.DefaultFolder));
            Assert.Equal(string.Empty, MoldSaveService.InitialFolder(null, _ => false));
        }

        [Fact]
        public void Save_SavesTheWholeDocument_OnlyAsCdr()
        {
            var document = new FakeDocument();
            var service = new MoldSaveService();

            var refused = service.Save(document, @"D:\Moldes\Camisa.pdf");
            Assert.False(refused.IsSuccess);
            Assert.Null(document.SavedAsPath);

            var result = service.Save(document, @"D:\Moldes\Camisa.cdr");
            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(@"D:\Moldes\Camisa.cdr", document.SavedAsPath);
            Assert.Equal("Molde salvo em \"D:\\Moldes\\Camisa.cdr\".", result.Message);
        }
    }
}
