using System;
using System.Collections.Generic;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;
using PXToolBox.Outputs.Tools;
using PXToolBox.Tests.Fakes;
using Xunit;

namespace PXToolBox.Tests
{
    /// <summary>
    /// Cobre o caso crítico descrito no escopo da entrega: ocultar FE e C, executar
    /// "Preparar Grid", mostrar FE e C de novo, e conferir a integridade do resultado.
    ///
    /// Ported da revisão candidata (Referência/Macros/modulos/Grid.bas): camadas ocultas
    /// PARTICIPAM do Grid (são mostradas temporariamente, processadas, e têm a visibilidade
    /// original restaurada ao final) — ver GridPreparationService.
    /// </summary>
    public class GridPreparationServiceTests
    {
        [Fact]
        public void PrepareGrid_HiddenFeAndC_ParticipateAndVisibilityIsRestored()
        {
            var document = new FakeDocument();
            var page = document.ActivePageFake;

            var maLayer = (FakeLayer)page.CreateLayer("CAM1.PP.MA");
            var feLayer = (FakeLayer)page.CreateLayer("CAM1.PP.FE");
            var cLayer = (FakeLayer)page.CreateLayer("CAM1.PP.C");

            var maShape = maLayer.AddShape(new FakeShape { Name = "Base", Width = 10, Height = 10, CenterX = 0, CenterY = 0 });
            var feShape = feLayer.AddShape(new FakeShape { Name = "Frente", Width = 5, Height = 5, CenterX = 20, CenterY = 20 });
            var cShape = cLayer.AddShape(new FakeShape { Name = "Costas", Width = 5, Height = 5, CenterX = -20, CenterY = -20 });

            // Passo 1 do caso crítico: oculta FE e C.
            feLayer.Visible = false;
            cLayer.Visible = false;

            var service = new GridPreparationService();

            // Passo 2: executa Preparar Grid.
            var result = service.PrepareGrid(document);
            Assert.True(result.IsSuccess, result.Message);

            // Passo 3/4: a visibilidade original de cada camada foi restaurada automaticamente
            // (o teste nem precisa "mostrar de novo" manualmente — isso é a própria garantia).
            Assert.True(maLayer.Visible);
            Assert.False(feLayer.Visible);
            Assert.False(cLayer.Visible);

            // Nenhum shape foi agrupado/movido para outra camada — todos continuam onde estavam.
            Assert.Contains(maShape, maLayer.ShapesList);
            Assert.Contains(feShape, feLayer.ShapesList);
            Assert.Contains(cShape, cLayer.ShapesList);

            // Todos PARTICIPARAM do Grid (inclusive os ocultos) e foram renomeados.
            Assert.Equal("PP", maShape.Name);
            Assert.Equal("PP", feShape.Name);
            Assert.Equal("PP", cShape.Name);

            Assert.Equal("DESIGN", page.Name);
            Assert.Equal("SISBolt_", document.Name);
        }

        [Fact]
        public void PrepareGrid_LockedLayerWithShapes_FailsBeforeMutatingAnything()
        {
            var document = new FakeDocument();
            var page = document.ActivePageFake;
            var layer = (FakeLayer)page.CreateLayer("CAM1.PP.MA");
            var shape = layer.AddShape(new FakeShape { Name = "Base", Width = 10, Height = 10 });
            layer.Locked = true;

            var service = new GridPreparationService();
            var result = service.PrepareGrid(document);

            Assert.False(result.IsSuccess);
            Assert.Equal(OperationStatus.InvalidSelection, result.Status);
            Assert.Equal("Base", shape.Name); // nada foi tocado
            Assert.True(layer.Locked);
        }

        /// <summary>
        /// Regressão do bug relatado pelo usuário ("Preparar Grid não centraliza") — causa
        /// confirmada por teste direto no CorelDRAW real (não suposição): redimensionar a página
        /// desloca o referencial em que Shape.CenterX/CenterY são relidos, então reaproveitar um
        /// centro combinado calculado ANTES do resize produz uma translação errada. Ver o
        /// comentário de ResizePageToFitAndCenter em GridPreparationService.cs e
        /// docs/mvp-smoke-test.md. FakePage/FakeShape simulam esse deslocamento deliberadamente
        /// (ver seus comentários) para que este teste detecte a regressão sem precisar do Corel.
        /// </summary>
        [Fact]
        public void PrepareGrid_CentersCombinedBoundingBoxExactlyOnPageCenter()
        {
            var document = new FakeDocument();
            var page = document.ActivePageFake;
            var layer = (FakeLayer)page.CreateLayer("CAM1.PP.MA");

            var s1 = layer.AddShape(new FakeShape { Name = "A", Width = 20, Height = 10, CenterX = 10, CenterY = 5 });
            var s2 = layer.AddShape(new FakeShape { Name = "B", Width = 30, Height = 20, CenterX = 115, CenterY = 110 });

            var service = new GridPreparationService();
            var result = service.PrepareGrid(document);

            Assert.True(result.IsSuccess, result.Message);

            var left = Math.Min(s1.CenterX - s1.Width / 2, s2.CenterX - s2.Width / 2);
            var right = Math.Max(s1.CenterX + s1.Width / 2, s2.CenterX + s2.Width / 2);
            var bottom = Math.Min(s1.CenterY - s1.Height / 2, s2.CenterY - s2.Height / 2);
            var top = Math.Max(s1.CenterY + s1.Height / 2, s2.CenterY + s2.Height / 2);

            Assert.Equal(page.CenterX, (left + right) / 2, 6);
            Assert.Equal(page.CenterY, (bottom + top) / 2, 6);
        }

        [Fact]
        public void PrepareGrid_EmptyPage_SucceedsAsNoOp()
        {
            var document = new FakeDocument();
            var service = new GridPreparationService();

            var result = service.PrepareGrid(document);

            Assert.True(result.IsSuccess);
        }

        /// <summary>
        /// Garantia explícita do escopo de validação: mesmo que algo lance uma exceção no meio do
        /// processamento (ex.: falha de COM ao mover um objeto), a visibilidade original de TODAS
        /// as camadas precisa ser restaurada antes da exceção se propagar — nunca deixar uma
        /// camada ocultada pelo usuário visível por acidente após uma falha.
        /// </summary>
        [Fact]
        public void PrepareGrid_ExceptionDuringProcessing_StillRestoresVisibilityBeforePropagating()
        {
            var document = new FakeDocument();
            var page = document.ActivePageFake;

            var feLayer = (FakeLayer)page.CreateLayer("CAM1.PP.FE");
            var maLayer = (FakeLayer)page.CreateLayer("CAM1.PP.MA");

            feLayer.AddShape(new FakeShape { Name = "Frente", Width = 5, Height = 5 });
            maLayer.ShapesList.Add(new ThrowingOnMoveShape { Name = "Base", Width = 10, Height = 10 });

            feLayer.Visible = false; // estado alterado pelo usuário, deve permanecer oculto ao final.

            var service = new GridPreparationService();

            Assert.Throws<InvalidOperationException>(() => service.PrepareGrid(document));

            Assert.False(feLayer.Visible); // restaurado para o estado original (oculto), não ficou visível.
            Assert.True(maLayer.Visible);
        }

        /// <summary>Forma de teste que simula uma falha de COM ao tentar mover o objeto (ver teste acima).</summary>
        private sealed class ThrowingOnMoveShape : ICorelShape
        {
            public string Name { get; set; } = string.Empty;
            public ShapeKind Kind => ShapeKind.Rectangle;
            public bool Visible { get; set; } = true;
            public bool Locked { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double PositionX { get => 0; set => throw new InvalidOperationException("Falha simulada ao mover o objeto."); }
            public double PositionY { get; set; }
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public void ApplyStyleString(string style) { }
            public IReadOnlyList<ICorelShape> Shapes { get; } = new List<ICorelShape>();
            public void Delete() { }
            public void MoveToLayer(ICorelLayer layer) { }
        }
    }
}
