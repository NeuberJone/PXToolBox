using System.Collections.Generic;
using PXToolBox.Core.Measurement;
using PXToolBox.Outputs.Tools;
using Xunit;

namespace PXToolBox.Tests
{
    public class MeasurementSumServiceTests
    {
        [Fact]
        public void SumHeightOnly_SumsHeightsInMeters_FromCentimeters()
        {
            var service = new MeasurementSumService();
            var sizes = new[] { new ShapeSize(10, 150), new ShapeSize(20, 50) }; // cm

            var result = service.SumHeightOnly(sizes, DocumentUnit.Centimeter);

            Assert.True(result.IsSuccess);
            Assert.Equal(2.0, result.Value!.TotalHeightMeters, 3);
            Assert.Equal("2,00m", result.Value!.ClipboardText);
        }

        /// <summary>
        /// Corrige a suposição anterior (comparação com Medidas.txt mostrou que as DUAS macros
        /// originais usam o mesmo Select Case de unidades — não há "mm/cm/m só no atalho").
        /// "Somar Altura (m)" precisa funcionar com o documento em polegada/pé/jarda, igual ao
        /// modo com escolha.
        /// </summary>
        [Fact]
        public void SumHeightOnly_AcceptsInchFootYard_SameAsChoiceMode()
        {
            var service = new MeasurementSumService();
            var sizes = new[] { new ShapeSize(10, 100) }; // 100 polegadas

            var result = service.SumHeightOnly(sizes, DocumentUnit.Inch);

            Assert.True(result.IsSuccess, result.Message);
            Assert.Equal(2.54, result.Value!.TotalHeightMeters, 3);
        }

        [Fact]
        public void SumHeightOnly_NullUnit_Fails()
        {
            var service = new MeasurementSumService();
            var sizes = new[] { new ShapeSize(10, 10) };

            var result = service.SumHeightOnly(sizes, null);

            Assert.False(result.IsSuccess);
            Assert.Equal(Core.Result.OperationStatus.InconclusiveRead, result.Status);
        }

        [Fact]
        public void SumSelected_Both_BuildsWidthByHeightClipboardText()
        {
            var service = new MeasurementSumService();
            var sizes = new[] { new ShapeSize(width: 100, height: 200) }; // cm -> 1m x 2m

            var result = service.SumSelected(sizes, DocumentUnit.Centimeter, MeasurementSumMode.Both);

            Assert.True(result.IsSuccess);
            Assert.Equal("1,00x2,00m", result.Value!.ClipboardText);
        }

        [Fact]
        public void SumSelected_AcceptsInch()
        {
            var service = new MeasurementSumService();
            var sizes = new[] { new ShapeSize(0, 100) }; // 100 inches

            var result = service.SumSelected(sizes, DocumentUnit.Inch, MeasurementSumMode.Height);

            Assert.True(result.IsSuccess);
            Assert.Equal(2.54, result.Value!.TotalHeightMeters, 3);
        }

        [Fact]
        public void SumHeightOnly_EmptySelection_Fails()
        {
            var service = new MeasurementSumService();
            var result = service.SumHeightOnly(new List<ShapeSize>(), DocumentUnit.Meter);

            Assert.False(result.IsSuccess);
            Assert.Equal(Core.Result.OperationStatus.InvalidSelection, result.Status);
        }
    }
}
