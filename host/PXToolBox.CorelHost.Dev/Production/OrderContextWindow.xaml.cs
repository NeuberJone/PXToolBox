using System;
using System.IO;
using System.Windows;
using PXToolBoxDev.Integrations;

namespace PXToolBoxDev.Production
{
    /// <summary>
    /// Janela de exibição do contexto de produção importado (ver
    /// Integrations/OrderContextService.cs e docs/integrations/nexor-order-context.md) — só
    /// leitura, só memória nesta entrega. O Nexor não tem pedido comercial formal — por isso
    /// o texto exibido aqui fala em "trabalho de impressão"/"rolo"/"pedido informado", nunca
    /// em "pedido" como identificador nem em "Job". Sem ThreadKeyboardRouter: não há nenhuma
    /// caixa de texto para editar aqui (mesmo padrão de ProcessingHistoryWindow, que também é
    /// só leitura).
    /// </summary>
    public partial class OrderContextWindow : Window
    {
        private OrderContextService.OrderContext? _current;

        /// <summary>True quando o usuário já tem um contexto carregado nesta janela — usado por
        /// quem chama (ControlUI) para decidir entre Activate() e reabrir o seletor de
        /// arquivo.</summary>
        public bool HasContext => _current != null;

        public OrderContextWindow()
        {
            InitializeComponent();
        }

        public void LoadContext(OrderContextService.OrderContext context)
        {
            _current = context;
            StatusText.Text = "";

            ExportedAtText.Text = $"Exportado em {context.ExportedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm} (horário local desta máquina).";

            RollText.Text = string.IsNullOrWhiteSpace(context.RollName)
                ? $"Rolo #{context.RollId}"
                : $"Rolo #{context.RollId} — {context.RollName}";
            MachineText.Text = string.IsNullOrWhiteSpace(context.Machine) ? "" : $"Máquina: {context.Machine}";

            // RollId/JobHash só são garantidamente únicos DENTRO da mesma base de dados do
            // Nexor — nunca globalmente (ver docs/ARCHITECTURE.md, seções 22-23). Dois campos
            // opcionais ajudam a desambiguar, com garantias DIFERENTES — nunca apresentados
            // como identidade global: InstallationId é por instalação/máquina (pode repetir
            // entre bases diferentes da mesma instalação, ou duplicar se a config for copiada
            // sem a base); DatabaseId é gravado dentro do próprio arquivo .db (mais confiável
            // para saber se dois contextos vêm da MESMA base — sobrevive a cópia/restauração
            // e a bases compartilhadas em rede).
            InstallationText.Text = string.IsNullOrWhiteSpace(context.InstallationId)
                ? "Instalação de origem não identificada neste arquivo (gerado por uma versão anterior do exportador, ou id indisponível na máquina de origem)."
                : $"Instalação de origem: {context.InstallationId} (identificador da máquina/config — não prova por si só que duas exportações vêm da mesma base de dados).";

            DatabaseText.Text = string.IsNullOrWhiteSpace(context.DatabaseId)
                ? "Base de dados de origem não identificada neste arquivo (gerado por uma versão anterior do exportador)."
                : $"Base de dados de origem: {context.DatabaseId} (gravado no próprio arquivo — igual para a mesma base, mesmo copiada ou compartilhada entre máquinas).";

            // Pedido pode vir vazio do exportador (trabalho sem pedido informado no Nexor).
            PedidoLabelText.Text = context.HasPedidoLabel ? context.PedidoLabel : "(não informado neste arquivo)";
            var pedidoDetails = !context.HasPedidoLabel ? ""
                : context.PedidoManual ? "Etiqueta corrigida manualmente pelo operador no Nexor."
                : "Etiqueta classificada automaticamente pelo Nexor.";
            if (!string.IsNullOrWhiteSpace(context.Fabric)) pedidoDetails += $" Tecido: {context.Fabric}.";
            if (!string.IsNullOrWhiteSpace(context.Tipo)) pedidoDetails += $" Tipo: {context.Tipo}.";
            PedidoDetailsText.Text = pedidoDetails.Trim();

            SourceFileText.Text = string.IsNullOrWhiteSpace(context.SourceFileReference)
                ? "(não informado pelo Nexor)"
                : context.SourceFileReference;

            if (string.IsNullOrWhiteSpace(context.SourceFileReference))
            {
                SourceFileAvailabilityText.Text = "";
            }
            else
            {
                // Só confere EXISTÊNCIA para informar — nunca abre, nunca lê o conteúdo.
                var exists = SafeFileExists(context.SourceFileReference!);
                SourceFileAvailabilityText.Text = exists
                    ? "Encontrado nesta máquina."
                    : "Não encontrado nesta máquina — o Nexor normalmente roda numa estação diferente.";
            }
        }

        private static bool SafeFileExists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private void OnLoadAnotherClick(object sender, RoutedEventArgs e)
        {
            if (_current != null)
            {
                var choice = MessageBox.Show(
                    "Carregar outro contexto substitui o contexto atual nesta tela. Continuar?",
                    "PX Tool Box DEV",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (choice != MessageBoxResult.Yes) return;
            }

            RequestLoadAnother?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>ControlUI assina este evento para reabrir o seletor de arquivo — mantém a
        /// decisão de QUAL arquivo escolher no mesmo lugar que já trata o primeiro carregamento,
        /// em vez de duplicar o diálogo aqui.</summary>
        public event EventHandler? RequestLoadAnother;

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            if (_current == null)
            {
                Close();
                return;
            }

            var choice = MessageBox.Show(
                "Limpar o contexto de produção carregado? Isso não afeta o Nexor nem nenhum dado do ListForge.",
                "PX Tool Box DEV",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (choice != MessageBoxResult.Yes) return;

            // O contexto é do painel (sessão) — o painel limpa o resumo e fecha esta janela.
            _current = null;
            if (RequestClear != null)
            {
                RequestClear.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Close();
            }
        }

        /// <summary>ControlUI assina para limpar o contexto da sessão (resumo no painel).</summary>
        public event EventHandler? RequestClear;

        private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
    }
}
