using System;
using System.Collections.Generic;

namespace PXToolBox.Core.Naming
{
    /// <summary>
    /// Nomes de objeto considerados "tokens de tamanho/marcação" dentro de um molde, usados por
    /// Correcoes.RenomearObjetosNoMold (NomeEstaNaLista) para decidir quais subitens de um grupo
    /// NÃO devem ser renomeados com o nome da camada (porque já representam um tamanho ou marcação
    /// válidos, não uma peça a identificar).
    /// </summary>
    public static class SizeTokens
    {
        private static readonly HashSet<string> AllowedNamesSet = new HashSet<string>(
            new[]
            {
                "PP", "P", "M", "G", "GG", "XGG", "XXGG",
                "PLPP", "BLP", "BLM", "BLG", "BLGG", "BLXGG", "BLXXGG",
                "2A", "4A", "6A", "8A", "12A",
                "X", "TAG", "MARCAÇÃO"
            },
            StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> AllowedNames => AllowedNamesSet;

        public static bool IsAllowed(string name) => AllowedNamesSet.Contains(name ?? string.Empty);
    }
}
