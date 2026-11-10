#if NET48
// Só compila na perna net48 — PXToolBoxDev (host) só existe lá. Comparação antes/depois do
// Diagnóstico (usada para observar o que as ferramentas do SISBolt mudam). A captura em si lê o
// Corel e não é testada aqui; a comparação é pura.
using System;
using System.Collections.Generic;
using PXToolBoxDev.Diagnostics;
using Xunit;

namespace PXToolBox.Tests
{
    public class PageCaptureDiffTests
    {
        private static ShapePropertyEntry Prop(string name, int id, string type, string value) =>
            new ShapePropertyEntry { Name = name, Id = id, ValueType = type, ValueDisplay = value };

        private static CapturedShape Shape(int id, Action<CapturedShape>? change = null)
        {
            var shape = new CapturedShape
            {
                StaticId = id, Path = "CAM1", Type = "cdrRectangleShape", Layer = "CAM1", Order = 1,
                PositionX = 1, PositionY = 2, Width = 3, Height = 4, Visible = true,
            };
            change?.Invoke(shape);
            return shape;
        }

        private static PageCapture Page(params CapturedShape[] shapes) => new PageCapture
        {
            CapturedAt = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero),
            DocumentName = "teste.cdr", PageIndex = 1, ToolVersion = "0.1.0-dev.68",
            RulerUnit = "polegada (cdrInch)", RulerUnitCode = "cdrInch", GeometryUnitCode = "cdrInch",
            Shapes = shapes,
        };

        private static readonly GeometryScale Inches = new GeometryScale("cdrInch", "cdrInch", "cdrInch");

        [Fact]
        public void PropertyChanges_ReportsAddedRemovedAndChanged_ByNameAndId()
        {
            var before = new[] { Prop("KEEP_DESIGN_SIZE", 0, "Boolean", "True"), Prop("KEEP_DESIGN_SIZE", 1, "Double", "10"), Prop("OLD", 0, "String", "x") };
            var after = new[] { Prop("KEEP_DESIGN_SIZE", 0, "Boolean", "True"), Prop("KEEP_DESIGN_SIZE", 1, "Double", "12"), Prop("NEW", 3, "Int32", "7") };

            Assert.Equal(new[]
            {
                "~ KEEP_DESIGN_SIZE [1]: Double 10 → Double 12",
                "+ NEW [3] = Int32 7",
                "- OLD [0] = String x",
            }, PageCaptureDiff.PropertyChanges(before, after));
        }

        [Fact]
        public void ShapeChanges_ReportsGeometryRotationParentAndSelection()
        {
            var before = Shape(5);
            var after = Shape(5, s => { s.PositionX = 1.5; s.Rotation = 90; s.ParentStaticId = 9; s.ParentKind = "grupo"; s.Selected = true; s.Name = "A"; });

            var lines = PageCaptureDiff.ShapeChanges(before, after, Inches);

            Assert.Contains("Nome: \"\" → \"A\"", lines);
            Assert.Contains("Dentro de: camada (nível de cima) → grupo #9", lines);
            Assert.Contains("Posição X: 1 → 1.5 pol (diferença 0.5)", lines);
            Assert.Contains("Rotação (graus): 0 → 90 (diferença 90)", lines);
            Assert.Contains("Selecionado: não → sim", lines);
            Assert.DoesNotContain(lines, l => l.StartsWith("Largura"));
        }

        [Fact]
        public void ShapeChanges_IgnoresTinyFloatingNoise_AndReportsObjectDataAndText()
        {
            var before = Shape(5, s => { s.Text = "NOME"; s.ObjectData = new[] { new KeyValuePair<string, string>("Nome", "a") }; });
            var after = Shape(5, s => { s.Width = 3 + 1e-9; s.Text = "OUTRO"; s.ObjectData = new[] { new KeyValuePair<string, string>("Nome", "b") }; });

            var lines = PageCaptureDiff.ShapeChanges(before, after, Inches);

            Assert.Equal(new[] { "Texto: \"NOME\" → \"OUTRO\"", "ObjectData \"Nome\": \"a\" → \"b\"" }, lines);
        }

        [Fact]
        public void Report_ListsNewRemovedAndChangedShapes_DocumentProperties_AndSelectedState()
        {
            var before = Page(Shape(1), Shape(2));
            before.DocumentProperties = new[] { Prop("EXPORT", 0, "String", "PNG") };
            var after = Page(
                Shape(1, s => { s.ParentStaticId = 3; s.ParentKind = "grupo"; s.Selected = true; s.Properties = new[] { Prop("ALIGNMENT_PROPS", 0, "String", "center") }; }),
                Shape(3, s => { s.Type = "cdrGroupShape"; s.Name = "G"; }));
            after.DocumentProperties = new[] { Prop("EXPORT", 0, "String", "JPG") };

            var report = PageCaptureDiff.Report(before, after, "SISBolt — Criar grupo com item único");

            Assert.Contains("Ferramenta testada: SISBolt — Criar grupo com item único", report);
            Assert.Contains("Documento.Properties:", report);
            Assert.Contains("~ EXPORT [0]: String PNG → String JPG", report);
            Assert.Contains("Objeto NOVO #3 cdrGroupShape \"G\"", report);
            Assert.Contains("Objeto REMOVIDO #2 cdrRectangleShape", report);
            Assert.Contains("Properties + ALIGNMENT_PROPS [0] = String center", report);
            Assert.Contains("== Selecionados depois (estado completo) ==", report);
            Assert.Contains("  Properties ALIGNMENT_PROPS [0] = String center", report);
        }

        [Fact]
        public void Report_NoDifferences_SaysSo_AndWarnsAboutAnotherDocument()
        {
            var before = Page(Shape(1));
            var after = Page(Shape(1));
            after.DocumentName = "outro.cdr";

            var report = PageCaptureDiff.Report(before, after);

            Assert.Contains("Nenhuma diferença encontrada", report);
            Assert.Contains("ATENÇÃO: a captura \"antes\" foi de outro documento/página", report);
        }

        [Fact]
        public void PropertyChanges_UnreadableEntries_NeverMatchEachOther()
        {
            var bad = new ShapePropertyEntry { Index = 2, ReadError = "falhou" };

            Assert.Empty(PageCaptureDiff.PropertyChanges(new[] { bad }, new[] { bad }));
            Assert.Equal(new[] { "+ entrada 2 = (erro: falhou)" }, PageCaptureDiff.PropertyChanges(Array.Empty<ShapePropertyEntry>(), new[] { bad }));
        }

        // ---- Unidades da geometria (fix/page-capture-geometry-units) ----

        // Caso real do Tamanho Padrão (docs/ARCHITECTURE.md, 30: StaticID 4, 7,612 × 7,452 cm na
        // régua = 2,99692125984252 × 2,93382677165354 pol na API). Se a ferramenta troca
        // Document.Unit para cm, a mesma geometria volta ×2,54. Antes da correção isso aparecia
        // como "Largura: 2.996921 → 7.61218" — objeto redimensionado que não foi.
        private static (PageCapture Before, PageCapture After) StandardSizeCase(Action<CapturedShape>? changeAfter = null)
        {
            const double widthInches = 2.99692125984252, heightInches = 2.93382677165354;
            const double xInches = 1.25, yInches = 4.5;
            var before = Page(Shape(4, s => { s.Width = widthInches; s.Height = heightInches; s.PositionX = xInches; s.PositionY = yInches; s.Selected = true; }));
            before.RulerUnit = "centímetro (cdrCentimeter)"; before.RulerUnitCode = "cdrCentimeter";
            before.PageWidth = 8.5; before.PageHeight = 11;
            var after = Page(Shape(4, s =>
            {
                s.Width = widthInches * 2.54; s.Height = heightInches * 2.54; s.PositionX = xInches * 2.54; s.PositionY = yInches * 2.54; s.Selected = true;
                s.Properties = new[] { Prop("KEEP_DESIGN_SIZE", 0, "Boolean", "True"), Prop("KEEP_DESIGN_SIZE", 1, "Double", "7,61218"), Prop("KEEP_DESIGN_SIZE", 2, "Double", "7,45192") };
                changeAfter?.Invoke(s);
            }));
            after.RulerUnit = "centímetro (cdrCentimeter)"; after.RulerUnitCode = "cdrCentimeter"; after.GeometryUnitCode = "cdrCentimeter";
            after.PageWidth = 8.5 * 2.54; after.PageHeight = 11 * 2.54;
            return (before, after);
        }

        [Fact]
        public void StandardSizeCase_SameObjectInInchesAndCentimeters_IsNotResized_ButTheThreeEntriesAppear()
        {
            var (before, after) = StandardSizeCase();

            var report = PageCaptureDiff.Report(before, after, "SISBolt — Tamanho Padrão");

            Assert.DoesNotContain("Largura:", report);
            Assert.DoesNotContain("Altura:", report);
            Assert.DoesNotContain("Posição X", report);
            Assert.DoesNotContain("Posição Y", report);
            Assert.DoesNotContain("Objeto alterado #4 cdrRectangleShape — CAM1" + Environment.NewLine + "  Largura", report);
            Assert.Contains("Properties + KEEP_DESIGN_SIZE [0] = Boolean True", report);
            Assert.Contains("Properties + KEEP_DESIGN_SIZE [1] = Double 7,61218", report);
            Assert.Contains("Properties + KEEP_DESIGN_SIZE [2] = Double 7,45192", report);
            Assert.Contains("Documento.Unit: cdrInch → cdrCentimeter", report);
            Assert.Contains("Unidade da régua (Document.Rulers.HUnits): antes centímetro (cdrCentimeter) · depois centímetro (cdrCentimeter)", report);
            Assert.Contains("Unidade efetiva da geometria (Document.Unit): antes cdrInch · depois cdrCentimeter", report);
            Assert.Contains("Página: antes 21.59 × 27.94 cm · depois 21.59 × 27.94 cm", report);
            // Estado completo: convertido para a régua, brutos preservados ao lado.
            Assert.Contains("tamanho: 7.61218 × 7.45192 cm", report);
            Assert.Contains("(brutos: 3.175, 11.43 · 7.61218 × 7.45192 em cdrCentimeter)", report);
        }

        [Fact]
        public void StandardSizeCase_RealMoveAndResize_StillAppear_InRulerUnits()
        {
            // +1 cm em X e +0,5 cm na largura, medidos já com Document.Unit em cm.
            var (before, after) = StandardSizeCase(s => { s.PositionX += 1; s.Width += 0.5; });

            var lines = PageCaptureDiff.ShapeChanges(before.Shapes[0], after.Shapes[0], GeometryScale.For(before, after));

            Assert.Contains("Posição X: 3.175 → 4.175 cm (diferença 1)", lines);
            Assert.Contains("Largura: 7.61218 → 8.11218 cm (diferença 0.5)", lines);
            Assert.DoesNotContain(lines, l => l.StartsWith("Altura") || l.StartsWith("Posição Y"));
        }

        [Fact]
        public void SameUnit_RealChange_StillAppears()
        {
            var lines = PageCaptureDiff.ShapeChanges(Shape(1), Shape(1, s => s.Height = 4.25), Inches);

            Assert.Equal(new[] { "Altura: 4 → 4.25 pol (diferença 0.25)" }, lines);
        }

        [Theory]
        [InlineData("", "cdrInch")]
        [InlineData("cdrInch", "cdrPixel")]
        [InlineData("cdrDidots", "cdrDidots")]
        public void UnknownGeometryUnit_MakesGeometryInconclusive_WithoutClaimingAChange(string beforeUnit, string afterUnit)
        {
            var before = Page(Shape(1));
            before.GeometryUnitCode = beforeUnit;
            var after = Page(Shape(1, s => { s.Width = 7.62; s.Properties = new[] { Prop("KEEP_DESIGN_SIZE", 0, "Boolean", "True") }; }));
            after.GeometryUnitCode = afterUnit;

            var report = PageCaptureDiff.Report(before, after);

            Assert.Contains("Comparação geométrica INCONCLUSIVA", report);
            Assert.Contains("Largura (bruto, unidade desconhecida): 3 → 7.62 — inconclusivo", report);
            Assert.DoesNotContain("Largura: ", report);
            Assert.Contains("Properties + KEEP_DESIGN_SIZE [0] = Boolean True", report);
        }

        [Fact]
        public void UnknownUnit_SameRawValues_SaysNothingAboutGeometry()
        {
            var before = Page(Shape(1)); before.GeometryUnitCode = "";
            var after = Page(Shape(1)); after.GeometryUnitCode = "";

            Assert.Empty(PageCaptureDiff.ShapeChanges(before.Shapes[0], after.Shapes[0], GeometryScale.For(before, after)));
        }

        [Theory]
        [InlineData("cdrInch", 1)]
        [InlineData("cdrCentimeter", 1 / 2.54)]
        [InlineData("cdrMillimeter", 1 / 25.4)]
        [InlineData("cdrPoint", 1 / 72.0)]
        [InlineData("cdrFoot", 12)]
        public void CorelUnits_KnownLinearUnits(string code, double inchesPerUnit)
        {
            Assert.Equal(inchesPerUnit, CorelUnits.InchesPerUnit(code)!.Value, 12);
        }

        [Theory]
        [InlineData("cdrPixel")]
        [InlineData("cdrDidots")]
        [InlineData("cdrCicero")]
        [InlineData("")]
        [InlineData(null)]
        public void CorelUnits_UnitsWithoutAFixedOrConfirmedSize_AreUnknown(string? code)
        {
            Assert.Null(CorelUnits.InchesPerUnit(code));
        }
    }
}
#endif
