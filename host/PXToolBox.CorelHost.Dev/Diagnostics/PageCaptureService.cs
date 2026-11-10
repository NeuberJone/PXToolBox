using System;
using System.Collections.Generic;
using Corel.Interop.VGCore;
using CorelApplication = Corel.Interop.VGCore.Application;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Captura a página ativa para a comparação antes/depois (Diagnóstico → "Capturar estado"),
    /// usada para observar o que uma ferramenta do SISBolt muda — mesmo método que descobriu o
    /// contrato do Tamanho Padrão (KEEP_DESIGN_SIZE), agora para a página inteira e não só um
    /// objeto. 100% SOMENTE LEITURA: só getters (Count, Item, Description, ItemByIndex, Name,
    /// Position*, Size*, RotationAngle, Selected…) — nenhum set, Delete, Add, Clear ou
    /// BeginCommandGroup. Mesmo isolamento de <see cref="ShapePropertiesInspectorService"/>: COM
    /// direto só nesta classe.
    ///
    /// Membros confirmados por reflection sobre Corel.Interop.VGCore.dll do CorelDRAW 27 antes
    /// de implementar: Document/Page/Layer/Shape.Properties, Page.AllLayers, Layer.Master,
    /// Shape.Shapes/PowerClip/ParentGroup/ObjectData/Text.Story.Text/RotationAngle/Selected.
    /// Nunca lança: o que não puder ser lido vira aviso/erro de leitura no próprio item.
    /// </summary>
    public static class PageCaptureService
    {
        /// <summary>Acima disso a captura para (documento de teste deve ser pequeno).</summary>
        public const int MaxShapes = 3000;

        public static PageCapture? Capture(CorelApplication? app, out string? failure)
        {
            failure = null;
            Document? document = null;
            try { document = app?.ActiveDocument; } catch (Exception) { /* abaixo */ }
            if (app == null)
            {
                failure = "Não conectado ao CorelDRAW.";
                return null;
            }

            if (document == null)
            {
                failure = "Nenhum documento aberto.";
                return null;
            }

            Page page;
            try
            {
                page = document.ActivePage;
            }
            catch (Exception ex)
            {
                failure = $"Não foi possível ler a página ativa: {ex.Message}";
                return null;
            }

            var warnings = new List<string>();
            var capture = new PageCapture
            {
                CapturedAt = DateTimeOffset.UtcNow,
                ToolVersion = ShapePropertiesInspectorService.ResolveToolVersion(),
                DocumentName = Safe(() => document.Name, ""),
                PageIndex = Safe(() => page.Index, 0),
                PageName = Safe(() => page.Name, ""),
                RulerUnit = Safe(() => ShapePropertiesInspectorService.FormatUnit(document.Rulers.HUnits), ""),
                RulerUnitCode = Safe(() => document.Rulers.HUnits.ToString(), ""),
                // Só leitura de Document.Unit — nunca alterada aqui (ver PageCapture.GeometryUnitCode).
                GeometryUnitCode = Safe(() => document.Unit.ToString(), ""),
                PageWidth = Safe(() => page.SizeWidth, 0d),
                PageHeight = Safe(() => page.SizeHeight, 0d),
                WorldScale = Safe(() => document.WorldScale, 1d),
                DocumentProperties = ReadProperties(() => document.Properties, "Documento.Properties", warnings),
                PageProperties = ReadProperties(() => page.Properties, "Página.Properties", warnings),
            };

            var layers = new List<CapturedLayer>();
            var shapes = new List<CapturedShape>();
            var truncated = false;
            Layers? allLayers = null;
            try { allLayers = page.AllLayers; } catch (Exception ex) { warnings.Add($"Camadas: {ex.Message}"); }
            var layerCount = Safe(() => allLayers?.Count ?? 0, 0);
            for (var i = 1; i <= layerCount; i++)
            {
                Layer layer;
                try { layer = allLayers![i]; }
                catch (Exception ex) { warnings.Add($"Camada {i}: {ex.Message}"); continue; }

                var layerName = Safe(() => layer.Name, $"camada {i}");
                layers.Add(new CapturedLayer
                {
                    Name = layerName,
                    Visible = Safe(() => layer.Visible, false),
                    Master = Safe(() => layer.Master, false),
                    Properties = ReadProperties(() => layer.Properties, $"Camada {layerName}.Properties", warnings),
                });
                truncated |= !VisitAll(Safe<Shapes?>(() => layer.Shapes, null), layerName, layerName, null, "", shapes, warnings);
            }

            capture.Layers = layers;
            capture.Shapes = shapes;
            capture.Truncated = truncated;
            capture.Warnings = warnings;
            return capture;
        }

        // Falso quando parou no limite.
        private static bool VisitAll(Shapes? children, string layerName, string path, int? parentId, string parentKind, List<CapturedShape> output, List<string> warnings)
        {
            var count = Safe(() => children?.Count ?? 0, 0);
            for (var i = 1; i <= count; i++)
            {
                if (output.Count >= MaxShapes) return false;

                Shape shape;
                try { shape = children![i]; }
                catch (Exception ex) { warnings.Add($"{path}, item {i}: {ex.Message}"); continue; }

                var captured = Read(shape, layerName, path, parentId, parentKind, i);
                output.Add(captured);
                var here = $"{path} › {Label(captured)}";

                if (captured.Type == nameof(cdrShapeType.cdrGroupShape)
                    && !VisitAll(Safe<Shapes?>(() => shape.Shapes, null), layerName, here, captured.StaticId, "grupo", output, warnings))
                {
                    return false;
                }

                if (captured.HasPowerClip
                    && !VisitAll(Safe<Shapes?>(() => shape.PowerClip?.Shapes, null), layerName, here + " › PowerClip", captured.StaticId, "PowerClip", output, warnings))
                {
                    return false;
                }
            }

            return true;
        }

        private static CapturedShape Read(Shape shape, string layerName, string path, int? parentId, string parentKind, int order)
        {
            var errors = new List<string>();
            T Get<T>(Func<T> read, T fallback, string what)
            {
                try { return read(); }
                catch (Exception ex) { errors.Add($"{what}: {ex.Message}"); return fallback; }
            }

            var type = Get(() => shape.Type, cdrShapeType.cdrNoShape, "tipo");
            var captured = new CapturedShape
            {
                StaticId = Get(() => shape.StaticID, 0, "StaticID"),
                Path = path,
                ParentStaticId = parentId,
                ParentKind = parentKind,
                Order = order,
                Type = type.ToString(),
                Name = Get(() => shape.Name ?? "", "", "nome"),
                Layer = Get(() => shape.Layer?.Name ?? layerName, layerName, "camada"),
                PositionX = Get(() => shape.PositionX, 0d, "posição X"),
                PositionY = Get(() => shape.PositionY, 0d, "posição Y"),
                Width = Get(() => shape.SizeWidth, 0d, "largura"),
                Height = Get(() => shape.SizeHeight, 0d, "altura"),
                Rotation = Get(() => shape.RotationAngle, 0d, "rotação"),
                Selected = Get(() => shape.Selected, false, "seleção"),
                Visible = Get(() => shape.Visible, false, "visível"),
                Locked = Get(() => shape.Locked, false, "bloqueado"),
                HasPowerClip = Get(() => shape.PowerClip != null, false, "PowerClip"),
            };

            if (type == cdrShapeType.cdrTextShape)
            {
                var text = Get(() => shape.Text?.Story?.Text ?? "", "", "texto");
                captured.Text = text.Length > 120 ? text.Substring(0, 120) + "…" : text;
            }

            var propertyErrors = new List<string>();
            captured.Properties = ReadProperties(() => shape.Properties, "Properties", propertyErrors);
            errors.AddRange(propertyErrors);
            captured.ObjectData = Get(() => ReadObjectData(shape), new List<KeyValuePair<string, string>>(), "ObjectData");
            captured.ReadError = errors.Count == 0 ? null : string.Join("; ", errors);
            return captured;
        }

        private static List<KeyValuePair<string, string>> ReadObjectData(Shape shape)
        {
            var result = new List<KeyValuePair<string, string>>();
            var items = shape.ObjectData;
            var count = items?.Count ?? 0;
            for (var i = 1; i <= count; i++)
            {
                var item = items![i];
                string name;
                try { name = item.DataField?.Name ?? $"campo {i}"; } catch (Exception) { name = $"campo {i}"; }
                string value;
                try { value = item.FormattedValue ?? ""; } catch (Exception ex) { value = $"(erro: {ex.Message})"; }
                // Campos padrão vazios (Nome, Custo, Comentários…) não dizem nada — só os preenchidos.
                if (value.Length > 0) result.Add(new KeyValuePair<string, string>(name, value));
            }

            return result;
        }

        private static IReadOnlyList<ShapePropertyEntry> ReadProperties(Func<Properties?> get, string what, List<string> warnings)
        {
            Properties? properties;
            int count;
            try
            {
                properties = get();
                count = properties?.Count ?? 0;
            }
            catch (Exception ex)
            {
                warnings.Add($"{what}: {ex.Message}");
                return Array.Empty<ShapePropertyEntry>();
            }

            var entries = new List<ShapePropertyEntry>();
            for (var i = 1; i <= count; i++)
            {
                entries.Add(ShapePropertiesInspectorService.ReadEntry(properties!, i));
            }

            return entries;
        }

        private static string Label(CapturedShape shape) =>
            string.IsNullOrEmpty(shape.Name) ? $"#{shape.StaticId}" : $"\"{shape.Name}\" #{shape.StaticId}";

        private static T Safe<T>(Func<T> read, T fallback)
        {
            try { return read(); }
            catch (Exception) { return fallback; }
        }
    }
}
