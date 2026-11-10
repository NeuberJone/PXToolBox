using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    public class RotationResetServiceTests
    {
        private const double Tolerance = 0.01;
        private readonly RotationResetService _service = new RotationResetService();
        private readonly FakeArrangement _arrangement = new FakeArrangement();
        private readonly FakeLayer _layer = new FakeLayer { Name = "CAM1.Frente.MA" };

        private FakeShape Curve(string name, double angle, double x = 10) =>
            _layer.AddShape(new FakeShape { Name = name, Kind = ShapeKind.Curve, RotationAngle = angle, CenterX = x, Geometry = FakeShape.Square(x, 0) });

        private static FakeSelection Select(params ICorelShape[] shapes)
        {
            var selection = new FakeSelection();
            selection.ShapesList.AddRange(shapes);
            return selection;
        }

        [Fact]
        public void RotatedCurve_IsZeroed_KeepingGeometryAndPlace()
        {
            var back = Curve("atrás", 0, 50);
            var curve = Curve("Frente", 30);
            var geometry = curve.Geometry;
            curve.OrderToFront();

            var result = _service.Reset(Select(curve), _arrangement, Tolerance);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(0, curve.RotationAngle);
            Assert.Same(geometry, curve.Geometry);
            Assert.Equal(10, curve.CenterX, 6);
            Assert.False(curve.IsDeleted);
            Assert.All(curve.Duplicates, d => Assert.True(d.IsDeleted));
            Assert.Equal(new ICorelShape[] { curve, back }, _layer.ShapesList);
            Assert.Equal(new ICorelShape[] { curve }, _arrangement.Selected);
            Assert.Contains("1 objeto(s) com ângulo zerado", result.Message);
        }

        [Fact]
        public void GeometryMismatch_RestoresBackupInSamePlace_WithProperties()
        {
            var curve = Curve("Frente", 45);
            curve.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            curve.FailingGeometryWrites = 1;

            var result = _service.Reset(Select(curve), _arrangement, Tolerance);

            Assert.Equal(OperationStatus.ExecutionFailed, result.Status);
            Assert.True(curve.IsDeleted);
            var backup = Assert.Single(curve.Duplicates);
            Assert.False(backup.IsDeleted);
            Assert.Equal(45, backup.RotationAngle);
            Assert.Equal("Frente", backup.Name);
            Assert.Equal(true, backup.GetProperty("KEEP_DESIGN_SIZE", 0));
            Assert.Equal(new ICorelShape[] { backup }, _layer.ShapesList);
            Assert.Equal(new ICorelShape[] { backup }, _arrangement.Selected);
            Assert.Contains("restaurado como estava", result.Message);
        }

        [Fact]
        public void RotatedRectangle_BecomesCurve_KeepingNameAndProperties()
        {
            var rect = _layer.AddShape(new FakeShape { Name = "Frente", Kind = ShapeKind.Rectangle, RotationAngle = 30, CenterX = 10, ConvertToCurvesDropsProperties = true });
            rect.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            rect.SetProperty("STRETCH_PROPS", 0, true);

            var result = _service.Reset(Select(rect), _arrangement, Tolerance);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(ShapeKind.Curve, rect.Kind);
            Assert.Equal(0, rect.RotationAngle);
            Assert.Equal(true, rect.GetProperty("KEEP_DESIGN_SIZE", 0));
            Assert.Equal(true, rect.GetProperty("STRETCH_PROPS", 0));
            Assert.Equal(new ICorelShape[] { rect }, _layer.ShapesList);
        }

        [Fact]
        public void ConvertToCurvesThatDoesNothing_LeavesObjectUntouched()
        {
            var rect = _layer.AddShape(new FakeShape { Name = "Frente", Kind = ShapeKind.Rectangle, RotationAngle = 30, ConvertToCurvesDoesNothing = true });

            var result = _service.Reset(Select(rect), _arrangement, Tolerance);

            Assert.Equal(OperationStatus.ExecutionFailed, result.Status);
            Assert.False(rect.IsDeleted);
            Assert.Equal(30, rect.RotationAngle);
            Assert.Equal(new ICorelShape[] { rect }, _layer.ShapesList);
            Assert.Contains("não converteu", result.Message);
        }

        [Fact]
        public void Text_IsNeverConverted()
        {
            var text = _layer.AddShape(new FakeShape { Name = "NO", Kind = ShapeKind.Text, RotationAngle = 15, IsArtisticText = true });

            var result = _service.Reset(Select(text), _arrangement, Tolerance);

            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
            Assert.Equal(ShapeKind.Text, text.Kind);
            Assert.Equal(15, text.RotationAngle);
            Assert.Empty(text.Duplicates);
            Assert.Contains("texto", result.Message);
        }

        [Fact]
        public void NothingRotated_ChangesNothing()
        {
            var curve = Curve("Frente", 0);

            var result = _service.Reset(Select(curve), _arrangement, Tolerance);

            Assert.True(result.IsSuccess);
            Assert.Equal("Nenhum objeto girado — nada mudou.", result.Message);
            Assert.Empty(curve.Duplicates);
        }

        [Fact]
        public void EmptySelection_Fails()
        {
            Assert.Equal(OperationStatus.InvalidSelection, _service.Reset(new FakeSelection(), _arrangement, Tolerance).Status);
        }

        [Theory]
        [InlineData(360.0)]
        [InlineData(-360.0)]
        [InlineData(0.0000001)]
        public void IsZero_NormalizesFullTurns(double angle) => Assert.True(RotationResetService.IsZero(angle));

        [Fact]
        public void GroupAtZero_ResetsChildrenInPlace_KeepingTheSameGroup()
        {
            var group = _layer.AddShape(new FakeShape { Name = "-G", Kind = ShapeKind.Group });
            var child = new FakeShape { Name = "Frente", Kind = ShapeKind.Curve, RotationAngle = 90, CenterX = 10, Geometry = FakeShape.Square(10, 0), ParentGroup = group, CurrentLayer = _layer };
            var tag = new FakeShape { Name = "Tag", Kind = ShapeKind.Text, ParentGroup = group, CurrentLayer = _layer };
            group.ShapesList.Add(tag);
            group.ShapesList.Add(child);

            var result = _service.Reset(Select(group), _arrangement, Tolerance);

            Assert.True(result.IsSuccess, result.Message);
            Assert.False(group.IsDeleted);
            Assert.Equal(0, child.RotationAngle);
            Assert.Equal(new ICorelShape[] { tag, child }, group.ShapesList);
            Assert.Equal(new ICorelShape[] { group }, _arrangement.Selected);
        }

        [Fact]
        public void RotatedGroup_IsRebuilt_SameChildrenOrderNamePropertiesAndPlace()
        {
            var front = Curve("na frente", 0, 70);
            var group = _layer.AddShape(new FakeShape { Name = "-G", Kind = ShapeKind.Group, RotationAngle = 30 });
            var back = Curve("atrás", 0, 90);
            group.SetProperty("KEEP_DESIGN_SIZE", 0, true);
            var a = new FakeShape { Name = "Frente", Kind = ShapeKind.Curve, CenterX = 10, Geometry = FakeShape.Square(10, 0), ParentGroup = group, CurrentLayer = _layer };
            var b = new FakeShape { Name = "x", Kind = ShapeKind.Curve, CenterX = 20, Geometry = FakeShape.Square(20, 0), ParentGroup = group, CurrentLayer = _layer };
            group.ShapesList.Add(a);
            group.ShapesList.Add(b);

            var result = _service.Reset(Select(group), _arrangement, Tolerance);

            Assert.True(result.IsSuccess, result.Message);
            var rebuilt = Assert.IsType<FakeShape>(Assert.Single(_arrangement.Selected));
            Assert.NotSame(group, rebuilt);
            Assert.Equal("-G", rebuilt.Name);
            Assert.Equal(true, rebuilt.GetProperty("KEEP_DESIGN_SIZE", 0));
            Assert.Equal(new ICorelShape[] { a, b }, rebuilt.ShapesList);
            Assert.Equal(0, a.RotationAngle);
            Assert.Equal(0, b.RotationAngle);
            Assert.Equal(new ICorelShape[] { front, rebuilt, back }, _layer.ShapesList);
        }

        [Fact]
        public void RotatedGroup_WithText_IsPartial_NeverReportedAsFullyZeroed()
        {
            var group = _layer.AddShape(new FakeShape { Name = "-G", Kind = ShapeKind.Group, RotationAngle = 30 });
            var curve = new FakeShape { Name = "Frente", Kind = ShapeKind.Curve, CenterX = 10, Geometry = FakeShape.Square(10, 0), ParentGroup = group, CurrentLayer = _layer };
            var text = new FakeShape { Name = "Tag", Kind = ShapeKind.Text, ParentGroup = group, CurrentLayer = _layer };
            group.ShapesList.Add(text);
            group.ShapesList.Add(curve);

            var result = _service.Reset(Select(group), _arrangement, Tolerance);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Contains("em parte", result.Message);
            Assert.Equal(30, text.RotationAngle);
            Assert.Equal(0, curve.RotationAngle);
        }

        [Fact]
        public void RotatedGroupInsideAnotherGroup_IsNotTouched()
        {
            var outer = _layer.AddShape(new FakeShape { Name = "fora", Kind = ShapeKind.Group });
            var inner = new FakeShape { Name = "dentro", Kind = ShapeKind.Group, RotationAngle = 20, ParentGroup = outer, CurrentLayer = _layer };
            outer.ShapesList.Add(inner);

            var result = _service.Reset(Select(outer), _arrangement, Tolerance);

            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
            Assert.False(inner.IsDeleted);
            Assert.Contains("grupo girado dentro de outro grupo", result.Message);
        }

        [Fact]
        public void CurveGeometry_Matches_ComparesControlPointsOnlyOnCurveSegments()
        {
            var a = FakeShape.Square(0, 0);
            Assert.True(a.Matches(FakeShape.Square(0.005, 0), Tolerance));
            Assert.False(a.Matches(FakeShape.Square(0.5, 0), Tolerance));
            var lineOnly = new CurveGeometry(1, a.Nodes, a.Segments.Select(_ => CurveSegment.Line()).ToList());
            Assert.False(a.Matches(lineOnly, Tolerance));
        }
    }
}
