using System;
using System.Linq;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>Estado de marcação de Tamanho Padrão de UM Shape (nunca recursivo — grupo e
    /// filhos são Shapes distintos, cada um com seu próprio estado).</summary>
    public enum StandardSizeMarkState
    {
        /// <summary>Nenhuma entrada <see cref="StandardSizeService.PropertyName"/> no Shape.</summary>
        Absent,

        /// <summary>Todas as entradas do contrato confirmado presentes.</summary>
        Complete,

        /// <summary>Alguma entrada presente, mas não o conjunto completo — ex.: a marcação
        /// antiga do PX, só com o flag habilitado (ID 0) e sem a dimensão. Nunca tratada como
        /// Absent (perderia a informação de que precisa de migração) nem como Complete
        /// (fingiria um estado que não existe).</summary>
        Incomplete,

        /// <summary>A leitura da propriedade falhou (Shape inválido/excluído, Properties
        /// inacessível) — nunca apresentado como Absent, porque isso fingiria "desmarcado"
        /// quando a resposta real é "não sei".</summary>
        Unknown,
    }

    /// <summary>Estado agregado de uma seleção inteira, para refletir no botão (ver
    /// ControlUI.UpdateStandardSizeButtonState).</summary>
    public enum StandardSizeSelectionState
    {
        /// <summary>Sem documento ou sem seleção.</summary>
        Unavailable,
        AllMarked,
        NoneMarked,
        Mixed,
        /// <summary>Pelo menos uma leitura falhou — nunca resolvido silenciosamente para
        /// NoneMarked/AllMarked.</summary>
        Unknown,
    }

    /// <summary>
    /// Manipula exclusivamente o contrato SISBolt de Tamanho Padrão em Shape.Properties.
    ///
    /// <para><b>Contrato CONFIRMADO por evidência real</b> (inspeção real, Count: 3, todos os
    /// três índices lidos com sucesso): nome exato <c>KEEP_DESIGN_SIZE</c>; ID 0 Boolean =
    /// flag habilitado; ID 1 Double = largura; ID 2 Double = altura.</para>
    ///
    /// <para><b>Bug de unidade — três tentativas, as duas primeiras descartadas por evidência
    /// real</b>: <see cref="ICorelShape.Width"/>/<see cref="ICorelShape.Height"/>
    /// (Shape.SizeWidth/SizeHeight no VGCore real) retornam sempre em POLEGADAS,
    /// independentemente de qualquer unidade configurada. Tentativa 1 (Document.Unit) e
    /// tentativa 2 (Application.Unit, e também Document.ToUnits — que usa Document.Unit por
    /// baixo) foram cada uma refutada por testes reais do usuário (documentos em mm e em cm,
    /// inclusive já salvos): ambas as propriedades reportam cdrInch mesmo com a régua/barra de
    /// propriedades mostrando outra unidade. Tentativa 3, implementada: um levantamento por
    /// reflection de TODOS os membros "Unit*" em toda a Corel.Interop.VGCore.dll achou
    /// <c>Document.Rulers.HUnits</c>/<c>VUnits</c> — confirmado por evidência real (mesmos
    /// documentos) como a propriedade sincronizada com a UI. <see
    /// cref="ICorelDocument.ConvertFromInches"/> usa <c>Application.ConvertUnits(valor,
    /// cdrInch, Document.Rulers.HUnits)</c>, que recebe origem e destino explícitos em vez de
    /// depender de qualquer estado ambiente. Os métodos de escrita recebem essa conversão como
    /// um delegate (nunca um enum de unidade), nulo quando a conversão não está disponível
    /// (ex.: sem documento) — falha com mensagem clara em vez de gravar um valor errado
    /// silenciosamente.</para>
    /// </summary>
    public sealed class StandardSizeService
    {
        public const string PropertyName = "KEEP_DESIGN_SIZE";
        public const int EnabledId = 0;
        public const int WidthId = 1;
        public const int HeightId = 2;

        private const string UnavailableConversionMessage = "Não foi possível converter a unidade de medida — Tamanho Padrão não foi aplicado.";

        /// <summary>Todo o conjunto de propriedades que compõe uma marcação COMPLETA, pelo
        /// contrato confirmado. Centralizado aqui para que a migração de marcações antigas
        /// (seção 2 da tarefa) e a classificação de estado (seção 4) nunca precisem ser
        /// atualizadas em mais de um lugar.</summary>
        private static readonly int[] RequiredIds = { EnabledId, WidthId, HeightId };

        /// <summary>Compatibilidade com código existente: trata Complete e Incomplete como
        /// "tem alguma marcação" — ver <see cref="Inspect"/> para o estado completo (inclusive
        /// Unknown, nunca escondido aqui).</summary>
        public bool HasStandardSize(ICorelShape shape)
        {
            var state = Inspect(shape);
            return state == StandardSizeMarkState.Complete || state == StandardSizeMarkState.Incomplete;
        }

        /// <summary>Classifica o estado real de marcação deste Shape — nunca lança: uma falha
        /// de leitura resulta em <see cref="StandardSizeMarkState.Unknown"/>, nunca em Absent
        /// (que fingiria "sem marcação" quando a resposta real é "não foi possível ler").</summary>
        public StandardSizeMarkState Inspect(ICorelShape shape)
        {
            try
            {
                var presentCount = RequiredIds.Count(id => shape.HasProperty(PropertyName, id));
                if (presentCount == 0) return StandardSizeMarkState.Absent;
                if (presentCount == RequiredIds.Length) return StandardSizeMarkState.Complete;
                return StandardSizeMarkState.Incomplete;
            }
            catch (Exception)
            {
                return StandardSizeMarkState.Unknown;
            }
        }

        /// <summary>Estado agregado da seleção inteira — ver <see cref="StandardSizeSelectionState"/>.
        /// Nunca desce em filhos de grupo: cada Shape da seleção (grupo ou não) é avaliado
        /// isoladamente, exatamente como <see cref="ICorelSelection.Shapes"/> já expõe (seleção
        /// de um grupo nunca é confundida com a seleção dos filhos dele).</summary>
        public StandardSizeSelectionState InspectSelection(ICorelSelection? selection)
        {
            if (selection == null || selection.Count == 0)
                return StandardSizeSelectionState.Unavailable;

            var states = selection.Shapes.Select(Inspect).ToList();
            if (states.Any(s => s == StandardSizeMarkState.Unknown))
                return StandardSizeSelectionState.Unknown;

            var markedCount = states.Count(s => s == StandardSizeMarkState.Complete || s == StandardSizeMarkState.Incomplete);
            if (markedCount == 0) return StandardSizeSelectionState.NoneMarked;
            if (markedCount == states.Count) return StandardSizeSelectionState.AllMarked;
            return StandardSizeSelectionState.Mixed;
        }

        public OperationResult SetStandardSize(ICorelShape shape, Func<double, double>? convertFromInches)
        {
            if (convertFromInches == null)
                return OperationResult.Fail(OperationStatus.InconclusiveRead, UnavailableConversionMessage);

            WriteStandardSize(shape, convertFromInches);
            return OperationResult.Success();
        }

        public OperationResult UpdateStandardSize(ICorelShape shape, Func<double, double>? convertFromInches)
        {
            if (convertFromInches == null)
                return OperationResult.Fail(OperationStatus.InconclusiveRead, UnavailableConversionMessage);

            WriteStandardSize(shape, convertFromInches);
            return OperationResult.Success();
        }

        public OperationResult RemoveStandardSize(ICorelShape shape)
        {
            shape.DeleteProperties(PropertyName);
            return OperationResult.Success();
        }

        public OperationResult Toggle(ICorelSelection selection, Func<double, double>? convertFromInches)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione um ou mais objetos.");
            }

            var shapes = selection.Shapes;
            var allMarked = shapes.All(HasStandardSize);
            if (allMarked)
            {
                // Desativar nunca precisa de conversão de unidade — só remove entradas
                // existentes, nunca escreve um valor novo.
                foreach (var shape in shapes)
                {
                    RemoveStandardSize(shape);
                }

                return OperationResult.Success($"Tamanho Padrão removido de {shapes.Count} objetos.");
            }

            if (convertFromInches == null)
            {
                return OperationResult.Fail(OperationStatus.InconclusiveRead, UnavailableConversionMessage);
            }

            var anyMarked = shapes.Any(HasStandardSize);
            foreach (var shape in shapes)
            {
                // Migração explícita (seção 2 da tarefa): uma marcação Incomplete (ex.: a
                // antiga, só com o ID 0) é removida por completo ANTES de escrever o conjunto
                // novo — nunca deixa duplicata/entrada órfã do esquema anterior convivendo com
                // a nova. Nunca varre o documento inteiro: só os Shapes desta seleção.
                if (Inspect(shape) == StandardSizeMarkState.Incomplete)
                {
                    RemoveStandardSize(shape);
                }

                WriteStandardSize(shape, convertFromInches);
            }

            var message = anyMarked
                ? $"Tamanho Padrão aplicado/atualizado em {shapes.Count} objetos."
                : $"Tamanho Padrão aplicado em {shapes.Count} objetos.";
            return OperationResult.Success(message);
        }

        public OperationResult UpdateSelection(ICorelSelection selection, Func<double, double>? convertFromInches)
        {
            if (selection.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Selecione um ou mais objetos.");
            }

            if (convertFromInches == null)
            {
                return OperationResult.Fail(OperationStatus.InconclusiveRead, UnavailableConversionMessage);
            }

            var anyMissing = selection.Shapes.Any(shape => Inspect(shape) == StandardSizeMarkState.Absent);
            var anyRepaired = selection.Shapes.Any(shape => Inspect(shape) == StandardSizeMarkState.Incomplete);
            foreach (var shape in selection.Shapes)
            {
                // Reparo explícito (pedido da seção 3): uma marcação Incomplete nunca é só
                // sobrescrita por cima — a entrada antiga é removida primeiro, e o resultado
                // informa que houve reparo (nunca silencioso).
                if (Inspect(shape) == StandardSizeMarkState.Incomplete)
                {
                    RemoveStandardSize(shape);
                }

                WriteStandardSize(shape, convertFromInches);
            }

            var message = anyRepaired
                ? $"Tamanho Padrão atualizado em {selection.Count} objetos (marcação incompleta corrigida em pelo menos um)."
                : anyMissing
                    ? $"Tamanho Padrão aplicado/atualizado em {selection.Count} objetos."
                    : $"Tamanho Padrão atualizado em {selection.Count} objetos.";
            return OperationResult.Success(message);
        }

        /// <summary>Lê largura E altura (convertidas via <paramref name="convertFromInches"/>)
        /// ANTES de escrever qualquer coisa — uma falha de leitura/conversão nunca chega a
        /// escrever nada. Se alguma escrita falhar depois de outra já ter sido aplicada, todas
        /// as já aplicadas são desfeitas (melhor esforço — Shape.Properties não tem um commit
        /// atômico de várias entradas de uma vez) em vez de deixar o objeto com uma marcação
        /// parcial (ex.: flag habilitado e largura, sem altura) como efeito colateral de uma
        /// falha. A exceção original sempre é relançada — o chamador (RunWithWorkspace) nunca
        /// anuncia sucesso por isso.</summary>
        private static void WriteStandardSize(ICorelShape shape, Func<double, double> convertFromInches)
        {
            var width = convertFromInches(shape.Width);
            var height = convertFromInches(shape.Height);
            try
            {
                shape.SetProperty(PropertyName, EnabledId, true);
                shape.SetProperty(PropertyName, WidthId, width);
                shape.SetProperty(PropertyName, HeightId, height);
            }
            catch
            {
                try { shape.DeleteProperties(PropertyName); }
                catch { /* melhor esforço de limpeza — a exceção original é o que importa */ }
                throw;
            }
        }
    }
}
