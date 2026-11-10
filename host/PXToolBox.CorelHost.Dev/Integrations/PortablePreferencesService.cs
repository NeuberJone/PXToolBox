using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using MoldSettingsModel = PXToolBox.Core.Molds.MoldSettings;

namespace PXToolBoxDev.Integrations
{
    /// <summary>
    /// "Preferências portáveis" do PX Tool Box DEV — PERFIL DE PERSONALIZAÇÃO do próprio PX,
    /// nunca os perfis de trabalho do ListForge (<see cref="ListForge.Models.WorkProfile"/>),
    /// que continuam existindo só na máquina/instalação (podem conter pasta de exportação
    /// específica da máquina). Feature SEPARADA e mais ESTREITA do que o botão já existente
    /// "Exportar/Importar configurações..." em <c>SettingsWindow</c> (que copia o objeto
    /// <see cref="ListForgeIntegratedSettings"/> inteiro, incluindo os perfis de trabalho — útil
    /// para backup/restauração completa desta máquina, mas é exatamente o "copiar o arquivo
    /// interno integralmente" que esta classe foi pedida para NÃO fazer). Lista explícita de
    /// campos, nunca um espelho do arquivo de configuração.
    ///
    /// Campos realmente portáveis identificados nesta entrega (nenhum outro foi inventado):
    /// - <see cref="ListForgeIntegratedSettings.EditorFontSize"/> — já documentado no próprio
    ///   tipo como "preferência portátil fora de qualquer perfil".
    /// - Preferência de modo Clássico/PX por perfil
    ///   (<see cref="ListForgeIntegratedSettings.PreferredWorkspaceModeByProfileId"/>) — portada
    ///   por NOME do perfil (não pelo Id interno, que é só desta máquina): um perfil com o
    ///   mesmo nome na máquina de destino recebe a preferência; um perfil ausente lá é só
    ///   ignorado e relatado como incompatível, nunca inventa um perfil novo.
    /// - Favoritos do painel (<see cref="ListForgeIntegratedSettings.PanelFavoriteToolIds"/>) —
    ///   Ids estáveis de <see cref="Favorites.PanelToolCatalog"/>, na ordem do usuário. Campo aditivo
    ///   no MESMO esquema 1: uma instalação mais antiga ignora a chave (só lê os campos que
    ///   conhece); um arquivo sem a chave não altera os favoritos atuais; Ids desconhecidos são
    ///   ignorados e relatados.
    /// - Layout do painel (<see cref="ListForgeIntegratedSettings.PanelSectionLayout"/>) — ordem
    ///   e visibilidade das seções por Id estável de <see cref="PanelLayout.PanelSectionCatalog"/>.
    ///   Mesmas regras dos favoritos: aditivo no esquema 1, ausente = mantém o layout atual, Ids
    ///   desconhecidos relatados e ignorados (seções novas entram visíveis no fim ao aplicar).
    ///   Seções CRIADAS pelo usuário são aceitas quando o mesmo arquivo traz a personalização
    ///   que as define.
    /// - "Mostrar nomes" dos Favoritos (<see cref="ListForgeIntegratedSettings.PanelFavoritesShowLabels"/>).
    /// - Personalização do painel (<see cref="ListForgeIntegratedSettings.PanelCustomization"/>):
    ///   nomes das seções, seções criadas e a seção de cada ferramenta. Exportada SEMPRE (vazia
    ///   quando nunca personalizada), para que importar reproduza o painel de origem; arquivo sem
    ///   a chave (versão mais antiga) mantém a personalização atual. Ferramentas e seções que esta
    ///   versão não conhece são relatadas e ignoradas.
    /// - Configurações de moldes (<see cref="MoldSettingsStore"/>, arquivo próprio): o mesmo JSON do
    ///   mold-settings.json, exportado sempre que quem chama passa as configurações em vigor. Arquivo
    ///   sem a chave mantém as atuais; configurações inválidas são relatadas e ignoradas inteiras
    ///   (nunca meio aplicadas). Quem grava é a janela, ao Salvar.
    ///
    /// NÃO exportados, nunca: caminhos de executáveis (ListForgeConfigStore/NexorConfigStore —
    /// arquivos próprios, nem tocados aqui), pasta de instalação do Corel, licença/autorização
    /// DEV, credenciais, logs/histórico/backups/listas, qualquer campo de
    /// <see cref="ListForge.Models.WorkProfileSettings"/> (pasta/nome de exportação — específicos
    /// da máquina), <see cref="ListForgeIntegratedSettings.Sizes"/> (tamanhos customizados — não
    /// pedido nesta entrega, não mencionado como "efetivamente implementado" no pedido do
    /// usuário; ver docs). "Layout" e "seções" citados no pedido original NÃO existem
    /// implementados nesta base de código ainda — nenhum campo foi inventado para eles.
    /// </summary>
    public static class PortablePreferencesService
    {
        public const string FormatId = "PxToolBoxDevPortablePreferences";
        public const int CurrentSchemaVersion = 1;

        public sealed class Document
        {
            public string FormatId { get; set; } = PortablePreferencesService.FormatId;
            public int SchemaVersion { get; set; } = CurrentSchemaVersion;
            public DateTimeOffset ExportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
            public double EditorFontSize { get; set; } = ListForgeIntegratedSettings.DefaultEditorFontSize;
            public Dictionary<string, string> PreferredWorkspaceModeByProfileName { get; set; } = new();

            /// <summary>null = arquivo sem favoritos (não altera os atuais ao importar).</summary>
            public List<string>? PanelFavoriteToolIds { get; set; }

            /// <summary>null = arquivo sem layout (não altera o atual ao importar).</summary>
            public List<PanelLayout.PanelSectionLayoutEntry>? PanelSectionLayout { get; set; }

            /// <summary>null = arquivo sem a chave (não altera o modo atual ao importar).</summary>
            public bool? PanelFavoritesShowLabels { get; set; }

            /// <summary>null = arquivo sem a chave (não altera a personalização atual ao importar);
            /// vazia = painel sem personalização (importar desfaz a atual).</summary>
            public PanelLayout.PanelCustomization? PanelCustomization { get; set; }

            /// <summary>null = arquivo sem a chave (não altera as configurações de moldes atuais ao
            /// importar). Mesmo formato do mold-settings.json.</summary>
            public JObject? MoldSettings { get; set; }
        }

        public sealed class ValidationResult
        {
            public bool IsValid { get; }
            public string? FailureReason { get; }
            public Document? Document { get; }
            public IReadOnlyList<string> RecognizedFieldSummaries { get; }
            public IReadOnlyList<string> IncompatibleFieldSummaries { get; }

            private ValidationResult(bool isValid, string? failureReason, Document? document, IReadOnlyList<string> recognized, IReadOnlyList<string> incompatible)
            {
                IsValid = isValid;
                FailureReason = failureReason;
                Document = document;
                RecognizedFieldSummaries = recognized;
                IncompatibleFieldSummaries = incompatible;
            }

            public static ValidationResult Fail(string reason) =>
                new(false, reason, null, Array.Empty<string>(), Array.Empty<string>());

            public static ValidationResult Ok(Document document, IReadOnlyList<string> recognized, IReadOnlyList<string> incompatible) =>
                new(true, null, document, recognized, incompatible);
        }

        public sealed class ApplyResult
        {
            public IReadOnlyList<string> AppliedSummaries { get; }
            public IReadOnlyList<string> SkippedSummaries { get; }

            public ApplyResult(IReadOnlyList<string> applied, IReadOnlyList<string> skipped)
            {
                AppliedSummaries = applied;
                SkippedSummaries = skipped;
            }
        }

        /// <summary>Resolve o Id interno de cada entrada de
        /// <see cref="ListForgeIntegratedSettings.PreferredWorkspaceModeByProfileId"/> para o
        /// NOME do perfil correspondente — nunca exporta o Id (específico desta máquina/
        /// instalação). Uma entrada cujo perfil não existe mais (removido) é simplesmente
        /// ignorada — nunca exportada como "órfã".</summary>
        public static Document Export(ListForgeIntegratedSettings settings, MoldSettingsModel? moldSettings = null)
        {
            // Só ferramentas conhecidas: posições de uma versão mais nova preservadas aqui não viajam.
            var customization = (settings.PanelCustomization?.Clone() ?? PanelLayout.PanelCustomization.Default()).Normalize();
            customization.ToolPlacements.RemoveAll(p => Favorites.PanelToolCatalog.Find(p.ToolId) == null);
            var document = new Document
            {
                EditorFontSize = settings.EditorFontSize,
                PanelFavoriteToolIds = settings.PanelFavoriteToolIds?.ToList(),
                // Só as seções conhecidas (catálogo + criadas): Ids desconhecidos preservados
                // localmente não viajam.
                PanelSectionLayout = settings.PanelSectionLayout == null
                    ? null
                    : new PanelLayout.PanelSectionLayout(settings.PanelSectionLayout, customization.CustomSections.Select(s => s.Id)).Sections.ToList(),
                PanelFavoritesShowLabels = settings.PanelFavoritesShowLabels == true,
                PanelCustomization = customization,
                MoldSettings = moldSettings == null ? null : MoldSettingsStore.ToJson(moldSettings),
            };

            foreach (var entry in settings.PreferredWorkspaceModeByProfileId)
            {
                var profile = settings.WorkProfiles.FirstOrDefault(p => string.Equals(p.Id, entry.Key, StringComparison.Ordinal));
                if (profile == null || string.IsNullOrWhiteSpace(profile.Name))
                    continue;

                document.PreferredWorkspaceModeByProfileName[profile.Name] = entry.Value;
            }

            return document;
        }

        public static IReadOnlyList<string> BuildExportSummary(Document document)
        {
            var lines = new List<string>
            {
                $"Tamanho de fonte do editor: {document.EditorFontSize:0.#}",
            };

            if (document.PreferredWorkspaceModeByProfileName.Count == 0)
            {
                lines.Add("Preferência de modo Clássico/PX por perfil: nenhuma definida ainda.");
            }
            else
            {
                foreach (var entry in document.PreferredWorkspaceModeByProfileName)
                {
                    var modeLabel = entry.Value == ListForgeIntegratedSettings.WorkspaceModePx ? "PX" : "Clássico";
                    lines.Add($"Perfil \"{entry.Key}\" → modo preferido: {modeLabel}");
                }
            }

            lines.Add(document.PanelFavoriteToolIds == null
                ? "Favoritos do painel: nenhum definido ainda."
                : DescribeFavorites(document.PanelFavoriteToolIds));

            lines.Add(document.PanelSectionLayout == null
                ? "Layout do painel: padrão (nunca personalizado)."
                : DescribeSectionLayout(document.PanelSectionLayout, document.PanelCustomization));

            if (document.PanelFavoritesShowLabels != null)
                lines.Add(DescribeShowLabels(document.PanelFavoritesShowLabels.Value));

            if (document.PanelCustomization != null)
                lines.Add(DescribeCustomization(document.PanelCustomization));

            var molds = ReadMoldSettings(document);
            if (molds != null)
                lines.Add(DescribeMolds(molds));

            return lines;
        }

        /// <summary>As configurações de moldes do arquivo (já validadas por <see cref="Validate"/>);
        /// null se o arquivo não as traz.</summary>
        public static MoldSettingsModel? ReadMoldSettings(Document document) =>
            document.MoldSettings == null ? null : MoldSettingsStore.FromJson(document.MoldSettings);

        private static string DescribeMolds(MoldSettingsModel settings) =>
            $"Configurações de moldes: {string.Join(", ", settings.Categories.Select(c => $"{c.Code} ({c.Sizes.Count} tamanhos)"))}; " +
            $"laser \"{settings.LaserNames.FirstOrDefault()}\", TAG \"{settings.TagName}\".";

        private static string DescribeShowLabels(bool showLabels) =>
            showLabels ? "Favoritos: mostrar nomes (botão inteiro)." : "Favoritos: só ícones.";

        private static string DescribeCustomization(PanelLayout.PanelCustomization customization)
        {
            if (customization.IsDefault) return "Personalização do painel: nenhuma (nomes originais, cada ferramenta na sua seção).";

            var parts = new List<string>();
            if (customization.SectionTitles.Count > 0)
                parts.Add($"{customization.SectionTitles.Count} seção(ões) renomeada(s)");
            if (customization.CustomSections.Count > 0)
                parts.Add($"seções criadas: {string.Join(", ", customization.CustomSections.Select(s => $"\"{s.Title}\""))}");
            if (customization.ToolPlacements.Count > 0)
                parts.Add($"{customization.ToolPlacements.Count} ferramenta(s) em outra seção");
            return $"Personalização do painel: {string.Join("; ", parts)}.";
        }

        private static string DescribeSectionLayout(IReadOnlyList<PanelLayout.PanelSectionLayoutEntry> entries, PanelLayout.PanelCustomization? customization = null)
        {
            var hidden = entries.Where(e => !e.Visible).Select(e => customization?.TitleOf(e.Id) ?? PanelLayout.PanelSectionCatalog.Find(e.Id)?.Title ?? e.Id).ToList();
            return hidden.Count == 0
                ? $"Layout do painel: {entries.Count} seções, todas visíveis."
                : $"Layout do painel: {entries.Count} seções, ocultas: {string.Join(", ", hidden)}.";
        }

        private static string DescribeFavorites(IReadOnlyList<string> ids) =>
            ids.Count == 0
                ? "Favoritos do painel: nenhum (lista vazia)."
                : $"Favoritos do painel ({ids.Count}): {string.Join(", ", ids.Select(id => Favorites.PanelToolCatalog.Find(id)?.Label ?? id))}";

        /// <summary>Nunca lança: todo problema (JSON inválido, formato/esquema desconhecido,
        /// campo fora do tipo/intervalo esperado) resulta num <see cref="ValidationResult"/> com
        /// <see cref="ValidationResult.IsValid"/> falso ou com o campo específico listado em
        /// <see cref="ValidationResult.IncompatibleFieldSummaries"/> — nunca interrompe a
        /// validação dos demais campos por causa de um só estar errado.</summary>
        public static ValidationResult Validate(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException)
            {
                return ValidationResult.Fail("O arquivo não é um JSON válido.");
            }

            var formatId = root.Value<string>("formatId");
            if (!string.Equals(formatId, FormatId, StringComparison.Ordinal))
            {
                return ValidationResult.Fail(
                    string.IsNullOrWhiteSpace(formatId)
                        ? "Arquivo sem identificador de formato — não é um arquivo de preferências do PX Tool Box DEV."
                        : $"Identificador de formato desconhecido (\"{formatId}\") — não é um arquivo de preferências do PX Tool Box DEV.");
            }

            var schemaVersion = root.Value<int?>("schemaVersion") ?? 0;
            if (schemaVersion != CurrentSchemaVersion)
            {
                return ValidationResult.Fail(
                    $"Versão do esquema não suportada ({schemaVersion}) — esta instalação entende só a versão {CurrentSchemaVersion}.");
            }

            var document = new Document { FormatId = FormatId, SchemaVersion = schemaVersion };
            var recognized = new List<string>();
            var incompatible = new List<string>();

            var fontSizeToken = root["editorFontSize"];
            if (fontSizeToken != null && (fontSizeToken.Type == JTokenType.Float || fontSizeToken.Type == JTokenType.Integer))
            {
                var fontSize = fontSizeToken!.Value<double>();
                if (fontSize >= ListForgeIntegratedSettings.MinEditorFontSize && fontSize <= ListForgeIntegratedSettings.MaxEditorFontSize)
                {
                    document.EditorFontSize = fontSize;
                    recognized.Add($"Tamanho de fonte do editor: {fontSize:0.#}");
                }
                else
                {
                    incompatible.Add($"Tamanho de fonte do editor fora do intervalo permitido ({ListForgeIntegratedSettings.MinEditorFontSize}–{ListForgeIntegratedSettings.MaxEditorFontSize}): {fontSize:0.#} — ignorado, mantido o valor atual.");
                }
            }
            else if (fontSizeToken != null)
            {
                incompatible.Add("Tamanho de fonte do editor com tipo inesperado — ignorado, mantido o valor atual.");
            }

            var modesToken = root["preferredWorkspaceModeByProfileName"] as JObject;
            if (modesToken != null)
            {
                foreach (var property in modesToken.Properties())
                {
                    var profileName = property.Name;
                    var modeValue = property.Value.Type == JTokenType.String ? property.Value.Value<string>() : null;

                    if (modeValue == ListForgeIntegratedSettings.WorkspaceModePx || modeValue == ListForgeIntegratedSettings.WorkspaceModeClassic)
                    {
                        document.PreferredWorkspaceModeByProfileName[profileName] = modeValue!;
                        var modeLabel = modeValue == ListForgeIntegratedSettings.WorkspaceModePx ? "PX" : "Clássico";
                        recognized.Add($"Perfil \"{profileName}\" → modo preferido: {modeLabel}");
                    }
                    else
                    {
                        incompatible.Add($"Perfil \"{profileName}\": valor de modo desconhecido (\"{modeValue}\") — ignorado.");
                    }
                }
            }

            var favoritesToken = root["panelFavoriteToolIds"];
            if (favoritesToken is JArray favoritesArray)
            {
                var favorites = new List<string>();
                foreach (var item in favoritesArray)
                {
                    var id = item.Type == JTokenType.String ? item.Value<string>() : null;
                    if (id != null && Favorites.PanelToolCatalog.Find(id) != null)
                    {
                        if (!favorites.Contains(id)) favorites.Add(id);
                    }
                    else
                    {
                        incompatible.Add($"Favorito desconhecido nesta versão (\"{id ?? item.ToString()}\") — ignorado.");
                    }
                }

                document.PanelFavoriteToolIds = favorites;
                recognized.Add(DescribeFavorites(favorites));
            }
            else if (favoritesToken != null && favoritesToken.Type != JTokenType.Null)
            {
                incompatible.Add("Favoritos do painel com tipo inesperado — ignorados, mantidos os atuais.");
            }

            var showLabelsToken = root["panelFavoritesShowLabels"];
            if (showLabelsToken != null && showLabelsToken.Type == JTokenType.Boolean)
            {
                document.PanelFavoritesShowLabels = showLabelsToken.Value<bool>();
                recognized.Add(DescribeShowLabels(document.PanelFavoritesShowLabels.Value));
            }
            else if (showLabelsToken != null && showLabelsToken.Type != JTokenType.Null)
            {
                incompatible.Add("\"Mostrar nomes\" dos Favoritos com tipo inesperado — ignorado, mantido o atual.");
            }

            var customizationToken = root["panelCustomization"];
            if (customizationToken is JObject customizationObject)
            {
                document.PanelCustomization = ValidateCustomization(customizationObject, incompatible);
                recognized.Add(DescribeCustomization(document.PanelCustomization));
            }
            else if (customizationToken != null && customizationToken.Type != JTokenType.Null)
            {
                incompatible.Add("Personalização do painel com tipo inesperado — ignorada, mantida a atual.");
            }

            var moldsToken = root["moldSettings"];
            if (moldsToken is JObject moldsObject)
            {
                string? problem;
                try
                {
                    var problems = MoldSettingsStore.FromJson(moldsObject).Validate();
                    problem = problems.Count == 0 ? null : problems[0];
                }
                catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is JsonException || ex is ArgumentException)
                {
                    problem = ex.Message;
                }

                if (problem == null)
                {
                    document.MoldSettings = moldsObject;
                    recognized.Add(DescribeMolds(MoldSettingsStore.FromJson(moldsObject)));
                }
                else
                {
                    incompatible.Add($"Configurações de moldes inválidas ({problem}) — ignoradas, mantidas as atuais.");
                }
            }
            else if (moldsToken != null && moldsToken.Type != JTokenType.Null)
            {
                incompatible.Add("Configurações de moldes com tipo inesperado — ignoradas, mantidas as atuais.");
            }

            var customIds = document.PanelCustomization?.CustomSections.Select(s => s.Id).ToList() ?? new List<string>();
            var layoutToken = root["panelSectionLayout"];
            if (layoutToken is JArray layoutArray)
            {
                var entries = new List<PanelLayout.PanelSectionLayoutEntry>();
                foreach (var item in layoutArray)
                {
                    var id = (item as JObject)?["id"];
                    var visible = (item as JObject)?["visible"];
                    var idText = id?.Type == JTokenType.String ? id.Value<string>() : null;
                    if (idText == null || (PanelLayout.PanelSectionCatalog.Find(idText) == null && !customIds.Contains(idText))
                        || (visible != null && visible.Type != JTokenType.Boolean))
                    {
                        incompatible.Add($"Seção do painel desconhecida nesta versão ou inválida ({(idText != null ? $"\"{idText}\"" : item.ToString(Formatting.None))}) — ignorada.");
                        continue;
                    }

                    if (entries.Any(e => e.Id == idText)) continue;
                    entries.Add(new PanelLayout.PanelSectionLayoutEntry(idText, visible?.Value<bool>() ?? true));
                }

                document.PanelSectionLayout = entries;
                recognized.Add(DescribeSectionLayout(new PanelLayout.PanelSectionLayout(entries, customIds).Sections, document.PanelCustomization));
            }
            else if (layoutToken != null && layoutToken.Type != JTokenType.Null)
            {
                incompatible.Add("Layout do painel com tipo inesperado — ignorado, mantido o atual.");
            }

            return ValidationResult.Ok(document, recognized, incompatible);
        }

        // Campo a campo, como o resto de Validate: uma entrada ruim é relatada e ignorada, nunca
        // derruba a personalização inteira.
        private static PanelLayout.PanelCustomization ValidateCustomization(JObject token, List<string> incompatible)
        {
            var customization = PanelLayout.PanelCustomization.Default();
            string? Text(JToken? value) => value?.Type == JTokenType.String ? value.Value<string>() : null;

            if (token["customSections"] is JArray sections)
            {
                foreach (var item in sections)
                {
                    var id = Text((item as JObject)?["id"]);
                    var title = Text((item as JObject)?["title"])?.Trim();
                    if (id == null || !id.StartsWith(PanelLayout.PanelCustomization.CustomSectionPrefix, StringComparison.Ordinal)
                        || string.IsNullOrEmpty(title) || customization.IsCustomSection(id))
                    {
                        incompatible.Add($"Seção criada inválida ({item.ToString(Formatting.None)}) — ignorada.");
                        continue;
                    }

                    customization.CustomSections.Add(new PanelLayout.CustomSectionEntry(id, title!));
                }
            }

            if (token["sectionTitles"] is JObject titles)
            {
                foreach (var property in titles.Properties())
                {
                    var title = Text(property.Value)?.Trim();
                    if (PanelLayout.PanelSectionCatalog.Find(property.Name) == null || string.IsNullOrEmpty(title))
                    {
                        incompatible.Add($"Nome de seção desconhecida nesta versão ou vazio (\"{property.Name}\") — ignorado.");
                        continue;
                    }

                    customization.SectionTitles[property.Name] = title!;
                }
            }

            if (token["toolPlacements"] is JArray placements)
            {
                foreach (var item in placements)
                {
                    var toolId = Text((item as JObject)?["toolId"]);
                    var sectionId = Text((item as JObject)?["sectionId"]);
                    if (toolId == null || Favorites.PanelToolCatalog.Find(toolId) == null)
                    {
                        incompatible.Add($"Ferramenta desconhecida nesta versão (\"{toolId ?? item.ToString(Formatting.None)}\") — mantida na seção de origem.");
                        continue;
                    }

                    if (!customization.SectionExists(sectionId))
                    {
                        incompatible.Add($"Ferramenta \"{Favorites.PanelToolCatalog.Find(toolId)!.Label}\": seção desconhecida (\"{sectionId}\") — mantida na seção de origem.");
                        continue;
                    }

                    // Place ignora repetidas e "mover para a própria origem"; a ordem do arquivo fica.
                    if (customization.SectionOf(toolId) == PanelLayout.PanelCustomization.HomeSectionOf(toolId))
                        customization.Place(toolId, sectionId!);
                }
            }

            return customization.Normalize();
        }

        /// <summary>Aplica os campos de <paramref name="document"/> em
        /// <paramref name="target"/> (mutação direta — quem chama decide quando/se persistir via
        /// <see cref="ListForgeIntegratedSettingsStore.Save"/>, já com a gravação segura). Um
        /// perfil citado por nome que não existe em <paramref name="target"/> é só reportado em
        /// <see cref="ApplyResult.SkippedSummaries"/> — nunca cria um perfil novo nem altera
        /// qualquer outro dado do perfil (pasta/nome de exportação, formato de Salvar avançado
        /// etc.), nunca a lista de perfis em si.</summary>
        public static ApplyResult Apply(Document document, ListForgeIntegratedSettings target)
        {
            var applied = new List<string> { $"Tamanho de fonte do editor: {target.EditorFontSize:0.#} → {document.EditorFontSize:0.#}" };
            target.EditorFontSize = document.EditorFontSize;

            var skipped = new List<string>();
            foreach (var entry in document.PreferredWorkspaceModeByProfileName)
            {
                var profile = target.WorkProfiles.FirstOrDefault(p => string.Equals(p.Name, entry.Key, StringComparison.Ordinal));
                if (profile == null)
                {
                    skipped.Add($"Perfil \"{entry.Key}\": não existe nesta instalação — preferência de modo ignorada.");
                    continue;
                }

                target.SetPreferredWorkspaceMode(profile.Id, entry.Value);
                var modeLabel = entry.Value == ListForgeIntegratedSettings.WorkspaceModePx ? "PX" : "Clássico";
                applied.Add($"Perfil \"{entry.Key}\" → modo preferido: {modeLabel}");
            }

            if (document.PanelFavoriteToolIds != null)
            {
                target.PanelFavoriteToolIds = document.PanelFavoriteToolIds.ToList();
                applied.Add(DescribeFavorites(target.PanelFavoriteToolIds));
            }

            if (document.PanelFavoritesShowLabels != null)
            {
                target.PanelFavoritesShowLabels = document.PanelFavoritesShowLabels.Value;
                applied.Add(DescribeShowLabels(document.PanelFavoritesShowLabels.Value));
            }

            if (document.PanelCustomization != null)
            {
                var customization = document.PanelCustomization.Clone().Normalize();
                target.PanelCustomization = customization.IsDefault ? null : customization;
                applied.Add(DescribeCustomization(customization));
            }

            // Seções criadas que valem depois desta importação (as do arquivo, ou as atuais).
            var customIds = target.PanelCustomization?.CustomSections.Select(s => s.Id).ToList() ?? new List<string>();
            if (document.PanelSectionLayout != null)
            {
                // Seções que o arquivo não traz (ex.: exportado por versão mais antiga) entram
                // visíveis no fim; desconhecidas preservadas no destino continuam no fim.
                var merged = new PanelLayout.PanelSectionLayout(
                    document.PanelSectionLayout.Concat(new PanelLayout.PanelSectionLayout(target.PanelSectionLayout).UnknownEntries), customIds);
                target.PanelSectionLayout = merged.ToPersist();
                applied.Add(DescribeSectionLayout(merged.Sections, target.PanelCustomization));
            }
            else if (document.PanelCustomization != null && target.PanelSectionLayout != null)
            {
                // Personalização sem layout no arquivo: o layout atual deixa de citar seções
                // criadas que não existem mais (as novas entram no fim, visíveis).
                target.PanelSectionLayout = new PanelLayout.PanelSectionLayout(target.PanelSectionLayout, customIds).ToPersist();
            }

            return new ApplyResult(applied, skipped);
        }

        /// <summary>camelCase para os NOMES DAS PROPRIEDADES do esquema (mesma convenção já
        /// usada por release-manifest.json), mas NUNCA para as chaves de
        /// <see cref="Document.PreferredWorkspaceModeByProfileName"/> — essas são nomes de
        /// perfil (dado do usuário, com acentos/maiúsculas que precisam ser preservados
        /// exatamente); <c>processDictionaryKeys: false</c> é necessário porque
        /// CamelCaseNamingStrategy, por padrão, também remexeria nessas chaves.</summary>
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(processDictionaryKeys: false, overrideSpecifiedNames: true),
            },
        };

        public static string Serialize(Document document) => JsonConvert.SerializeObject(document, SerializerSettings);
    }
}
