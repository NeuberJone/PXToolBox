using System;
using System.Reflection;
using Corel.Interop.VGCore;
using CorelApplication = Corel.Interop.VGCore.Application;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Lê <c>Shape.Properties</c> do objeto único selecionado — 100% somente leitura. Nunca
    /// chama nenhum membro de escrita da API real confirmada por reflection sobre
    /// Corel.Interop.VGCore.dll (<c>IVGProperties.set_Item</c>, <c>Delete</c>,
    /// <c>DeleteByIndex</c>, <c>DeleteAll</c>, <c>PutFile</c>, <c>SetPoint</c>, <c>SetVector</c>,
    /// <c>SetCurve</c>) nem nada em <c>Shape</c> que altere nome/camada/documento. Usa
    /// <c>ItemByIndex</c>/<c>Description</c>/<c>Count</c> (todos de leitura, confirmados por
    /// reflection antes de implementar — ver docs/ARCHITECTURE.md).
    ///
    /// Acesso COM direto, deliberadamente FORA da abstração ICorelShape/ICorelWorkspace
    /// (PXToolBox.Outputs.Abstractions): esta é uma ferramenta de diagnóstico que expõe dados
    /// COM brutos por natureza (Shape.Properties, StaticID, cdrShapeType) — a abstração existe
    /// para isolar a lógica de NEGÓCIO das Saídas do COM, não para esconder justamente o que
    /// este inspetor precisa mostrar. Mesmo padrão de isolamento aplicado de outra forma: o
    /// acesso COM fica concentrado só nesta classe, nunca espalhado pela UI (ShapeInspectorWindow
    /// só chama este serviço, nunca VGCore diretamente).
    /// </summary>
    public sealed class ShapePropertiesInspectorService
    {
        /// <summary>Resolve o objeto único atualmente selecionado — devolve a referência COM
        /// real (para "Atualizar" continuar inspecionando o MESMO objeto depois, mesmo que a
        /// seleção no Corel mude) junto com o relatório já construído a partir dela. Quando o
        /// estado não permite abrir o inspetor (sem documento/seleção/múltiplos objetos),
        /// <paramref name="shape"/> fica nulo e o relatório devolvido só tem
        /// <see cref="ShapePropertiesReport.Status"/>/<see cref="ShapePropertiesReport.StatusMessage"/>
        /// preenchidos.</summary>
        public ShapePropertiesReport InspectActiveSelection(CorelApplication? app, out Shape? shape)
        {
            shape = null;
            var diagnostics = BuildDiagnosticContext(app);

            if (app == null)
            {
                return new ShapePropertiesReport
                {
                    Status = ShapeSelectionStatus.NotConnected,
                    StatusMessage = "Não conectado ao CorelDRAW.",
                    Diagnostics = diagnostics,
                };
            }

            Document? document;
            try
            {
                document = app.ActiveDocument;
            }
            catch (Exception)
            {
                document = null;
            }

            if (document == null)
            {
                return new ShapePropertiesReport
                {
                    Status = ShapeSelectionStatus.NoDocument,
                    StatusMessage = "Nenhum documento aberto.",
                    Diagnostics = diagnostics,
                };
            }

            ShapeRange? selection;
            try
            {
                selection = app.ActiveSelectionRange;
            }
            catch (Exception)
            {
                selection = null;
            }

            var count = selection?.Count ?? 0;
            if (count == 0)
            {
                return new ShapePropertiesReport
                {
                    Status = ShapeSelectionStatus.NoSelection,
                    StatusMessage = "Selecione um objeto para inspecionar.",
                    Diagnostics = diagnostics,
                };
            }

            if (count > 1)
            {
                return new ShapePropertiesReport
                {
                    Status = ShapeSelectionStatus.MultipleSelected,
                    StatusMessage = "Selecione apenas um objeto.",
                    Diagnostics = diagnostics,
                };
            }

            shape = (Shape)selection![1];
            return BuildReport(shape);
        }

        /// <summary>Reconstrói o relatório a partir da MESMA referência COM já aberta (botão
        /// "Atualizar") — nunca troca pelo objeto atualmente selecionado, para continuar
        /// inspecionando o mesmo objeto mesmo que a seleção no Corel tenha mudado.</summary>
        public ShapePropertiesReport BuildReport(Shape shape)
        {
            // shape.Layer.Application chega na MESMA Application sem precisar que quem chamou
            // (o botão "Atualizar") guarde uma referência separada — melhor esforço: se isto
            // falhar, os campos de diagnóstico ficam vazios, nunca interrompendo a inspeção.
            CorelApplication? app = null;
            try { app = shape.Layer?.Application; } catch (Exception) { /* diagnóstico é melhor esforço */ }
            var diagnostics = BuildDiagnosticContext(app);

            ShapeObjectSummary summary;
            try
            {
                summary = new ShapeObjectSummary
                {
                    Name = shape.Name ?? "",
                    ShapeType = shape.Type.ToString(),
                    LayerName = shape.Layer?.Name ?? "",
                    PageIndex = shape.Page?.Index ?? 0,
                    StaticId = shape.StaticID,
                    Width = shape.SizeWidth,
                    Height = shape.SizeHeight,
                };
            }
            catch (Exception ex)
            {
                // Qualquer falha já ao ler os dados básicos do próprio Shape (não de
                // Properties) indica objeto inválido/excluído — "Atualizar" chama este mesmo
                // método de novo mais tarde; não mantém nada além da referência já passada.
                return new ShapePropertiesReport
                {
                    Status = ShapeSelectionStatus.ShapeNoLongerAvailable,
                    StatusMessage = $"O objeto original não está mais disponível. ({ex.Message})",
                    Diagnostics = diagnostics,
                };
            }

            var entries = new System.Collections.Generic.List<ShapePropertyEntry>();
            Properties? properties = null;
            try
            {
                properties = shape.Properties;
            }
            catch (Exception)
            {
                // Shape.Properties em si inacessível — relatório ainda mostra o contexto do
                // objeto, só sem nenhuma propriedade (não é o mesmo que Count == 0 por não ter
                // propriedades — mas, sem uma forma segura de distinguir, trata como vazio em
                // vez de interromper a inspeção).
            }

            var count = 0;
            try
            {
                count = properties?.Count ?? 0;
            }
            catch (Exception)
            {
                count = 0;
            }

            // IVGProperties.ItemByIndex/Description são 1-based, não 0-based — confirmado
            // por evidência real (índice 0 falha com "objeto não encontrado" em toda
            // inspeção real já feita; índice 1 sempre lê a primeira entrada de verdade) e
            // pelo padrão já estabelecido em CorelCollections.cs para as demais coleções
            // VGCore (Shapes/Layers/Pages, todas 1-based). O laço anterior (0 a count-1)
            // nunca lia a ÚLTIMA entrada real e perdia uma tentativa inútil no índice 0.
            for (var i = 1; i <= count; i++)
            {
                entries.Add(ReadEntry(properties!, i));
            }

            return new ShapePropertiesReport
            {
                Status = ShapeSelectionStatus.Ok,
                Diagnostics = diagnostics,
                Object = summary,
                Properties = entries,
            };
        }

        /// <summary>Versão do PX Tool Box (mesma fonte de ControlUI.ResolveAppVersion) e as
        /// DUAS unidades reais do Corel (Document.Unit e Application.Unit — confirmado por
        /// evidência real que NENHUMA das duas é a que o SISBolt usa para Tamanho Padrão; ver
        /// StandardSizeService) — pedido explícito do usuário depois de uma investigação real
        /// ter precisado de várias idas e voltas só pra descobrir isso. Melhor esforço: cada
        /// campo fica vazio se não puder ser lido, nunca lança.</summary>
        private static ShapeDiagnosticContext BuildDiagnosticContext(CorelApplication? app)
        {
            var context = new ShapeDiagnosticContext { ToolVersion = ResolveToolVersion() };
            if (app == null) return context;

            try { context.ApplicationUnitLabel = FormatUnit(app.Unit); } catch (Exception) { /* melhor esforço */ }

            try
            {
                var document = app.ActiveDocument;
                if (document != null)
                {
                    context.DocumentName = document.Name ?? "";
                    context.DocumentUnitLabel = FormatUnit(document.Unit);

                    try
                    {
                        var rulers = document.Rulers;
                        if (rulers != null)
                        {
                            context.RulersHUnitLabel = FormatUnit(rulers.HUnits);
                            context.RulersVUnitLabel = FormatUnit(rulers.VUnits);
                        }
                    }
                    catch (Exception) { /* melhor esforço */ }
                }
            }
            catch (Exception) { /* melhor esforço */ }

            return context;
        }

        internal static string ResolveToolVersion() =>
            typeof(ShapePropertiesInspectorService).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
            ?? "0.0.0-dev.local";

        internal static string FormatUnit(cdrUnit unit)
        {
            var label = unit switch
            {
                cdrUnit.cdrMillimeter => "milímetro",
                cdrUnit.cdrCentimeter => "centímetro",
                cdrUnit.cdrMeter => "metro",
                cdrUnit.cdrInch => "polegada",
                cdrUnit.cdrFoot => "pé",
                cdrUnit.cdrYard => "jarda",
                cdrUnit.cdrPoint => "ponto",
                cdrUnit.cdrPixel => "pixel",
                _ => "não traduzida pelo PX",
            };
            return $"{label} ({unit})";
        }

        internal static ShapePropertyEntry ReadEntry(Properties properties, int index)
        {
            var entry = new ShapePropertyEntry { Index = index };
            try
            {
                string name = "";
                int id = 0;
                properties.Description(index, out name, out id);
                entry.Name = name ?? "";
                entry.Id = id;

                var value = properties.ItemByIndex[index];
                var (valueType, display) = ShapePropertyValueFormatter.Format(value);
                entry.ValueType = valueType;
                entry.ValueDisplay = display;
            }
            catch (Exception ex)
            {
                // Uma propriedade impossível de ler não pode impedir a inspeção das demais
                // (pedido explícito da tarefa) — a entrada fica com o erro registrado, e o
                // laço em BuildReport continua para o próximo índice normalmente.
                entry.ReadError = ex.Message;
            }

            return entry;
        }
    }
}
