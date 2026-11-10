using System;
using System.IO;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Lógica pura (testável sem WPF) para carregar um contexto de produção a partir de um
    /// CAMINHO digitado/colado pelo usuário — complementa o seletor de arquivo existente
    /// (ver ControlUI.xaml.cs, "Carregar contexto de produção…") com "Colar caminho do
    /// contexto…" (ver Production/OrderContextPathDialog). Usa o MESMO
    /// <see cref="OrderContextService.Validate"/> do importador por seletor — nunca uma
    /// segunda validação divergente.
    ///
    /// Nunca interpreta o texto como comando (só <see cref="File.Exists"/>/
    /// <see cref="Directory.Exists"/>/<see cref="File.ReadAllText(string)"/> — nenhum
    /// Process.Start, nenhuma chamada de rede para um texto com aparência de URL: um
    /// "caminho" como "https://..." simplesmente falha em File.Exists, como qualquer caminho
    /// inexistente, sem nenhum tratamento especial).
    /// </summary>
    public static class OrderContextPathLoader
    {
        public sealed class LoadResult
        {
            public bool IsValid { get; }
            public string? FailureReason { get; }
            public OrderContextService.OrderContext? Context { get; }
            public string? NormalizedPath { get; }

            private LoadResult(bool isValid, string? failureReason, OrderContextService.OrderContext? context, string? normalizedPath)
            {
                IsValid = isValid;
                FailureReason = failureReason;
                Context = context;
                NormalizedPath = normalizedPath;
            }

            public static LoadResult Fail(string reason) => new(false, reason, null, null);
            public static LoadResult Ok(OrderContextService.OrderContext context, string normalizedPath) =>
                new(true, null, context, normalizedPath);
        }

        /// <summary>Remove espaço em volta e um único par de aspas externas (o "Copiar como
        /// caminho" do Explorer envolve o caminho inteiro em aspas) — nunca remove aspas
        /// internas/desbalanceadas, só o par externo completo.</summary>
        public static string NormalizePath(string? rawInput)
        {
            var trimmed = (rawInput ?? string.Empty).Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            return trimmed;
        }

        /// <summary>Nunca lança: todo problema (caminho vazio, inexistente, diretório, arquivo
        /// ilegível, JSON incompatível) resulta num resultado inválido com motivo claro —
        /// mesma garantia de <see cref="OrderContextService.Validate"/>.</summary>
        public static LoadResult Load(string? rawInput)
        {
            var path = NormalizePath(rawInput);
            if (string.IsNullOrEmpty(path))
                return LoadResult.Fail("Informe o caminho do arquivo .json.");

            if (Directory.Exists(path))
                return LoadResult.Fail("O caminho indicado é uma pasta, não um arquivo.");

            if (!File.Exists(path))
                return LoadResult.Fail($"Arquivo não encontrado: {path}");

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                return LoadResult.Fail($"Não foi possível ler o arquivo.\n\n{ex.GetType().Name}: {ex.Message}");
            }

            var validation = OrderContextService.Validate(json);
            if (!validation.IsValid)
                return LoadResult.Fail(validation.FailureReason ?? "Contexto de produção inválido.");

            return LoadResult.Ok(validation.Context!, path);
        }
    }
}
