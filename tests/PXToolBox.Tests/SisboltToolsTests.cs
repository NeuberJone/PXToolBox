using System;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    // Versões PX das ferramentas do SISBolt com contrato observado nas capturas antes/depois
    // (docs/sisbolt-tools-investigation.md). Só fakes — o reconhecimento pelo SISBolt só se
    // confirma no Corel.
    public class SisboltMarkingServiceTests
    {
        private readonly SisboltMarkingService _service = new SisboltMarkingService();

        private static FakeSelection Select(params ICorelShape[] shapes)
        {
            var selection = new FakeSelection();
            selection.ShapesList.AddRange(shapes);
            return selection;
        }

        public static TheoryData<string, string, ShapeKind> Markings => new TheoryData<string, string, ShapeKind>
        {
            { nameof(SisboltMarking.StretchToMold), "STRETCH_PROPS", ShapeKind.Rectangle },
            { nameof(SisboltMarking.IgnoreVariableMetrics), "IGNORE_SISBOLT_VARIABLE_METRICS", ShapeKind.Text },
            { nameof(SisboltMarking.FreeHand), "FREE_HAND_PROPS", ShapeKind.Text },
        };

        private static SisboltMarking ByName(string name) =>
            (SisboltMarking)typeof(SisboltMarking).GetProperty(name)!.GetValue(null)!;

        private static FakeShape MarkedWithEverythingElse(ShapeKind kind)
        {
            var shape = new FakeShape { Kind = kind, Name = "NO", Width = 3, Height = 1, PositionX = 2, PositionY = 5 };
            shape.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            shape.SetProperty("KEEP_DESIGN_SIZE", 1, 7.61218);
            shape.SetProperty("KEEP_DESIGN_SIZE", 2, 7.45192);
            shape.SetProperty("ALIGNMENT_PROPS", 0, 0);
            return shape;
        }

        private static void AssertEverythingElseKept(FakeShape shape)
        {
            Assert.Equal(true, shape.GetProperty("KEEP_DESIGN_SIZE", 0));
            Assert.Equal(7.61218, shape.GetProperty("KEEP_DESIGN_SIZE", 1));
            Assert.Equal(7.45192, shape.GetProperty("KEEP_DESIGN_SIZE", 2));
            Assert.Equal(0, shape.GetProperty("ALIGNMENT_PROPS", 0));
            Assert.Equal(("NO", 3d, 1d, 2d, 5d), (shape.Name, shape.Width, shape.Height, shape.PositionX, shape.PositionY));
        }

        [Theory]
        [MemberData(nameof(Markings))]
        public void Toggle_OnThenOff_LikeStandardSize_WritesTrueThenRemoves_KeepingEverythingElse(string markingName, string propertyName, ShapeKind kind)
        {
            var shape = MarkedWithEverythingElse(kind);
            var marking = ByName(markingName);

            var on = _service.Toggle(Select(shape), marking);

            Assert.True(on.IsSuccess, on.Message);
            Assert.Equal(true, shape.GetProperty(propertyName, 0));
            Assert.IsType<bool>(shape.GetProperty(propertyName, 0));
            Assert.False(shape.HasProperty(propertyName, 1));
            Assert.Equal(StandardSizeSelectionState.AllMarked, _service.InspectSelection(Select(shape), marking));
            AssertEverythingElseKept(shape);

            var off = _service.Toggle(Select(shape), marking);

            Assert.True(off.IsSuccess, off.Message);
            Assert.Contains("desativado", off.Message);
            Assert.False(shape.HasProperty(propertyName, 0));
            Assert.Equal(StandardSizeSelectionState.NoneMarked, _service.InspectSelection(Select(shape), marking));
            AssertEverythingElseKept(shape);
        }

        [Fact]
        public void Toggle_MixedSelection_MarksAll_LikeStandardSize()
        {
            var marked = new FakeShape();
            marked.SetProperty("STRETCH_PROPS", 0, true);
            var unmarked = new FakeShape();
            var selection = Select(marked, unmarked);

            Assert.Equal(StandardSizeSelectionState.Mixed, _service.InspectSelection(selection, SisboltMarking.StretchToMold));

            _service.Toggle(selection, SisboltMarking.StretchToMold);

            Assert.Equal(true, marked.GetProperty("STRETCH_PROPS", 0));
            Assert.Equal(true, unmarked.GetProperty("STRETCH_PROPS", 0));
            Assert.Equal(StandardSizeSelectionState.AllMarked, _service.InspectSelection(selection, SisboltMarking.StretchToMold));
        }

        [Fact]
        public void EntryWithFalse_CountsAsInactive_AndTurningOnMakesItTrue()
        {
            var shape = new FakeShape();
            shape.SetProperty("STRETCH_PROPS", 0, false);

            Assert.Equal(StandardSizeSelectionState.NoneMarked, _service.InspectSelection(Select(shape), SisboltMarking.StretchToMold));
            _service.Toggle(Select(shape), SisboltMarking.StretchToMold);

            Assert.Equal(true, shape.GetProperty("STRETCH_PROPS", 0));
        }

        [Fact]
        public void InspectSelection_NoSelection_IsUnavailable_AndReadFailure_IsUnknown()
        {
            Assert.Equal(StandardSizeSelectionState.Unavailable, _service.InspectSelection(null, SisboltMarking.FreeHand));
            Assert.Equal(StandardSizeSelectionState.Unavailable, _service.InspectSelection(Select(), SisboltMarking.FreeHand));
            Assert.Equal(StandardSizeSelectionState.Unknown,
                _service.InspectSelection(Select(new FakeShape { ThrowOnPropertyAccess = true }), SisboltMarking.FreeHand));
        }

        [Fact]
        public void Toggle_EmptySelection_WritesNothing()
        {
            Assert.Equal(OperationStatus.InvalidSelection, _service.Toggle(Select(), SisboltMarking.StretchToMold).Status);
        }

        // Regra do usuário (2026-11-06): Mão Livre só em objetos com os nomes NO, NU, AP, TS.
        [Theory]
        [InlineData("NO")]
        [InlineData("NU")]
        [InlineData("AP")]
        [InlineData("TS")]
        public void FreeHand_TurnsOnForTheFourCodes(string name)
        {
            var text = new FakeShape { Kind = ShapeKind.Text, Name = name };

            Assert.True(_service.Toggle(Select(text), SisboltMarking.FreeHand).IsSuccess);
            Assert.Equal(true, text.GetProperty("FREE_HAND_PROPS", 0));
        }

        [Theory]
        [InlineData("Texto Nome")]
        [InlineData("no")]
        [InlineData("NO ")]
        [InlineData("")]
        public void FreeHand_OtherNames_AreRefused_EvenWithOneValidInTheSelection(string name)
        {
            var valid = new FakeShape { Kind = ShapeKind.Text, Name = "NO" };
            var other = new FakeShape { Kind = ShapeKind.Text, Name = name };

            var result = _service.Toggle(Select(valid, other), SisboltMarking.FreeHand);

            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
            Assert.Contains("NO, NU, AP ou TS", result.Message);
            Assert.False(valid.HasProperty("FREE_HAND_PROPS", 0));
            Assert.False(other.HasProperty("FREE_HAND_PROPS", 0));
        }

        [Fact]
        public void FreeHand_TurningOff_DoesNotRequireTheName()
        {
            // Texto marcado e depois renomeado: ainda dá para desligar.
            var renamed = new FakeShape { Kind = ShapeKind.Text, Name = "Texto Nome" };
            renamed.SetProperty("FREE_HAND_PROPS", 0, true);

            var result = _service.Toggle(Select(renamed), SisboltMarking.FreeHand);

            Assert.True(result.IsSuccess, result.Message);
            Assert.False(renamed.HasProperty("FREE_HAND_PROPS", 0));
        }

        [Fact]
        public void OtherMarkings_DoNotRequireACodeName()
        {
            var text = new FakeShape { Kind = ShapeKind.Text, Name = "Qualquer" };
            var rectangle = new FakeShape { Name = "Escudo" };

            Assert.True(_service.Toggle(Select(text), SisboltMarking.IgnoreVariableMetrics).IsSuccess);
            Assert.True(_service.Toggle(Select(rectangle), SisboltMarking.StretchToMold).IsSuccess);
        }

        [Theory]
        [InlineData(nameof(SisboltMarking.IgnoreVariableMetrics))]
        [InlineData(nameof(SisboltMarking.FreeHand))]
        public void TextOnlyMarkings_AnyNonTextInSelection_WritesNothing(string markingName)
        {
            var text = new FakeShape { Kind = ShapeKind.Text };
            var rectangle = new FakeShape { Kind = ShapeKind.Rectangle };
            var marking = ByName(markingName);

            var result = _service.Toggle(Select(text, rectangle), marking);

            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
            Assert.False(text.HasProperty(marking.PropertyName, 0));
            Assert.False(rectangle.HasProperty(marking.PropertyName, 0));
        }
    }

    public class SingleItemGroupServiceTests
    {
        private readonly SingleItemGroupService _service = new SingleItemGroupService();

        private static (FakeLayer Layer, FakeShape Shape, FakeSelection Selection) Scenario()
        {
            var layer = new FakeLayer();
            var other = new FakeShape { CurrentLayer = layer };
            var shape = new FakeShape { CurrentLayer = layer, Width = 3, Height = 2, PositionX = 1, PositionY = 4 };
            shape.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            layer.ShapesList.Add(other);
            layer.ShapesList.Add(shape);
            var selection = new FakeSelection();
            selection.ShapesList.Add(shape);
            return (layer, shape, selection);
        }

        [Fact]
        public void CreateGroup_OriginalBecomesTheOnlyChild_GroupIsNamedInFrontAndSelected()
        {
            var (layer, shape, selection) = Scenario();
            var originalId = shape.StaticId;

            var result = _service.CreateGroup(selection);

            Assert.True(result.IsSuccess, result.Message);
            var group = (FakeShape)layer.ShapesList[0];
            Assert.Equal(ShapeKind.Group, group.Kind);
            Assert.Same(shape, Assert.Single(group.Shapes));
            Assert.Equal(originalId, shape.StaticId);
            Assert.Equal(SingleItemGroupService.GroupName, group.Name);
            Assert.Equal(1, group.OrderToFrontCount);
            Assert.True(group.SelectedAlone);
            Assert.False(group.HasProperty("KEEP_DESIGN_SIZE", 0));
            Assert.Equal(true, shape.GetProperty("KEEP_DESIGN_SIZE", 0));
            Assert.Equal((3d, 2d, 1d, 4d), (shape.Width, shape.Height, shape.PositionX, shape.PositionY));
            Assert.Equal(2, layer.ShapesList.Count);
        }

        [Fact]
        public void CreateGroup_InsideAGroup_OrSeveralSelected_ChangesNothing()
        {
            var (layer, shape, selection) = Scenario();
            shape.ParentGroup = new FakeShape { Kind = ShapeKind.Group };

            Assert.Equal(OperationStatus.InvalidSelection, _service.CreateGroup(selection).Status);

            selection.ShapesList.Add(new FakeShape { CurrentLayer = layer });
            shape.ParentGroup = null;
            Assert.Equal(OperationStatus.InvalidSelection, _service.CreateGroup(selection).Status);
            Assert.DoesNotContain(layer.ShapesList, s => s.Kind == ShapeKind.Group);
        }

        [Fact]
        public void CreateGroup_UnexpectedStructure_StopsBeforeNamingOrSelecting()
        {
            var (_, shape, selection) = Scenario();
            FakeShape? produced = null;
            shape.GroupAloneResult = s =>
            {
                produced = new FakeShape { Kind = ShapeKind.Group };
                produced.ShapesList.Add(new FakeShape());
                return produced;
            };

            var result = _service.CreateGroup(selection);

            Assert.Equal(OperationStatus.ExecutionFailed, result.Status);
            Assert.Contains("Ctrl+Z", result.Message);
            Assert.Equal("", produced!.Name);
            Assert.False(produced.SelectedAlone);
            Assert.Equal(0, produced.OrderToFrontCount);
        }
    }

    public class HorizontalAlignmentLinkServiceTests
    {
        private static readonly Guid Fixed = new Guid("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        private readonly HorizontalAlignmentLinkService _service = new HorizontalAlignmentLinkService();

        [Fact]
        public void SaveLink_WritesTheSameGuidOnBoth_AsHyphenatedString_AndKeepsAlignmentIds0To4()
        {
            var target = new FakeShape();
            for (var id = 1; id <= 4; id++) target.SetProperty("ALIGNMENT_PROPS", id, "");
            target.SetProperty("ALIGNMENT_PROPS", 0, 0);
            var reference = new FakeShape();
            reference.SetProperty("KEEP_DESIGN_SIZE", 0, true);

            var result = _service.SaveLink(target, reference, () => Fixed);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal("3f2504e0-4f89-11d3-9a0c-0305e82c3301", reference.GetProperty("GUID_IDENTIFIER_PROPS", 0));
            Assert.Equal("3f2504e0-4f89-11d3-9a0c-0305e82c3301", target.GetProperty("ALIGNMENT_PROPS", 5));
            Assert.Equal(0, target.GetProperty("ALIGNMENT_PROPS", 0));
            Assert.All(Enumerable.Range(1, 4), id => Assert.Equal("", target.GetProperty("ALIGNMENT_PROPS", id)));
            Assert.False(target.HasProperty("GUID_IDENTIFIER_PROPS", 0));
            Assert.False(reference.HasProperty("ALIGNMENT_PROPS", 5));
            Assert.Equal(true, reference.GetProperty("KEEP_DESIGN_SIZE", 0));
        }

        [Fact]
        public void SaveLink_ReferenceAlreadyHasGuid_IsOutOfScope_AndChangesNothing()
        {
            var target = new FakeShape();
            var reference = new FakeShape();
            reference.SetProperty("GUID_IDENTIFIER_PROPS", 0, "existente");

            var result = _service.SaveLink(target, reference, () => Fixed);

            Assert.Equal(OperationStatus.NotImplemented, result.Status);
            Assert.Equal("existente", reference.GetProperty("GUID_IDENTIFIER_PROPS", 0));
            Assert.False(target.HasProperty("ALIGNMENT_PROPS", 5));
        }

        [Fact]
        public void SaveLink_SecondWriteFails_RemovesTheReferenceGuid_AndRethrows()
        {
            var target = new FakeShape { ThrowOnSetPropertyId = 5 };
            var reference = new FakeShape();

            Assert.Throws<InvalidOperationException>(() => _service.SaveLink(target, reference, () => Fixed));
            Assert.False(reference.HasProperty("GUID_IDENTIFIER_PROPS", 0));
        }

        [Fact]
        public void SaveLink_SameObject_IsRefused()
        {
            var shape = new FakeShape();

            Assert.Equal(OperationStatus.InvalidSelection, _service.SaveLink(shape, shape, () => Fixed).Status);
            Assert.False(shape.HasProperty("GUID_IDENTIFIER_PROPS", 0));
        }

        [Fact]
        public void ValidateSelection_NeedsTwoDistinctObjects_FirstIsTargetSecondIsReference()
        {
            var a = new FakeShape();
            var b = new FakeShape();
            var two = new FakeSelection();
            two.ShapesList.AddRange(new[] { a, b });
            var one = new FakeSelection();
            one.ShapesList.Add(a);

            var ok = _service.ValidateSelection(two);

            Assert.True(ok.IsSuccess);
            Assert.Same(a, ok.Value.Target);
            Assert.Same(b, ok.Value.Reference);
            Assert.Equal(OperationStatus.InvalidSelection, _service.ValidateSelection(one).Status);
        }

        [Fact]
        public void SaveLink_ReplacesAPreviousLinkOnTheTarget_AndSaysSo()
        {
            var target = new FakeShape();
            target.SetProperty("ALIGNMENT_PROPS", 5, "antigo");

            var result = _service.SaveLink(target, new FakeShape(), () => Fixed);

            Assert.Contains("substituído", result.Message);
            Assert.Equal(Fixed.ToString("D"), target.GetProperty("ALIGNMENT_PROPS", 5));
        }
    }
}
