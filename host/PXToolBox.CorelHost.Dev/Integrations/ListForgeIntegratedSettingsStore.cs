using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ListForge.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PXToolBoxDev.Integrations
{
    /// <summary>Resultado de <see cref="ListForgeIntegratedSettingsStore.Load"/> — sempre traz
    /// valores usáveis (<see cref="Settings"/>), mesmo quando algo deu errado; <see cref="Notice"/>
    /// explica o que aconteceu, para a UI decidir se avisa o usuário.</summary>
    public sealed class ListForgeIntegratedSettingsLoadResult
    {
        public ListForgeIntegratedSettingsLoadResult(ListForgeIntegratedSettings settings, string notice)
        {
            Settings = settings;
            Notice = notice;
        }

        public ListForgeIntegratedSettings Settings { get; }

        /// <summary>Vazio quando o carregamento foi normal (arquivo ausente na primeira
        /// execução, ou arquivo válido lido com sucesso). Não vazio quando o arquivo existia mas
        /// estava corrompido/ilegível/de um esquema desconhecido — nesse caso o arquivo ORIGINAL
        /// nunca é sobrescrito silenciosamente: é copiado para um nome ".corrompido-<data>"
        /// antes de <see cref="Settings"/> voltar aos padrões.</summary>
        public string Notice { get; }
    }

    /// <summary>
    /// Carrega/grava <see cref="ListForgeIntegratedSettings"/> em
    /// <c>%LocalAppData%\PXToolBox\Dev\config\listforge-integrated-settings.json</c> — mesma
    /// pasta já usada por <see cref="ListForgeConfigStore"/> (infraestrutura de configuração já
    /// existente do PX DEV, reaproveitada em vez de inventar uma nova convenção), num arquivo
    /// próprio do módulo ListForge Integrado, separado do caminho do executável do standalone.
    ///
    /// Nunca lê nem grava nada do ListForge standalone, do PX de produção ou do MoldMotor — são
    /// três distribuições/produtos com identidade e armazenamento próprios (AGENTS.md). Sem
    /// sincronização em nuvem.
    /// </summary>
    public static class ListForgeIntegratedSettingsStore
    {
        private static string? _configDirectoryOverride;

        private static string ConfigDirectory => _configDirectoryOverride ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PXToolBox", "Dev", "config");

        private static string FilePath => Path.Combine(ConfigDirectory, "listforge-integrated-settings.json");

        /// <summary>Exposto só para o botão "Abrir pasta de configuração" — mesma pasta de
        /// <see cref="ListForgeConfigStore"/>, nunca a pasta do ListForge standalone.</summary>
        public static string ConfigDirectoryForDisplay => ConfigDirectory;

        /// <summary>Exposto só para diagnóstico (ver <see cref="PxLogger"/>, parâmetro
        /// relatedFilePath) — nunca o conteúdo do arquivo, só o caminho.</summary>
        public static string FilePathForDisplay => FilePath;

        /// <summary>Só para testes — aponta esta classe (e, por consequência,
        /// <see cref="ListForgeProcessingHistoryService.DefaultPath"/>, que deriva de
        /// <see cref="ConfigDirectoryForDisplay"/>) para um diretório temporário isolado, nunca
        /// a pasta real do usuário em <c>%LocalAppData%\PXToolBox\Dev\config\</c>. Mesmo padrão
        /// já usado por <see cref="PxLogger.SetLogDirectoryForTesting"/>.</summary>
        public static void SetConfigDirectoryForTesting(string? directory) => _configDirectoryOverride = directory;

        public static ListForgeIntegratedSettingsLoadResult Load()
        {
            if (!File.Exists(FilePath))
            {
                // Primeira execução (ou configuração removida manualmente) — padrões, sem aviso:
                // isto não é um erro.
                return new ListForgeIntegratedSettingsLoadResult(ListForgeIntegratedSettings.Default(), "");
            }

            string json;
            try
            {
                json = File.ReadAllText(FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Não dá para nem ler o arquivo (ex.: outro processo com lock exclusivo) — não é
                // o mesmo caso de "conteúdo corrompido" (o arquivo em si pode estar são); não
                // tenta mover/sobrescrever um arquivo que nem conseguiu abrir. Só informa e usa
                // os padrões nesta execução.
                return new ListForgeIntegratedSettingsLoadResult(
                    ListForgeIntegratedSettings.Default(),
                    $"Não foi possível ler a configuração salva ({ex.GetType().Name}: {ex.Message}). Usando os valores padrão nesta sessão.");
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException)
            {
                return QuarantineAndReturnDefaults(json, "o arquivo não é um JSON válido");
            }

            var schemaVersion = root.Value<int?>(nameof(ListForgeIntegratedSettings.SchemaVersion)) ?? 0;

            // Migração explícita do esquema 1 (sem perfis de trabalho — formato/pasta/nome de
            // exportação eram campos únicos no topo) para o esquema 2 (iteração 13: esses mesmos
            // campos passaram a viver dentro de cada perfil de trabalho — ver
            // ListForgeIntegratedSettings). Lida com o JSON bruto em vez de desserializar direto
            // para o tipo atual: o tipo atual não tem mais os campos antigos, então uma
            // desserialização direta os perderia silenciosamente.
            if (schemaVersion == 1)
            {
                var migrated = MigrateFromSchemaVersion1(root);
                return new ListForgeIntegratedSettingsLoadResult(
                    migrated,
                    "Configurações migradas para o novo formato com perfis de trabalho (versão 2 do esquema). " +
                    "O formato do Salvar avançado e a pasta/nome padrão de exportação que você já tinha configurado " +
                    "foram preservados no perfil \"Padrão\".");
            }

            if (schemaVersion != ListForgeIntegratedSettings.CurrentSchemaVersion)
            {
                // Esquema desconhecido (nunca visto por esta versão do PX — ex.: uma build mais
                // nova gravou uma versão que esta build mais antiga não entende, ou um número
                // inválido/ausente). Nunca tenta "adivinhar" uma migração para um esquema que
                // não existe: preserva o arquivo original em quarentena.
                return QuarantineAndReturnDefaults(json, $"versão de esquema desconhecida ({schemaVersion})");
            }

            ListForgeIntegratedSettings? parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<ListForgeIntegratedSettings>(json);
            }
            catch (JsonException)
            {
                parsed = null;
            }

            if (parsed == null)
                return QuarantineAndReturnDefaults(json, "o arquivo não é um JSON válido");

            parsed.Normalize();
            return new ListForgeIntegratedSettingsLoadResult(parsed, "");
        }

        private static ListForgeIntegratedSettings MigrateFromSchemaVersion1(JObject root)
        {
            var settings = new ListForgeIntegratedSettings
            {
                EditorFontSize = root.Value<double?>("EditorFontSize") ?? ListForgeIntegratedSettings.DefaultEditorFontSize,
            };

            var legacySettings = WorkProfileService.CaptureOfficialDefaultSettings();
            legacySettings.AdvancedSaveMode = string.Equals(root.Value<string>("AdvancedSaveMode"), ListForgeIntegratedSettings.ModeZip, StringComparison.OrdinalIgnoreCase)
                ? ListForgeIntegratedSettings.ModeZip
                : ListForgeIntegratedSettings.ModeLooseFiles;
            legacySettings.UseDefaultOutputDir = root.Value<bool?>("UseDefaultOutputDir") ?? false;
            legacySettings.OutputDir = root.Value<string>("OutputDir") ?? "";
            legacySettings.UseDefaultListName = root.Value<bool?>("UseDefaultListName") ?? false;
            legacySettings.DefaultListName = root.Value<string>("DefaultListName") ?? "lista";

            // EnsureProfiles cria o perfil "Padrão" já com estas configurações (em vez das
            // oficiais) quando ainda não existe nenhum perfil — exatamente o caso aqui, já que
            // settings.WorkProfiles começa vazio.
            new WorkProfileService().EnsureProfiles(settings, legacySettings);
            settings.SchemaVersion = ListForgeIntegratedSettings.CurrentSchemaVersion;
            return settings;
        }

        private static ListForgeIntegratedSettingsLoadResult QuarantineAndReturnDefaults(string originalContent, string reason)
        {
            var quarantinePath = Path.Combine(
                ConfigDirectory,
                $"listforge-integrated-settings.corrompido-{DateTime.Now:yyyyMMdd-HHmmss}.json");

            try
            {
                File.WriteAllText(quarantinePath, originalContent);
            }
            catch
            {
                // Melhor esforço: mesmo sem conseguir preservar uma cópia, continua com os
                // padrões nesta sessão — nunca sobrescreve o arquivo original sozinho por conta
                // disso (só quem chamar Save() mais tarde, de forma explícita, grava de novo).
            }

            return new ListForgeIntegratedSettingsLoadResult(
                ListForgeIntegratedSettings.Default(),
                $"A configuração salva estava inválida ({reason}) e foi preservada em \"{Path.GetFileName(quarantinePath)}\", na mesma pasta. Usando os valores padrão até você salvar de novo.");
        }

        /// <summary>Grava de forma segura contra arquivo parcialmente escrito e tolerante a
        /// bloqueio transitório no destino — ver <see cref="AtomicFileWriter"/> (infraestrutura
        /// compartilhada com <see cref="ListForgeProcessingHistoryService"/>, que tinha uma
        /// cópia quase idêntica desta mesma lógica antes desta correção). O arquivo anterior
        /// válido NUNCA é removido antes do novo conteúdo estar inteiramente escrito e pronto —
        /// se qualquer etapa da substituição falhar, o arquivo anterior é restaurado antes de
        /// propagar a exceção original (nunca mascarada) para quem chama decidir a mensagem
        /// exibida ao usuário (ver ClassicWorkspaceWindow/SettingsWindow/SizeConfigWindow) — eles
        /// também são responsáveis por manter as alterações em memória visíveis na interface
        /// mesmo quando o salvamento falha (não é responsabilidade desta classe, que só persiste
        /// o que já está no objeto <paramref name="settings"/> recebido).
        ///
        /// <para>Favoritos do painel: por padrão o valor que está EM DISCO é mantido (e copiado
        /// para <paramref name="settings"/>) — as janelas do ListForge guardam este objeto por
        /// muito tempo e não podem desfazer favoritos alterados no painel enquanto estavam
        /// abertas. <paramref name="includePanelFavorites"/> grava o valor do objeto: só para
        /// importações que trouxeram favoritos.</para>
        ///
        /// <para>Layout do painel (<see cref="ListForgeIntegratedSettings.PanelSectionLayout"/>):
        /// mesma regra, com <paramref name="includePanelSectionLayout"/>.</para></summary>
        public static void Save(ListForgeIntegratedSettings settings, bool includePanelFavorites = false, bool includePanelSectionLayout = false,
            bool includePanelFavoritesShowLabels = false, bool includePanelCustomization = false)
        {
            if ((!includePanelFavorites || !includePanelSectionLayout || !includePanelFavoritesShowLabels || !includePanelCustomization) && TryReadStoredRoot(out var stored))
            {
                if (!includePanelCustomization)
                    settings.PanelCustomization = ReadCustomizationToken(stored![nameof(ListForgeIntegratedSettings.PanelCustomization)]);
                if (!includePanelFavorites)
                    settings.PanelFavoriteToolIds = ReadFavoritesToken(stored![nameof(ListForgeIntegratedSettings.PanelFavoriteToolIds)], out _);
                if (!includePanelSectionLayout)
                    settings.PanelSectionLayout = ReadSectionLayoutToken(stored![nameof(ListForgeIntegratedSettings.PanelSectionLayout)], out _);
                if (!includePanelFavoritesShowLabels)
                    settings.PanelFavoritesShowLabels = ReadShowLabelsToken(stored![nameof(ListForgeIntegratedSettings.PanelFavoritesShowLabels)]);
            }

            settings.Normalize();
            settings.SchemaVersion = ListForgeIntegratedSettings.CurrentSchemaVersion;

            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            AtomicFileWriter.WriteTextSafely(FilePath, json);

            if (includePanelFavorites || includePanelFavoritesShowLabels)
            {
                PanelFavoritesChanged?.Invoke();
            }

            // O painel relê layout e personalização juntos (ReloadSectionLayout).
            if (includePanelSectionLayout || includePanelCustomization)
            {
                PanelSectionLayoutChanged?.Invoke();
            }
        }

        /// <summary>Favoritos do painel foram gravados (pelo próprio painel ou por uma
        /// importação na tela de Configurações) — o painel relê e se redesenha.</summary>
        public static event Action? PanelFavoritesChanged;

        /// <summary>Layout do painel foi gravado (por "Personalizar painel…" ou por uma
        /// importação na tela de Configurações) — o painel relê e reorganiza as seções.</summary>
        public static event Action? PanelSectionLayoutChanged;

        public sealed class PanelFavoritesLoadResult
        {
            public PanelFavoritesLoadResult(IReadOnlyList<string> ids, string notice)
            {
                Ids = ids;
                Notice = notice;
            }

            public IReadOnlyList<string> Ids { get; }

            /// <summary>Vazio quando a leitura foi normal (inclusive arquivo/campo ausente).</summary>
            public string Notice { get; }
        }

        /// <summary>
        /// Leitura SÓ do campo de favoritos, sem efeito colateral — diferente de
        /// <see cref="Load"/>, nunca move arquivo para quarentena nem migra: o painel abre em
        /// qualquer caso, no pior deles sem favoritos e com um aviso.
        /// </summary>
        public static PanelFavoritesLoadResult LoadPanelFavorites()
        {
            if (!File.Exists(FilePath))
            {
                return new PanelFavoritesLoadResult(Array.Empty<string>(), "");
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(FilePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new PanelFavoritesLoadResult(Array.Empty<string>(), $"Favoritos não carregados: a configuração não pôde ser lida ({ex.GetType().Name}).");
            }

            var ids = ReadFavoritesToken(root[nameof(ListForgeIntegratedSettings.PanelFavoriteToolIds)], out var notice);
            return new PanelFavoritesLoadResult(ids ?? new List<string>(), notice);
        }

        /// <summary>
        /// Grava SÓ os favoritos, mantendo intacto todo o resto do arquivo (inclusive campos
        /// que esta versão não conhece). Arquivo ausente → configuração padrão com os favoritos;
        /// esquema 1 → migra como <see cref="Load"/> e grava; arquivo ilegível ou de esquema
        /// desconhecido → lança <see cref="InvalidOperationException"/> sem tocar nele.
        /// </summary>
        public static void SavePanelFavorites(IReadOnlyList<string> ids)
        {
            var list = ids.ToList();
            SavePanelField(
                nameof(ListForgeIntegratedSettings.PanelFavoriteToolIds),
                new JArray(list),
                settings => settings.PanelFavoriteToolIds = list,
                settings => Save(settings, includePanelFavorites: true),
                () => PanelFavoritesChanged?.Invoke());
        }

        /// <summary>Favoritos com nome (true) ou só ícone (false, também quando nunca definido
        /// ou ilegível). Nunca lança.</summary>
        public static bool LoadPanelFavoritesShowLabels() =>
            TryReadStoredRoot(out var root) && ReadShowLabelsToken(root![nameof(ListForgeIntegratedSettings.PanelFavoritesShowLabels)]) == true;

        /// <summary>Grava SÓ o modo de exibição dos favoritos — mesmas garantias de
        /// <see cref="SavePanelFavorites"/>.</summary>
        public static void SavePanelFavoritesShowLabels(bool showLabels)
        {
            SavePanelField(
                nameof(ListForgeIntegratedSettings.PanelFavoritesShowLabels),
                new JValue(showLabels),
                settings => settings.PanelFavoritesShowLabels = showLabels,
                settings => Save(settings, includePanelFavoritesShowLabels: true),
                () => PanelFavoritesChanged?.Invoke());
        }

        /// <summary>Personalização do painel em disco (nomes, seções criadas, posições), ou a
        /// padrão quando nunca definida ou ilegível. Nunca lança.</summary>
        public static PanelLayout.PanelCustomization LoadPanelCustomization() =>
            (TryReadStoredRoot(out var root) ? ReadCustomizationToken(root![nameof(ListForgeIntegratedSettings.PanelCustomization)]) : null)
            ?? PanelLayout.PanelCustomization.Default();

        /// <summary>Grava a personalização e a ordem/visibilidade das seções JUNTAS (uma seção
        /// criada precisa das duas) — mesmas garantias de <see cref="SavePanelFavorites"/>.</summary>
        public static void SavePanelCustomization(PanelLayout.PanelCustomization customization, IReadOnlyList<PanelLayout.PanelSectionLayoutEntry> layout)
        {
            var normalized = customization.Clone().Normalize();
            SavePanelField(
                nameof(ListForgeIntegratedSettings.PanelCustomization),
                normalized.IsDefault ? JValue.CreateNull() : JObject.FromObject(normalized),
                settings => settings.PanelCustomization = normalized.IsDefault ? null : normalized,
                settings => Save(settings, includePanelCustomization: true),
                () => { });
            SavePanelSectionLayout(layout);
        }

        private static PanelLayout.PanelCustomization? ReadCustomizationToken(JToken? token)
        {
            if (token == null || token.Type != JTokenType.Object) return null;
            try
            {
                return token.ToObject<PanelLayout.PanelCustomization>()?.Normalize();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>"Mostrar nomes" em disco, ou null se nunca definido/ilegível.</summary>
        public static bool? ReadStoredPanelFavoritesShowLabelsOrNull() =>
            TryReadStoredRoot(out var root) ? ReadShowLabelsToken(root![nameof(ListForgeIntegratedSettings.PanelFavoritesShowLabels)]) : null;

        /// <summary>Personalização em disco, ou null se nunca definida/ilegível.</summary>
        public static PanelLayout.PanelCustomization? ReadStoredPanelCustomizationOrNull() =>
            TryReadStoredRoot(out var root) ? ReadCustomizationToken(root![nameof(ListForgeIntegratedSettings.PanelCustomization)]) : null;

        private static bool? ReadShowLabelsToken(JToken? token) =>
            token != null && token.Type == JTokenType.Boolean ? (bool)token : (bool?)null;

        /// <summary>Favoritos atualmente em disco, ou null se nunca definidos/ilegíveis — para
        /// quem exporta a partir de uma cópia antiga do objeto (tela de Configurações).</summary>
        public static List<string>? ReadStoredPanelFavoritesOrNull() =>
            TryReadStoredRoot(out var root)
                ? ReadFavoritesToken(root![nameof(ListForgeIntegratedSettings.PanelFavoriteToolIds)], out _)
                : null;

        public sealed class PanelSectionLayoutLoadResult
        {
            public PanelSectionLayoutLoadResult(List<PanelLayout.PanelSectionLayoutEntry>? entries, string notice)
            {
                Entries = entries;
                Notice = notice;
            }

            /// <summary>null = nunca definido (o painel usa o layout padrão).</summary>
            public List<PanelLayout.PanelSectionLayoutEntry>? Entries { get; }

            public string Notice { get; }
        }

        /// <summary>Leitura SÓ do layout do painel, sem efeito colateral — mesmo contrato de
        /// <see cref="LoadPanelFavorites"/>: o painel abre sempre, no pior caso com o layout
        /// padrão e um aviso.</summary>
        public static PanelSectionLayoutLoadResult LoadPanelSectionLayout()
        {
            if (!File.Exists(FilePath))
            {
                return new PanelSectionLayoutLoadResult(null, "");
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(FilePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new PanelSectionLayoutLoadResult(null, $"Layout do painel não carregado: a configuração não pôde ser lida ({ex.GetType().Name}) — usando o layout padrão.");
            }

            var entries = ReadSectionLayoutToken(root[nameof(ListForgeIntegratedSettings.PanelSectionLayout)], out var notice);
            return new PanelSectionLayoutLoadResult(entries, notice);
        }

        /// <summary>Grava SÓ o layout do painel — mesmas garantias de
        /// <see cref="SavePanelFavorites"/>.</summary>
        public static void SavePanelSectionLayout(IReadOnlyList<PanelLayout.PanelSectionLayoutEntry> entries)
        {
            var list = entries.Select(e => new PanelLayout.PanelSectionLayoutEntry(e.Id, e.Visible)).ToList();
            SavePanelField(
                nameof(ListForgeIntegratedSettings.PanelSectionLayout),
                JArray.FromObject(list),
                settings => settings.PanelSectionLayout = list,
                settings => Save(settings, includePanelSectionLayout: true),
                () => PanelSectionLayoutChanged?.Invoke());
        }

        /// <summary>Layout atualmente em disco, ou null se nunca definido/ilegível.</summary>
        public static List<PanelLayout.PanelSectionLayoutEntry>? ReadStoredPanelSectionLayoutOrNull() =>
            TryReadStoredRoot(out var root)
                ? ReadSectionLayoutToken(root![nameof(ListForgeIntegratedSettings.PanelSectionLayout)], out _)
                : null;

        // Grava um campo do painel mexendo só nele: arquivo ausente → configuração padrão com o
        // campo; esquema 1 → migra como Load e grava; ilegível ou esquema desconhecido → lança
        // sem tocar no arquivo; esquema atual → troca só a chave no JSON (preserva campos que
        // esta versão não conhece).
        private static void SavePanelField(
            string fieldName,
            JToken value,
            Action<ListForgeIntegratedSettings> assign,
            Action<ListForgeIntegratedSettings> saveWhole,
            Action notify)
        {
            if (!File.Exists(FilePath))
            {
                var fresh = ListForgeIntegratedSettings.Default();
                assign(fresh);
                saveWhole(fresh);
                return;
            }

            if (!TryReadStoredRoot(out var root))
            {
                throw new InvalidOperationException("a configuração existente não pôde ser lida — o arquivo não foi alterado.");
            }

            var schemaVersion = root!.Value<int?>(nameof(ListForgeIntegratedSettings.SchemaVersion)) ?? 0;
            if (schemaVersion == 1)
            {
                var migrated = Load().Settings;
                assign(migrated);
                saveWhole(migrated);
                return;
            }

            if (schemaVersion != ListForgeIntegratedSettings.CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"a configuração existente tem uma versão de esquema desconhecida ({schemaVersion}) — o arquivo não foi alterado.");
            }

            if (value.Type == JTokenType.Null)
                root.Remove(fieldName);
            else
                root[fieldName] = value;
            AtomicFileWriter.WriteTextSafely(FilePath, root.ToString(Formatting.Indented));
            notify();
        }

        private static bool TryReadStoredRoot(out JObject? root)
        {
            root = null;
            try
            {
                if (!File.Exists(FilePath)) return false;
                root = JObject.Parse(File.ReadAllText(FilePath));
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return false;
            }
        }

        // null = campo ausente/nulo. Tipo inesperado → lista vazia e aviso; itens não-texto
        // ignorados.
        private static List<string>? ReadFavoritesToken(JToken? token, out string notice)
        {
            notice = "";
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token is not JArray array)
            {
                notice = "Favoritos não carregados: o campo salvo tem um formato inesperado.";
                return new List<string>();
            }

            return array.Where(item => item.Type == JTokenType.String).Select(item => item.Value<string>()!).ToList();
        }

        // null = campo ausente/nulo (layout padrão). Tipo inesperado → null e aviso; itens que
        // não sejam { Id: texto, Visible: bool opcional } são ignorados.
        internal static List<PanelLayout.PanelSectionLayoutEntry>? ReadSectionLayoutToken(JToken? token, out string notice)
        {
            notice = "";
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            if (token is not JArray array)
            {
                notice = "Layout do painel não carregado: o campo salvo tem um formato inesperado — usando o layout padrão.";
                return null;
            }

            var entries = new List<PanelLayout.PanelSectionLayoutEntry>();
            foreach (var item in array.OfType<JObject>())
            {
                var id = item.Properties().FirstOrDefault(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))?.Value;
                var visible = item.Properties().FirstOrDefault(p => string.Equals(p.Name, "Visible", StringComparison.OrdinalIgnoreCase))?.Value;
                if (id?.Type != JTokenType.String) continue;
                if (visible != null && visible.Type != JTokenType.Boolean) continue;
                entries.Add(new PanelLayout.PanelSectionLayoutEntry(id.Value<string>()!, visible?.Value<bool>() ?? true));
            }

            return entries;
        }
    }
}
