namespace PXToolBox.Core.Naming
{
    /// <summary>
    /// Códigos de texto curtos usados para marcar objetos de texto num layout (NO=Nome,
    /// NU=Número, AP=Apelido, TS=Tipo Sanguíneo). Ported de Utilidades.CodigoNome/CodigoNumero/
    /// CodigoApelido/CodigoTipoSanguineo (CorelMacros).
    /// </summary>
    public sealed class ObjectCode
    {
        public static readonly ObjectCode No = new ObjectCode("NO", "Texto Nome");
        public static readonly ObjectCode Nu = new ObjectCode("NU", "Texto Número");
        public static readonly ObjectCode Ap = new ObjectCode("AP", "Texto Apelido");
        public static readonly ObjectCode Ts = new ObjectCode("TS", "Texto Tipo Sanguíneo");

        public string ShortCode { get; }
        public string LongLabel { get; }

        private ObjectCode(string shortCode, string longLabel)
        {
            ShortCode = shortCode;
            LongLabel = longLabel;
        }

        /// <summary>
        /// Alterna o nome do objeto entre a forma curta ("NO") e a forma longa ("Texto Nome"),
        /// exatamente como a macro original: se já está no código curto, expande; senão, aplica
        /// o código curto (mesmo que o nome atual seja outra coisa qualquer).
        /// </summary>
        public string Toggle(string currentName) =>
            currentName == ShortCode ? LongLabel : ShortCode;
    }
}
