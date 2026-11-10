namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Rastreia se a saída/JSON gerados ainda correspondem à entrada e às opções atuais —
    /// equivalente, no integrado, ao par _inputRevision/_comparisonInputRevision do ListForge
    /// standalone (ver docs/listforge-classic-parity.md, grupo "Modos e opções de
    /// processamento"). Não decide nada por conta própria: quem chama decide quando marcar a
    /// entrada como alterada e quando marcar como processada, e o que fazer com
    /// <see cref="IsStale"/> (ver ClassicWorkspaceWindow.ConfirmIfStale).
    /// </summary>
    public sealed class OutputFreshnessTracker
    {
        private int _inputRevision;
        private int _processedRevision = -1;

        /// <summary>Chamar sempre que a entrada OU uma opção que afeta o processamento (separador,
        /// capitalização, ordenação) mudar.</summary>
        public void MarkInputChanged() => _inputRevision++;

        /// <summary>Chamar depois de um processamento que efetivamente gerou saída nova.</summary>
        public void MarkProcessed() => _processedRevision = _inputRevision;

        /// <summary>true se a entrada/opções mudaram desde o último processamento (ou se nunca
        /// houve processamento nenhum).</summary>
        public bool IsStale => _processedRevision != _inputRevision;
    }
}
