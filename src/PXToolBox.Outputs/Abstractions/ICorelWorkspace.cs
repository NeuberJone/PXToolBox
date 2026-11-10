using System.Collections.Generic;

namespace PXToolBox.Outputs.Abstractions
{
    /// <summary>
    /// Ponto único de acesso ao estado do Corel usado pelos serviços de Saídas: documento ativo
    /// (ou null se nenhum documento estiver aberto) e seleção atual, além de utilitários de
    /// área de transferência usados por algumas macros originais (ex.: soma de medidas).
    /// </summary>
    public interface ICorelWorkspace
    {
        ICorelDocument? ActiveDocument { get; }

        ICorelSelection ActiveSelection { get; }

        /// <summary>Documentos abertos agora (Application.Documents), lidos de novo a cada acesso.</summary>
        IReadOnlyList<ICorelDocument> OpenDocuments { get; }

        void CopyToClipboard(string text);
    }
}
