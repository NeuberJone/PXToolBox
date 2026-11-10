using System;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Alinhamento horizontal automático" — vínculo entre DOIS objetos distintos, como o SISBolt
    /// grava (cenário confirmado: referência ainda sem identificador): um GUID novo, como string
    /// com hífens, em GUID_IDENTIFIER_PROPS [0] da REFERÊNCIA e o MESMO valor em
    /// ALIGNMENT_PROPS [5] do objeto A ALINHAR. Só o vínculo: ALIGNMENT_PROPS [0]–[4] (âncora e
    /// margens do "Alinhamento") e todas as outras propriedades ficam como estão.
    ///
    /// Fora do escopo até ser capturado: referência que JÁ tem GUID_IDENTIFIER_PROPS [0] (não se
    /// sabe se o SISBolt reutiliza o identificador) — recusado sem alterar nada.
    /// </summary>
    public sealed class HorizontalAlignmentLinkService
    {
        public const string IdentifierPropertyName = "GUID_IDENTIFIER_PROPS";
        public const int IdentifierId = 0;
        public const string AlignmentPropertyName = "ALIGNMENT_PROPS";
        public const int LinkId = 5;

        /// <summary>Exige exatamente dois objetos distintos. Papéis iniciais: o primeiro da
        /// seleção é o objeto a alinhar e o segundo, a referência — a janela mostra os dois e
        /// deixa trocar antes de salvar.</summary>
        public OperationResult<(ICorelShape Target, ICorelShape Reference)> ValidateSelection(ICorelSelection selection)
        {
            if (selection.Count != 2)
            {
                return OperationResult<(ICorelShape, ICorelShape)>.Fail(OperationStatus.InvalidSelection,
                    "Selecione exatamente dois objetos: o que será alinhado e a referência.");
            }

            var first = selection.Shapes[0];
            var second = selection.Shapes[1];
            if (first.StaticId == second.StaticId)
            {
                return OperationResult<(ICorelShape, ICorelShape)>.Fail(OperationStatus.InvalidSelection, "Selecione dois objetos distintos.");
            }

            return OperationResult<(ICorelShape, ICorelShape)>.Success((first, second));
        }

        /// <summary>Identificador já gravado na referência, ou null.</summary>
        public string? ExistingIdentifier(ICorelShape reference) =>
            reference.HasProperty(IdentifierPropertyName, IdentifierId) ? reference.GetProperty(IdentifierPropertyName, IdentifierId)?.ToString() ?? "" : null;

        /// <summary>Vínculo atual do objeto a alinhar (ALIGNMENT_PROPS [5]), ou null.</summary>
        public string? ExistingLink(ICorelShape target) =>
            target.HasProperty(AlignmentPropertyName, LinkId) ? target.GetProperty(AlignmentPropertyName, LinkId)?.ToString() ?? "" : null;

        /// <summary>Grava o vínculo. Se a segunda escrita falhar, desfaz a primeira (melhor
        /// esforço) e relança — o chamador agrupa tudo num Ctrl+Z.</summary>
        public OperationResult SaveLink(ICorelShape target, ICorelShape reference, Func<Guid> newGuid)
        {
            if (target.StaticId == reference.StaticId)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "O objeto a alinhar e a referência precisam ser objetos distintos.");
            }

            var existing = ExistingIdentifier(reference);
            if (existing != null)
            {
                return OperationResult.Fail(OperationStatus.NotImplemented,
                    $"A referência já tem um identificador ({IdentifierPropertyName} = {existing}). Esse caso ainda não foi mapeado — nada foi alterado.");
            }

            var previousLink = ExistingLink(target);
            var guid = newGuid().ToString("D");
            reference.SetProperty(IdentifierPropertyName, IdentifierId, guid);
            try
            {
                target.SetProperty(AlignmentPropertyName, LinkId, guid);
            }
            catch
            {
                try { reference.DeleteProperty(IdentifierPropertyName, IdentifierId); }
                catch { /* melhor esforço — a exceção original é o que importa */ }
                throw;
            }

            return OperationResult.Success(previousLink == null
                ? $"Vínculo salvo ({guid})."
                : $"Vínculo salvo ({guid}); o objeto alinhado tinha outro vínculo ({previousLink}), substituído.");
        }
    }
}
