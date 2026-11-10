using System;
using System.Collections.Generic;
using System.Linq;
using PXToolBox.Core.Naming;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Marcação do SISBolt que é só um flag em Shape.Properties: nome exato, ID 0, Boolean True.
    /// Contratos OBSERVADOS com a comparação antes/depois do PX (docs/sisbolt-tools-investigation.md)
    /// — nunca deduzidos. <see cref="TextOnly"/> limita a textos (cenário confirmado: texto de
    /// variável) até outros tipos serem capturados.
    /// </summary>
    public sealed class SisboltMarking
    {
        private SisboltMarking(string label, string propertyName, bool textOnly, IReadOnlyList<string>? allowedNames = null)
        {
            Label = label;
            PropertyName = propertyName;
            TextOnly = textOnly;
            AllowedNames = allowedNames;
        }

        public string Label { get; }
        public string PropertyName { get; }
        public bool TextOnly { get; }

        /// <summary>Se definido, só objetos com um destes nomes EXATOS recebem a marcação (mesma
        /// comparação de <see cref="ObjectCode.Toggle"/>). Desligar não exige o nome.</summary>
        public IReadOnlyList<string>? AllowedNames { get; }

        /// <summary>"Esticar no Tamanho do Molde": STRETCH_PROPS [0] = True, num retângulo simples
        /// (sem mudar a geometria na hora).</summary>
        public static SisboltMarking StretchToMold { get; } = new("Esticar no Tamanho do Molde", "STRETCH_PROPS", textOnly: false);

        /// <summary>"Ignorar métricas para variável selecionada": IGNORE_SISBOLT_VARIABLE_METRICS
        /// [0] = True, num texto NO selecionado.</summary>
        public static SisboltMarking IgnoreVariableMetrics { get; } = new("Ignorar métricas para variável selecionada", "IGNORE_SISBOLT_VARIABLE_METRICS", textOnly: true);

        /// <summary>"Mão Livre": FREE_HAND_PROPS [0] = True. Só em textos com os códigos NO, NU, AP
        /// ou TS (regra do usuário, 2026-11-06; os códigos só existem em textos).</summary>
        public static SisboltMarking FreeHand { get; } = new("Mão Livre", "FREE_HAND_PROPS", textOnly: true,
            allowedNames: new[] { ObjectCode.No.ShortCode, ObjectCode.Nu.ShortCode, ObjectCode.Ap.ShortCode, ObjectCode.Ts.ShortCode });
    }

    /// <summary>
    /// Alterna uma <see cref="SisboltMarking"/> nos objetos selecionados, com o MESMO
    /// comportamento do Tamanho Padrão (pedido do usuário, 2026-11-06): todos marcados → remove
    /// a marcação de todos; senão → marca todos com True. O botão mostra o estado da seleção
    /// (<see cref="InspectSelection"/>: ativo, misto, inativo).
    ///
    /// Desativar REMOVE as entradas com o nome da marcação (como o Tamanho Padrão faz com
    /// KEEP_DESIGN_SIZE), em vez de gravar False. A captura da desativação no SISBolt ainda não
    /// existe — se ela mostrar False, só <see cref="Deactivate"/> muda. Nunca toca em
    /// KEEP_DESIGN_SIZE, em outras propriedades, nome, texto, ObjectData ou geometria. Cada
    /// objeto é avaliado sozinho (sem descer em grupos); o chamador agrupa em um Ctrl+Z.
    /// </summary>
    public sealed class SisboltMarkingService
    {
        public const int FlagId = 0;

        /// <summary>True se a entrada existe com o valor Boolean True.</summary>
        public bool IsActive(ICorelShape shape, SisboltMarking marking) =>
            shape.HasProperty(marking.PropertyName, FlagId) && shape.GetProperty(marking.PropertyName, FlagId) is bool value && value;

        /// <summary>Estado da seleção para o botão — mesmos estados do Tamanho Padrão. Falha de
        /// leitura em qualquer objeto → Unknown (nunca "inativo").</summary>
        public StandardSizeSelectionState InspectSelection(ICorelSelection? selection, SisboltMarking marking)
        {
            if (selection == null || selection.Count == 0)
                return StandardSizeSelectionState.Unavailable;

            int active;
            try
            {
                active = selection.Shapes.Count(shape => IsActive(shape, marking));
            }
            catch (Exception)
            {
                return StandardSizeSelectionState.Unknown;
            }

            if (active == 0) return StandardSizeSelectionState.NoneMarked;
            return active == selection.Count ? StandardSizeSelectionState.AllMarked : StandardSizeSelectionState.Mixed;
        }

        public OperationResult Toggle(ICorelSelection selection, SisboltMarking marking)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione um ou mais objetos.");
            }

            var shapes = selection.Shapes;
            if (marking.TextOnly && shapes.Any(shape => shape.Kind != ShapeKind.Text))
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection,
                    "Selecione só textos (o cenário confirmado é um texto de variável, como o NO) — nada foi alterado.");
            }

            if (shapes.All(shape => IsActive(shape, marking)))
            {
                foreach (var shape in shapes) Deactivate(shape, marking);
                return OperationResult.Success($"{marking.Label} desativado em {shapes.Count} objeto(s).");
            }

            if (marking.AllowedNames != null && shapes.Any(shape => !marking.AllowedNames.Contains(shape.Name)))
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection,
                    $"{marking.Label} só vale para textos com o nome {string.Join(", ", marking.AllowedNames.Take(marking.AllowedNames.Count - 1))} ou {marking.AllowedNames.Last()} — nada foi alterado.");
            }

            foreach (var shape in shapes.Where(shape => !IsActive(shape, marking)))
            {
                shape.SetProperty(marking.PropertyName, FlagId, true);
            }

            return OperationResult.Success($"{marking.Label} ativado em {shapes.Count} objeto(s) ({marking.PropertyName} [0] = True).");
        }

        private static void Deactivate(ICorelShape shape, SisboltMarking marking) => shape.DeleteProperties(marking.PropertyName);
    }
}
