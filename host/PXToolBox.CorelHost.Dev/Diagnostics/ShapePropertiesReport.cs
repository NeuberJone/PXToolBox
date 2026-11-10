using System;
using System.Collections.Generic;

namespace PXToolBoxDev.Diagnostics
{
    public enum ShapeSelectionStatus
    {
        NotConnected,
        NoDocument,
        NoSelection,
        MultipleSelected,
        Ok,
        ShapeNoLongerAvailable,
    }

    /// <summary>Uma entrada de <c>Shape.Properties</c> (ver
    /// Corel.Interop.VGCore.IVGProperties.Description/ItemByIndex — assinatura real confirmada
    /// por reflection sobre Corel.Interop.VGCore.dll instalado antes de implementar, não
    /// suposta). <see cref="Index"/> é o índice 1-based real usado por
    /// <c>Description(Index, ref Name, ref ID)</c>/<c>ItemByIndex[Index]</c> (confirmado por
    /// evidência real — índice 0 sempre falha com "objeto não encontrado"; corrigido de uma
    /// suposição 0-based anterior) — <see cref="Id"/> é o "ID" numérico que, junto com
    /// <see cref="Name"/>, identifica a propriedade na API (<c>Item[Name, ID]</c>).</summary>
    public sealed class ShapePropertyEntry
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public int Id { get; set; }
        public string ValueDisplay { get; set; } = "";
        public string ValueType { get; set; } = "";

        /// <summary>Não nulo quando ler ESTA propriedade específica falhou — nunca interrompe a
        /// leitura das demais (pedido explícito da tarefa).</summary>
        public string? ReadError { get; set; }
    }

    /// <summary>Contexto do objeto selecionado — só para facilitar comparação, nunca tratado
    /// como parte de Shape.Properties (Name/StaticID são membros próprios de Shape).</summary>
    public sealed class ShapeObjectSummary
    {
        public string Name { get; set; } = "";
        public string ShapeType { get; set; } = "";
        public string LayerName { get; set; } = "";
        public int PageIndex { get; set; }
        public int StaticId { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    /// <summary>Contexto de diagnóstico que não depende da seleção — pedido explícito do
    /// usuário depois de uma investigação real precisar de várias idas e voltas pra descobrir
    /// a versão instalada e qual unidade cada preferência do Corel estava usando. Sempre
    /// preenchido quando possível, mesmo sem seleção/documento, pra nunca precisar perguntar
    /// de novo "qual versão/unidade era essa inspeção".</summary>
    public sealed class ShapeDiagnosticContext
    {
        /// <summary>AssemblyInformationalVersion do PX Tool Box (mesma fonte de
        /// ControlUI.ResolveAppVersion) — "0.0.0-dev.local" fora de uma build real.</summary>
        public string ToolVersion { get; set; } = "";

        /// <summary>Document.Unit real (rótulo + nome técnico cdrXxx) — null/"" sem documento.
        /// Confirmado por evidência real: NÃO é a unidade que o Tamanho Padrão do SISBolt usa
        /// (ver StandardSizeService) — só informativo, para comparação.</summary>
        public string DocumentUnitLabel { get; set; } = "";

        /// <summary>Application.Unit real — também confirmado por evidência real que NÃO é a
        /// unidade que o SISBolt usa; mostrado só para eliminar essa hipótese nas próximas
        /// investigações sem precisar perguntar de novo.</summary>
        public string ApplicationUnitLabel { get; set; } = "";

        /// <summary>Nome do documento ativo (Document.Name) — "" sem documento. Ajuda a
        /// confirmar que duas inspeções comparadas são do mesmo arquivo.</summary>
        public string DocumentName { get; set; } = "";

        /// <summary>Document.Rulers.HUnits/VUnits — terceiro candidato real a "unidade que a
        /// UI mostra" (régua/barra de propriedades), diferente de Document.Unit e
        /// Application.Unit (ambas confirmadas por evidência real como cdrInch mesmo com a UI
        /// mostrando cm, inclusive com o documento já salvo). Nome H/V porque a API separa
        /// unidade horizontal/vertical — mostrado só para eliminar/confirmar esta hipótese sem
        /// precisar de mais uma rodada de adivinhação.</summary>
        public string RulersHUnitLabel { get; set; } = "";

        public string RulersVUnitLabel { get; set; } = "";
    }

    public sealed class ShapePropertiesReport
    {
        public ShapeSelectionStatus Status { get; set; }
        public string StatusMessage { get; set; } = "";
        public ShapeDiagnosticContext Diagnostics { get; set; } = new();
        public ShapeObjectSummary? Object { get; set; }
        public IReadOnlyList<ShapePropertyEntry> Properties { get; set; } = Array.Empty<ShapePropertyEntry>();
    }
}
