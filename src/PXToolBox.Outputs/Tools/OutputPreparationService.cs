using PXToolBox.Core.Result;
using PXToolBox.Outputs.Abstractions;

namespace PXToolBox.Outputs.Tools
{
    /// <summary>
    /// "Preparar Escudo", "Preparar Bandeira" e "Preparar Faixa de Capitão". Ported de
    /// Grid.PreparaEscudoParaSaida / PreparaBandeiraParaSaida / PreparaFaixaDeCapitaoParaSaida,
    /// que na revisão candidata (Referência/Macros/modulos/Grid.bas) compartilham uma rotina
    /// privada única (PrepararMaterial) — replicada aqui como <see cref="PrepareForOutput"/>.
    ///
    /// O caminho de saída padrão "Z:\Neuber\Saídas\" foi preservado DE PROPÓSITO (ver
    /// Referência/Macros/LEIA-ME.md: "destinos em Z:... continuam como no fluxo anterior") —
    /// diferente de uma suposição anterior deste projeto de que o caminho precisava ser
    /// genérico desde já. Continua configurável via <paramref name="outputFolder"/> para quem
    /// instalar numa máquina sem esse caminho de rede.
    /// </summary>
    public sealed class OutputPreparationService
    {
        public const string ShieldName = "Escudo";
        public const string FlagName = "Bandeira";
        public const string CaptainSashName = "Faixa De Capitão";

        public const string DefaultOutputFolder = "Z:\\Neuber\\Saídas";

        public OperationResult PrepareForOutput(ICorelDocument document, string outputName)
        {
            var page = document.ActivePage;

            var selectable = page.GetSelectableShapes();
            if (selectable.Count == 0)
            {
                return OperationResult.Fail(OperationStatus.InvalidSelection, "Nenhum objeto selecionado.");
            }

            var group = page.Group(selectable);
            page.CenterOnPage(new[] { group });

            // Grid.PreparaMaterialParaSaida: redimensiona a página ao tamanho do conteúdo selecionado.
            page.Width = group.Width;
            page.Height = group.Height;
            page.CenterOnPage(new[] { group });

            page.Name = outputName;
            document.Name = outputName;

            return OperationResult.Success($"Documento preparado para saída \"{outputName}\".");
        }

        /// <summary>
        /// Grid.SalvaArquivoMaterial: salva como .cdr no caminho montado a partir da pasta de
        /// saída (padrão <see cref="DefaultOutputFolder"/>) e, se informado, do nome do layout.
        /// Chamar só depois de <see cref="PrepareForOutput"/> ter tido sucesso.
        /// </summary>
        public OperationResult SaveOutput(ICorelDocument document, string outputName, string? layoutName, string? outputFolder = null)
        {
            var path = BuildOutputFilePath(outputFolder ?? DefaultOutputFolder, outputName, layoutName);
            document.SaveAsCdr(path);
            return OperationResult.Success($"Salvo em \"{path}\".");
        }

        /// <summary>
        /// Pasta efetiva, em ordem: a digitada no painel para este uso; a pasta padrão definida
        /// nesta máquina; o padrão original <see cref="DefaultOutputFolder"/>. Vazio/espaços contam
        /// como "não informado".
        /// </summary>
        public static string ResolveOutputFolder(string? typedFolder, string? machineDefaultFolder) =>
            !string.IsNullOrWhiteSpace(typedFolder) ? typedFolder!.Trim()
            : !string.IsNullOrWhiteSpace(machineDefaultFolder) ? machineDefaultFolder!.Trim()
            : DefaultOutputFolder;

        /// <summary>
        /// Subpasta dentro da pasta efetiva (ex.: a do rolo do contexto de produção). Sem
        /// subpasta (null/vazio), devolve a própria pasta. Quem chama garante que o nome já é
        /// válido como nome de pasta — aqui só se junta o caminho.
        /// </summary>
        public static string WithSubfolder(string folder, string? subfolder) =>
            string.IsNullOrWhiteSpace(subfolder) ? folder : $"{folder.TrimEnd('\\')}\\{subfolder!.Trim()}";

        public string BuildOutputFilePath(string outputFolder, string outputName, string? layoutName)
        {
            var folder = outputFolder.TrimEnd('\\');
            return string.IsNullOrEmpty(layoutName)
                ? $"{folder}\\{outputName}.cdr"
                : $"{folder}\\{outputName} - {layoutName}.cdr";
        }
    }
}
