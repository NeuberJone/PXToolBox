namespace PXToolBox.Core.Result
{
    /// <summary>
    /// Resultado de uma operação de Saídas. Nunca reporte sucesso se <see cref="Status"/> não for
    /// <see cref="OperationStatus.Success"/> — o host (CorelHost) depende disso para decidir se o
    /// grupo de desfazer deve ser confirmado ou descartado e o que exibir ao usuário.
    /// </summary>
    public sealed class OperationResult<T>
    {
        public OperationStatus Status { get; }
        public string Message { get; }
        public T? Value { get; }

        public bool IsSuccess => Status == OperationStatus.Success;

        private OperationResult(OperationStatus status, string message, T? value)
        {
            Status = status;
            Message = message;
            Value = value;
        }

        public static OperationResult<T> Success(T value, string message = "") =>
            new OperationResult<T>(OperationStatus.Success, message, value);

        public static OperationResult<T> Fail(OperationStatus status, string message)
        {
            if (status == OperationStatus.Success)
            {
                throw new System.ArgumentException("Use Success() para o status de sucesso.", nameof(status));
            }

            return new OperationResult<T>(status, message, default);
        }
    }

    /// <summary>Variante sem valor de retorno, para operações que apenas produzem efeito no documento.</summary>
    public sealed class OperationResult
    {
        public OperationStatus Status { get; }
        public string Message { get; }

        public bool IsSuccess => Status == OperationStatus.Success;

        private OperationResult(OperationStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public static OperationResult Success(string message = "") =>
            new OperationResult(OperationStatus.Success, message);

        public static OperationResult Fail(OperationStatus status, string message)
        {
            if (status == OperationStatus.Success)
            {
                throw new System.ArgumentException("Use Success() para o status de sucesso.", nameof(status));
            }

            return new OperationResult(status, message);
        }
    }
}
