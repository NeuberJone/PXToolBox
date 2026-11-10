using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PXToolBox.Core.Molds;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Propagar logos" do Cadastro de moldes — reescrito no padrão do PX a partir do MoldMotor, na
    /// versão simples: a seleção traz grupos de tamanho já montados da mesma peça; os logos L1… do
    /// grupo de referência vão para os outros na mesma posição relativa ao centro da peça, com a
    /// escala da altura da peça (uma escala só, nos dois eixos).
    /// <list type="bullet">
    /// <item>Referência: o grupo com mais logos. Empate: o do tamanho escolhido no painel; sem ele no
    /// empate, recusa.</item>
    /// <item>Logo que o destino já tem (mesmo L&lt;n&gt;): só é movido e redimensionado. Logo que falta:
    /// cópia do logo da referência, que entra no grupo do destino, atrás da TAG.</item>
    /// <item>Não mexe em peça, laser, TAG, nomes nem em logos do destino que a referência não tem.</item>
    /// </list>
    /// Fora desta versão (o MoldMotor tem): conjuntos soltos (sem grupo) e a referência registrada do
    /// molde.
    /// </summary>
    public sealed class MoldLogoPropagationService
    {
        private readonly Func<MoldSettings> _settings;

        public MoldLogoPropagationService(Func<MoldSettings>? settings = null)
        {
            _settings = settings ?? (() => MoldSettings.Default);
        }

        private sealed class SizeGroup
        {
            public SizeGroup(ICorelShape group, string size, ICorelShape piece, Dictionary<string, ICorelShape> logos)
            {
                Group = group;
                Size = size;
                Piece = piece;
                Logos = logos;
            }

            public ICorelShape Group { get; }
            public string Size { get; }
            public ICorelShape Piece { get; }
            public Dictionary<string, ICorelShape> Logos { get; }
        }

        /// <param name="preferredReference">Tamanho escolhido no painel: desempata a referência quando
        /// mais de um grupo tem todos os logos (ex.: ao propagar de novo).</param>
        public OperationResult Propagate(ICorelSelection selection, IShapeArrangement arrangement, string preferredReference)
        {
            var settings = _settings();
            if (selection.Count < 2) return Refuse("Selecione pelo menos dois grupos de tamanho da mesma peça: o que tem os logos e os que vão recebê-los.");

            var groups = new List<SizeGroup>();
            foreach (var shape in selection.Shapes)
            {
                var size = shape.Name.Trim().ToUpperInvariant();
                if (shape.Kind != ShapeKind.Group || settings.CategoryOf(size).Length == 0)
                {
                    return Refuse($"\"{(size.Length == 0 ? "(sem nome)" : shape.Name.Trim())}\" não é um grupo de tamanho. Monte os grupos antes com Definir objetos (ou em lote) e selecione só os grupos.");
                }

                var recognized = MoldPieceRecognizer.Recognize(shape, settings);
                if (recognized.Piece == null) return Refuse($"Grupo \"{size}\": {recognized.Problem}.");
                if (recognized.Piece.Width <= 0 || recognized.Piece.Height <= 0) return Refuse($"Grupo \"{size}\": a peça não tem largura e altura.");
                if (shape.Layer == null) return Refuse($"Grupo \"{size}\": não foi possível saber a camada.");

                var logos = new Dictionary<string, ICorelShape>(StringComparer.OrdinalIgnoreCase);
                foreach (var logo in shape.Shapes.Where(s => LayerStandardService.IsLogoName(s.Name)))
                {
                    var name = logo.Name.Trim().ToUpperInvariant();
                    if (logos.ContainsKey(name)) return Refuse($"Grupo \"{size}\" tem mais de um {name}.");
                    logos[name] = logo;
                }

                groups.Add(new SizeGroup(shape, size, recognized.Piece, logos));
            }

            var repeated = groups.GroupBy(g => g.Size).FirstOrDefault(g => g.Count() > 1);
            if (repeated != null) return Refuse($"Há mais de um grupo \"{repeated.Key}\" na seleção.");
            var pieces = groups.Select(g => g.Piece.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (pieces.Count > 1) return Refuse($"Os grupos são de peças diferentes ({string.Join(", ", pieces)}) — selecione os tamanhos de uma peça só.");

            var most = groups.Max(g => g.Logos.Count);
            if (most == 0) return Refuse("Nenhum grupo selecionado tem logos (L1, L2…) para propagar.");
            var leaders = groups.Where(g => g.Logos.Count == most).ToList();
            var reference = leaders.Count == 1 ? leaders[0] : leaders.FirstOrDefault(g => Same(g.Size, preferredReference ?? string.Empty));
            if (reference == null)
            {
                return Refuse($"Mais de um grupo tem todos os {most} logos ({string.Join(", ", leaders.Select(l => l.Size))}) — escolha em Tamanho qual deles é a referência.");
            }

            var referenceLogos = reference.Logos.OrderBy(l => LogoNumber(l.Key)).ToList();
            var results = new List<ICorelShape> { reference.Group };
            var parts = new List<string>();
            foreach (var destination in groups.Where(g => g != reference))
            {
                var scale = destination.Piece.Height / reference.Piece.Height;
                var layer = destination.Group.Layer!;
                var created = new List<ICorelShape>();
                var adjusted = 0;
                foreach (var entry in referenceLogos)
                {
                    var name = entry.Key;
                    var source = entry.Value;
                    var centerX = destination.Piece.CenterX + (source.CenterX - reference.Piece.CenterX) * scale;
                    var centerY = destination.Piece.CenterY + (source.CenterY - reference.Piece.CenterY) * scale;
                    ICorelShape logo;
                    if (destination.Logos.TryGetValue(name, out var existing))
                    {
                        logo = existing;
                        adjusted++;
                    }
                    else
                    {
                        // A cópia nasce dentro do grupo da referência; MoveToLayer a tira de lá.
                        logo = source.Duplicate();
                        logo.MoveToLayer(layer);
                        logo.OrderToFront();
                        logo.Name = name;
                        created.Add(logo);
                    }

                    Fit(logo, source.Width * scale, source.Height * scale, centerX, centerY);
                }

                var group = destination.Group;
                if (created.Count > 0)
                {
                    var children = group.Ungroup();
                    var tag = children.FirstOrDefault(c => MoldPieceRecognizer.IsTag(c.Name, settings));
                    tag?.OrderToFront();
                    var members = new List<ICorelShape>();
                    if (tag != null) members.Add(tag);
                    members.AddRange(created);
                    members.AddRange(children.Where(c => c != tag));
                    group = arrangement.Group(members);
                    group.Name = destination.Size;
                }

                results.Add(group);
                var part = $"{destination.Size} — {created.Count} novo(s), {adjusted} ajustado(s)";
                var extra = destination.Logos.Keys.Where(k => !reference.Logos.ContainsKey(k)).OrderBy(LogoNumber).ToList();
                if (extra.Count > 0) part += $" ({string.Join(", ", extra)} não está na referência e ficou como estava)";
                parts.Add(part);
            }

            var sizes = settings.AllSizes.ToList();
            arrangement.Select(results.OrderBy(g => sizes.FindIndex(s => Same(s, g.Name))).ToList());
            return OperationResult.Success($"Logos do grupo \"{reference.Size}\" ({string.Join(", ", referenceLogos.Select(l => l.Key))}) propagados: {string.Join("; ", parts)}.");
        }

        /// <summary>Tamanho primeiro, posição depois: no Corel o SetSize pode deslocar o centro.</summary>
        private static void Fit(ICorelShape shape, double width, double height, double centerX, double centerY)
        {
            shape.Width = width;
            shape.Height = height;
            shape.PositionX += centerX - shape.CenterX;
            shape.PositionY += centerY - shape.CenterY;
        }

        private static OperationResult Refuse(string message) =>
            OperationResult.Fail(OperationStatus.InvalidSelection, message + " Nada foi alterado.");

        private static int LogoNumber(string name) =>
            LayerStandardService.IsLogoName(name) && int.TryParse(name.Trim().Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;

        private static bool Same(string left, string right) =>
            string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
