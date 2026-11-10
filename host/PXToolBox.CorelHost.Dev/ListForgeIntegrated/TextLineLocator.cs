using System;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Calcula o índice/comprimento de uma linha (numeração 1-based, igual a
    /// ValidationIssue.LineNumber) dentro de um texto com quebras "\n" ou "\r\n" — usado para
    /// navegar da lista de avisos/erros até a linha correspondente no editor de entrada
    /// (TextBox.Select/ScrollToLine). Lógica pura, sem depender de WPF, para ser testável sem
    /// uma janela real.
    /// </summary>
    public static class TextLineLocator
    {
        public static int GetLineStartIndex(string text, int oneBasedLineNumber)
        {
            if (oneBasedLineNumber <= 1 || text.Length == 0) return 0;

            var currentLine = 1;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    currentLine++;
                    if (currentLine == oneBasedLineNumber) return i + 1;
                }
            }

            // Linha pedida além do fim do texto: aponta para o fim (nunca lança).
            return text.Length;
        }

        public static int GetLineLength(string text, int oneBasedLineNumber)
        {
            var start = GetLineStartIndex(text, oneBasedLineNumber);
            var end = text.IndexOf('\n', start);
            if (end < 0) end = text.Length;
            if (end > start && text[end - 1] == '\r') end--;
            return Math.Max(0, end - start);
        }
    }
}
