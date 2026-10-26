using System.Collections.Generic;
using PXToolBox.Core.Measurement;

namespace PXToolBox.Outputs.Abstractions
{
    public interface ICorelDocument
    {
        string Name { get; set; }

        /// <summary>Caminho completo do arquivo, ou string vazia se o documento ainda não foi salvo.</summary>
        string FullFileName { get; }

        DocumentUnit? Unit { get; }

        ICorelPage ActivePage { get; }

        /// <summary>
        /// Camada ativa (destacada no Gerenciador de Objetos). A API do CorelDRAW não expõe
        /// multi-seleção de camadas — só esta, singular — por isso ferramentas que precisam de
        /// "camada(s) selecionada(s)" usam esta propriedade e deixam isso explícito na UI, nunca
        /// substituindo silenciosamente por "todas as camadas" ou "camadas dos objetos
        /// selecionados" (ver PieceTypeRenameService.RenameActiveLayer).
        /// </summary>
        ICorelLayer? ActiveLayer { get; }

        IReadOnlyList<ICorelPage> Pages { get; }

        void SaveAsCdr(string fullPath);
    }
}
