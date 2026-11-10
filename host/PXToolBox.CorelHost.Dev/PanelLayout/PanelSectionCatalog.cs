using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBoxDev.PanelLayout
{
    /// <summary>
    /// Seções do painel que "Personalizar painel…" pode ordenar e ocultar, por identificador
    /// ESTÁVEL — é isso que vai para a configuração e para as preferências portáteis, nunca o
    /// título nem o Expander. A ordem desta lista é o layout padrão (a ordem do painel antes da
    /// personalização existir). Seção nova numa versão futura entra aqui; quem já tinha layout
    /// salvo a recebe visível no fim (ver <see cref="PanelSectionLayout"/>).
    /// </summary>
    public static class PanelSectionCatalog
    {
        public sealed class Section
        {
            public Section(string id, string title)
            {
                Id = id;
                Title = title;
            }

            public string Id { get; }
            public string Title { get; }
        }

        public const string Favorites = "favorites";
        public const string Grid = "grid";
        public const string Assembly = "assembly";
        public const string Molds = "molds";
        public const string Codes = "codes";
        public const string Layers = "layers";
        public const string Output = "output";
        public const string ToolsPieceType = "tools.piece-type";
        public const string ToolsObjects = "tools.objects";
        public const string ToolsMeasures = "tools.measures";
        public const string Lists = "lists";
        public const string Production = "production";
        public const string Diagnostics = "diagnostics";

        public static IReadOnlyList<Section> All { get; } = new[]
        {
            // Ordem padrão pelo fluxo de trabalho (reorganização de 2026-11-06, escolhida pelo
            // usuário): Montagem logo depois do Grid; as três "Outras ferramentas — X" com nome
            // curto. Ids nunca mudam (layouts e favoritos salvos dependem deles).
            new Section(Favorites, "Favoritos"),
            new Section(Grid, "Grid"),
            new Section(Assembly, "Montagem"),
            new Section(Molds, "Cadastro de moldes"),
            new Section(Codes, "Códigos"),
            new Section(Layers, "Camadas"),
            new Section(ToolsObjects, "Objetos"),
            new Section(ToolsPieceType, "Tipo de peça"),
            new Section(ToolsMeasures, "Medidas"),
            new Section(Output, "Preparação e saída"),
            new Section(Production, "Produção"),
            new Section(Lists, "Listas"),
            new Section(Diagnostics, "Diagnóstico"),
        };

        public static Section? Find(string? id) =>
            id == null ? null : All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal));
    }
}
