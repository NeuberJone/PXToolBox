using System;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace PXToolBoxDev.Diagnostics
{
    /// <summary>
    /// Diagnóstico somente leitura para o sintoma relatado pelo usuário: às vezes um painel
    /// recolhível para de atualizar visualmente — o espaço do conteúdo fica reservado (o
    /// Expander está de fato expandido), mas os controles dentro dele não aparecem, até
    /// redimensionar/minimizar-restaurar/reabrir o painel.
    ///
    /// <para><b>Investigação (ver branch fix/corel-panel-rendering)</b>: revisão estática deste
    /// repositório e, como referência de leitura (nunca alterado por esta classe nem por
    /// nenhuma outra parte deste add-in — é outro produto, outro repositório, AGENTS.md), do
    /// MoldMotor em <c>C:\Projetos\MoldMotor</c>, versão instalada confirmada v1.0.6 (working
    /// tree atual do repositório — HEAD ainda está em 1.0.5; <c>MoldMotorV1</c>/<c>V2</c>,
    /// apesar do nome, são builds MAIS ANTIGAS, 1.0.4/1.0.3, e não correspondem às capturas de
    /// tela recebidas). Nenhuma causa foi demonstrada ao vivo dentro do CorelDRAW real nesta
    /// rodada (não reproduzido nesta sessão — sem acesso a uma instância real do Corel).
    ///
    /// <b>Correção importante</b>: as duas capturas de tela recebidas mostram o MoldMotor
    /// (v1.0.6) com conteúdo ausente em "Renomear seleção"/"Zerar ângulos" (espaço reservado,
    /// controles ausentes) e o PX Tool Box DEV renderizando normalmente NO MESMO INSTANTE — isso
    /// NÃO comprova falha simultânea dos dois add-ins; só comprova a falha no MoldMotor naquele
    /// momento. Por isso a hipótese anterior desta classe ("estado global do host, como
    /// <c>Application.Optimization</c>, afetando os dois ao mesmo tempo") perdeu força como
    /// explicação principal — continua sendo uma hipótese legítima para OUTRAS ocorrências, mas
    /// não é mais o candidato líder.
    ///
    /// <b>Achado mais concreto desta rodada</b> (MoldMotor, não alterado):
    /// <c>src\MoldMotor.CorelAddin\Resources\Controls.xaml</c>, estilo <c>ConfigSection</c> —
    /// EXATAMENTE o estilo usado pelas duas seções mostradas quebradas na captura
    /// (<c>MoldsView.xaml</c>, "RENOMEAR SELEÇÃO"/"ZERAR ÂNGULOS") — anima a Opacity do corpo do
    /// bloco de 0 a 1 via <c>Trigger.EnterActions</c>/<c>BeginStoryboard</c> quando
    /// <c>IsExpanded</c> vira <c>True</c>, sem <c>Trigger.ExitActions</c> correspondente e sem
    /// nenhum <c>Opacity</c> base explícito no elemento animado. O estilo VIZINHO no MESMO
    /// arquivo (<c>ConfigSectionHeaderToggle</c>, o chevron do cabeçalho) tem um comentário do
    /// próprio desenvolvedor do MoldMotor dizendo que ELE deliberadamente evita animar por
    /// transform "porque é o caminho mais estável dentro do CorelDRAW" — ou seja, a equipe do
    /// MoldMotor já documentou que animações são a parte frágil dentro deste host, e a única
    /// animação que sobrou no arquivo é exatamente a que explicaria "espaço reservado, conteúdo
    /// ausente" (um valor de Opacity que não chegou a 1, por qualquer motivo: a entrada no
    /// trigger não disparar do jeito esperado, o storyboard ser interrompido, ou o alvo nomeado
    /// não ser resolvido a tempo). Isto é uma HIPÓTESE fortemente sustentada por leitura de
    /// código (inclusive pelo próprio comentário da equipe do MoldMotor sobre animações nesse
    /// host), não uma causa confirmada ao vivo — e vive inteiramente no repositório do
    /// MoldMotor, não neste. O template do <c>Expander</c> deste host
    /// (<c>ControlUI.xaml</c>, estilo <c>Expander</c> padrão) foi lido por completo: não tem
    /// nenhuma animação, <c>Storyboard</c> ou <c>CacheMode</c> — é um <c>ToggleButton</c> +
    /// <c>ContentPresenter.Visibility</c> comuns, sem o mesmo risco. Nenhum bug equivalente foi
    /// demonstrado neste repositório.
    ///
    /// <c>Application.Optimization</c> continua sendo uma hipótese aberta para casos em que
    /// REALMENTE se observe os dois add-ins (ou qualquer painel nativo do CorelDRAW, não só
    /// WPF) travados ao mesmo tempo — por isso esta classe continua relatando o valor atual.</para>
    ///
    /// <para>Esta classe NUNCA modifica <c>Optimization</c> nem nenhum outro estado do host — só
    /// lê e relata. Nunca força <c>RenderOptions</c>/<c>Dispatcher</c> globalmente. Nunca lê ou
    /// grava conteúdo de documentos/listas. <see cref="DescribeExpanderContents"/> só percorre a
    /// árvore visual DESTE add-in (nunca tenta alcançar janelas/controles do MoldMotor ou de
    /// outro processo).</para>
    /// </summary>
    internal static class PanelRenderDiagnostics
    {
        public static string Describe(object? corelApplication, DependencyObject? ownVisualRoot)
        {
            var sb = new StringBuilder();
            sb.Append("Diagnóstico de redesenho (").Append(DateTime.Now.ToString("HH:mm:ss")).Append("):").Append(Environment.NewLine);
            sb.Append(" - Thread de UI respondeu a este clique agora mesmo: sim — se a thread estivesse bloqueada, este texto não teria aparecido. Descarta \"thread bloqueada\" neste instante específico (não garante que não tenha bloqueado antes ou depois).").Append(Environment.NewLine);
            sb.Append(" - Application.Optimization (CorelDRAW, propriedade COM compartilhada entre TODOS os add-ins do processo): ").Append(DescribeOptimization(corelApplication)).Append(Environment.NewLine);
            sb.Append(" - RenderOptions.ProcessRenderMode (WPF, neste processo): ").Append(DescribeProcessRenderMode()).Append(Environment.NewLine);
            sb.Append(" - RenderCapability.Tier (aceleração de hardware disponível ao WPF agora): ").Append(DescribeRenderTier()).Append(Environment.NewLine);
            sb.Append(DescribeExpanderContents(ownVisualRoot));
            return sb.ToString();
        }

        /// <summary>
        /// Percorre a árvore visual a partir de <paramref name="root"/> (sempre deste add-in —
        /// nunca de outro processo/produto) e relata, para cada <see cref="Expander"/>
        /// encontrado: se está expandido e, quando está, a <c>Visibility</c>/<c>Opacity</c>/
        /// tamanho real do elemento de conteúdo nomeado "Content" pelo template próprio deste
        /// host (ver <c>ControlUI.xaml</c>). Serve para distinguir, da próxima vez que ESTE
        /// painel (não o do MoldMotor) parecer "sem atualizar": (1) conteúdo existente mas sem
        /// pintura — não detectável por aqui, exigiria captura de tela pixel a pixel; (2)
        /// Visibility/Opacity incorretos — detectado diretamente abaixo; (3) conteúdo não criado
        /// — <c>VisualChildrenCount</c> zerado quando devia ter filhos; (4) layout com medida
        /// errada — <c>ActualWidth</c>/<c>ActualHeight</c> zerados com Visibility=Visible.
        /// Nunca força nenhuma atualização/InvalidateVisual — só lê o estado atual.
        /// </summary>
        public static string DescribeExpanderContents(DependencyObject? root)
        {
            if (root == null)
            {
                return " - Árvore visual deste painel: não disponível (sem referência ao controle raiz).";
            }

            var sb = new StringBuilder();
            var found = 0;
            WalkExpanders(root, sb, ref found);

            return found == 0
                ? " - Árvore visual deste painel: nenhum Expander encontrado (inesperado — confira se a árvore mudou)."
                : sb.ToString().TrimEnd(Environment.NewLine.ToCharArray());
        }

        private static void WalkExpanders(DependencyObject node, StringBuilder sb, ref int found)
        {
            if (node is Expander expander)
            {
                found++;
                var header = (expander.Header as string) ?? expander.Header?.ToString() ?? "(sem título)";
                sb.Append(" - Expander \"").Append(header).Append("\": IsExpanded=").Append(expander.IsExpanded);

                if (expander.IsExpanded)
                {
                    var content = expander.Template?.FindName("Content", expander) as FrameworkElement;
                    if (content == null)
                    {
                        sb.Append(" | elemento de conteúdo \"Content\" não encontrado no template (template mudou?).");
                    }
                    else
                    {
                        sb.Append(" | Content.Visibility=").Append(content.Visibility)
                          .Append(" Opacity=").Append(content.Opacity.ToString("0.00"))
                          .Append(" ActualWidth=").Append(content.ActualWidth.ToString("0"))
                          .Append(" ActualHeight=").Append(content.ActualHeight.ToString("0"))
                          .Append(" VisualChildrenCount=").Append(VisualTreeHelper.GetChildrenCount(content));

                        var suspicious = content.Visibility != Visibility.Visible
                            || content.Opacity < 0.99
                            || content.ActualHeight <= 0
                            || VisualTreeHelper.GetChildrenCount(content) == 0;
                        if (suspicious)
                        {
                            sb.Append(" [SUSPEITO — expandido mas com Visibility/Opacity/tamanho/conteúdo fora do esperado]");
                        }
                    }
                }

                sb.Append(Environment.NewLine);
            }

            var childCount = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < childCount; i++)
            {
                WalkExpanders(VisualTreeHelper.GetChild(node, i), sb, ref found);
            }
        }

        private static string DescribeOptimization(object? corelApplication)
        {
            if (corelApplication == null)
            {
                return "não disponível (sem conexão com o CorelDRAW neste momento).";
            }

            try
            {
                // Reflection em vez de "dynamic": este projeto não referencia Microsoft.CSharp
                // (dynamic exigiria adicionar essa referência só para esta leitura pontual).
                // PropertyInfo.GetValue sobre um objeto COM funciona pelo mesmo mecanismo de
                // late binding, sem a dependência extra.
                var property = corelApplication.GetType().GetProperty("Optimization");
                if (property == null)
                {
                    return "indisponível nesta versão do CorelDRAW (propriedade Optimization não encontrada).";
                }

                bool value = Convert.ToBoolean(property.GetValue(corelApplication));
                return value
                    ? "True — redesenho da tela suspenso pelo CorelDRAW. Se nenhuma operação do MoldMotor/PX Tool Box estiver em andamento agora, isto por si só já é evidência forte da causa (ver comentário desta classe); redimensionar ou minimizar/restaurar a janela do CorelDRAW deve recuperar a imagem enquanto isto estiver True."
                    : "False (normal).";
            }
            catch (Exception ex)
            {
                return $"indisponível nesta versão do CorelDRAW ({ex.GetType().Name}: {ex.Message}).";
            }
        }

        private static string DescribeProcessRenderMode()
        {
            try
            {
                return RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly
                    ? "SoftwareOnly (renderização por software forçada neste processo — não é o padrão; pode indicar problema de composição/driver)."
                    : "Default (aceleração de hardware permitida — padrão).";
            }
            catch (Exception ex)
            {
                return $"indisponível ({ex.GetType().Name}).";
            }
        }

        private static string DescribeRenderTier()
        {
            try
            {
                int tier = RenderCapability.Tier >> 16;
                return tier switch
                {
                    0 => "0 (sem aceleração de hardware — tudo via software neste momento; pode indicar problema de composição/driver).",
                    1 => "1 (aceleração parcial).",
                    2 => "2 (aceleração completa — normal em hardware moderno).",
                    _ => tier + " (valor não documentado pela plataforma).",
                };
            }
            catch (Exception ex)
            {
                return $"indisponível ({ex.GetType().Name}).";
            }
        }
    }
}
