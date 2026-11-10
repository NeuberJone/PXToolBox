#if NET48
// O catálogo é embutido no host DEV, que só é referenciado pela perna net48.
using PXToolBoxDev.ReleaseNotes;
using Xunit;

namespace PXToolBox.Tests
{
    public class ReleaseNotesTests
    {
        [Fact]
        public void ForVersion_KnownVersion_LoadsItsEmbeddedNotes()
        {
            var notes = ReleaseNotesCatalog.ForVersion("0.1.0-dev.59");

            Assert.Equal("0.1.0-dev.59", notes.Version);
            Assert.Empty(notes.Notice);
            Assert.Contains(notes.Implemented, text => text.Contains("Notas da versão"));
            Assert.Contains(notes.Implemented, text => text.Contains("Implementado, Alterado e Removido"));
            Assert.Empty(notes.Changed);
            Assert.Empty(notes.Removed);
        }

        [Theory]
        [InlineData("0.1.0-dev.58")]
        [InlineData("0.1.0-dev.999")]
        [InlineData("0.1.0-DEV.59")]
        [InlineData(" 0.1.0-dev.59")]
        [InlineData("0.1.0-dev.59 ")]
        [InlineData("0.1.0-dev.59+local")]
        [InlineData("0.1.0-dev.5")]
        public void ForVersion_WithoutExactMatch_PreservesVersionAndDoesNotUseOtherReleaseNotes(string version)
        {
            var notes = ReleaseNotesCatalog.ForVersion(version);

            Assert.Equal(version, notes.Version);
            Assert.False(string.IsNullOrWhiteSpace(notes.Notice));
            Assert.Contains(version, notes.Notice);
            Assert.Empty(notes.Implemented);
            Assert.Empty(notes.Changed);
            Assert.Empty(notes.Removed);
        }

        [Fact]
        public void ForVersion_LocalBuild_DoesNotInheritNotesFromAPreviousLookup()
        {
            var published = ReleaseNotesCatalog.ForVersion("0.1.0-dev.59");
            var local = ReleaseNotesCatalog.ForVersion("0.0.0-dev.local");
            var publishedAgain = ReleaseNotesCatalog.ForVersion("0.1.0-dev.59");

            Assert.Equal("0.0.0-dev.local", local.Version);
            Assert.False(string.IsNullOrWhiteSpace(local.Notice));
            Assert.Contains("0.0.0-dev.local", local.Notice);
            Assert.Contains("Build local de desenvolvimento", local.Notice);
            Assert.Contains("não há notas de versão publicadas", local.Notice);
            Assert.Empty(local.Implemented);
            Assert.Empty(local.Changed);
            Assert.Empty(local.Removed);
            Assert.NotEmpty(published.Implemented);
            Assert.Equal(published.Implemented, publishedAgain.Implemented);
            Assert.Empty(publishedAgain.Notice);
        }
    }
}
#endif
