using System.Collections.Generic;
using System.Linq;
using ListForge.Models;
using ListForge.Services;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// Preferências e configuração local do modo Clássico do ListForge Integrado — ver
    /// docs/listforge-classic-parity.md, grupos "Configurações" e "Perfis de trabalho", e
    /// docs/ARCHITECTURE.md (seção do armazenamento próprio do PX DEV) para o inventário real
    /// do standalone por trás de cada campo aqui.
    ///
    /// Esquema 2 (iteração 13): as opções que afetam o PROCESSAMENTO (separador, capitalização,
    /// ordenação, lista avançada, formato do Salvar avançado, pasta/nome padrão de exportação)
    /// deixaram de ser um único conjunto fixo e passaram a viver dentro de cada
    /// <see cref="WorkProfile"/> em <see cref="WorkProfiles"/> — exatamente como no ListForge
    /// standalone (<c>AppConfig.WorkProfiles</c>/<c>ActiveWorkProfileId</c>, ver
    /// ListForge.Services.WorkProfileService, movido para o núcleo compartilhado
    /// ListForge.Core nesta entrega). Só <see cref="EditorFontSize"/> permanece fora de
    /// qualquer perfil — é puramente cosmético, não afeta o resultado do processamento, igual
    /// ao standalone (EditorFontSize também fica fora de WorkProfileSettings lá).
    ///
    /// Esta classe implementa <see cref="ListForge.Core.IWorkProfileConfig"/> para reusar as
    /// mesmas regras de CRUD de perfil do standalone (WorkProfileService) sem reimplementá-las
    /// — nunca uma versão simplificada só para preencher a tabela de paridade.
    ///
    /// Esta classe é só os DADOS — carregamento/gravação (inclusive a migração do esquema 1
    /// para o 2) fica em <see cref="ListForgeIntegratedSettingsStore"/>.
    /// </summary>
    public sealed class ListForgeIntegratedSettings : ListForge.Core.IWorkProfileConfig
    {
        public const int CurrentSchemaVersion = 2;
        public const double MinEditorFontSize = 8;
        public const double MaxEditorFontSize = 32;
        public const double DefaultEditorFontSize = 13;
        public const string ModeLooseFiles = "LooseFiles";
        public const string ModeZip = "Zip";

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        // ---- Preferência portátil fora de qualquer perfil (ver comentário da classe) ----
        public double EditorFontSize { get; set; } = DefaultEditorFontSize;

        // ---- Perfis de trabalho (ListForge.Core.IWorkProfileConfig) ----
        public int WorkProfilesSchemaVersion { get; set; } = WorkProfileService.SchemaVersion;
        public string ActiveWorkProfileId { get; set; } = "";
        public List<WorkProfile> WorkProfiles { get; set; } = new();

        // ---- Preferência de modo (Clássico/PX) por perfil — ver
        // docs/design/listforge-px-workflow/design.md, seção 3.3: a preferência precisa ser por
        // perfil, mas NUNCA dentro de WorkProfile/WorkProfileSettings (ListForge.Core,
        // compartilhado de verdade com o ListForge Standalone — que não tem conceito de "modo
        // PX"). Por isso vive aqui, exclusiva do PX DEV, chaveada por WorkProfile.Id (já existe,
        // estável). Valores esperados: "Classic" ou "Px"; qualquer outro valor ou chave ausente
        // resolve para "Classic" (nunca lança) — campo aditivo, sem exigir migração de esquema,
        // mesmo padrão já usado por Sizes.
        public Dictionary<string, string> PreferredWorkspaceModeByProfileId { get; set; } = new();

        // ---- Tamanhos customizados (ver docs/listforge-classic-parity.md, grupo
        // "Configurações aplicáveis ao integrado") — igual ao standalone, é um conjunto GLOBAL,
        // independente de perfil de trabalho: lá vive em sizes.json (ConfigManager.SizeConfigPath),
        // um arquivo separado de config.json/WorkProfiles; aqui, por simplicidade (um único
        // arquivo de configuração do PX DEV, já estabelecido nas rodadas anteriores), vive neste
        // mesmo objeto, mas como campo de nível de módulo, nunca dentro de WorkProfileSettings.
        // Campo aditivo (esquema 2 continua o mesmo — um arquivo salvo antes desta rodada não tem
        // esta chave no JSON; Newtonsoft simplesmente mantém o valor padrão do inicializador
        // abaixo, sem precisar de migração explícita, diferente da reestruturação de perfis).
        public SizeConfig Sizes { get; set; } = SizeConfig.Default();

        // ---- Favoritos do painel (PXToolBoxDev.Favorites.PanelToolCatalog) — preferência do PX,
        // fora de qualquer perfil de trabalho. Ids estáveis, na ordem do usuário. Campo aditivo
        // como Sizes (esquema continua 2): null = nunca definido (arquivo antigo), lista vazia =
        // usuário removeu todos. Ids desconhecidos são mantidos (ver PanelFavoritesList). Só o
        // painel grava este campo, por ListForgeIntegratedSettingsStore.SavePanelFavorites; o
        // Save comum preserva o valor que está em disco (as janelas do ListForge mantêm cópias
        // longas deste objeto e não podem sobrescrever favoritos mudados no painel).
        // Omitido do JSON enquanto null: quem nunca definiu favoritos continua com o arquivo no
        // mesmo formato de antes.
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<string>? PanelFavoriteToolIds { get; set; }

        // ---- Layout do painel (PXToolBoxDev.PanelLayout.PanelSectionCatalog): ordem e
        // visibilidade das seções, por Id estável. Global, fora de qualquer perfil, mesmas regras
        // de PanelFavoriteToolIds: null = nunca definido (layout padrão), omitido do JSON
        // enquanto null, só o painel grava (SavePanelSectionLayout) e o Save comum preserva o
        // valor em disco.
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public List<PanelLayout.PanelSectionLayoutEntry>? PanelSectionLayout { get; set; }

        // ---- Favoritos com nome (true) ou só ícone (false/null = padrão). Mesmas regras dos
        // campos acima: só o painel grava (SavePanelFavoritesShowLabels), o Save comum preserva o
        // valor em disco, omitido do JSON enquanto null.
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public bool? PanelFavoritesShowLabels { get; set; }

        // ---- Personalização do painel (PXToolBoxDev.PanelLayout.PanelCustomization): nomes das
        // seções, seções criadas e onde cada ferramenta aparece. Mesmas regras: só o painel grava
        // (SavePanelCustomization), o Save comum preserva o valor em disco, omitido enquanto null.
        [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public PanelLayout.PanelCustomization? PanelCustomization { get; set; }

        /// <summary>Configuração inicial válida (primeira execução) — já com o perfil "Padrão"
        /// garantido, igual ao que <see cref="Normalize"/> garantiria de qualquer forma.</summary>
        public static ListForgeIntegratedSettings Default()
        {
            var settings = new ListForgeIntegratedSettings();
            settings.Normalize();
            return settings;
        }

        /// <summary>
        /// Valida e corrige campo a campo (nunca lança): tamanho de fonte fora da faixa volta
        /// ao padrão isoladamente, e <see cref="WorkProfileService.EnsureProfiles"/> garante que
        /// sempre exista um perfil "Padrão" válido e um <see cref="ActiveWorkProfileId"/>
        /// apontando para um perfil existente — mesma regra do standalone
        /// (MainViewModel.EnsureWorkProfiles). Chamado tanto depois de carregar do disco quanto
        /// antes de salvar.
        /// </summary>
        public void Normalize()
        {
            if (double.IsNaN(EditorFontSize) || EditorFontSize < MinEditorFontSize || EditorFontSize > MaxEditorFontSize)
                EditorFontSize = DefaultEditorFontSize;

            new WorkProfileService().EnsureProfiles(this, WorkProfileService.CaptureOfficialDefaultSettings());

            // Mesma normalização pura do standalone (ListForge.Core.SizeHelper.Normalize,
            // chamada por ConfigManager.LoadSizeConfig/SaveSizeConfig lá) — nunca lança; um grupo
            // ausente ou com Label vazio volta ao padrão oficial desse campo isoladamente. Não
            // garante tamanhos-base não vazios por grupo (o standalone também não garante isso
            // aqui — essa validação é só na hora de salvar pela tela, ver SizeConfigWindow).
            Sizes = ListForge.Core.SizeHelper.Normalize(Sizes ?? SizeConfig.Default());

            if (PreferredWorkspaceModeByProfileId == null)
                PreferredWorkspaceModeByProfileId = new();

            // null continua null (nunca definido); senão, sem vazios/repetidos.
            if (PanelFavoriteToolIds != null)
                PanelFavoriteToolIds = new Favorites.PanelFavoritesList(PanelFavoriteToolIds).IdsToPersist.ToList();

            PanelCustomization = PanelCustomization?.Normalize();

            if (PanelSectionLayout != null)
                PanelSectionLayout = new PanelLayout.PanelSectionLayout(PanelSectionLayout, PanelCustomization?.CustomSections.Select(s => s.Id)).ToPersist();
        }

        public const string WorkspaceModeClassic = "Classic";
        public const string WorkspaceModePx = "Px";

        /// <summary>Nunca lança: chave ausente ou valor desconhecido resolve para Clássico.</summary>
        public string GetPreferredWorkspaceMode(string profileId)
        {
            if (!string.IsNullOrEmpty(profileId)
                && PreferredWorkspaceModeByProfileId.TryGetValue(profileId, out var mode)
                && mode == WorkspaceModePx)
                return WorkspaceModePx;
            return WorkspaceModeClassic;
        }

        public void SetPreferredWorkspaceMode(string profileId, string mode)
        {
            if (string.IsNullOrEmpty(profileId)) return;
            PreferredWorkspaceModeByProfileId[profileId] = mode == WorkspaceModePx ? WorkspaceModePx : WorkspaceModeClassic;
        }

        /// <summary>Cópia profunda (inclusive da lista de perfis) — usada por
        /// <see cref="ListForgeIntegrated.SettingsWindow"/> para editar sem afetar o objeto
        /// vivo até confirmar, e por operações de importação.</summary>
        public ListForgeIntegratedSettings Clone() =>
            Newtonsoft.Json.JsonConvert.DeserializeObject<ListForgeIntegratedSettings>(
                Newtonsoft.Json.JsonConvert.SerializeObject(this)) ?? Default();
    }
}
