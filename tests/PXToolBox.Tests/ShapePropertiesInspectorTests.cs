#if NET48
// Só compila na perna net48 — mesmo motivo de ListForgeIntegratedTests.cs (o host
// PXToolBoxDev, net48/WPF, só existe nessa perna). Testa a parte PURA (sem COM) da ferramenta
// "Inspecionar Shape.Properties": classificação de valor e formatação do relatório — a
// travessia COM em si (Count/ItemByIndex/Description/seleção) só é confirmável dentro do Corel
// real (ver docs/ARCHITECTURE.md e comentário de ShapePropertiesInspectorService).
using System;
using System.Threading;
using PXToolBoxDev.Diagnostics;
using Xunit;

namespace PXToolBox.Tests
{
    public class ShapePropertyValueFormatterTests
    {
        [Theory]
        [InlineData(true, "Boolean", "True")]
        [InlineData(false, "Boolean", "False")]
        public void Format_Boolean(bool value, string expectedType, string expectedDisplay)
        {
            var (type, display) = ShapePropertyValueFormatter.Format(value);
            Assert.Equal(expectedType, type);
            Assert.Equal(expectedDisplay, display);
        }

        [Fact]
        public void Format_Integer()
        {
            var (type, display) = ShapePropertyValueFormatter.Format(42);
            Assert.Equal("Integer", type);
            Assert.Equal("42", display);
        }

        [Fact]
        public void Format_Double()
        {
            var (type, display) = ShapePropertyValueFormatter.Format(3.5);
            Assert.Equal("Double", type);
            Assert.Equal("3.5", display);
        }

        [Fact]
        public void Format_String()
        {
            var (type, display) = ShapePropertyValueFormatter.Format("Tamanho padrão");
            Assert.Equal("String", type);
            Assert.Equal("Tamanho padrão", display);
        }

        [Fact]
        public void Format_Date()
        {
            var (type, display) = ShapePropertyValueFormatter.Format(new DateTime(2026, 10, 28, 9, 30, 0));
            Assert.Equal("Date", type);
            Assert.Equal("2026-10-28 09:30:00", display);
        }

        [Fact]
        public void Format_Null()
        {
            var (type, display) = ShapePropertyValueFormatter.Format(null);
            Assert.Equal("(nulo)", type);
            Assert.Equal("", display);
        }

        /// <summary>
        /// Tipo COM/struct desconhecido — nunca serializado, nunca chama ToString arbitrário
        /// além do padrão do .NET; só mostra o nome do tipo .NET real (pedido explícito da
        /// tarefa: "não chamar métodos destrutivos ou serializadores arbitrários").
        /// </summary>
        [Fact]
        public void Format_UnknownComOrStructType_ShowsSafeMarkerWithRealTypeName()
        {
            var (type, display) = ShapePropertyValueFormatter.Format(new byte[] { 1, 2, 3 });
            Assert.Equal("COM", type);
            Assert.Contains("Byte[]", display);
            Assert.StartsWith("<tipo COM:", display);
        }
    }

    public class ShapePropertiesReportFormatterTests
    {
        private static ShapePropertiesReport SampleReport() => new()
        {
            Status = ShapeSelectionStatus.Ok,
            Object = new ShapeObjectSummary
            {
                Name = "Logo",
                ShapeType = "cdrCurveShape",
                LayerName = "CAM1.G.MA",
                PageIndex = 1,
                StaticId = 12345,
                Width = 10.5,
                Height = 20.25,
            },
            Properties = new[]
            {
                new ShapePropertyEntry { Index = 0, Name = "SISBolt.TamanhoPadrao", Id = 1, ValueDisplay = "True", ValueType = "Boolean" },
                new ShapePropertyEntry { Index = 1, Name = "SISBolt.Outra", Id = 2, ReadError = "falha de leitura simulada" },
            },
        };

        [Fact]
        public void Build_IncludesObjectContextAndEachPropertyEntry()
        {
            var text = ShapePropertiesReportFormatter.Build(SampleReport());

            Assert.Contains("PX Shape.Properties Inspector", text);
            Assert.Contains("Name: Logo", text);
            Assert.Contains("Type: cdrCurveShape", text);
            Assert.Contains("Layer: CAM1.G.MA", text);
            Assert.Contains("StaticID: 12345", text);
            Assert.Contains("Count: 2", text);
            Assert.Contains("[0]", text);
            Assert.Contains("Name: SISBolt.TamanhoPadrao", text);
            Assert.Contains("ID: 1", text);
            Assert.Contains("Value: True", text);
            Assert.Contains("Type: Boolean", text);
            Assert.Contains("[1]", text);
            Assert.Contains("<erro ao ler: falha de leitura simulada>", text);
        }

        [Fact]
        public void Build_NoProperties_ShowsExplicitNotAnErrorMessage()
        {
            var report = new ShapePropertiesReport
            {
                Status = ShapeSelectionStatus.Ok,
                Object = new ShapeObjectSummary { Name = "Vazio" },
                Properties = Array.Empty<ShapePropertyEntry>(),
            };

            var text = ShapePropertiesReportFormatter.Build(report);

            Assert.Contains("Count: 0", text);
            Assert.Contains("Nenhuma Shape.Properties encontrada neste objeto.", text);
        }

        [Fact]
        public void Build_IncludesDiagnosticsHeader_ToolVersionAndBothUnits()
        {
            // Pedido explícito do usuário depois de uma investigação real precisar de várias
            // idas e voltas pra descobrir a versão instalada e qual unidade cada preferência
            // do Corel estava usando — mostrar os dois (Document.Unit e Application.Unit)
            // elimina essa pergunta nas próximas comparações.
            var report = SampleReport();
            report.Diagnostics = new ShapeDiagnosticContext
            {
                ToolVersion = "0.1.0-dev.41",
                DocumentName = "Sem título-1",
                DocumentUnitLabel = "milímetro (cdrMillimeter)",
                ApplicationUnitLabel = "polegada (cdrInch)",
            };

            var text = ShapePropertiesReportFormatter.Build(report);

            Assert.Contains("PX Tool Box: v0.1.0-dev.41", text);
            Assert.Contains("Documento: Sem título-1", text);
            Assert.Contains("Unidade do documento (Document.Unit): milímetro (cdrMillimeter)", text);
            Assert.Contains("Unidade do aplicativo (Application.Unit): polegada (cdrInch)", text);
        }

        [Fact]
        public void Build_IncludesRulersUnits_WhenDiagnosticsHasThem()
        {
            // Terceiro candidato real investigado: Document.Rulers.HUnits/VUnits, diferente de
            // Document.Unit e Application.Unit (ambas confirmadas por evidência real como
            // cdrInch mesmo com a UI mostrando cm, inclusive com o documento já salvo) — ver
            // StandardSizeService e ARCHITECTURE.md.
            var report = SampleReport();
            report.Diagnostics = new ShapeDiagnosticContext
            {
                ToolVersion = "0.1.0-dev.42",
                RulersHUnitLabel = "centímetro (cdrCentimeter)",
                RulersVUnitLabel = "centímetro (cdrCentimeter)",
            };

            var text = ShapePropertiesReportFormatter.Build(report);

            Assert.Contains("Unidade da régua horizontal (Document.Rulers.HUnits): centímetro (cdrCentimeter)", text);
            Assert.Contains("Unidade da régua vertical (Document.Rulers.VUnits): centímetro (cdrCentimeter)", text);
        }

        [Fact]
        public void Build_EmptyDiagnostics_OmitsDocumentLinesButStillShowsVersion()
        {
            // Sem documento/app (ex.: não conectado), os campos vazios de Diagnostics não
            // produzem linhas "Documento: "/"Unidade: " vazias e confusas — só a versão, que
            // é sempre resolvível mesmo sem Corel.
            var report = new ShapePropertiesReport
            {
                Status = ShapeSelectionStatus.NotConnected,
                StatusMessage = "Não conectado ao CorelDRAW.",
                Diagnostics = new ShapeDiagnosticContext { ToolVersion = "0.1.0-dev.41" },
            };

            var text = ShapePropertiesReportFormatter.Build(report);

            Assert.Contains("PX Tool Box: v0.1.0-dev.41", text);
            Assert.DoesNotContain("Documento:", text);
            Assert.DoesNotContain("Unidade do documento", text);
            Assert.DoesNotContain("Unidade do aplicativo", text);
            Assert.DoesNotContain("Unidade da régua", text);
        }
    }

    /// <summary>
    /// Regressão do mesmo padrão de crash corrigido em ClassicWorkspaceWindow (ver
    /// ARCHITECTURE.md, iteração 8) — sem campo editável aqui, mas a mesma disciplina de
    /// construir a janela real numa thread STA antes de confiar que ela abre sem lançar.
    /// </summary>
    public class ShapeInspectorWindowTests
    {
        [Fact]
        public void ConstructsWithoutThrowing_WhenNotConnectedToCorel()
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = new ShapeInspectorWindow(null);
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
    }
}
#endif
