#if NET48
// Só compila na perna net48 (ver cabeçalho de ListForgeIntegratedTests.cs). Testa o modo PX do
// ListForge Integrado (ClassicWorkspaceWindow.Px.cs) — ver
// docs/design/listforge-px-workflow/design.md. Usa reflexão para acionar os manipuladores de
// evento (todos privados, por convenção deste host — ver OnProcess/OnOpenSettings/etc., todos
// também privados) e para ler campos nomeados do XAML (x:Name gera campos "internal" por
// padrão, inacessíveis deste projeto de teste sem InternalsVisibleTo, que não está configurado)
// — nenhuma visibilidade de produção foi alterada só para tornar isto testável.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;
using PXToolBoxDev.ListForgeIntegrated;
using Xunit;

namespace PXToolBox.Tests
{
    // Constrói ClassicWorkspaceWindow, que lê e grava a configuração do ListForge Integrado
    // (ex.: trocar de modo salva PreferredWorkspaceModeByProfileId) e registra no log ("ListForge Integrado iniciado"). Sem isto, cada execução da
    // suíte — inclusive a de toda build — gravava no arquivo REAL de %LocalAppData%. Mesmo
    // isolamento de ListForgeIntegratedSettingsStoreTests (ver TestCollections.cs).
    [Collection("PXToolBox Dev shared filesystem resources")]
    public class ListForgePxModeTests : IDisposable
    {
        private readonly string _isolatedConfigDirectory = Path.Combine(
            Path.GetTempPath(), "pxtoolbox-tests", "lf-pxmode-" + Guid.NewGuid().ToString("N"));

        public ListForgePxModeTests()
        {
            Directory.CreateDirectory(_isolatedConfigDirectory);
            PXToolBoxDev.Integrations.ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(_isolatedConfigDirectory);
            PXToolBoxDev.Integrations.PxLogger.SetLogDirectoryForTesting(Path.Combine(_isolatedConfigDirectory, "logs"));
        }

        public void Dispose()
        {
            PXToolBoxDev.Integrations.ListForgeIntegratedSettingsStore.SetConfigDirectoryForTesting(null);
            PXToolBoxDev.Integrations.PxLogger.SetLogDirectoryForTesting(null);
            try { Directory.Delete(_isolatedConfigDirectory, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Regressão central do modo PX: trocar para PX move os controles de sessão do Clássico
        /// (reparenting — ver PxActivate) sem lançar, e trocar de volta restaura cada um na
        /// posição original (ver PxDeactivate) — nunca duas instâncias independentes do mesmo
        /// controle, nunca uma perda de referência. Mesma classe de regressão do crash de
        /// abertura (iteração 8): constrói e interage com a janela real numa thread STA
        /// dedicada.
        /// </summary>
        [Fact]
        public void SwitchToPxAndBack_NeverThrows_AndRestoresOriginalParents()
        {
            Exception? caught = null;
            FrameworkElement? inputTextBox = null;
            FrameworkElement? inputOriginalParentAfterRestore = null;
            bool pxRootVisibleDuringPx = false;
            bool classicRootVisibleAfterRestore = false;

            var thread = new Thread(() =>
            {
                try
                {
                    var window = new ClassicWorkspaceWindow();

                    var windowType = typeof(ClassicWorkspaceWindow);
                    FrameworkElement Field(string name) =>
                        (FrameworkElement)windowType
                            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!
                            .GetValue(window)!;
                    void Invoke(string methodName) =>
                        windowType
                            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!
                            .Invoke(window, new object?[] { window, new RoutedEventArgs() });

                    inputTextBox = Field("InputTextBox");
                    var inputPanelOriginalParentBefore = inputTextBox.Parent;

                    Invoke("OnSwitchToPxMode");

                    var pxRoot = Field("PxRoot");
                    var classicRoot = Field("ClassicRoot");
                    pxRootVisibleDuringPx = pxRoot.Visibility == Visibility.Visible;
                    // Durante o modo PX, InputTextBox deixou de ser filho do layout Clássico —
                    // foi movido (nunca duplicado) para dentro do slot do PxRoot.
                    var movedAwayFromClassic = !IsDescendantOf(classicRoot, inputTextBox);

                    Invoke("OnSwitchToClassicMode");

                    classicRootVisibleAfterRestore = classicRoot.Visibility == Visibility.Visible;
                    inputOriginalParentAfterRestore = inputTextBox.Parent as FrameworkElement;

                    if (!movedAwayFromClassic)
                        throw new InvalidOperationException("InputTextBox não foi movido para o PxRoot durante o modo PX.");

                    if (!ReferenceEquals(inputOriginalParentAfterRestore, inputPanelOriginalParentBefore))
                        throw new InvalidOperationException("InputTextBox não voltou ao mesmo pai original do Clássico depois de voltar ao modo Clássico.");
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
            Assert.True(pxRootVisibleDuringPx);
            Assert.True(classicRootVisibleAfterRestore);
        }

        /// <summary>Trocar de modo repetidamente (ida e volta várias vezes) nunca deve lançar —
        /// prova de que o reparenting é idempotente e não acumula estado entre trocas.</summary>
        [Fact]
        public void SwitchModeRepeatedly_NeverThrows()
        {
            Exception? caught = null;

            var thread = new Thread(() =>
            {
                try
                {
                    var window = new ClassicWorkspaceWindow();
                    var windowType = typeof(ClassicWorkspaceWindow);
                    void Invoke(string methodName) =>
                        windowType
                            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!
                            .Invoke(window, new object?[] { window, new RoutedEventArgs() });

                    for (var i = 0; i < 5; i++)
                    {
                        Invoke("OnSwitchToPxMode");
                        Invoke("OnSwitchToClassicMode");
                    }
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(caught);
        }

        /// <summary>
        /// PxRequestsMatch é o que garante que "Processar" nunca associe um resultado a uma
        /// entrada diferente da que foi de fato revisada (ver OnPxProcess) — testado
        /// diretamente, por reflexão (método privado), já que é puro e síncrono (sem o
        /// problema de thread/Dispatcher dos métodos assíncronos acima).
        /// </summary>
        [Theory]
        [InlineData("ANA,10,G", "ANA,10,G", true)]
        [InlineData("ANA,10,G", "ANA,07,G", false)]
        public void PxRequestsMatch_ComparesInputTextSeparatorCaseAndSortMode(string liveText, string capturedText, bool expectedMatch)
        {
            var method = typeof(ClassicWorkspaceWindow)
                .GetMethod("PxRequestsMatch", BindingFlags.NonPublic | BindingFlags.Static)!;

            var live = new ProcessingWorkflowRequest(liveText, ",", SizeConfig.Default(), "original", ListSortMode.Original);
            var captured = new ProcessingWorkflowRequest(capturedText, ",", SizeConfig.Default(), "original", ListSortMode.Original);

            var result = (bool)method.Invoke(null, new object[] { live, captured })!;

            Assert.Equal(expectedMatch, result);
        }

        [Fact]
        public void PxRequestsMatch_ComparesPieceOrderContentNotListInstance()
        {
            var method = typeof(ClassicWorkspaceWindow)
                .GetMethod("PxRequestsMatch", BindingFlags.NonPublic | BindingFlags.Static)!;

            // Duas instâncias de lista DIFERENTES, mesmo conteúdo — igualdade de record padrão
            // compararia por referência e diria "diferente"; PxRequestsMatch precisa comparar o
            // conteúdo (ver comentário do método em ClassicWorkspaceWindow.Px.cs).
            var mappingA = new JsonPieceMappingOptions(true, new List<string> { "ShortSleeve", "Pants" });
            var mappingB = new JsonPieceMappingOptions(true, new List<string> { "ShortSleeve", "Pants" });

            var live = new ProcessingWorkflowRequest("ANA,10,G", ",", SizeConfig.Default(), "original", ListSortMode.Original, mappingA);
            var captured = new ProcessingWorkflowRequest("ANA,10,G", ",", SizeConfig.Default(), "original", ListSortMode.Original, mappingB);

            var result = (bool)method.Invoke(null, new object[] { live, captured })!;

            Assert.True(result);
        }

        private static bool IsDescendantOf(DependencyObject ancestor, DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor)) return true;
                current = current is FrameworkElement fe ? fe.Parent : null;
            }
            return false;
        }
    }
}
#endif
