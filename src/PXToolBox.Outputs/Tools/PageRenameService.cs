using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// Renomeia a página ativa (usado por "Nomear página com o contexto", seção Produção). Nome
    /// vazio não muda nada; a mensagem traz o nome anterior para poder ser desfeito à mão além
    /// do Ctrl+Z.
    /// </summary>
    public sealed class PageRenameService
    {
        public OperationResult Rename(ICorelPage page, string? newName)
        {
            var name = newName?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return OperationResult.Fail(OperationStatus.Cancelled, "Nenhum nome informado — a página não foi alterada.");
            }

            var previous = page.Name;
            if (previous == name)
            {
                return OperationResult.Success($"A página ativa já se chama \"{name}\".");
            }

            page.Name = name!;
            return OperationResult.Success(string.IsNullOrEmpty(previous)
                ? $"Página ativa renomeada para \"{name}\"."
                : $"Página ativa renomeada de \"{previous}\" para \"{name}\".");
        }
    }
}
