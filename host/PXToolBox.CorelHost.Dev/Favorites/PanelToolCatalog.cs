using System;
using System.Collections.Generic;
using System.Linq;

namespace PXToolBoxDev.Favorites
{
    /// <summary>
    /// Ferramentas do painel que podem virar favoritas, por identificador ESTÁVEL — é isso que
    /// vai para a configuração e para as preferências portáteis, nunca o rótulo nem o controle
    /// WPF. Renomear um botão não muda o Id; um Id só sai daqui se a ferramenta deixar de
    /// existir (configurações antigas com ele continuam abrindo — ver
    /// <see cref="PanelFavoritesList"/>). Os 10 primeiros são os da dev.50; os demais foram
    /// acrescentados depois, sem mudar nenhum dos anteriores.
    ///
    /// Todo botão de ferramenta das seções do painel está aqui (pedido do usuário, 2026-11). O
    /// botão original é achado pelo Id em <c>AutomationProperties.AutomationId</c> no XAML.
    /// Ferramentas que usam campos da própria seção (Escudo/Bandeira/Faixa: layout e pasta;
    /// Renomear: o nome digitado; Tipo de peça: as caixas de tipo) usam, pelo atalho, os valores
    /// que estiverem nesses campos — a dica do atalho repete a do original, que diz isso.
    /// </summary>
    public static class PanelToolCatalog
    {
        /// <summary>Ícone do atalho nos Favoritos: um glifo de Segoe Fluent Icons/Segoe MDL2
        /// Assets (fontes do Windows 10/11) ou um texto curto (sigla do próprio botão, como
        /// MA/NO, ou Σ das somas). A marca do canto não é do ícone: vem da seção
        /// (<see cref="Tool.Badge"/>).</summary>
        public sealed class ToolIcon
        {
            private ToolIcon(string symbol, bool isText, bool isRemoval = false)
            {
                Symbol = symbol;
                IsText = isText;
                IsRemoval = isRemoval;
            }

            public string Symbol { get; }
            public bool IsText { get; }

            /// <summary>Ferramenta que REMOVE algo da página (Remover NO/NU/AP/TS…): desenhada em
            /// vermelho nos Favoritos, para não confundir com a que coloca/alterna o mesmo
            /// código (pedido do usuário, 2026-11).</summary>
            public bool IsRemoval { get; }

            public static ToolIcon Glyph(string glyph) => new ToolIcon(glyph, false);

            public static ToolIcon Text(string text) => new ToolIcon(text, true);

            public static ToolIcon RemovalText(string text) => new ToolIcon(text, true, isRemoval: true);

            public static ToolIcon RemovalGlyph(string glyph) => new ToolIcon(glyph, false, isRemoval: true);
        }

        /// <summary>
        /// Marca do canto = a seção da ferramenta (pedido do usuário, 2026-11-06): inicial; quando
        /// duas seções começam com a mesma letra, primeira e segunda letras (Códigos "Co",
        /// Camadas "Ca", Montagem "Mo" × Medidas "Me"). Preparação e saída × Produção dariam "Pr"
        /// nas duas: Preparação e saída usa as iniciais das palavras ("PS"). Ferramentas de
        /// remoção mostram "−" (vermelho, com moldura) em vez da seção.
        /// </summary>
        /// <remarks>Calculado pelos nomes ORIGINAIS das seções (por nome); com seções renomeadas
        /// ou criadas, o painel usa <see cref="PanelLayout.PanelCustomization.SectionBadges"/>.</remarks>
        public static IReadOnlyDictionary<string, string> SectionBadges { get; } =
            PanelLayout.PanelCustomization.ComputeBadges(PanelLayout.PanelSectionCatalog.All.Select(s => (s.Title, s.Title)).ToList());

        public sealed class Tool
        {
            public Tool(string id, string label, string section, ToolIcon icon)
            {
                Id = id;
                Label = label;
                Section = section;
                Icon = icon;
            }

            public string Id { get; }
            public string Label { get; }
            public string Section { get; }
            public ToolIcon Icon { get; }

            /// <summary>Marca do canto: "−" para remoção; senão a da seção (<see cref="SectionBadges"/>).</summary>
            public string Badge => Icon.IsRemoval ? "−" : SectionBadges.TryGetValue(Section, out var badge) ? badge : "";
        }

        public const string GridPrepare = "grid.prepare";
        public const string GridStandardSizeToggle = "grid.standard-size.toggle";
        public const string GridStandardSizeUpdate = "grid.standard-size.update";
        public const string LayersToggleMa = "layers.toggle-ma";
        public const string LayersToggleFe = "layers.toggle-fe";
        public const string LayersToggleC = "layers.toggle-c";
        public const string LayersHideSelected = "layers.hide-selected";
        public const string LayersRestoreLastHidden = "layers.restore-last-hidden";
        public const string OutputPrepareNewGrid = "output.prepare-new-grid";
        public const string OutputPrepareNewMold = "output.prepare-new-mold";

        private const string Grid = "Grid";
        private const string Assembly = "Montagem";
        private const string Molds = "Cadastro de moldes";
        private const string Codes = "Códigos";
        private const string Layers = "Camadas";
        private const string Output = "Preparação e saída";
        private const string PieceType = "Tipo de peça";
        private const string Objects = "Objetos";
        private const string Measures = "Medidas";
        private const string Lists = "Listas";
        private const string Production = "Produção";
        private const string Diagnostics = "Diagnóstico";

        // Glifos (Segoe MDL2 Assets). Cada ferramenta tem um ícone ÚNICO (pedido do usuário,
        // 2026-11-06: nada de repetir o símbolo e diferenciar só pela marca do canto) — conferido
        // por PanelFavoritesTests.Catalog_EveryToolHasItsOwnIcon. As siglas de "Remover … da
        // página" repetem as de alternar, mas são outro ícone: vermelho e com moldura (IsRemoval).
        // A marca no canto é a seção (Tool.Badge).
        public static IReadOnlyList<Tool> All { get; } = new[]
        {
            // dev.50 — Ids e ordem nunca mudam.
            new Tool(GridPrepare, "Preparar Grid", Grid, ToolIcon.Glyph("")),
            new Tool(GridStandardSizeToggle, "Alternar Tamanho Padrão", Grid, ToolIcon.Glyph("")),
            new Tool(GridStandardSizeUpdate, "Atualizar Tamanho Padrão", Grid, ToolIcon.Glyph("")),
            new Tool(LayersToggleMa, "Visibilidade MA", Layers, ToolIcon.Text("MA")),
            new Tool(LayersToggleFe, "Visibilidade FE", Layers, ToolIcon.Text("FE")),
            new Tool(LayersToggleC, "Visibilidade C", Layers, ToolIcon.Text("C")),
            new Tool(LayersHideSelected, "Ocultar camadas dos objetos selecionados", Layers, ToolIcon.Glyph("")),
            new Tool(LayersRestoreLastHidden, "Mostrar últimas camadas ocultadas", Layers, ToolIcon.Glyph("")),
            new Tool(OutputPrepareNewGrid, "Preparar Grid Novo (padrão antigo)", Output, ToolIcon.Glyph("")),
            new Tool(OutputPrepareNewMold, "Preparar Molde Novo (padrão antigo)", Output, ToolIcon.Glyph("")),
            new Tool("output.prepare-new-grid.new-standard", "Preparar Grid Novo (padrão novo)", Output, ToolIcon.Glyph("")),
            new Tool("output.prepare-new-mold.new-standard", "Preparar Molde Novo (padrão novo)", Output, ToolIcon.Glyph("")),
            new Tool("output.convert-layer-standard", "Converter documento: padrão antigo ↔ novo", Output, ToolIcon.Glyph("")),

            // Todos os demais botões (2026-11).
            new Tool("grid.apply-outline", "Aplicar contorno do Grid", Grid, ToolIcon.Glyph("")),
            new Tool("codes.toggle-no", "Alternar NO na seleção", Codes, ToolIcon.Text("NO")),
            new Tool("codes.toggle-nu", "Alternar NU na seleção", Codes, ToolIcon.Text("NU")),
            new Tool("codes.toggle-ap", "Alternar AP na seleção", Codes, ToolIcon.Text("AP")),
            new Tool("codes.toggle-ts", "Alternar TS na seleção", Codes, ToolIcon.Text("TS")),
            new Tool("codes.remove-no-nu-page", "Remover NO/NU da página", Codes, ToolIcon.RemovalText("NO\nNU")),
            new Tool("codes.remove-no-page", "Remover NO da página", Codes, ToolIcon.RemovalText("NO")),
            new Tool("codes.remove-nu-page", "Remover NU da página", Codes, ToolIcon.RemovalText("NU")),
            new Tool("codes.remove-ap-page", "Remover AP da página", Codes, ToolIcon.RemovalText("AP")),
            new Tool("codes.remove-ts-page", "Remover TS da página", Codes, ToolIcon.RemovalText("TS")),
            new Tool("codes.remove-all-page", "Remover todos os códigos (NO/NU/AP/TS) da página", Codes, ToolIcon.RemovalGlyph("")),
            new Tool("grid.sisbolt.stretch-to-mold", "Alternar Esticar no Tamanho do Molde", Assembly, ToolIcon.Glyph("")),
            new Tool("grid.sisbolt.ignore-variable-metrics", "Alternar Ignorar métricas da variável", Assembly, ToolIcon.Glyph("")),
            new Tool("grid.sisbolt.free-hand", "Alternar Mão Livre", Assembly, ToolIcon.Glyph("")),
            new Tool("grid.sisbolt.reset-rotation", "Resetar ângulo", Assembly, ToolIcon.Glyph("")),
            new Tool("grid.sisbolt.single-item-group", "Criar grupo com item único", Assembly, ToolIcon.Glyph("")),
            new Tool("grid.sisbolt.auto-horizontal-alignment", "Alinhamento horizontal automático", Assembly, ToolIcon.Glyph("")),
            new Tool("molds.settings", "Configurações de moldes", Molds, ToolIcon.Glyph("")),
            new Tool("molds.define", "Definir objetos", Molds, ToolIcon.Glyph("\uE8B9")),
            new Tool("molds.define-batch", "Detectar objetos em lote", Molds, ToolIcon.Glyph("\uE762")),
            new Tool("molds.propagate-logos", "Propagar logos", Molds, ToolIcon.Glyph("\uE16F")),
            new Tool("molds.validate", "Validar molde", Molds, ToolIcon.Glyph("\uE73E")),
            new Tool("molds.duplicate", "Duplicar peça", Molds, ToolIcon.Glyph("\uE8F1")),
            new Tool("molds.create-layers", "Criar camadas para a seleção", Molds, ToolIcon.Glyph("")),
            new Tool("molds.uppercase", "Converter peça e camada para maiúsculas", Molds, ToolIcon.Glyph("")),
            new Tool("molds.delete-empty-layers", "Excluir camadas vazias", Molds, ToolIcon.Glyph("")),
            new Tool("molds.distribute", "Ordenar e distribuir", Molds, ToolIcon.Glyph("")),
            new Tool("molds.create-grid", "Criar Grid", Molds, ToolIcon.Glyph("")),
            new Tool("molds.save", "Salvar molde", Molds, ToolIcon.Glyph("\uE74E")),
            new Tool("molds.select-rect", "Selecionar RECT", Molds, ToolIcon.Glyph("")),
            new Tool("output.prepare-shield", "Preparar Escudo", Output, ToolIcon.Glyph("")),
            new Tool("output.prepare-flag", "Preparar Bandeira", Output, ToolIcon.Glyph("")),
            new Tool("output.prepare-captain-sash", "Preparar Faixa de Capitão", Output, ToolIcon.Glyph("")),
            new Tool("output.use-context-layout-name", "Usar contexto de produção no nome do layout", Output, ToolIcon.Glyph("")),
            new Tool("output.set-default-folder", "Definir pasta padrão de saída", Output, ToolIcon.Glyph("")),
            new Tool("output.reset-default-folder", "Voltar à pasta de saída original", Output, ToolIcon.Glyph("")),
            new Tool("tools.piece-type.selected-objects", "Alterar tipo dos objetos selecionados", PieceType, ToolIcon.Glyph("")),
            new Tool("tools.piece-type.active-layer", "Alterar tipo da camada ativa", PieceType, ToolIcon.Glyph("")),
            new Tool("tools.piece-type.document", "Alterar tipo no documento inteiro", PieceType, ToolIcon.Glyph("")),
            new Tool("tools.objects.use-context-name", "Usar contexto de produção no nome dos objetos", Objects, ToolIcon.Glyph("")),
            new Tool("tools.objects.rename-selected", "Renomear Objetos Selecionados", Objects, ToolIcon.Glyph("")),
            new Tool("tools.objects.group-intersection", "Agrupar Interseção", Objects, ToolIcon.Glyph("")),
            new Tool("tools.measures.sum-height", "Somar Altura", Measures, ToolIcon.Text("Σ↕")),
            new Tool("tools.measures.sum-width", "Somar Largura", Measures, ToolIcon.Text("Σ↔")),
            new Tool("tools.measures.sum-both", "Somar Altura e Largura", Measures, ToolIcon.Text("Σ")),
            new Tool("lists.open-integrated", "Abrir ListForge Integrado", Lists, ToolIcon.Glyph("")),
            new Tool("lists.open-standalone", "Abrir ListForge Standalone", Lists, ToolIcon.Glyph("")),
            new Tool("lists.locate-executable", "Localizar executável do ListForge", Lists, ToolIcon.Glyph("")),
            new Tool("production.open-nexor", "Abrir Nexor", Production, ToolIcon.Glyph("")),
            new Tool("production.locate-executable", "Localizar executável do Nexor", Production, ToolIcon.Glyph("")),
            new Tool("production.load-context", "Carregar contexto de produção", Production, ToolIcon.Glyph("")),
            new Tool("production.paste-context-path", "Colar caminho do contexto", Production, ToolIcon.Glyph("")),
            new Tool("production.recent-contexts", "Contextos de produção recentes", Production, ToolIcon.Glyph("")),
            new Tool("production.show-context-details", "Ver detalhes do contexto de produção", Production, ToolIcon.Glyph("")),
            new Tool("production.name-active-page", "Nomear página ativa com o contexto", Production, ToolIcon.Glyph("")),
            new Tool("production.export-result", "Exportar resultado para o Nexor", Production, ToolIcon.Glyph("")),
            new Tool("production.clear-context", "Limpar contexto de produção", Production, ToolIcon.Glyph("")),
            new Tool("diagnostics.manual", "Manual de uso", Diagnostics, ToolIcon.Glyph("\uE897")),
            new Tool("diagnostics.release-notes", "Notas da versão", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.inspect-properties", "Inspecionar propriedades", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.capture-checklist", "Roteiro de capturas", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.capture-before", "Capturar estado (antes)", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.capture-compare", "Comparar com a captura (depois)", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.render", "Diagnóstico de redesenho", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.copy-render", "Copiar diagnóstico de redesenho", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.layer-identity", "Testar identidade de camadas", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.layer-identity-report", "Ver/ocultar relatório de identidade", Diagnostics, ToolIcon.Glyph("")),
            new Tool("diagnostics.copy-layer-identity-report", "Copiar relatório de identidade", Diagnostics, ToolIcon.Glyph("")),
        };

        public static Tool? Find(string? id) =>
            id == null ? null : All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
    }
}
