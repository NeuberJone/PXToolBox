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

        /// <summary>Converte um valor em polegadas (unidade real de Shape.Width/Height — ver
        /// StandardSizeService) pra unidade que a régua/barra de propriedades do Corel mostra
        /// NESTE momento — real: Application.ConvertUnits(valor, cdrInch, Document.Rulers.
        /// HUnits). Preferido sobre <see cref="Unit"/> (Document.Unit) ou Application.Unit:
        /// DUAS tentativas anteriores usando essas propriedades, cada uma confirmada por
        /// evidência real (documentos em mm e em cm, inclusive já salvos), reportaram cdrInch
        /// mesmo com a UI mostrando outra unidade — nenhuma das duas reflete o que o usuário
        /// configurou. Document.Rulers.HUnits foi a única, dentre todas as propriedades de
        /// unidade expostas por Corel.Interop.VGCore (levantadas por reflection, não
        /// adivinhadas uma a uma), que bateu com a UI nos mesmos testes.</summary>
        double ConvertFromInches(double inches);

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

        /// <summary>
        /// Todas as camadas do documento, cada uma uma única vez: as camadas locais de cada
        /// página mais as camadas da página mestra (onde ficam as camadas mestras e as
        /// especiais). Lida de novo a cada acesso — nunca guardar a lista entre operações.
        /// </summary>
        IReadOnlyList<ICorelLayer> AllLayers { get; }

        /// <summary>
        /// Identidade opaca do documento durante a sessão — no host, o próprio objeto COM (RCW).
        /// Só pode ser comparada com <see cref="object.ReferenceEquals"/>; nunca invocada nem
        /// convertida. Um arquivo fechado e reaberto é um objeto novo, com identidade nova.
        /// </summary>
        object Identity { get; }

        void SaveAsCdr(string fullPath);

        /// <summary>Salva SÓ a seleção atual num arquivo novo, sem que o documento passe a apontar
        /// para ele (Document.SaveAsCopy com Range = cdrSelection, sobrescrevendo).</summary>
        void SaveSelectionAsCopy(string fullPath);

        /// <summary>Imagem PNG (RGB, fundo transparente) SÓ da seleção atual, com o tamanho pedido em pixels
        /// (Document.ExportBitmap com Range = cdrSelection + ExportFilter.Finish). Não muda o documento.</summary>
        void ExportSelectionPng(string fullPath, int widthPixels, int heightPixels);
    }
}
