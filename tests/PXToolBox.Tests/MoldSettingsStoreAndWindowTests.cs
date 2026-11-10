#if NET48
// Só na perna net48 — PXToolBoxDev (host) só existe lá. Arquivo temporário isolado; nunca o real.
using System;
using System.IO;
using System.Threading;
using PXToolBox.Core.Molds;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.Molds;
using Xunit;

namespace PXToolBox.Tests
{
    [Collection("WPF created in code — no parallel WPF initialization")]
    public class MoldSettingsStoreAndWindowTests : IDisposable
    {
        private readonly string _file = Path.Combine(Path.GetTempPath(), "px-mold-settings-" + Guid.NewGuid().ToString("N") + ".json");

        public MoldSettingsStoreAndWindowTests() => MoldSettingsStore.SetFilePathForTesting(_file);

        public void Dispose()
        {
            MoldSettingsStore.SetFilePathForTesting(null);
            if (File.Exists(_file)) File.Delete(_file);
        }

        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new Xunit.Sdk.XunitException(failure.ToString());
        }

        [Fact]
        public void NoFile_IsFactoryDefault_WithoutNotice()
        {
            var (settings, notice) = MoldSettingsStore.Load();
            Assert.Same(MoldSettings.Default, settings);
            Assert.Null(notice);
        }

        [Fact]
        public void SaveAndLoad_RoundTrip()
        {
            var custom = new MoldSettings(
                new MoldSizeCategory("MA", new[] { "P", "G" }),
                new MoldSizeCategory("FE", new[] { "P", "G" }, new[] { "BL" }),
                new MoldSizeCategory("C", new[] { "4", "8" }, null, new[] { "A" }),
                "G", new[] { "x", "LASER" }, "Tag", "", new MoldDefineOptions(false, "#00FF00", 0.35, false, 72, 0));

            MoldSettingsStore.Save(custom);
            var (loaded, notice) = MoldSettingsStore.Load();

            Assert.Null(notice);
            Assert.Equal((false, "#00FF00", 0.35, false, 72.0, 0.0), (loaded.Define.CreateLaserCopy, loaded.Define.LaserColor,
                loaded.Define.LaserWidthMillimeters, loaded.Define.CreateTag, loaded.Define.TagFontSizePoints, loaded.Define.TagOutlineWidthMillimeters));
            Assert.Equal(new[] { "BLP", "BLG" }, loaded.Female.Sizes);
            Assert.Equal(new[] { "4A", "8A" }, loaded.Kids.Sizes);
            Assert.Equal(new[] { "x", "LASER" }, loaded.LaserNames);
            Assert.Equal("Tag", loaded.TagName);
            Assert.Equal("", loaded.IdentifierName);

            MoldSettingsStore.ResetToDefault();
            Assert.False(File.Exists(_file));
        }

        [Theory]
        [InlineData("{ não é json")]
        [InlineData("{\"schema\": 99}")]
        [InlineData("{\"schema\": 1, \"male\": {\"code\": \"MA\", \"baseSizes\": []}}")]
        [InlineData("{\"schema\": 1, \"laserNames\": [1, 2]}")]
        public void BrokenOrInvalidFile_FallsBackToDefault_WithNotice(string content)
        {
            File.WriteAllText(_file, content);
            var (settings, notice) = MoldSettingsStore.Load();
            Assert.Same(MoldSettings.Default, settings);
            Assert.Contains("valores de fábrica", notice);
        }

        [Fact]
        public void FileFromDev83_WithoutDefineBlock_UsesFactoryDefineOptions()
        {
            File.WriteAllText(_file, "{\"schema\": 1, \"gridBaseSize\": \"G\", \"laserNames\": [\"x\"], \"tagName\": \"TAG\", \"identifierName\": \"\"}");

            var (settings, notice) = MoldSettingsStore.Load();

            Assert.Null(notice);
            Assert.Same(MoldDefineOptions.Default, settings.Define);
        }

        [Fact]
        public void TagPositionAndMargins_RoundTrip_AndOlderOrUnknownFallBackToFactory()
        {
            var d = MoldSettings.Default;
            MoldSettingsStore.Save(new MoldSettings(d.Male, d.Female, d.Kids, d.GridBaseSize, d.LaserNames, d.TagName, d.IdentifierName,
                new MoldDefineOptions(true, "#FF0000", 0.2, true, 50, 2, MoldTagPosition.MiddleRight, 0.5, 1, 1.5, 2)));
            var loaded = MoldSettingsStore.Load().Settings.Define;
            Assert.Equal((MoldTagPosition.MiddleRight, 0.5, 1.0, 1.5, 2.0),
                (loaded.TagPosition, loaded.TagLeftMarginCm, loaded.TagRightMarginCm, loaded.TagTopMarginCm, loaded.TagBottomMarginCm));

            // dev.83–89 (sem posição/margens) e nome de posição desconhecido: embaixo no centro, sem margens.
            File.WriteAllText(_file, "{\"schema\": 1, \"define\": {\"createTag\": true, \"tagPosition\": \"Diagonal\", \"tagTopMarginCm\": 3}}");
            var (fallback, notice) = MoldSettingsStore.Load();
            Assert.Null(notice);
            Assert.Equal((MoldTagPosition.BottomCenter, 0.0, 3.0), (fallback.Define.TagPosition, fallback.Define.TagLeftMarginCm, fallback.Define.TagTopMarginCm));
        }

        [Fact]
        public void InnerOutline_RoundTrip_AndOlderFileIsOff()
        {
            var d = MoldSettings.Default;
            MoldSettingsStore.Save(new MoldSettings(d.Male, d.Female, d.Kids, d.GridBaseSize, d.LaserNames, d.TagName, d.IdentifierName, d.Define,
                new MoldInnerOutlineOptions(true, 0.8, MoldLineStyle.Dotted, MoldLineStyle.Solid, MoldLineStyle.Dashed, MoldInnerOutlineOptions.ParseColors("G #00FF00"))));
            var loaded = MoldSettingsStore.Load().Settings.InnerOutline;
            Assert.Equal((true, 0.8, MoldLineStyle.Dotted, MoldLineStyle.Solid, MoldLineStyle.Dashed), (loaded.Enabled, loaded.WidthMillimeters, loaded.MaleStyle, loaded.FemaleStyle, loaded.KidsStyle));
            Assert.Equal("#00FF00", loaded.SizeColors["G"]);

            File.WriteAllText(_file, "{\"schema\": 1, \"innerOutline\": {\"enabled\": true, \"femaleStyle\": \"Ondulado\"}}");
            var partial = MoldSettingsStore.Load().Settings.InnerOutline;
            Assert.Equal((true, 0.5, MoldLineStyle.Dashed), (partial.Enabled, partial.WidthMillimeters, partial.FemaleStyle));

            File.WriteAllText(_file, "{\"schema\": 1}");
            Assert.False(MoldSettingsStore.Load().Settings.InnerOutline.Enabled);
        }

        [Fact]
        public void Window_InnerOutline_PreviewAndSave()
        {
            RunSta(() =>
            {
                var window = new MoldSettingsWindow();
                Assert.Equal(MoldSettings.Default.AllSizes.Count, window.InnerColorsPreviewCount);

                window.InnerOutlineChecked = true;
                window.InnerColorsText = "G #00FF00\r\nBLP azul";
                Assert.False(window.TrySave());
                Assert.Contains(window.Problems, p => p.Contains("BLP"));

                window.InnerColorsText = "g #00ff00";
                window.InnerFemaleStyleIndex = 2;
                Assert.True(window.TrySave());
                var saved = MoldSettingsStore.Load().Settings;
                Assert.True(saved.InnerOutline.Enabled);
                Assert.Equal(("#00FF00", MoldLineStyle.Dotted), (saved.InnerOutlineColorOf("G"), saved.InnerOutline.FemaleStyle));
                window.Close();
            });
        }

        [Fact]
        public void Window_TagPositionAndMargins_DescribedAndSaved()
        {
            RunSta(() =>
            {
                var window = new MoldSettingsWindow();
                Assert.Equal("A TAG fica dentro da caixa da peça, na base, centralizada.", window.TagPreviewText);

                window.TagPositionIndex = 0;
                window.TagLeftMarginText = "1,5";
                Assert.Equal("A TAG fica dentro da caixa da peça, no topo, encostada na esquerda, deslocada 1,5 cm para a direita.", window.TagPreviewText);

                window.TagLeftMarginText = "-1";
                Assert.False(window.TrySave());
                window.TagLeftMarginText = "2";
                Assert.True(window.TrySave());
                var saved = MoldSettingsStore.Load().Settings.Define;
                Assert.Equal((MoldTagPosition.TopLeft, 2.0), (saved.TagPosition, saved.TagLeftMarginCm));
                window.Close();
            });
        }

        [Fact]
        public void Window_ShowsPreview_BlocksInvalid_SavesValid()
        {
            RunSta(() =>
            {
                var window = new MoldSettingsWindow();
                Assert.Equal("Tamanhos: BLPP, BLP, BLM, BLG, BLGG, BLXGG, BLXXGG", window.PreviewText("female"));
                Assert.Equal("O Grid copia: G (MA), BLG (FE), 12A (C).", window.GridPreviewText);
                Assert.Empty(window.Problems);

                window.TagNameText = "";
                Assert.False(window.TrySave());
                Assert.False(File.Exists(_file));

                window.TagNameText = "Tag";
                window.LaserColorText = "vermelho";
                Assert.False(window.TrySave());
                Assert.Contains(window.Problems, p => p.Contains("Cor do laser"));

                window.LaserColorText = "0000ff";
                window.LaserWidthText = "0,5";
                window.CreateLaserCopyChecked = false;
                window.GridBaseSizeText = "gg";
                Assert.True(window.TrySave());
                var saved = MoldSettingsStore.Load().Settings;
                Assert.Equal("GG", saved.GridBaseSize);
                Assert.Equal(("#0000FF", 0.5, false), (saved.Define.LaserColor, saved.Define.LaserWidthMillimeters, saved.Define.CreateLaserCopy));

                window.RestoreDefault();
                Assert.Equal("G", window.Draft.GridBaseSize);
                window.Close();
            });
        }
    }
}
#endif
