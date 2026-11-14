#if NET48
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using PXToolBox.Core.Nesting;
using PXToolBoxDev.Integrations;
using PXToolBoxDev.Nesting;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>Perfis do "Encaixar peças": sempre num arquivo temporário, nunca o do usuário.</summary>
    [Collection("WPF created in code — no parallel WPF initialization")]
    public sealed class NestingProfileTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "px-nesting-profiles-" + Guid.NewGuid().ToString("N"));
        private readonly string _file;

        public NestingProfileTests()
        {
            _file = Path.Combine(_directory, "nesting-profiles.json");
            NestingProfileStore.SetFilePathForTesting(_file);
            Assert.Equal(_file, NestingProfileStore.FilePath);
        }

        public void Dispose()
        {
            NestingProfileStore.SetFilePathForTesting(null);
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Fact]
        public void Store_WithoutFile_HasNoProfiles_AndKeepsWhatWasSaved()
        {
            var (none, readable, notice) = NestingProfileStore.Load();
            Assert.Empty(none);
            Assert.True(readable);
            Assert.Null(notice);

            NestingProfileStore.Save(new[]
            {
                new NestingProfile { Name = " Rolo 160 ", Area = NestingProfileArea.Roll, RollWidth = "160", RollHeight = "300", GroupBlocks = true, BlockGap = "0,5", Legends = true, SplitByLayer = true, Method = NestingProfileMethod.Contour,
                    Rotation = NestingProfileRotation.HalfTurn, SearchBest = true, TimeLimit = "45", Margin = "1", Spacing = "0,3", ContourCell = "2" },
                new NestingProfile { Name = "Retalhos", Area = NestingProfileArea.Areas, AreasText = "A: 50 x 50\r\n30 x 30",
                    AreaOrder = NestAreaOrder.SmallestFirst, DrawAreas = false, ResizePage = true, Origin = NestOrigin.BottomRight },
            });

            var (profiles, _, _) = NestingProfileStore.Load();
            Assert.Equal(new[] { "Rolo 160", "Retalhos" }, profiles.Select(p => p.Name));
            var roll = profiles[0];
            Assert.Equal((NestingProfileArea.Roll, "160", NestingProfileMethod.Contour, NestingProfileRotation.HalfTurn, true, "45", "1", "0,3", "2"),
                (roll.Area, roll.RollWidth, roll.Method, roll.Rotation, roll.SearchBest, roll.TimeLimit, roll.Margin, roll.Spacing, roll.ContourCell));
            var scraps = profiles[1];
            Assert.Equal((NestingProfileArea.Areas, "A: 50 x 50\r\n30 x 30", NestAreaOrder.SmallestFirst, false, true, NestOrigin.BottomRight),
                (scraps.Area, scraps.AreasText, scraps.AreaOrder, scraps.DrawAreas, scraps.ResizePage, scraps.Origin));
            Assert.Equal(NestOrigin.TopLeft, roll.Origin);
            Assert.Equal(("300", ""), (roll.RollHeight, scraps.RollHeight));
            Assert.Equal((true, false), (roll.GroupBlocks, scraps.GroupBlocks));
            Assert.Equal(("0,5", true), (roll.BlockGap, roll.Legends));
            Assert.Equal(("2", false), (scraps.BlockGap, scraps.Legends));
            Assert.Equal((true, false), (roll.SplitByLayer, scraps.SplitByLayer));
        }

        [Fact]
        public void Store_MissingOrUnknownFields_UseTheWindowDefaults()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_file, "{ \"schema\": 1, \"profiles\": [ { \"name\": \"Velho\", \"area\": \"Lua\", \"rotation\": \"2\", \"method\": \"contour\" } ] }");

            var (profiles, readable, notice) = NestingProfileStore.Load();

            Assert.True(readable);
            Assert.Null(notice);
            var old = Assert.Single(profiles);
            var defaults = new NestingProfile();
            Assert.Equal((NestingProfileArea.Page, NestingProfileRotation.None, NestingProfileMethod.Contour, defaults.Margin, defaults.TimeLimit, defaults.AreasText, NestOrigin.TopLeft),
                (old.Area, old.Rotation, old.Method, old.Margin, old.TimeLimit, old.AreasText, old.Origin));
            Assert.Equal("", old.RollHeight);   // perfis de antes da dev.111: comprimento livre
            Assert.False(old.GroupBlocks);      // antes da dev.113: folhas uma embaixo da outra
            Assert.Equal(("2", false), (old.BlockGap, old.Legends));   // antes da dev.119: 2 cm, sem legenda
            Assert.False(old.SplitByLayer);
        }

        [Theory]
        [InlineData("{ nada")]
        [InlineData("{ \"schema\": 2, \"profiles\": [] }")]
        [InlineData("{ \"schema\": 1 }")]
        [InlineData("{ \"schema\": 1, \"profiles\": [ { \"name\": \"A\" }, { \"name\": \"a\" } ] }")]
        public void Store_UnreadableFile_GivesNoProfiles_AndSaysNothingWillBeWrittenOverIt(string content)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_file, content);

            var (profiles, readable, notice) = NestingProfileStore.Load();

            Assert.Empty(profiles);
            Assert.False(readable);
            Assert.StartsWith("Não foi possível ler os perfis (", notice);
            Assert.Contains("Nada será gravado por cima", notice);
        }

        [Fact]
        public void Validate_RefusesEmptyLongAndRepeatedNames_AndTooManyProfiles()
        {
            Assert.Equal(new[] { "Perfil sem nome." }, NestingProfile.Validate(new[] { new NestingProfile { Name = " " } }));
            Assert.Contains("mais de 40 letras", Assert.Single(NestingProfile.Validate(new[] { new NestingProfile { Name = new string('x', 41) } })));
            Assert.Equal(new[] { "Perfil repetido: “Rolo”." },
                NestingProfile.Validate(new[] { new NestingProfile { Name = "Rolo" }, new NestingProfile { Name = "ROLO " } }));
            Assert.Equal(new[] { "Até 30 perfis: exclua um antes de salvar outro." },
                NestingProfile.Validate(Enumerable.Range(1, 31).Select(k => new NestingProfile { Name = $"P{k}" }).ToList()));
            Assert.Throws<ArgumentException>(() => NestingProfileStore.Save(new[] { new NestingProfile { Name = "" } }));
            Assert.False(File.Exists(_file));
        }

        [Fact]
        public void Window_SavesAppliesUpdatesAndDeletesProfiles()
        {
            RunWindow(() =>
            {
                var items = new[] { new NestItem(0, "Frente", 500, 700), new NestItem(1, "Manga", 250, 200) };
                var window = new NestingWindow(new NestingSelection(items, 2100, 2970));
                try
                {
                    T Find<T>(string name) => (T)window.FindName(name);
                    var message = Find<TextBlock>("ProfileMessage");
                    var list = Find<WrapPanel>("ProfileList");
                    Assert.Equal("Nenhum perfil salvo.", message.Text);
                    Assert.Empty(list.Children);

                    Click(Find<Button>("SaveProfileButton"));
                    Assert.Equal("Digite o nome do perfil ao lado de “Salvar”.", message.Text);
                    Assert.False(File.Exists(_file));

                    Find<RadioButton>("RollMode").IsChecked = true;
                    Find<TextBox>("RollWidth").Text = "150";
                    Find<TextBox>("Spacing").Text = "1";
                    Find<RadioButton>("QuarterTurns").IsChecked = true;
                    Find<CheckBox>("SearchBest").IsChecked = true;
                    Find<TextBox>("ProfileName").Text = "Rolo 150";
                    Click(Find<Button>("SaveProfileButton"));
                    Assert.Equal("Perfil “Rolo 150” salvo.", message.Text);
                    Assert.Equal("Rolo 150", ((Button)Assert.Single(list.Children)).Content);
                    Assert.True(File.Exists(_file));

                    // Mesmo nome com outra caixa: atualiza, não duplica.
                    Find<TextBox>("RollWidth").Text = "160";
                    Find<TextBox>("ProfileName").Text = "rolo 150";
                    Click(Find<Button>("SaveProfileButton"));
                    Assert.Equal("Perfil “rolo 150” atualizado.", message.Text);
                    Assert.Equal("160", Assert.Single(NestingProfileStore.Load().Profiles).RollWidth);
                }
                finally { window.Close(); }

                // Outra janela: o perfil está lá e preenche os campos de uma vez.
                window = new NestingWindow(new NestingSelection(items, 2100, 2970));
                try
                {
                    T Find<T>(string name) => (T)window.FindName(name);
                    var list = Find<WrapPanel>("ProfileList");
                    Assert.Equal(new[] { "rolo 150" }, list.Children.Cast<Button>().Select(b => (string)b.Content));
                    // Abre com as últimas opções da janela anterior; muda tudo para ver o perfil preencher de novo.
                    Assert.True(Find<RadioButton>("RollMode").IsChecked);
                    Find<RadioButton>("PageMode").IsChecked = true;
                    Find<TextBox>("RollWidth").Text = "100";
                    Find<TextBox>("Spacing").Text = "0,5";
                    Find<RadioButton>("NoRotation").IsChecked = true;
                    Find<CheckBox>("SearchBest").IsChecked = false;

                    Click((Button)list.Children[0]);
                    Assert.True(Find<RadioButton>("RollMode").IsChecked);
                    Assert.Equal(("160", "1", true, true), (Find<TextBox>("RollWidth").Text, Find<TextBox>("Spacing").Text,
                        Find<RadioButton>("QuarterTurns").IsChecked == true, Find<CheckBox>("SearchBest").IsChecked == true));
                    Assert.Equal("rolo 150", Find<TextBox>("ProfileName").Text);
                    Assert.Equal("Perfil “rolo 150” aplicado.", Find<TextBlock>("ProfileMessage").Text);
                    var report = Find<TextBox>("ReportText").Text;
                    Assert.Contains("Busca: 5 de 5 ordem(ns)", report);
                    Assert.True(Find<Button>("CopyButton").IsEnabled); // plano pronto (sem o painel, "Encaixar no Corel" fica desligado)

                    Find<TextBox>("ProfileName").Text = "outro";
                    Click(Find<Button>("DeleteProfileButton"));
                    Assert.Equal("Nenhum perfil chamado “outro”.", Find<TextBlock>("ProfileMessage").Text);
                    Find<TextBox>("ProfileName").Text = "ROLO 150";
                    Click(Find<Button>("DeleteProfileButton"));
                    Assert.Equal("Perfil “ROLO 150” excluído.", Find<TextBlock>("ProfileMessage").Text);
                    Assert.Empty(list.Children);
                    Assert.Empty(NestingProfileStore.Load().Profiles);
                }
                finally { window.Close(); }
            });
        }

        [Fact]
        public void Window_ReadsTheFileAgainBeforeSaving_SoProfilesImportedMeanwhileAreKept()
        {
            RunWindow(() =>
            {
                var window = new NestingWindow(new NestingSelection(new[] { new NestItem(0, "Frente", 500, 700) }, 2100, 2970));
                try
                {
                    T Find<T>(string name) => (T)window.FindName(name);
                    Find<TextBox>("ProfileName").Text = "Antigo";
                    Click(Find<Button>("SaveProfileButton"));

                    // Com a janela aberta, as preferências importadas trocam a lista.
                    NestingProfileStore.Save(new[] { new NestingProfile { Name = "Importado" } });

                    Find<TextBox>("ProfileName").Text = "Novo";
                    Click(Find<Button>("SaveProfileButton"));
                    Assert.Equal(new[] { "Importado", "Novo" }, NestingProfileStore.Load().Profiles.Select(p => p.Name));
                    Assert.Equal(new[] { "Importado", "Novo" }, Find<WrapPanel>("ProfileList").Children.Cast<Button>().Select(b => (string)b.Content));
                }
                finally { window.Close(); }
            });
        }

        [Fact]
        public void Window_WithAnUnreadableFile_ShowsTheNotice_AndNeverWritesOverIt()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_file, "{ quebrado");
            RunWindow(() =>
            {
                var window = new NestingWindow(new NestingSelection(new[] { new NestItem(0, "Frente", 500, 700) }, 2100, 2970));
                try
                {
                    T Find<T>(string name) => (T)window.FindName(name);
                    Assert.StartsWith("Não foi possível ler os perfis (", Find<TextBlock>("ProfileMessage").Text);
                    Assert.False(Find<Button>("SaveProfileButton").IsEnabled);
                    Assert.False(Find<Button>("DeleteProfileButton").IsEnabled);
                    Assert.Equal("Prévia pronta.", Find<TextBlock>("MessageText").Text); // a janela funciona
                }
                finally { window.Close(); }
            });
            Assert.Equal("{ quebrado", File.ReadAllText(_file));
        }

        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        private static void RunWindow(Action body)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { body(); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "A janela não terminou.");
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
#endif
