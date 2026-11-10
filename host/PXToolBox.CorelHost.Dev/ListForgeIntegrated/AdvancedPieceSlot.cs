using System.Collections.Generic;
using System.Linq;
using ListForge.Core;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Um seletor de tipo de peça por posição de tamanho, para a UI de "Lista avançada" (ver
    /// docs/listforge-classic-parity.md, grupo "Modos e opções de processamento" — equivalente a
    /// AdvancedJsonPieceSlot do standalone). Simplificado de propósito: sem filtro cruzado de
    /// opções já escolhidas em outros slots (o standalone remove a opção já usada da lista de
    /// cada ComboBox) — <c>JsonPieceMappingService.Validate</c>, já chamado dentro de
    /// <c>ProcessingWorkflowService.Execute</c>, rejeita uma ordem com tipos repetidos e isso
    /// aparece como qualquer outro problema de validação na lista de avisos/erros já existente;
    /// a UI não precisa duplicar essa regra só para impedir o usuário de errar antes de
    /// processar.
    /// </summary>
    public sealed class AdvancedPieceSlot
    {
        private static readonly IReadOnlyList<PieceTypeOption> AllOptions =
            new[] { new PieceTypeOption("", "Selecionar") }.Concat(PieceTypeMapper.AvailableOptions).ToList();

        public int Position { get; set; }
        public string PositionLabel => $"{Position}º tamanho";
        public string SelectedKey { get; set; } = "";
        public IReadOnlyList<PieceTypeOption> Options => AllOptions;
    }
}
