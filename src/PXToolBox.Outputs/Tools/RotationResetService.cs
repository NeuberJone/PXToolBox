using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Resetar ângulo": deixa cada objeto selecionado com rotação 0° SEM mudar a aparência — o
    /// mecanismo do "Zerar ângulo" do MoldMotor (já validado no CorelDRAW), portado para cá; o PX
    /// não depende do MoldMotor (AGENTS.md).
    ///
    /// <list type="bullet">
    /// <item>Curva: lê nós e pontos de controle (coordenadas de página), zera a rotação, regrava
    /// exatamente o que leu e confere ângulo, caixa e geometria relidos. A escrita nunca é aceita
    /// como prova; se a conferência falhar, o objeto volta a ser a cópia de segurança feita antes.</item>
    /// <item>Retângulo e elipse girados: convertidos em curva (como a ferramenta original, que
    /// também entrega uma curva) e tratados como curva.</item>
    /// <item>Grupo em 0°: cada filho é tratado no lugar. Grupo girado: desagrupa, trata os filhos,
    /// reagrupa os mesmos objetos na mesma ordem e no mesmo lugar da camada.</item>
    /// <item>Texto, bitmap e demais tipos: não mexe (converter um texto em curva acabaria com a
    /// variável NO/NU/AP/TS).</item>
    /// </list>
    ///
    /// Diferença deliberada da ferramenta original (captura dev.68, que devolvia uma curva sem
    /// Properties): nome e Properties do objeto (Tamanho Padrão, Esticar e as demais marcações) são
    /// preservados — no objeto convertido, na cópia de segurança e no grupo reconstruído.
    /// </summary>
    public sealed class RotationResetService
    {
        /// <summary>Tolerância da conferência, em mm — o host converte para a unidade do documento.</summary>
        public const double ToleranceMillimeters = 0.01;

        private const double AngleEpsilonDegrees = 1e-6;

        public enum Outcome
        {
            Changed,
            AlreadyZero,
            Unsupported,
            Partial,
            Failed
        }

        public OperationResult Reset(ICorelSelection selection, IShapeArrangement arrangement, double tolerance)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione um ou mais objetos.");
            }

            var run = new Run(arrangement, tolerance);
            var selected = selection.Shapes.ToList();
            var finalSelection = new List<ICorelShape>(selected.Count);
            var tally = new Tally();
            foreach (var shape in selected)
            {
                tally.Add(run.ResetShape(shape, out var replacement));
                finalSelection.Add(replacement ?? shape);
            }

            // Objeto substituído (grupo reconstruído, cópia de segurança) só continua selecionado
            // pela referência nova.
            arrangement.Select(finalSelection);

            var message = Describe(tally, run.UnsupportedKinds, run.Failures);
            if (tally.Failed > 0)
            {
                return OperationResult.Fail(OperationStatus.ExecutionFailed, message);
            }

            if (tally.Changed == 0 && tally.Partial == 0 && tally.Unsupported > 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, message);
            }

            return OperationResult.Success(message);
        }

        public static bool IsZero(double angleDegrees)
        {
            var normalized = angleDegrees % 360.0;
            if (normalized > 180.0) normalized -= 360.0;
            else if (normalized <= -180.0) normalized += 360.0;
            return Math.Abs(normalized) < AngleEpsilonDegrees;
        }

        private static string Describe(Tally tally, IReadOnlyCollection<string> unsupportedKinds, IReadOnlyList<string> failures)
        {
            var parts = new List<string>();
            if (tally.Changed > 0) parts.Add($"{tally.Changed} objeto(s) com ângulo zerado, aparência preservada");
            if (tally.Partial > 0) parts.Add($"{tally.Partial} grupo(s) zerado(s) em parte (há objetos de tipo não suportado ainda girados dentro)");
            if (tally.AlreadyZero > 0) parts.Add($"{tally.AlreadyZero} já em 0°");
            if (tally.Unsupported > 0)
            {
                parts.Add($"{tally.Unsupported} não alterado(s) — tipo não suportado ({string.Join(", ", unsupportedKinds)})");
            }

            if (tally.Failed > 0) parts.Add($"{tally.Failed} falhou(aram): {string.Join("; ", failures)}");
            var text = string.Join("; ", parts) + ".";
            return tally.Changed == 0 && tally.Partial == 0 && tally.Unsupported == 0 && tally.Failed == 0
                ? "Nenhum objeto girado — nada mudou."
                : text;
        }

        /// <summary>Resultado de um grupo a partir dos filhos (mesma regra do MoldMotor): falha em
        /// qualquer filho é falha; trabalho feito com algo ainda girado é parcial — nunca "zerado".</summary>
        private sealed class Tally
        {
            public int Changed { get; private set; }
            public int AlreadyZero { get; private set; }
            public int Unsupported { get; private set; }
            public int Partial { get; private set; }
            public int Failed { get; private set; }

            public void Add(Outcome outcome)
            {
                switch (outcome)
                {
                    case Outcome.Changed: Changed++; break;
                    case Outcome.AlreadyZero: AlreadyZero++; break;
                    case Outcome.Unsupported: Unsupported++; break;
                    case Outcome.Partial: Partial++; break;
                    default: Failed++; break;
                }
            }

            public Outcome Resolve(bool groupRotationFixed)
            {
                var changed = Changed + Partial + (groupRotationFixed ? 1 : 0);
                var unresolved = Unsupported + Partial;
                if (Failed > 0) return Outcome.Failed;
                if (changed > 0 && unresolved > 0) return Outcome.Partial;
                if (changed > 0) return Outcome.Changed;
                return unresolved > 0 ? Outcome.Unsupported : Outcome.AlreadyZero;
            }
        }

        private sealed class Run
        {
            private readonly IShapeArrangement _arrangement;
            private readonly double _tolerance;

            public Run(IShapeArrangement arrangement, double tolerance)
            {
                _arrangement = arrangement;
                _tolerance = tolerance;
            }

            public SortedSet<string> UnsupportedKinds { get; } = new SortedSet<string>(StringComparer.Ordinal);

            public List<string> Failures { get; } = new List<string>();

            /// <summary><paramref name="replacement"/> só vem preenchido quando o objeto foi trocado
            /// por outro (grupo reconstruído ou cópia de segurança restaurada).</summary>
            public Outcome ResetShape(ICorelShape shape, out ICorelShape? replacement)
            {
                replacement = null;

                // Grupo SEMPRE pelo caminho de grupo: um grupo em 0° pode ter filhos girados.
                if (shape.Kind == ShapeKind.Group)
                {
                    return IsZero(shape.RotationAngle) ? ResetChildrenInPlace(shape) : RebuildRotatedGroup(shape, out replacement);
                }

                if (IsZero(shape.RotationAngle))
                {
                    return Outcome.AlreadyZero;
                }

                switch (shape.Kind)
                {
                    case ShapeKind.Curve:
                        return ResetCurve(shape, out replacement);
                    case ShapeKind.Rectangle:
                    case ShapeKind.Ellipse:
                        return ConvertAndReset(shape, out replacement);
                    default:
                        UnsupportedKinds.Add(KindLabel(shape));
                        return Outcome.Unsupported;
                }
            }

            private Outcome ResetCurve(ICorelShape shape, out ICorelShape? replacement)
            {
                replacement = null;
                var before = shape.ReadCurveGeometry();
                if (before == null)
                {
                    UnsupportedKinds.Add("curva com segmento ilegível");
                    return Outcome.Unsupported;
                }

                var saved = Snapshot.Of(shape);
                var backup = shape.Duplicate();
                if (MutateAndVerify(shape, before))
                {
                    backup.Delete();
                    return Outcome.Changed;
                }

                replacement = RestoreBackup(shape, backup, saved);
                return Outcome.Failed;
            }

            private Outcome ConvertAndReset(ICorelShape shape, out ICorelShape? replacement)
            {
                replacement = null;
                var saved = Snapshot.Of(shape);
                var label = KindLabel(shape);
                var backup = shape.Duplicate();

                shape.ConvertToCurves();
                if (shape.Kind != ShapeKind.Curve)
                {
                    // ConvertToCurves pode não fazer nada sem lançar: nunca confiar só na chamada.
                    // O objeto não mudou; só a cópia de segurança sai.
                    backup.Delete();
                    Failures.Add($"\"{saved.Name}\": o Corel não converteu o {label} em curva");
                    return Outcome.Failed;
                }

                saved.ApplyTo(shape);
                var before = shape.ReadCurveGeometry();
                if (before != null && MutateAndVerify(shape, before))
                {
                    backup.Delete();
                    return Outcome.Changed;
                }

                replacement = RestoreBackup(shape, backup, saved);
                return Outcome.Failed;
            }

            /// <summary>Zera a rotação, regrava a geometria lida e confere tudo relendo.</summary>
            /// Falha do Corel no meio conta como conferência reprovada: quem chama restaura a cópia.
            private bool MutateAndVerify(ICorelShape shape, CurveGeometry before)
            {
                try
                {
                    var boxBefore = Box.Of(shape);
                    shape.ClearRotation();
                    var writeFailures = shape.WriteCurveGeometry(before);
                    var after = shape.ReadCurveGeometry();
                    return writeFailures == 0
                        && IsZero(shape.RotationAngle)
                        && Box.Of(shape).Matches(boxBefore, _tolerance)
                        && after != null && before.Matches(after, _tolerance);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            /// <summary>Apaga o objeto alterado e deixa a cópia de segurança no lugar dele (a cópia
            /// foi criada logo à frente do original, então herda a mesma posição na pilha).</summary>
            private ICorelShape RestoreBackup(ICorelShape changed, ICorelShape backup, Snapshot saved, string? reason = null)
            {
                changed.Delete();
                saved.ApplyTo(backup);
                Failures.Add(reason ?? $"\"{saved.Name}\": a geometria não ficou igual depois de zerar; objeto restaurado como estava");
                return backup;
            }

            private Outcome ResetChildrenInPlace(ICorelShape group)
            {
                var children = FrontToBack(group.Shapes);
                if (children.Count == 0)
                {
                    Failures.Add($"\"{group.Name}\": grupo vazio ou ilegível");
                    return Outcome.Failed;
                }

                var tally = new Tally();
                var expected = new List<ICorelShape>(children.Count);
                foreach (var child in children)
                {
                    tally.Add(ResetShape(child, out var childReplacement));
                    expected.Add(childReplacement ?? child);
                }

                RestoreOrder(() => group.Shapes, expected);
                return tally.Resolve(groupRotationFixed: false);
            }

            private Outcome RebuildRotatedGroup(ICorelShape group, out ICorelShape? replacement)
            {
                replacement = null;
                // Dentro de outro grupo, reagrupar tiraria os filhos do grupo de fora — não mexe.
                if (group.IsInsideGroup)
                {
                    UnsupportedKinds.Add("grupo girado dentro de outro grupo");
                    return Outcome.Unsupported;
                }

                var saved = Snapshot.Of(group);
                var childOrder = FrontToBack(group.Shapes);
                if (childOrder.Count == 0)
                {
                    Failures.Add($"\"{saved.Name}\": grupo vazio ou ilegível");
                    return Outcome.Failed;
                }

                // Posição na camada: quem está logo atrás do grupo — lido antes de desagrupar.
                ICorelShape? backNeighbor = null;
                var onlyShapeInLayer = false;
                if (group.Layer != null)
                {
                    var siblings = FrontToBack(group.Layer.Shapes);
                    var index = siblings.FindIndex(s => s.StaticId == group.StaticId);
                    if (index >= 0 && index + 1 < siblings.Count) backNeighbor = siblings[index + 1];
                    onlyShapeInLayer = siblings.Count == 1;
                }

                var ungrouped = group.Ungroup();
                // Depois do Ungroup cada filho carrega a própria rotação. As referências são as
                // devolvidas pelo Ungroup, na ordem de pilha lida antes.
                var byId = ungrouped.ToDictionary(s => s.StaticId);
                var tally = new Tally();
                var finalChildren = new List<ICorelShape>(childOrder.Count);
                foreach (var original in childOrder)
                {
                    var child = byId.TryGetValue(original.StaticId, out var fresh) ? fresh : original;
                    tally.Add(ResetShape(child, out var childReplacement));
                    finalChildren.Add(childReplacement ?? child);
                }

                var rebuilt = _arrangement.Group(finalChildren);
                saved.ApplyTo(rebuilt);
                for (var i = finalChildren.Count - 1; i >= 0; i--)
                {
                    finalChildren[i].OrderToFront();
                }

                if (backNeighbor != null) rebuilt.OrderFrontOf(backNeighbor);
                else if (!onlyShapeInLayer) rebuilt.OrderToBack();

                replacement = rebuilt;
                return tally.Resolve(groupRotationFixed: true);
            }

            /// <summary>Confere a ordem por StaticID e, só se mudou, restaura (de trás para a frente,
            /// OrderToFront em cada um) — mesma técnica do MoldMotor.</summary>
            private static void RestoreOrder(Func<IReadOnlyList<ICorelShape>> read, List<ICorelShape> expected)
            {
                if (SameOrder(FrontToBack(read()), expected)) return;
                for (var i = expected.Count - 1; i >= 0; i--)
                {
                    expected[i].OrderToFront();
                }
            }

            private static bool SameOrder(List<ICorelShape> current, List<ICorelShape> expected) =>
                current.Count == expected.Count && current.Select(s => s.StaticId).SequenceEqual(expected.Select(s => s.StaticId));

            /// <summary>Do mais à frente para o mais atrás por OrderIsInFrontOf — nunca pela posição
            /// na coleção, que depende de Document.ShapeEnumDirection.</summary>
            private static List<ICorelShape> FrontToBack(IReadOnlyList<ICorelShape> shapes)
            {
                var ordered = shapes.ToList();
                ordered.Sort((a, b) => a.StaticId == b.StaticId ? 0 : a.IsInFrontOf(b) ? -1 : b.IsInFrontOf(a) ? 1 : 0);
                return ordered;
            }

            private static string KindLabel(ICorelShape shape) => shape.Kind switch
            {
                ShapeKind.Rectangle => "retângulo",
                ShapeKind.Ellipse => "elipse",
                ShapeKind.Curve => "curva",
                ShapeKind.Text => "texto",
                ShapeKind.Bitmap => "bitmap",
                ShapeKind.Polygon => "polígono",
                ShapeKind.Blend => "mistura",
                ShapeKind.Symbol => "símbolo",
                ShapeKind.Group => "grupo",
                _ => "outro tipo"
            };
        }

        private readonly struct Box
        {
            private readonly double _centerX, _centerY, _width, _height;

            private Box(double centerX, double centerY, double width, double height)
            {
                _centerX = centerX;
                _centerY = centerY;
                _width = width;
                _height = height;
            }

            public static Box Of(ICorelShape shape) => new Box(shape.CenterX, shape.CenterY, shape.Width, shape.Height);

            public bool Matches(Box other, double tolerance) =>
                Math.Abs(_centerX - other._centerX) <= tolerance && Math.Abs(_centerY - other._centerY) <= tolerance
                && Math.Abs(_width - other._width) <= tolerance && Math.Abs(_height - other._height) <= tolerance;
        }

        /// <summary>Nome e Properties de um objeto, para regravar no que ficar no lugar dele.</summary>
        private sealed class Snapshot
        {
            private Snapshot(string name, IReadOnlyList<ShapePropertyEntry> properties)
            {
                Name = name;
                Properties = properties;
            }

            public string Name { get; }

            public IReadOnlyList<ShapePropertyEntry> Properties { get; }

            public static Snapshot Of(ICorelShape shape) => new Snapshot(shape.Name, shape.ReadAllProperties());

            /// <summary>Só completa o que falta — nunca apaga nem sobrescreve uma entrada existente.</summary>
            public void ApplyTo(ICorelShape target)
            {
                if (Name.Length > 0 && target.Name != Name) target.Name = Name;
                foreach (var entry in Properties)
                {
                    if (entry.Value != null && !target.HasProperty(entry.Name, entry.Id))
                    {
                        target.SetProperty(entry.Name, entry.Id, entry.Value);
                    }
                }
            }
        }
    }
}
