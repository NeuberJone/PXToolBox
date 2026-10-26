namespace PXToolBox.Core.Naming
{
    /// <summary>
    /// Categoria de camada reconhecida pelo sufixo pontuado do nome (ex.: "CAM1.ALGO.MA").
    /// Ported de Utilidades.OrganizarCamadas (sufixos ".C", ".FE", ".MA") no repositório CorelMacros.
    /// </summary>
    public enum LayerCategory
    {
        Unknown = 0,
        MA,
        FE,
        C
    }
}
