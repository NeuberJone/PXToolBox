using System.Collections.Generic;
using PXToolBox.Core.Measurement;
using PXToolBox.Outputs.Tools;
using Xunit;

namespace PXToolBox.Tests
{
    public class MeasurementSumServiceTests
    {
        /// <summary>
        /// Equivalente ao antigo SumHeightOnly_SumsHeightsInMeters_FromCentimeters — "Somar
        /// Altura (m)" foi removido por ser uma duplicata exata deste modo (ver comentário de
        /// classe em MeasurementSumService): os dois sempre somam em metros, qualquer unidade do
        /// documento.
        /// </summary>
        [Fact]
        public void SumSelected_Height_SumsInMeters_FromCentimeters()
        {
            var service = new MeasurementSumService();
            var sizes = new[] { new ShapeSize(10, 150), new ShapeSize(20, 50) }; // cm

            var result = service.SumSelected(sizes, DocumentUnit.Centimeter, MeasurementSumMode.Height);

            Assert.True(result.IsSuccess);
            Assert.Equal(2.0, result.Value!.TotalHeightMeters, 3);
            Assert.Equal("2,00m", result.Value!.ClipboardText);
        }

        [Fact]
        public void SumSelected_Height_NullUnit_Fails()
        {
            var service = new MeasurementSumService();
            var sizes = new[] { new ShapeSize(10, 10) };

            var result = service.SumSelected(sizes, null, MeasurementSumMode.Height);

            Assert.False(result.IsSuccess);
            Assert.Equal(Core.Result.OperationStatus.InconclusiveRead, result.Status);
        }

        [Fact]
        public void SumSelected_Height_EmptySelection_Fails()
        {
            var service = new MeasurementSumService();

            var result = service.SumSelected(new List<ShapeSize>(), DocumentUnit.Meter, MeasurementSumMode.Height);

            Assert.False(result.IsSuccess);
            Assert.Equal(Core.Result.OperationStatus.InvalidSelection, result.Status);
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
    }
}
