using System;
using System.IO;
using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Salvar molde" do Cadastro de moldes — reescrito no padrão do PX a partir do salvamento de molde
    /// do MoldMotor: o nome digitado vira o arquivo .cdr sugerido, o usuário confirma onde salvar e o
    /// documento inteiro é salvo lá (passa a ser esse arquivo). Sem gravação em banco.
    /// </summary>
    public sealed class MoldSaveService
    {
        /// <summary>Pasta de moldes de sempre (a mesma do MoldMotor); só usada se já existir.</summary>
        public const string DefaultFolder = @"C:\SISBolt\molds";

        /// <summary>Nome digitado → nome de arquivo .cdr (com ou sem a extensão digitada).</summary>
        public static bool TryCreateFileName(string? value, out string fileName, out string error)
        {
            fileName = string.Empty;
            error = string.Empty;
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                error = "Informe o nome do molde.";
                return false;
            }

            var hasExtension = trimmed.EndsWith(".cdr", StringComparison.OrdinalIgnoreCase);
            var baseName = (hasExtension ? trimmed.Substring(0, trimmed.Length - 4) : trimmed).TrimEnd();
            if (baseName.Length == 0 || baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = "O nome do molde não pode ter \\ / : * ? \" < > |.";
                return false;
            }

            fileName = baseName + ".cdr";
            return true;
        }

        /// <summary>Pasta inicial da janela de salvar: a do documento (se já foi salvo), senão a pasta de
        /// moldes de sempre (se existir), senão nenhuma (a janela decide).</summary>
        public static string InitialFolder(string? documentFullFileName, Func<string, bool> folderExists)
        {
            var documentFolder = string.IsNullOrWhiteSpace(documentFullFileName) ? null : Path.GetDirectoryName(documentFullFileName);
            if (!string.IsNullOrEmpty(documentFolder) && folderExists(documentFolder!)) return documentFolder!;
            return folderExists(DefaultFolder) ? DefaultFolder : string.Empty;
        }

        public OperationResult Save(ICorelDocument document, string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !fullPath.EndsWith(".cdr", StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "O arquivo precisa ser .cdr. Nada foi salvo.");
            }

            document.SaveAsCdr(fullPath);
            return OperationResult.Success($"Molde salvo em \"{fullPath}\".");
        }
    }
}
