using System.Collections.Generic;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public sealed class StandardSizeServiceTests
    {
        [Fact]
        public void HasStandardSize_TrueForCompleteMarking()
        {
            var shape = ShapeWithStandardSize(10);
            var service = new StandardSizeService();

            Assert.True(service.HasStandardSize(shape));
        }

        [Fact]
        public void HasStandardSize_AlsoTrueForIncompleteLegacyMarking()
        {
            // Mudança deliberada: uma marcação antiga (só ID 0) CONTA como "marcada" pra
            // HasStandardSize/Toggle — ver Inspect para o estado completo (Complete vs
            // Incomplete), que é quem de fato distingue os dois casos pra migração.
            var shape = ShapeWithStandardSize(10);
            shape.DeleteProperty("KEEP_DESIGN_SIZE", 1);

            var service = new StandardSizeService();
            Assert.True(service.HasStandardSize(shape));
            Assert.Equal(StandardSizeMarkState.Incomplete, service.Inspect(shape));
        }

        [Fact]
        public void SetStandardSize_WidthWriteFails_RollsBackEnabledFlagInsteadOfLeavingPartialMarking()
        {
            // Se a escrita da largura (ID 1) falhar depois do flag (ID 0) já ter sido
            // gravado, o flag é desfeito — nunca fica uma marcação parcial (só o flag, sem
            // dimensão) como efeito colateral de uma falha. A exceção original ainda precisa
            // subir, pra quem chama nunca anunciar sucesso.
            var shape = new FakeShape { Width = 10, Height = 20, ThrowOnSetPropertyId = StandardSizeService.WidthId };
            var service = new StandardSizeService();

            Assert.Throws<System.InvalidOperationException>(() => service.SetStandardSize(shape, Identity));
            Assert.Equal(StandardSizeMarkState.Absent, service.Inspect(shape));
        }

        [Fact]
        public void SetStandardSize_HeightWriteFails_RollsBackFlagAndWidthInsteadOfLeavingPartialMarking()
        {
            // Mesma garantia, mas no TERCEIRO write (altura, ID 2) falhando depois dos outros
            // DOIS já aplicados (flag + largura) — ambos precisam ser desfeitos, não só o
            // mais recente.
            var shape = new FakeShape { Width = 10, Height = 20, ThrowOnSetPropertyId = StandardSizeService.HeightId };
            var service = new StandardSizeService();

            Assert.Throws<System.InvalidOperationException>(() => service.SetStandardSize(shape, Identity));
            Assert.Equal(StandardSizeMarkState.Absent, service.Inspect(shape));
            Assert.False(shape.HasProperty("KEEP_DESIGN_SIZE", 0));
            Assert.False(shape.HasProperty("KEEP_DESIGN_SIZE", 1));
        }

        [Fact]
        public void SetStandardSize_CreatesTrueAndCurrentWidthAndHeight()
        {
            var shape = new FakeShape { Width = 10, Height = 20 };
            var service = new StandardSizeService();

            var result = service.SetStandardSize(shape, Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(true, shape.GetProperty("KEEP_DESIGN_SIZE", 0));
            Assert.Equal(10d, shape.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(20d, shape.GetProperty("KEEP_DESIGN_SIZE", 2));
        }

        [Fact]
        public void SetStandardSize_ConvertsFromInchesViaSuppliedConversion_ReproducesRealBugEvidence()
        {
            // Reprodução exata da evidência real: o MESMO objeto (7,77244 x 8,33333 cm)
            // marcado pelo SISBolt gravou 7,77244/8,33333 (já em cm); uma versão anterior do
            // PX, sem converter, gravava os valores brutos de Shape.Width/Height em
            // POLEGADAS (3,0600157480315 / 3,28083858267717 — exatamente os valores em cm
            // divididos por 2,54). ICorelShape.Width/Height SEMPRE retornam em polegadas (ver
            // comentário da classe) — por isso o fake aqui usa os valores em polegadas, igual
            // ao Corel real. O serviço nunca decide COMO converter — só aplica o delegate que
            // recebe (ver ICorelDocument.ConvertFromInches, implementado com Document.ToUnits
            // real no host — aqui simulado com a conta exata polegada→cm).
            var shape = new FakeShape { Width = 3.0600157480315, Height = 3.28083858267717 };
            var service = new StandardSizeService();

            var result = service.SetStandardSize(shape, inches => inches * 2.54);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(7.77244, (double)shape.GetProperty("KEEP_DESIGN_SIZE", 1)!, precision: 4);
            Assert.Equal(8.33333, (double)shape.GetProperty("KEEP_DESIGN_SIZE", 2)!, precision: 4);
        }

        [Fact]
        public void SetStandardSize_InchDocument_StoresRawValueUnconverted()
        {
            // Unidade do documento É polegada — a conversão é identidade, sem fator nenhum.
            var shape = new FakeShape { Width = 5, Height = 7 };
            var service = new StandardSizeService();

            service.SetStandardSize(shape, Identity);

            Assert.Equal(5d, shape.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(7d, shape.GetProperty("KEEP_DESIGN_SIZE", 2));
        }

        [Fact]
        public void SetStandardSize_UnrecognizedDocumentUnit_FailsWithoutWritingAnything()
        {
            // Mesmo padrão já estabelecido em MeasurementSumService: unidade não reconhecida
            // (null) nunca grava um valor errado silenciosamente — falha com mensagem clara.
            var shape = new FakeShape { Width = 10, Height = 20 };
            var service = new StandardSizeService();

            var result = service.SetStandardSize(shape, null);

            Assert.False(result.IsSuccess);
            Assert.Equal(StandardSizeMarkState.Absent, service.Inspect(shape));
        }

        [Fact]
        public void UpdateSelection_UnrecognizedDocumentUnit_FailsWithoutTouchingAnyShape()
        {
            var shape = ShapeWithStandardSize(10);
            var service = new StandardSizeService();

            var result = service.UpdateSelection(SelectionOf(shape), null);

            Assert.False(result.IsSuccess);
            // Nenhuma tentativa de escrita aconteceu — o valor original continua lá.
            Assert.Equal(10d, shape.GetProperty("KEEP_DESIGN_SIZE", 1));
        }

        [Fact]
        public void Toggle_ActivatingWithUnrecognizedDocumentUnit_FailsWithoutWritingAnything()
        {
            var shape = new FakeShape { Width = 10, Height = 20 };
            var service = new StandardSizeService();

            var result = service.Toggle(SelectionOf(shape), null);

            Assert.False(result.IsSuccess);
            Assert.Equal(StandardSizeMarkState.Absent, service.Inspect(shape));
        }

        [Fact]
        public void Toggle_DeactivatingWithUnrecognizedDocumentUnit_StillSucceeds()
        {
            // Desativar nunca escreve um valor novo (só remove) — não precisa da unidade do
            // documento, então uma unidade não reconhecida não deveria bloquear a remoção.
            var shape = ShapeWithStandardSize(10);
            var service = new StandardSizeService();

            var result = service.Toggle(SelectionOf(shape), null);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(StandardSizeMarkState.Absent, service.Inspect(shape));
        }

        [Fact]
        public void UpdateStandardSize_WritesNewWidthAndHeightWithoutChangingGeometry()
        {
            var shape = ShapeWithStandardSize(10);
            shape.Width = 12;
            shape.Height = 24;
            var service = new StandardSizeService();

            var result = service.UpdateStandardSize(shape, Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(true, shape.GetProperty("KEEP_DESIGN_SIZE", 0));
            Assert.Equal(12d, shape.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(24d, shape.GetProperty("KEEP_DESIGN_SIZE", 2));
            Assert.Equal(12d, shape.Width);
            Assert.Equal(24d, shape.Height);
        }

        [Fact]
        public void RemoveStandardSize_DeletesAllThreeEntriesPhysically()
        {
            var shape = ShapeWithStandardSize(10);
            var service = new StandardSizeService();

            var result = service.RemoveStandardSize(shape);

            Assert.True(result.IsSuccess, result.Message);
            Assert.False(shape.HasProperty("KEEP_DESIGN_SIZE", 0));
            Assert.False(shape.HasProperty("KEEP_DESIGN_SIZE", 1));
            Assert.False(shape.HasProperty("KEEP_DESIGN_SIZE", 2));
        }

        [Fact]
        public void Operations_PreserveUnrelatedPropertiesIncludingEmptyName()
        {
            var shape = new FakeShape { Width = 10, Height = 20 };
            shape.SetProperty("", 0, "preserve");
            shape.SetProperty("OTHER_PROPERTY", 7, 42d);
            var service = new StandardSizeService();

            service.SetStandardSize(shape, Identity);
            service.UpdateStandardSize(shape, Identity);
            service.RemoveStandardSize(shape);

            Assert.Equal("preserve", shape.GetProperty("", 0));
            Assert.Equal(42d, shape.GetProperty("OTHER_PROPERTY", 7));
        }

        [Fact]
        public void Toggle_Deactivate_RemovesOnlyStandardSizeEntries_PreservesOtherSISBoltMarkings()
        {
            // Nomes ilustrativos de outras marcações SISBolt citadas na tarefa (alinhamento,
            // objeto base, mão livre, esticar) — não são os nomes reais confirmados (nenhum
            // relatório de referência os documenta nesta entrega), só representam "qualquer
            // outra propriedade do objeto" pra provar que Alternar (desativar) nunca as toca.
            // Cobre especificamente o caminho de CorelShapeAdapter.DeleteProperties: a lógica
            // aqui testada com FakeShape é a MESMA chamada por StandardSizeService — mas o
            // comportamento real da enumeração COM (Description/Delete por nome+ID) só é
            // confirmável no Corel real (ver roteiro em docs/ARCHITECTURE.md, seção 25).
            var shape = ShapeWithStandardSize(10);
            shape.SetProperty("ALIGNMENT_PROPS", 0, true);
            shape.SetProperty("BASE_OBJECT", 3, 99d);
            shape.SetProperty("FREEHAND_PROPS", 1, "livre");
            shape.SetProperty("STRETCH_PROPS", 2, 1.5d);
            var service = new StandardSizeService();

            var result = service.Toggle(SelectionOf(shape), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.False(shape.HasProperty("KEEP_DESIGN_SIZE", 0));
            Assert.False(shape.HasProperty("KEEP_DESIGN_SIZE", 1));
            Assert.False(shape.HasProperty("KEEP_DESIGN_SIZE", 2));
            Assert.Equal(true, shape.GetProperty("ALIGNMENT_PROPS", 0));
            Assert.Equal(99d, shape.GetProperty("BASE_OBJECT", 3));
            Assert.Equal("livre", shape.GetProperty("FREEHAND_PROPS", 1));
            Assert.Equal(1.5d, shape.GetProperty("STRETCH_PROPS", 2));
        }

        [Fact]
        public void Toggle_MigrationOfLegacyId0Marking_PreservesOtherSISBoltMarkings()
        {
            // Mesma preservação, mas no caminho de MIGRAÇÃO (marcação antiga ID 0 só, numa
            // seleção mista que ativa/migra em vez de desativar) — seção 2 e 5 da tarefa.
            var legacy = new FakeShape { Width = 10, Height = 20 };
            legacy.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            legacy.SetProperty("ALIGNMENT_PROPS", 0, true);
            legacy.SetProperty("BASE_OBJECT", 3, 99d);
            var absent = new FakeShape { Width = 30, Height = 40 };
            var service = new StandardSizeService();

            var result = service.Toggle(SelectionOf(legacy, absent), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(StandardSizeMarkState.Complete, service.Inspect(legacy));
            Assert.Equal(true, legacy.GetProperty("ALIGNMENT_PROPS", 0));
            Assert.Equal(99d, legacy.GetProperty("BASE_OBJECT", 3));
        }

        [Fact]
        public void Toggle_MixedSelection_AddsOrUpdatesEverySelectedShape()
        {
            var existing = ShapeWithStandardSize(10);
            existing.Width = 12;
            var missing = new FakeShape { Width = 20, Height = 30 };
            var selection = SelectionOf(existing, missing);
            var service = new StandardSizeService();

            var result = service.Toggle(selection, Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Tamanho Padrão aplicado/atualizado em 2 objetos.", result.Message);
            Assert.Equal(12d, existing.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(20d, missing.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(missing.Height, missing.GetProperty("KEEP_DESIGN_SIZE", 2));
        }

        [Fact]
        public void Toggle_AllMarked_RemovesEverySelectedShape()
        {
            var first = ShapeWithStandardSize(10);
            var second = ShapeWithStandardSize(15);
            var service = new StandardSizeService();

            var result = service.Toggle(SelectionOf(first, second), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Tamanho Padrão removido de 2 objetos.", result.Message);
            Assert.False(service.HasStandardSize(first));
            Assert.False(service.HasStandardSize(second));
        }

        [Fact]
        public void UpdateSelection_UpdatesMultipleObjectsAndNeverRemoves()
        {
            var existing = ShapeWithStandardSize(10);
            existing.Width = 11;
            var missing = new FakeShape { Width = 22, Height = 33 };
            var service = new StandardSizeService();

            var result = service.UpdateSelection(SelectionOf(existing, missing), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("Tamanho Padrão aplicado/atualizado em 2 objetos.", result.Message);
            Assert.True(service.HasStandardSize(existing));
            Assert.True(service.HasStandardSize(missing));
            Assert.Equal(11d, existing.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(22d, missing.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(33d, missing.GetProperty("KEEP_DESIGN_SIZE", 2));
        }

        [Fact]
        public void SelectionOperations_WithNoObjects_ReturnsRequiredMessage()
        {
            var service = new StandardSizeService();

            var toggle = service.Toggle(new FakeSelection(), Identity);
            var update = service.UpdateSelection(new FakeSelection(), Identity);

            Assert.Equal(OperationStatus.InvalidSelection, toggle.Status);
            Assert.Equal("Selecione um ou mais objetos.", toggle.Message);
            Assert.Equal(OperationStatus.InvalidSelection, update.Status);
            Assert.Equal("Selecione um ou mais objetos.", update.Message);
        }

        [Fact]
        public void SelectionOperations_DoNotRecurseIntoSelectedGroups()
        {
            var group = new FakeShape { Width = 50, Height = 60, Kind = PXToolBox.Outputs.Abstractions.ShapeKind.Group };
            var child = new FakeShape { Width = 5, Height = 6 };
            group.ShapesList.Add(child);
            var service = new StandardSizeService();

            var result = service.UpdateSelection(SelectionOf(group), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.True(service.HasStandardSize(group));
            Assert.False(service.HasStandardSize(child));
        }

        [Fact]
        public void SelectionOperations_DoNotRecurseIntoPowerClipContents()
        {
            // PowerClipShapes é uma coleção DIFERENTE de Shapes (ver ICorelShape) — marcar o
            // contêiner do PowerClip nunca deve tocar o conteúdo recortado dentro dele, igual
            // a como grupos comuns não recursam nos filhos.
            var container = new FakeShape { Width = 50, Height = 60, HasPowerClipContents = true };
            var clippedContent = new FakeShape { Width = 5, Height = 6 };
            container.PowerClipShapesList.Add(clippedContent);
            var service = new StandardSizeService();

            var result = service.UpdateSelection(SelectionOf(container), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.True(service.HasStandardSize(container));
            Assert.False(service.HasStandardSize(clippedContent));
        }

        [Fact]
        public void SelectionOf_MultipleShapes_IsNeverConfusedWithASingleGroupSelection()
        {
            // Uma seleção com VÁRIOS objetos (ex.: dois retângulos soltos) não é um grupo —
            // cada Shape da seleção é avaliado/marcado isoladamente, nunca tratado como se
            // fosse "um grupo com dois filhos".
            var first = new FakeShape { Width = 10, Height = 10, Kind = PXToolBox.Outputs.Abstractions.ShapeKind.Rectangle };
            var second = new FakeShape { Width = 20, Height = 20, Kind = PXToolBox.Outputs.Abstractions.ShapeKind.Rectangle };
            var service = new StandardSizeService();

            var result = service.UpdateSelection(SelectionOf(first, second), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.True(service.HasStandardSize(first));
            Assert.True(service.HasStandardSize(second));
            Assert.Equal(10d, first.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(20d, second.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(10d, first.GetProperty("KEEP_DESIGN_SIZE", 2));
            Assert.Equal(20d, second.GetProperty("KEEP_DESIGN_SIZE", 2));
        }

        [Fact]
        public void Inspect_AbsentWhenNoPropertyPresent()
        {
            var shape = new FakeShape { Width = 10, Height = 20 };
            var service = new StandardSizeService();

            Assert.Equal(StandardSizeMarkState.Absent, service.Inspect(shape));
        }

        [Fact]
        public void Inspect_CompleteWhenAllRequiredPropertiesPresent()
        {
            var shape = ShapeWithStandardSize(10);
            var service = new StandardSizeService();

            Assert.Equal(StandardSizeMarkState.Complete, service.Inspect(shape));
        }

        [Fact]
        public void Inspect_IncompleteForLegacyMarkingWithOnlyTheEnabledFlag()
        {
            // A marcação antiga do PX (ID 0 só) — nunca Absent (perderia a necessidade de
            // migração) nem Complete (fingiria um estado que não existe).
            var shape = new FakeShape { Width = 10, Height = 20 };
            shape.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            var service = new StandardSizeService();

            Assert.Equal(StandardSizeMarkState.Incomplete, service.Inspect(shape));
        }

        [Fact]
        public void Inspect_UnknownWhenPropertyReadFails_NeverAbsent()
        {
            var shape = new FakeShape { Width = 10, Height = 20, ThrowOnPropertyAccess = true };
            var service = new StandardSizeService();

            Assert.Equal(StandardSizeMarkState.Unknown, service.Inspect(shape));
        }

        [Fact]
        public void InspectSelection_NoSelection_IsUnavailable()
        {
            var service = new StandardSizeService();

            Assert.Equal(StandardSizeSelectionState.Unavailable, service.InspectSelection(null));
            Assert.Equal(StandardSizeSelectionState.Unavailable, service.InspectSelection(new FakeSelection()));
        }

        [Fact]
        public void InspectSelection_AllMarked_WhenEveryShapeComplete()
        {
            var service = new StandardSizeService();
            var selection = SelectionOf(ShapeWithStandardSize(10), ShapeWithStandardSize(15));

            Assert.Equal(StandardSizeSelectionState.AllMarked, service.InspectSelection(selection));
        }

        [Fact]
        public void InspectSelection_NoneMarked_WhenNoShapeHasAnyEntry()
        {
            var service = new StandardSizeService();
            var selection = SelectionOf(new FakeShape { Width = 10 }, new FakeShape { Width = 20 });

            Assert.Equal(StandardSizeSelectionState.NoneMarked, service.InspectSelection(selection));
        }

        [Fact]
        public void InspectSelection_Mixed_WhenSomeMarkedAndSomeNot()
        {
            var service = new StandardSizeService();
            var selection = SelectionOf(ShapeWithStandardSize(10), new FakeShape { Width = 20 });

            Assert.Equal(StandardSizeSelectionState.Mixed, service.InspectSelection(selection));
        }

        [Fact]
        public void InspectSelection_IncompleteCountsAsMarked_ForAggregation()
        {
            var legacy = new FakeShape { Width = 10 };
            legacy.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            var service = new StandardSizeService();

            Assert.Equal(StandardSizeSelectionState.AllMarked, service.InspectSelection(SelectionOf(legacy)));
        }

        [Fact]
        public void InspectSelection_Unknown_WhenAnyReadFails_NeverSilentlyResolved()
        {
            var ok = ShapeWithStandardSize(10);
            var broken = new FakeShape { Width = 20, ThrowOnPropertyAccess = true };
            var service = new StandardSizeService();

            Assert.Equal(StandardSizeSelectionState.Unknown, service.InspectSelection(SelectionOf(ok, broken)));
        }

        [Fact]
        public void Toggle_AllIncompleteSelection_RemovesTheStaleMarkingEntirely()
        {
            // Uma seleção onde TUDO já tem alguma marcação (mesmo que antiga/incompleta) é
            // "toda marcada" pro propósito do Alternar — desativa tudo, removendo por completo
            // a entrada antiga (nunca deixa resíduo). O usuário pode clicar de novo para
            // marcar com o contrato completo, igual a qualquer objeto nunca marcado.
            var legacy = new FakeShape { Width = 10, Height = 20 };
            legacy.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            var service = new StandardSizeService();

            var result = service.Toggle(SelectionOf(legacy), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(StandardSizeMarkState.Absent, service.Inspect(legacy));
        }

        [Fact]
        public void Toggle_MixedSelectionWithLegacyIncompleteMarking_MigratesItToCompleteSet()
        {
            // Seleção mista (um Absent + um Incomplete) ativa tudo — e, ao ativar, o
            // Incomplete é migrado (entrada antiga removida antes de escrever o conjunto
            // completo), nunca deixando a entrada velha convivendo com a nova.
            var legacy = new FakeShape { Width = 10, Height = 20 };
            legacy.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            var absent = new FakeShape { Width = 30, Height = 40 };
            var service = new StandardSizeService();

            var result = service.Toggle(SelectionOf(legacy, absent), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(StandardSizeMarkState.Complete, service.Inspect(legacy));
            Assert.Equal(StandardSizeMarkState.Complete, service.Inspect(absent));
            Assert.Equal(10d, legacy.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(20d, legacy.GetProperty("KEEP_DESIGN_SIZE", 2));
        }

        [Fact]
        public void UpdateSelection_LegacyIncompleteMarking_MigratesToCompleteSet()
        {
            var legacy = new FakeShape { Width = 15, Height = 25 };
            legacy.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            var service = new StandardSizeService();

            var result = service.UpdateSelection(SelectionOf(legacy), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(StandardSizeMarkState.Complete, service.Inspect(legacy));
            Assert.Equal(15d, legacy.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(25d, legacy.GetProperty("KEEP_DESIGN_SIZE", 2));
        }

        [Fact]
        public void Inspect_IncompleteWhenHeightMissing_EvenWithFlagAndWidthPresent()
        {
            // Marcação produzida por uma versão intermediária desta classe nesta mesma sessão
            // (só ID 0 + ID 1, sem altura — o contrato completo só foi confirmado depois) —
            // mesma garantia de migração que a marcação antiga ID-0-só.
            var shape = new FakeShape { Width = 10, Height = 20 };
            shape.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            shape.SetProperty("KEEP_DESIGN_SIZE", 1, 10d);
            var service = new StandardSizeService();

            Assert.Equal(StandardSizeMarkState.Incomplete, service.Inspect(shape));
        }

        [Fact]
        public void UpdateSelection_MarkingMissingOnlyHeight_MigratesToCompleteSet()
        {
            var shape = new FakeShape { Width = 10, Height = 20 };
            shape.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            shape.SetProperty("KEEP_DESIGN_SIZE", 1, 10d);
            var service = new StandardSizeService();

            var result = service.UpdateSelection(SelectionOf(shape), Identity);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(StandardSizeMarkState.Complete, service.Inspect(shape));
            Assert.Equal(20d, shape.GetProperty("KEEP_DESIGN_SIZE", 2));
        }

        /// <summary>Conversão nula — representa um documento cuja unidade "atual" já é a
        /// mesma de ICorelShape.Width/Height (polegada), usada na maioria dos testes porque o
        /// que eles verificam é independente de qual conversão é aplicada.</summary>
        private static readonly System.Func<double, double> Identity = inches => inches;

        private static FakeShape ShapeWithStandardSize(double width)
        {
            var shape = new FakeShape { Width = width, Height = width * 2 };
            shape.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            shape.SetProperty("KEEP_DESIGN_SIZE", 1, width);
            shape.SetProperty("KEEP_DESIGN_SIZE", 2, width * 2);
            return shape;
        }

        private static FakeSelection SelectionOf(params FakeShape[] shapes)
        {
            var selection = new FakeSelection();
            foreach (var shape in shapes)
            {
                selection.ShapesList.Add(shape);
            }
            return selection;
        }
    }
}
