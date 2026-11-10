using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>Um objeto da página capturado (ver <see cref="PageCaptureService"/>). Posição e
    /// tamanho são os valores BRUTOS da API, na unidade efetiva da geometria da captura
    /// (<see cref="PageCapture.GeometryUnitCode"/>) — a comparação converte antes de comparar
    /// (<see cref="GeometryScale"/>). Rotação é sempre em graus.</summary>
    public sealed class CapturedShape
    {
        public int StaticId { get; set; }

        /// <summary>Onde o objeto está: camada, grupos e PowerClips acima dele.</summary>
        public string Path { get; set; } = "";

        /// <summary>StaticID do grupo/PowerClip que contém o objeto; null no nível da camada.</summary>
        public int? ParentStaticId { get; set; }

        /// <summary>"grupo" ou "PowerClip" quando <see cref="ParentStaticId"/> existe.</summary>
        public string ParentKind { get; set; } = "";

        /// <summary>Posição entre os irmãos (1 = primeiro, ordem do Corel).</summary>
        public int Order { get; set; }

        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public string Layer { get; set; } = "";
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Rotation { get; set; }
        public bool Selected { get; set; }
        public bool Visible { get; set; }
        public bool Locked { get; set; }
        public bool HasPowerClip { get; set; }

        /// <summary>Texto (até 120 caracteres), só de objetos de texto.</summary>
        public string? Text { get; set; }

        public IReadOnlyList<ShapePropertyEntry> Properties { get; set; } = Array.Empty<ShapePropertyEntry>();

        /// <summary>Campos do Gerenciador de Dados do Objeto (ObjectData), nome → valor.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> ObjectData { get; set; } = Array.Empty<KeyValuePair<string, string>>();

        /// <summary>Falha ao ler algum dado do objeto (o resto foi lido).</summary>
        public string? ReadError { get; set; }
    }

    public sealed class CapturedLayer
    {
        public string Name { get; set; } = "";
        public bool Visible { get; set; }
        public bool Master { get; set; }
        public IReadOnlyList<ShapePropertyEntry> Properties { get; set; } = Array.Empty<ShapePropertyEntry>();
    }

    /// <summary>Foto, só leitura, da página ativa: propriedades do documento, da página e das
    /// camadas e todos os objetos (inclusive dentro de grupos e PowerClips).</summary>
    public sealed class PageCapture
    {
        public DateTimeOffset CapturedAt { get; set; }
        public string ToolVersion { get; set; } = "";
        public string DocumentName { get; set; } = "";
        public int PageIndex { get; set; }
        public string PageName { get; set; } = "";

        /// <summary>Unidade da régua (Document.Rulers.HUnits) — a que a interface mostra; rótulo
        /// legível. É também a unidade em que o SISBolt grava KEEP_DESIGN_SIZE.</summary>
        public string RulerUnit { get; set; } = "";

        /// <summary>Nome técnico (cdrXxx) da unidade da régua; "" se não foi lida.</summary>
        public string RulerUnitCode { get; set; } = "";

        /// <summary>
        /// Unidade EFETIVA das leituras de geometria (Position*/Size*, inclusive o tamanho da
        /// página) nesta captura: <c>Document.Unit</c>, a unidade que o modelo de automação do
        /// Corel usa para medidas — distinta da régua. Evidência real (docs/ARCHITECTURE.md,
        /// 26-30): documento com régua em cm, Document.Unit = cdrInch e SizeWidth/SizeHeight
        /// em polegadas (fator 2,54 até a régua). Uma ferramenta (o SISBolt, por exemplo) pode
        /// mudar Document.Unit, e aí a mesma geometria volta em outra unidade — por isso é lida
        /// em CADA captura. "" = não foi possível ler (comparação geométrica inconclusiva).
        /// </summary>
        public string GeometryUnitCode { get; set; } = "";

        /// <summary>Tamanho da página, bruto, na unidade efetiva — para conferir no Corel que a
        /// conversão bate com o que a interface mostra.</summary>
        public double PageWidth { get; set; }
        public double PageHeight { get; set; }

        /// <summary>Document.WorldScale (escala de desenho); 1 = sem escala. Só informativo.</summary>
        public double WorldScale { get; set; } = 1;
        public IReadOnlyList<ShapePropertyEntry> DocumentProperties { get; set; } = Array.Empty<ShapePropertyEntry>();
        public IReadOnlyList<ShapePropertyEntry> PageProperties { get; set; } = Array.Empty<ShapePropertyEntry>();
        public IReadOnlyList<CapturedLayer> Layers { get; set; } = Array.Empty<CapturedLayer>();
        public IReadOnlyList<CapturedShape> Shapes { get; set; } = Array.Empty<CapturedShape>();

        /// <summary>Página com mais objetos que o limite: o resto não foi capturado.</summary>
        public bool Truncated { get; set; }

        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Unidades de medida do Corel (nome cdrXxx) com tamanho fixo em polegadas. As que dependem
    /// de outra coisa (cdrPixel: resolução) ou cuja definição no Corel não foi confirmada
    /// (didot, cícero, ágata, Q, H) ficam de fora: tratadas como desconhecidas — nunca
    /// convertidas por suposição.
    /// </summary>
    public static class CorelUnits
    {
        private static readonly Dictionary<string, (double InchesPerUnit, string Abbreviation)> Known = new(StringComparer.Ordinal)
        {
            ["cdrInch"] = (1, "pol"),
            ["cdrMillimeter"] = (1 / 25.4, "mm"),
            ["cdrCentimeter"] = (1 / 2.54, "cm"),
            ["cdrMeter"] = (1 / 0.0254, "m"),
            ["cdrKilometer"] = (1000 / 0.0254, "km"),
            ["cdrTenthMicron"] = (1e-7 / 0.0254, "0,1 µm"),
            ["cdrFoot"] = (12, "pé"),
            ["cdrYard"] = (36, "jd"),
            ["cdrMile"] = (63360, "mi"),
            ["cdrPoint"] = (1 / 72.0, "pt"),
            ["cdrPica"] = (1 / 6.0, "pica"),
        };

        /// <summary>Polegadas por unidade; null se a unidade não é conhecida (ou vazia).</summary>
        public static double? InchesPerUnit(string? code) =>
            code != null && Known.TryGetValue(code, out var unit) ? unit.InchesPerUnit : (double?)null;

        public static string Abbreviation(string? code) =>
            code != null && Known.TryGetValue(code, out var unit) ? unit.Abbreviation : (string.IsNullOrEmpty(code) ? "?" : code!);
    }

    /// <summary>
    /// Como comparar a geometria de duas capturas: cada lado na sua unidade efetiva, convertidos
    /// para polegadas antes de comparar, e mostrados na unidade da régua da captura "depois"
    /// (o que o usuário vê no Corel). Se a unidade efetiva de algum lado for desconhecida, a
    /// comparação geométrica é INCONCLUSIVA — os valores brutos aparecem, sem afirmar mudança.
    /// Só a geometria é convertida: Properties e ObjectData ficam sempre com os valores brutos.
    /// </summary>
    public sealed class GeometryScale
    {
        public GeometryScale(string beforeUnitCode, string afterUnitCode, string displayUnitCode)
        {
            BeforeUnitCode = beforeUnitCode;
            AfterUnitCode = afterUnitCode;
            BeforeInchesPerUnit = CorelUnits.InchesPerUnit(beforeUnitCode);
            AfterInchesPerUnit = CorelUnits.InchesPerUnit(afterUnitCode);
            // Régua desconhecida: mostra em polegadas (ainda é uma unidade honesta).
            var display = CorelUnits.InchesPerUnit(displayUnitCode) != null ? displayUnitCode : "cdrInch";
            DisplayInchesPerUnit = CorelUnits.InchesPerUnit(display)!.Value;
            DisplayLabel = CorelUnits.Abbreviation(display);
        }

        public static GeometryScale For(PageCapture before, PageCapture after) =>
            new GeometryScale(before.GeometryUnitCode, after.GeometryUnitCode, after.RulerUnitCode);

        public string BeforeUnitCode { get; }
        public string AfterUnitCode { get; }
        public double? BeforeInchesPerUnit { get; }
        public double? AfterInchesPerUnit { get; }
        public double DisplayInchesPerUnit { get; }
        public string DisplayLabel { get; }

        public bool IsConclusive => BeforeInchesPerUnit != null && AfterInchesPerUnit != null;

        public bool UnitChanged => BeforeUnitCode != AfterUnitCode;

        /// <summary>Valor bruto de um lado → unidade de exibição; null se a unidade é desconhecida.</summary>
        public double? ToDisplay(double raw, bool after)
        {
            var factor = after ? AfterInchesPerUnit : BeforeInchesPerUnit;
            return factor == null ? (double?)null : raw * factor.Value / DisplayInchesPerUnit;
        }
    }

    /// <summary>
    /// Comparação antes/depois de uma ferramenta (do SISBolt ou qualquer outra) — texto para o
    /// usuário copiar e mandar. Registra só o que foi OBSERVADO mudar: propriedades
    /// (Shape/Página/Camada/Documento.Properties), ObjectData, nome, tipo, posição, tamanho,
    /// rotação, grupo/PowerClip, ordem, seleção, visibilidade, bloqueio e texto. Objetos são
    /// casados pelo StaticID (único no documento; objeto novo ganha um novo).
    /// </summary>
    public static class PageCaptureDiff
    {
        private const double Tolerance = 1e-6;

        // Diferença bruta mínima para mencionar quando a unidade é desconhecida.
        private const double RawTolerance = 1e-9;

        public static string Report(PageCapture before, PageCapture after, string? toolLabel = null)
        {
            var text = new StringBuilder();
            text.AppendLine("PX Tool Box DEV — comparação antes/depois (só leitura)");
            if (!string.IsNullOrWhiteSpace(toolLabel)) text.AppendLine($"Ferramenta testada: {toolLabel!.Trim()}");
            text.AppendLine($"Versão do PX: {after.ToolVersion}");
            text.AppendLine($"Documento: {after.DocumentName} · página {after.PageIndex}{Named(after.PageName)}");
            text.AppendLine($"Antes: {Stamp(before.CapturedAt)} · depois: {Stamp(after.CapturedAt)}");
            text.AppendLine($"Objetos: {before.Shapes.Count} antes, {after.Shapes.Count} depois");
            var scale = GeometryScale.For(before, after);
            text.AppendLine($"Unidade da régua (Document.Rulers.HUnits): antes {Unit(before.RulerUnit)} · depois {Unit(after.RulerUnit)}");
            text.AppendLine($"Unidade efetiva da geometria (Document.Unit): antes {Unit(before.GeometryUnitCode)} · depois {Unit(after.GeometryUnitCode)}");
            if (!scale.IsConclusive)
            {
                text.AppendLine("Comparação geométrica INCONCLUSIVA: a unidade efetiva da geometria não é conhecida em pelo menos uma");
                text.AppendLine("captura — posição e tamanho aparecem só como valores brutos, sem afirmar que mudaram.");
            }
            else
            {
                if (scale.UnitChanged)
                {
                    text.AppendLine($"Document.Unit mudou entre as capturas ({before.GeometryUnitCode} → {after.GeometryUnitCode}): a mesma geometria volta em outra");
                    text.AppendLine("unidade. Posição e tamanho foram convertidos para a mesma unidade antes de comparar.");
                }

                text.AppendLine($"Posição e tamanho mostrados em {scale.DisplayLabel} (convertidos); Properties e ObjectData com os valores brutos.");
                text.AppendLine($"Página: antes {Size(scale, before.PageWidth, before.PageHeight, false)} · depois {Size(scale, after.PageWidth, after.PageHeight, true)} — confira com o tamanho da página no Corel.");
            }

            if (Math.Abs(before.WorldScale - 1) > Tolerance || Math.Abs(after.WorldScale - 1) > Tolerance)
            {
                text.AppendLine($"Escala de desenho (WorldScale): antes {N(before.WorldScale)} · depois {N(after.WorldScale)} — valores sem a escala aplicada.");
            }

            if (before.DocumentName != after.DocumentName || before.PageIndex != after.PageIndex)
            {
                text.AppendLine($"ATENÇÃO: a captura \"antes\" foi de outro documento/página ({before.DocumentName} · página {before.PageIndex}).");
            }

            foreach (var warning in before.Warnings.Select(w => "antes: " + w).Concat(after.Warnings.Select(w => "depois: " + w)))
            {
                text.AppendLine($"Aviso de leitura — {warning}");
            }

            if (before.Truncated || after.Truncated)
            {
                text.AppendLine("ATENÇÃO: página com muitos objetos — só os primeiros foram capturados. Teste num documento pequeno.");
            }

            var changes = new StringBuilder();
            if (before.GeometryUnitCode != after.GeometryUnitCode)
                changes.AppendLine($"Documento.Unit: {Unit(before.GeometryUnitCode)} → {Unit(after.GeometryUnitCode)}");
            if (before.RulerUnitCode != after.RulerUnitCode)
                changes.AppendLine($"Unidade da régua: {Unit(before.RulerUnit)} → {Unit(after.RulerUnit)}");
            AppendPropertyChanges(changes, "Documento.Properties", before.DocumentProperties, after.DocumentProperties);
            AppendPropertyChanges(changes, "Página.Properties", before.PageProperties, after.PageProperties);
            AppendLayerChanges(changes, before.Layers, after.Layers);
            AppendShapeChanges(changes, before, after, scale);

            text.AppendLine();
            if (changes.Length == 0)
            {
                text.AppendLine("Nenhuma diferença encontrada. Se a ferramenta deveria ter mudado algo, ela pode guardar a");
                text.AppendLine("informação fora do que o PX lê (na configuração do próprio programa, por exemplo).");
            }
            else
            {
                text.AppendLine("== Diferenças ==");
                text.Append(changes);
            }

            text.AppendLine();
            text.AppendLine("== Selecionados depois (estado completo) ==");
            var selected = after.Shapes.Where(s => s.Selected).ToList();
            if (selected.Count == 0) text.AppendLine("(nenhum)");
            foreach (var shape in selected)
            {
                AppendFullShape(text, shape, scale, after);
            }

            return text.ToString();
        }

        private static void AppendPropertyChanges(StringBuilder text, string title, IReadOnlyList<ShapePropertyEntry> before, IReadOnlyList<ShapePropertyEntry> after)
        {
            var lines = PropertyChanges(before, after);
            if (lines.Count == 0) return;
            text.AppendLine(title + ":");
            foreach (var line in lines) text.AppendLine("  " + line);
        }

        /// <summary>"+ NOME [id] Tipo = valor", "- …", "~ NOME [id]: antes → depois". Chave:
        /// nome + id (como a API: Item[Name, ID]).</summary>
        public static IReadOnlyList<string> PropertyChanges(IReadOnlyList<ShapePropertyEntry> before, IReadOnlyList<ShapePropertyEntry> after)
        {
            var lines = new List<string>();
            var old = Index(before);
            var now = Index(after);
            foreach (var pair in now)
            {
                if (!old.TryGetValue(pair.Key, out var previous))
                {
                    lines.Add($"+ {Describe(pair.Value)}");
                }
                else if (ValueOf(previous) != ValueOf(pair.Value))
                {
                    lines.Add($"~ {Key(pair.Value)}: {ValueOf(previous)} → {ValueOf(pair.Value)}");
                }
            }

            lines.AddRange(old.Where(p => !now.ContainsKey(p.Key)).Select(p => $"- {Describe(p.Value)}"));
            return lines;
        }

        private static void AppendLayerChanges(StringBuilder text, IReadOnlyList<CapturedLayer> before, IReadOnlyList<CapturedLayer> after)
        {
            foreach (var layer in after)
            {
                var previous = before.FirstOrDefault(l => l.Name == layer.Name && l.Master == layer.Master);
                if (previous == null)
                {
                    text.AppendLine($"Camada nova: {layer.Name}{(layer.Master ? " (mestre)" : "")}");
                    foreach (var p in layer.Properties) text.AppendLine($"  + {Describe(p)}");
                    continue;
                }

                var lines = PropertyChanges(previous.Properties, layer.Properties).ToList();
                if (previous.Visible != layer.Visible) lines.Insert(0, $"Visível: {YesNo(previous.Visible)} → {YesNo(layer.Visible)}");
                if (lines.Count == 0) continue;
                text.AppendLine($"Camada {layer.Name}:");
                foreach (var line in lines) text.AppendLine("  " + line);
            }

            foreach (var gone in before.Where(l => !after.Any(a => a.Name == l.Name && a.Master == l.Master)))
            {
                text.AppendLine($"Camada removida: {gone.Name}");
            }
        }

        private static void AppendShapeChanges(StringBuilder text, PageCapture before, PageCapture after, GeometryScale scale)
        {
            var old = before.Shapes.GroupBy(s => s.StaticId).ToDictionary(g => g.Key, g => g.First());
            var now = after.Shapes.GroupBy(s => s.StaticId).ToDictionary(g => g.Key, g => g.First());

            foreach (var shape in after.Shapes.Where(s => !old.ContainsKey(s.StaticId)))
            {
                text.AppendLine($"Objeto NOVO {Title(shape)}");
                AppendShapeDetails(text, shape, scale, after);
            }

            foreach (var shape in after.Shapes.Where(s => old.ContainsKey(s.StaticId)))
            {
                var lines = ShapeChanges(old[shape.StaticId], shape, scale);
                if (lines.Count == 0) continue;
                text.AppendLine($"Objeto alterado {Title(shape)}");
                foreach (var line in lines) text.AppendLine("  " + line);
            }

            foreach (var shape in before.Shapes.Where(s => !now.ContainsKey(s.StaticId)))
            {
                text.AppendLine($"Objeto REMOVIDO {Title(shape)}");
            }
        }

        public static IReadOnlyList<string> ShapeChanges(CapturedShape before, CapturedShape after, GeometryScale scale)
        {
            var lines = new List<string>();
            void Text(string label, string? a, string? b)
            {
                if ((a ?? "") != (b ?? "")) lines.Add($"{label}: \"{a}\" → \"{b}\"");
            }

            void Number(string label, double a, double b)
            {
                if (Math.Abs(a - b) > Tolerance) lines.Add($"{label}: {N(a)} → {N(b)} (diferença {N(b - a)})");
            }

            // Geometria: compara em polegadas (cada lado na sua unidade efetiva); mostra na
            // unidade de exibição. Unidade desconhecida: só os brutos, marcados inconclusivos.
            void Geometry(string label, double a, double b)
            {
                if (!scale.IsConclusive)
                {
                    if (Math.Abs(a - b) > RawTolerance)
                        lines.Add($"{label} (bruto, unidade desconhecida): {N(a)} → {N(b)} — inconclusivo");
                    return;
                }

                var inchesA = a * scale.BeforeInchesPerUnit!.Value;
                var inchesB = b * scale.AfterInchesPerUnit!.Value;
                if (Math.Abs(inchesA - inchesB) <= Tolerance) return;

                var shownA = scale.ToDisplay(a, after: false)!.Value;
                var shownB = scale.ToDisplay(b, after: true)!.Value;
                lines.Add($"{label}: {N(shownA)} → {N(shownB)} {scale.DisplayLabel} (diferença {N(shownB - shownA)})");
            }

            void Flag(string label, bool a, bool b)
            {
                if (a != b) lines.Add($"{label}: {YesNo(a)} → {YesNo(b)}");
            }

            Text("Tipo", before.Type, after.Type);
            Text("Nome", before.Name, after.Name);
            Text("Camada", before.Layer, after.Layer);
            if (before.ParentStaticId != after.ParentStaticId)
            {
                lines.Add($"Dentro de: {Parent(before)} → {Parent(after)}");
            }
            else if (before.Order != after.Order)
            {
                lines.Add($"Ordem entre os irmãos: {before.Order} → {after.Order}");
            }

            Geometry("Posição X", before.PositionX, after.PositionX);
            Geometry("Posição Y", before.PositionY, after.PositionY);
            Geometry("Largura", before.Width, after.Width);
            Geometry("Altura", before.Height, after.Height);
            Number("Rotação (graus)", before.Rotation, after.Rotation);
            Flag("Selecionado", before.Selected, after.Selected);
            Flag("Visível", before.Visible, after.Visible);
            Flag("Bloqueado", before.Locked, after.Locked);
            Flag("Tem PowerClip", before.HasPowerClip, after.HasPowerClip);
            Text("Texto", before.Text, after.Text);
            lines.AddRange(PropertyChanges(before.Properties, after.Properties).Select(l => "Properties " + l));
            var oldData = before.ObjectData.ToDictionary(p => p.Key, p => p.Value);
            var newData = after.ObjectData.ToDictionary(p => p.Key, p => p.Value);
            foreach (var field in newData.Keys.Union(oldData.Keys))
            {
                oldData.TryGetValue(field, out var a);
                newData.TryGetValue(field, out var b);
                Text($"ObjectData \"{field}\"", a, b);
            }

            if (after.ReadError != null && after.ReadError != before.ReadError) lines.Add($"Erro de leitura: {after.ReadError}");
            return lines;
        }

        private static void AppendFullShape(StringBuilder text, CapturedShape shape, GeometryScale scale, PageCapture capture)
        {
            text.AppendLine(Title(shape));
            AppendShapeDetails(text, shape, scale, capture);
        }

        // Sempre um objeto da captura "depois" (novo ou selecionado).
        private static void AppendShapeDetails(StringBuilder text, CapturedShape shape, GeometryScale scale, PageCapture capture)
        {
            text.AppendLine($"  Tipo: {shape.Type} · camada: {shape.Layer} · dentro de: {Parent(shape)} · ordem: {shape.Order}");
            var raw = $"{N(shape.PositionX)}, {N(shape.PositionY)} · {N(shape.Width)} × {N(shape.Height)} em {Unit(capture.GeometryUnitCode)}";
            var x = scale.ToDisplay(shape.PositionX, after: true);
            text.AppendLine(x == null
                ? $"  Posição e tamanho (brutos, unidade desconhecida): {raw} · rotação: {N(shape.Rotation)}°"
                : $"  Posição: {N(x.Value)}, {N(scale.ToDisplay(shape.PositionY, true)!.Value)} · tamanho: {Size(scale, shape.Width, shape.Height, true)} · rotação: {N(shape.Rotation)}° (brutos: {raw})");
            text.AppendLine($"  Selecionado: {YesNo(shape.Selected)} · visível: {YesNo(shape.Visible)} · bloqueado: {YesNo(shape.Locked)} · PowerClip: {YesNo(shape.HasPowerClip)}");
            if (shape.Text != null) text.AppendLine($"  Texto: \"{shape.Text}\"");
            if (shape.Properties.Count == 0) text.AppendLine("  Properties: (nenhuma)");
            foreach (var p in shape.Properties) text.AppendLine($"  Properties {Describe(p)}");
            foreach (var d in shape.ObjectData) text.AppendLine($"  ObjectData \"{d.Key}\" = \"{d.Value}\"");
            if (shape.ReadError != null) text.AppendLine($"  Erro de leitura: {shape.ReadError}");
        }

        private static Dictionary<string, ShapePropertyEntry> Index(IReadOnlyList<ShapePropertyEntry> entries)
        {
            var result = new Dictionary<string, ShapePropertyEntry>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                var key = Key(entry);
                // Leitura com erro não tem nome confiável: entra pela posição, sem casar com outras.
                if (entry.ReadError != null) key = $"#{entry.Index} (erro)";
                if (!result.ContainsKey(key)) result[key] = entry;
            }

            return result;
        }

        private static string Key(ShapePropertyEntry entry) => $"{entry.Name} [{entry.Id}]";

        private static string ValueOf(ShapePropertyEntry entry) =>
            entry.ReadError != null ? $"(erro: {entry.ReadError})" : $"{entry.ValueType} {entry.ValueDisplay}";

        private static string Describe(ShapePropertyEntry entry) =>
            entry.ReadError != null ? $"entrada {entry.Index} = {ValueOf(entry)}" : $"{Key(entry)} = {ValueOf(entry)}";

        private static string Title(CapturedShape shape) =>
            $"#{shape.StaticId} {shape.Type}{(string.IsNullOrEmpty(shape.Name) ? "" : $" \"{shape.Name}\"")} — {shape.Path}";

        private static string Parent(CapturedShape shape) =>
            shape.ParentStaticId == null ? "camada (nível de cima)" : $"{shape.ParentKind} #{shape.ParentStaticId}";

        private static string Named(string name) => string.IsNullOrWhiteSpace(name) ? "" : $" \"{name}\"";

        private static string Unit(string label) => string.IsNullOrWhiteSpace(label) ? "não lida" : label;

        private static string Size(GeometryScale scale, double width, double height, bool after)
        {
            var w = scale.ToDisplay(width, after);
            var h = scale.ToDisplay(height, after);
            return w == null || h == null ? $"{N(width)} × {N(height)} (unidade desconhecida)" : $"{N(w.Value)} × {N(h.Value)} {scale.DisplayLabel}";
        }

        private static string Stamp(DateTimeOffset at) => at.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

        private static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        private static string YesNo(bool value) => value ? "sim" : "não";
    }
}
