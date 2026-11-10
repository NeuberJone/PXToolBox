using System;
using System.Linq;
using PXToolBoxDev.Integrations;

namespace PXToolBoxDev.Production
{
    /// <summary>
    /// Resumo curto do contexto de produção carregado, para a seção Produção do painel — só com
    /// dados do próprio arquivo exportado pelo Nexor (nada consultado no Nexor, no Corel nem no
    /// disco). Mesmos termos de <see cref="OrderContextWindow"/>: "rolo", "trabalho de impressão"
    /// e "pedido informado" — nunca "pedido" como identificador, nunca "Job". Linha nula = o
    /// arquivo não traz esse dado e a linha não aparece.
    /// </summary>
    public sealed class OrderContextSummary
    {
        private OrderContextSummary(string rollLine, string jobLine, string? pedidoLine, string exportedLine)
        {
            RollLine = rollLine;
            JobLine = jobLine;
            PedidoLine = pedidoLine;
            ExportedLine = exportedLine;
        }

        public string RollLine { get; }
        public string JobLine { get; }
        public string? PedidoLine { get; }
        public string ExportedLine { get; }

        public static OrderContextSummary Build(OrderContextService.OrderContext context)
        {
            var roll = string.IsNullOrWhiteSpace(context.RollName)
                ? $"Rolo #{context.RollId}"
                : $"Rolo {context.RollName} (#{context.RollId})";
            if (!string.IsNullOrWhiteSpace(context.Machine))
            {
                roll += $" · máquina {context.Machine}";
            }

            // O trabalho de impressão é reconhecido pelo arquivo de origem; sem ele, o código do
            // trabalho no Nexor (jobHash, sempre presente — campo obrigatório do contrato).
            var fileName = SafeFileName(context.SourceFileReference);
            var job = fileName != null
                ? $"Trabalho de impressão: {fileName}"
                : $"Trabalho de impressão: código {context.JobHash}";
            var extras = string.Join(" · ", new[] { context.Tipo, context.Fabric }
                .Where(v => !string.IsNullOrWhiteSpace(v)));
            if (extras.Length > 0)
            {
                job += $" ({extras})";
            }

            var pedido = context.HasPedidoLabel ? $"Pedido informado: {context.PedidoLabel}" : null;
            var exported = $"Exportado do Nexor em {context.ExportedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
            return new OrderContextSummary(roll, job, pedido, exported);
        }

        /// <summary>
        /// Sugestão de nome a partir do contexto — usada em "Nome do layout" (Preparar
        /// Escudo/Bandeira/Faixa), no campo de Renomear Objetos e no nome da página ativa: o
        /// pedido informado; sem ele, o nome do arquivo de origem sem extensão; null se nenhum dos
        /// dois existir. O nome entra no arquivo salvo ("Escudo - &lt;layout&gt;.cdr"), então
        /// caracteres inválidos em nome de arquivo do Windows viram espaço e espaços repetidos
        /// são reduzidos — o resto do texto do Nexor é mantido como está.
        /// </summary>
        public static string? SuggestLayoutName(OrderContextService.OrderContext context)
        {
            string? raw = context.HasPedidoLabel ? context.PedidoLabel : null;
            if (raw == null)
            {
                var file = SafeFileName(context.SourceFileReference);
                if (file != null)
                {
                    var dot = file.LastIndexOf('.');
                    raw = dot > 0 ? file.Substring(0, dot) : file;
                }
            }

            return raw == null ? null : CleanForFileName(raw);
        }

        /// <summary>
        /// Subpasta do rolo para Escudo/Bandeira/Faixa (dentro da pasta de saída efetiva): o nome
        /// do rolo; sem ele, "Rolo &lt;número&gt;". Mesma limpeza de
        /// <see cref="SuggestLayoutName"/> — é um nome de pasta.
        /// </summary>
        public static string RollFolderName(OrderContextService.OrderContext context) =>
            (string.IsNullOrWhiteSpace(context.RollName) ? null : CleanForFileName(context.RollName))
            ?? $"Rolo {context.RollId}";

        /// <summary>Linha curta para a mensagem e o log de uma saída salva com contexto
        /// carregado — só identifica de qual rolo/trabalho veio; nada é enviado ao Nexor.</summary>
        public static string OutputNote(OrderContextService.OrderContext context)
        {
            var roll = string.IsNullOrWhiteSpace(context.RollName) ? $"rolo #{context.RollId}" : $"rolo {context.RollName}";
            var file = SafeFileName(context.SourceFileReference);
            return file != null
                ? $"Contexto: {roll} · trabalho {file}"
                : $"Contexto: {roll} · trabalho código {context.JobHash}";
        }

        // Caracteres inválidos em nome de arquivo do Windows viram espaço; espaços repetidos são
        // reduzidos; pontos/espaços nas pontas saem. Nada aproveitável → null.
        private static string? CleanForFileName(string raw)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var cleaned = new string(raw.Select(c => invalid.Contains(c) ? ' ' : c).ToArray());
            cleaned = string.Join(" ", cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)).Trim(' ', '.');
            return cleaned.Length == 0 ? null : cleaned;
        }

        // Só o nome, de um caminho que pode ser de outra máquina — nunca acessa o arquivo.
        private static string? SafeFileName(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return null;
            var trimmed = reference!.Trim();
            var slash = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
            var name = slash >= 0 ? trimmed.Substring(slash + 1) : trimmed;
            return name.Length == 0 ? null : name;
        }
    }
}
