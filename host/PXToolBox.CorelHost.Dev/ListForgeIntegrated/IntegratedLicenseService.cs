using ListForge.Services;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Implementação de <see cref="ILicenseService"/> usada pelo modo Clássico do ListForge
    /// Integrado dentro do PX Tool Box DEV.
    ///
    /// DECISÃO PENDENTE (ver docs/ARCHITECTURE.md, seção 10): o ListForge standalone controla
    /// seus créditos Trial através de TrialManager/ConfigManager — infraestrutura ligada a
    /// caminhos da máquina e DPAPI que foi deliberadamente EXCLUÍDA do núcleo compartilhado
    /// (ListForge.Core) por não ser "processamento". Por isso este host não tem, hoje, acesso a
    /// nenhum mecanismo real de licença/Trial do ListForge para aplicar, compartilhar ou
    /// contornar — não existe verificação alcançável aqui para contornar.
    ///
    /// Esta implementação é deliberadamente "sem restrição" (nunca bloqueia o processamento),
    /// mas NUNCA se apresenta como uma decisão de licenciamento já tomada: Edition e
    /// ProcessingStatusSuffix dizem explicitamente, na própria interface, que a decisão está
    /// pendente. Não consome nem lê o estado Trial do standalone — os dois ficam completamente
    /// independentes até essa decisão ser tomada conscientemente.
    /// </summary>
    public sealed class IntegratedLicenseService : ILicenseService
    {
        public string Edition => "Integrado (DEV) — licenciamento ainda não decidido";

        public bool IsTrial => false;

        public int ProcessingLimit => int.MaxValue;

        public int RemainingProcessings => int.MaxValue;

        public bool CanProcess => true;

        public string ProcessingStatusSuffix =>
            " | Licenciamento do modo integrado: decisão pendente — ver docs/ARCHITECTURE.md, seção 10.";

        public void ConsumeSuccessfulProcessing()
        {
            // Intencionalmente vazio: não há estado de Trial próprio do modo integrado ainda
            // (decisão pendente) e este serviço nunca deve escrever no estado Trial do
            // ListForge standalone (TrialManager), que pertence só a ele.
        }
    }
}
