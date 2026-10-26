namespace PXToolBox.Core.Result
{
    /// <summary>
    /// Motivos diferenciados de não-sucesso de uma operação de Saídas, exigidos pela
    /// arquitetura (ver docs/ARCHITECTURE.md, seção "Segurança das operações no documento"):
    /// ausência de documento, seleção inválida, leitura inconclusiva e falha de execução são
    /// tratados como casos distintos — nenhum deles deve ser relatado como sucesso.
    /// </summary>
    public enum OperationStatus
    {
        Success,
        NoActiveDocument,
        InvalidSelection,
        InconclusiveRead,
        ExecutionFailed,
        Cancelled,
        NotImplemented
    }
}
