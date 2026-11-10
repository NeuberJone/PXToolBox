using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class LayerVisibilityServiceTests
    {
        [Fact]
        public void SetVisibility_OnlyAffectsLayersOfThatCategory()
        {
            var page = new FakePage();
            var fe1 = (FakeLayer)page.CreateLayer("CAM1.PP.FE");
            var fe2 = (FakeLayer)page.CreateLayer("CAM1.GG.FE");
            var ma1 = (FakeLayer)page.CreateLayer("CAM1.PP.MA");

            var service = new LayerVisibilityService();
            var result = service.SetVisibility(page, LayerCategory.FE, visible: false);

            Assert.Equal(2, result.Value);
            Assert.False(fe1.Visible);
            Assert.False(fe2.Visible);
            Assert.True(ma1.Visible); // não afetada
        }

        [Fact]
        public void Toggle_ShowsAll_WhenAnyIsHidden()
        {
            var page = new FakePage();
            var c1 = (FakeLayer)page.CreateLayer("CAM1.PP.C");
            var c2 = (FakeLayer)page.CreateLayer("CAM1.GG.C");
            c1.Visible = false;
            c2.Visible = true;

            var service = new LayerVisibilityService();
            var result = service.Toggle(page, LayerCategory.C);

            Assert.True(result.IsSuccess);
            Assert.True(result.Value.NewVisibility);
            Assert.True(c1.Visible);
            Assert.True(c2.Visible);
        }

        [Fact]
        public void Toggle_HidesAll_WhenAllAreVisible()
        {
            var page = new FakePage();
            var c1 = (FakeLayer)page.CreateLayer("CAM1.PP.C");
            c1.Visible = true;

            var service = new LayerVisibilityService();
            var result = service.Toggle(page, LayerCategory.C);

            Assert.False(result.Value.NewVisibility);
            Assert.False(c1.Visible);
        }

        [Fact]
        public void Toggle_NoMatchingLayers_ReturnsInconclusive()
        {
            var page = new FakePage();
            var service = new LayerVisibilityService();

            var result = service.Toggle(page, LayerCategory.MA);

            Assert.False(result.IsSuccess);
        }

        private static FakeSelection Select(params FakeShape[] shapes)
        {
            var selection = new FakeSelection();
            selection.ShapesList.AddRange(shapes);
            return selection;
        }

        private static FakeLayer AddLayer(FakeDocument document, string name) =>
            (FakeLayer)document.ActivePageFake.CreateLayer(name);

        private static OperationResult Hide(LayerVisibilityService service, FakeDocument doc, params FakeShape[] shapes) =>
            service.HideLayersOfSelection(doc, Select(shapes), new[] { doc });

        [Fact]
        public void HideLayersOfSelection_HidesOnlyLayersContainingSelectedObjects_EachOnce()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var ma = AddLayer(doc, "CAM1.PP.MA");
            var untouched = AddLayer(doc, "CAM1.PP.C");
            untouched.AddShape(new FakeShape { Name = "nao-selecionado" });

            var service = new LayerVisibilityService();
            var result = Hide(service, doc,
                fe.AddShape(new FakeShape { Name = "a" }),
                fe.AddShape(new FakeShape { Name = "b" }),
                ma.AddShape(new FakeShape { Name = "c" }));

            Assert.True(result.IsSuccess);
            Assert.False(fe.Visible);
            Assert.False(ma.Visible);
            Assert.True(untouched.Visible);
            Assert.StartsWith("2 camada(s)", result.Message);
            Assert.Equal(2, service.LastHidden!.Entries.Count);
        }

        [Fact]
        public void HideLayersOfSelection_NeverHidesSpecialLayers()
        {
            var doc = new FakeDocument();
            var guides = AddLayer(doc, "Guias");
            guides.IsSpecialLayer = true;
            var normal = AddLayer(doc, "CAM1.PP.FE");

            var result = Hide(new LayerVisibilityService(), doc, guides.AddShape(new FakeShape()), normal.AddShape(new FakeShape()));

            Assert.True(result.IsSuccess);
            Assert.True(guides.Visible);
            Assert.False(normal.Visible);
            Assert.Contains("especial", result.Message);
        }

        [Fact]
        public void HideLayersOfSelection_OnlySpecialLayers_FailsWithoutChangingAnything()
        {
            var doc = new FakeDocument();
            var guides = AddLayer(doc, "Guias");
            guides.IsSpecialLayer = true;

            var service = new LayerVisibilityService();
            var result = Hide(service, doc, guides.AddShape(new FakeShape()));

            Assert.False(result.IsSuccess);
            Assert.True(guides.Visible);
            Assert.Null(service.LastHidden);
        }

        [Fact]
        public void HideLayersOfSelection_EmptySelection_FailsAsInvalidSelection()
        {
            var result = Hide(new LayerVisibilityService(), new FakeDocument());

            Assert.False(result.IsSuccess);
            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
        }

        [Theory]
        [InlineData(typeof(ICorelLayer))]
        [InlineData(typeof(ICorelDocument))]
        public void Contract_LayerAndDocumentAbstractionsExposeNoPropertiesAccess(System.Type abstraction)
        {
            // A restauração não pode gravar nada no CDR: sem Properties nas abstrações de
            // camada/documento, nenhum serviço consegue escrever ali nem por engano.
            Assert.DoesNotContain(abstraction.GetMethods(), m => m.Name.Contains("Propert"));
        }

        [Fact]
        public void Restore_ShowsOnlyLayersHiddenByTheLastAction_NotOtherHiddenLayers()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var hiddenByUser = AddLayer(doc, "CAM1.PP.C");
            hiddenByUser.Visible = false;
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));

            var result = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.True(result.IsSuccess);
            Assert.True(fe.Visible);
            Assert.False(hiddenByUser.Visible);
            Assert.StartsWith("1 camada(s) restaurada(s): CAM1.PP.FE.", result.Message);
            Assert.Null(service.LastHidden);
        }

        [Fact]
        public void Hide_PreviouslyHiddenLayer_IsNotRecorded_AndStaysHiddenAfterRestore()
        {
            var doc = new FakeDocument();
            var visible = AddLayer(doc, "CAM1.PP.FE");
            var alreadyHidden = AddLayer(doc, "CAM1.PP.MA");
            var shapes = new[] { visible.AddShape(new FakeShape()), alreadyHidden.AddShape(new FakeShape()) };
            alreadyHidden.Visible = false;
            var service = new LayerVisibilityService();

            var hide = Hide(service, doc, shapes);
            Assert.Single(service.LastHidden!.Entries);
            service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.Contains("Já oculta(s), não registrada(s): CAM1.PP.MA", hide.Message);
            Assert.True(visible.Visible);
            Assert.False(alreadyHidden.Visible);
        }

        [Fact]
        public void AttemptsWithoutChanges_PreserveTheLastEffectiveRecord()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));
            var record = service.LastHidden;

            Hide(service, doc);
            Hide(service, doc, fe.AddShape(new FakeShape())); // camada já oculta

            Assert.Same(record, service.LastHidden);
            Assert.True(service.RestoreLastHiddenLayers(doc, new[] { doc }).IsSuccess);
            Assert.True(fe.Visible);
        }

        [Fact]
        public void Restore_WithoutRecord_Fails()
        {
            var doc = new FakeDocument();
            var result = new LayerVisibilityService().RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public void Restore_ConsumesTheRecord()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));
            service.RestoreLastHiddenLayers(doc, new[] { doc });
            fe.Visible = false;

            var second = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.False(second.IsSuccess);
            Assert.False(fe.Visible);
            Assert.Contains("já foi restaurada", service.UnavailableReason);
        }

        [Fact]
        public void ClosingTheSourceDocument_InvalidatesTheRecord()
        {
            var doc = new FakeDocument { Name = "origem.cdr" };
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));

            service.OnDocumentClosed(doc.Identity);

            Assert.Null(service.LastHidden);
            Assert.Contains("\"origem.cdr\" foi fechado", service.UnavailableReason);
        }

        [Fact]
        public void ClosingAnotherDocument_KeepsTheRecord()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));

            service.OnDocumentClosed(new FakeDocument().Identity);
            service.DiscardIfSourceClosed(new[] { doc, new FakeDocument() });

            Assert.NotNull(service.LastHidden);
        }

        [Fact]
        public void SourceMissingFromOpenDocuments_DiscardsRecord_EvenWithoutCloseEvent()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));

            service.DiscardIfSourceClosed(new[] { new FakeDocument() });

            Assert.Null(service.LastHidden);
        }

        [Fact]
        public void ReopenedFile_IsNotTreatedAsTheSource_AndNothingIsRestored()
        {
            var original = new FakeDocument { Name = "pedido.cdr", FullFileName = @"C:\x\pedido.cdr" };
            var fe = AddLayer(original, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, original, fe.AddShape(new FakeShape()));

            // Fechado e reaberto: mesmo nome, mesmo caminho, mesmas camadas — objetos novos.
            var reopened = new FakeDocument { Name = "pedido.cdr", FullFileName = @"C:\x\pedido.cdr" };
            var reopenedFe = AddLayer(reopened, "CAM1.PP.FE");
            reopenedFe.Visible = false;

            var result = service.RestoreLastHiddenLayers(reopened, new[] { reopened });

            Assert.False(result.IsSuccess);
            Assert.Contains("reabrir o arquivo não recupera", result.Message);
            Assert.False(reopenedFe.Visible);
            Assert.Null(service.LastHidden);
        }

        [Fact]
        public void Restore_SourceOpenButNotActive_RefusesAndKeepsRecord()
        {
            var source = new FakeDocument { Name = "origem.cdr" };
            var fe = AddLayer(source, "CAM1.PP.FE");
            var other = new FakeDocument { Name = "outro.cdr" };
            var otherFe = AddLayer(other, "CAM1.PP.FE");
            otherFe.Visible = false;
            var service = new LayerVisibilityService();
            Hide(service, source, fe.AddShape(new FakeShape()));

            var refused = service.RestoreLastHiddenLayers(other, new[] { source, other });

            Assert.False(refused.IsSuccess);
            Assert.Contains("Ative o documento de origem \"origem.cdr\"", refused.Message);
            Assert.False(fe.Visible);
            Assert.False(otherFe.Visible);
            Assert.NotNull(service.LastHidden);

            var restored = service.RestoreLastHiddenLayers(source, new[] { source, other });

            Assert.True(restored.IsSuccess);
            Assert.True(fe.Visible);
            Assert.False(otherFe.Visible);
        }

        [Fact]
        public void Restore_DeletedLayer_IsReportedAsNotFound_OthersRestored()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var ma = AddLayer(doc, "CAM1.PP.MA");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()), ma.AddShape(new FakeShape()));
            doc.ActivePageFake.LayersList.Remove(ma);

            var result = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.True(result.IsSuccess);
            Assert.True(fe.Visible);
            Assert.Contains("Não encontrada(s) (excluída(s) ou recriada(s) por Desfazer): CAM1.PP.MA", result.Message);
        }

        [Fact]
        public void Restore_LayerRecreatedWithSameName_IsNotRestoredByApproximation()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));
            doc.ActivePageFake.LayersList.Remove(fe);
            var recreated = AddLayer(doc, "CAM1.PP.FE"); // ex.: excluída e recriada/desfeita
            recreated.Visible = false;

            var result = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.False(result.IsSuccess);
            Assert.False(recreated.Visible);
            Assert.StartsWith("0 camada(s) restaurada(s).", result.Message);
            Assert.Null(service.LastHidden);
        }

        [Fact]
        public void Restore_FindsTheSameLayerAfterReorderRenameAndMoveToAnotherPage()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            AddLayer(doc, "CAM1.PP.MA");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));
            doc.ActivePageFake.LayersList.Remove(fe);
            var page2 = new FakePage();
            page2.CreateLayer("outra");
            page2.LayersList.Add(fe);
            doc.PagesList.Insert(0, page2);
            fe.Name = "renomeada";

            var result = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.True(result.IsSuccess);
            Assert.True(fe.Visible);
            Assert.Contains("renomeada", result.Message);
        }

        [Fact]
        public void Restore_AmbiguousLayerIdentity_InvalidatesWithoutChangingAnything()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var ma = AddLayer(doc, "CAM1.PP.MA");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()), ma.AddShape(new FakeShape()));
            var lookalike = AddLayer(doc, "outra");
            lookalike.Visible = false;
            lookalike.SharedIdentity = fe;

            var result = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.False(result.IsSuccess);
            Assert.Contains("Restauração invalidada", result.Message);
            Assert.False(fe.Visible);
            Assert.False(ma.Visible);
            Assert.False(lookalike.Visible);
            Assert.Null(service.LastHidden);
        }

        [Fact]
        public void Restore_SourceListedTwice_InvalidatesWithoutChangingAnything()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));

            var result = service.RestoreLastHiddenLayers(doc, new[] { doc, doc });

            Assert.False(result.IsSuccess);
            Assert.False(fe.Visible);
            Assert.Null(service.LastHidden);
        }

        [Fact]
        public void Hide_WithoutReliableIdentity_HidesButDoesNotRecord_AndDropsPreviousRecord()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var ma = AddLayer(doc, "CAM1.PP.MA");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));
            ma.UnstableIdentity = true;

            var result = Hide(service, doc, ma.AddShape(new FakeShape()));

            Assert.True(result.IsSuccess);
            Assert.False(ma.Visible);
            Assert.Contains("Restauração indisponível", result.Message);
            Assert.Null(service.LastHidden);
            Assert.Contains("identidade única", service.UnavailableReason);
        }

        [Fact]
        public void Hide_DocumentWithoutReliableIdentity_DoesNotRecord()
        {
            var doc = new FakeDocument { UnstableIdentity = true };
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();

            var result = Hide(service, doc, fe.AddShape(new FakeShape()));

            Assert.True(result.IsSuccess);
            Assert.Null(service.LastHidden);
        }

        [Fact]
        public void Restore_NeverShowsSpecialLayers()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));
            fe.IsSpecialLayer = true;

            var result = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.False(result.IsSuccess);
            Assert.False(fe.Visible);
            Assert.Contains("especial", result.Message);
        }

        [Fact]
        public void Restore_LayerShownManuallyMeanwhile_IsReportedAsAlreadyVisible()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var ma = AddLayer(doc, "CAM1.PP.MA");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()), ma.AddShape(new FakeShape()));
            ma.Visible = true;

            var result = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.True(result.IsSuccess);
            Assert.Contains("1 camada(s) restaurada(s): CAM1.PP.FE.", result.Message);
            Assert.Contains("Já visível(is): CAM1.PP.MA", result.Message);
        }

        [Fact]
        public void NewEffectiveHide_ReplacesTheRecord()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var ma = AddLayer(doc, "CAM1.PP.MA");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));

            Hide(service, doc, ma.AddShape(new FakeShape()));
            var result = service.RestoreLastHiddenLayers(doc, new[] { doc });

            Assert.True(result.IsSuccess);
            Assert.True(ma.Visible);
            Assert.False(fe.Visible);
        }

        [Fact]
        public void ReleaseSessionState_DropsTheRecord_WithReason()
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var service = new LayerVisibilityService();
            Hide(service, doc, fe.AddShape(new FakeShape()));

            service.ReleaseSessionState("Indisponível: painel encerrado.");

            Assert.Null(service.LastHidden);
            Assert.Equal("Indisponível: painel encerrado.", service.UnavailableReason);
            Assert.False(service.RestoreLastHiddenLayers(doc, new[] { doc }).IsSuccess);
            Assert.False(fe.Visible);
        }

        // O serviço não pode reter o registro descartado (nem, por ele, as identidades de
        // sessão): depois do descarte, só o coletor pode alcançá-lo. Simulado — não comprova o
        // ciclo de vida do RCW dentro do Corel.
        [Theory]
        [InlineData("fechar documento de origem")]
        [InlineData("nova ocultação")]
        [InlineData("encerrar painel")]
        [InlineData("restaurar")]
        public void DiscardedRecord_IsNotRetainedByTheService(string discardBy)
        {
            var doc = new FakeDocument();
            var fe = AddLayer(doc, "CAM1.PP.FE");
            var ma = AddLayer(doc, "CAM1.PP.MA");
            var service = new LayerVisibilityService();
            var oldRecord = HideAndTrackRecord(service, doc, fe.AddShape(new FakeShape()));

            switch (discardBy)
            {
                case "fechar documento de origem": service.OnDocumentClosed(doc.Identity); break;
                case "nova ocultação": Hide(service, doc, ma.AddShape(new FakeShape())); break;
                case "encerrar painel": service.ReleaseSessionState("Indisponível: painel encerrado."); break;
                case "restaurar": service.RestoreLastHiddenLayers(doc, new[] { doc }); break;
            }

            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            Assert.False(oldRecord.IsAlive);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static System.WeakReference HideAndTrackRecord(LayerVisibilityService service, FakeDocument doc, FakeShape shape)
        {
            Hide(service, doc, shape);
            return new System.WeakReference(service.LastHidden!);
        }

        [Fact]
        public void IdentityComparison_IsReferenceEqualityOnly()
        {
            var a = new object();

            Assert.True(LayerVisibilityService.IsSameSessionObject(a, a));
            Assert.False(LayerVisibilityService.IsSameSessionObject(a, new object()));
            Assert.False(LayerVisibilityService.IsSameSessionObject("x", new string('x', 1)));
            Assert.Equal(2, LayerVisibilityService.CountIdentityMatches(new[] { a, new object(), a }, a));
        }
    }
}
