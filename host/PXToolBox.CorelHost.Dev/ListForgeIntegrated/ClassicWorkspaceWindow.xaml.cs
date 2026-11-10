using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CorelApplication = Corel.Interop.VGCore.Application;
using ListForge.Core;
using ListForge.Models;
using ListForge.Services;

namespace PXToolBoxDev.ListForgeIntegrated
{
    /// <summary>
    /// Modo Clássico do ListForge Integrado. Janela própria (não o docker compacto), para dar
    /// espaço real de edição/revisão de listas. Chama exclusivamente a camada compartilhada do
    /// ListForge (ListForge.Core/ListForge.Services) — nenhuma regra de parsing/validação/
    /// exportação é reimplementada aqui.
    ///
    /// Não toca em nenhum objeto COM do Corel: esta janela não depende de documento aberto no
    /// CorelDRAW e o processamento roda em uma Task separada da thread de UI (OnProcess).
    /// </summary>
    public partial class ClassicWorkspaceWindow : Window
    {
        private readonly ILicenseService _licenseService = new IntegratedLicenseService();
        private readonly ProcessingWorkflowService _workflow;
        private readonly ProcessingPreviewService _previewService;
        private readonly ListComparisonService _comparisonService = new();
        private ComparisonSnapshot? _comparisonSnapshot;

        private readonly AdvancedSaveService _advancedSaveService = new();

        // Configurações do módulo (ver docs/listforge-classic-parity.md, grupos
        // "Configurações" e "Perfis de trabalho") — carregadas uma vez na abertura da janela
        // (Integrations/ListForgeIntegratedSettingsStore, %LocalAppData%\PXToolBox\Dev\config\,
        // exclusivo do PX DEV e deste módulo; nunca o ConfigManager do ListForge standalone).
        // _settings é a fonte de verdade durante toda a sessão da janela; SettingsWindow só lê/
        // grava quando aberta explicitamente pelo usuário.
        private Integrations.ListForgeIntegratedSettings _settings = Integrations.ListForgeIntegratedSettings.Default();
        private string _settingsLoadNotice = "";

        // Perfis de trabalho (ver docs/listforge-classic-parity.md, grupo "Perfis de
        // trabalho") — mesmas regras do standalone (WorkProfileService, movido para
        // ListForge.Core nesta entrega). O combo e os botões de gerenciamento vivem nesta
        // janela (não em SettingsWindow) porque é aqui que estão os controles de separador/
        // capitalização/ordenação/lista avançada que compõem um perfil; SettingsWindow continua
        // só com o tamanho de fonte e o formato/pasta/nome do Salvar avançado do perfil ATIVO.
        private readonly WorkProfileService _workProfileService = new();
        private readonly ObservableCollection<WorkProfile> _workProfiles = new();
        private bool _isApplyingWorkProfile;
        private bool _hasUnsavedWorkProfileChanges;

        private AdvancedSaveMode _advancedSaveMode = AdvancedSaveMode.LooseFiles;
        private readonly OutputExportService _exportService = new OutputExportService();
        private readonly JsonPieceMappingService _pieceMappingService = new();
        private readonly ObservableCollection<AdvancedPieceSlot> _advancedSlots = new();

        // Extrair lista de link (ver docs/listforge-classic-parity.md, grupo "Entrada e
        // importação") — LinkListImportService foi movido do projeto standalone-only para
        // ListForge.Core nesta entrega (único acoplamento era ConfigManager.AppName, usado só
        // para um cabeçalho HTTP, sem efeito observável). Mantém o HttpClient interno vivo
        // enquanto esta janela existir; descartado em Window_Closed.
        private readonly LinkListImportService _linkListImportService = new();

        // Limitação conhecida: usa sempre o SizeConfig padrão do núcleo compartilhado
        // (ListForge.Models.SizeConfig.Default()) — ainda não lê um SizeConfig customizado pelo
        // usuário no ListForge standalone (arquivo local da instalação do ListForge, resolvido
        // por ListForge.Config.ConfigManager, que permanece fora do núcleo compartilhado).
        // Tamanhos customizados (ver docs/listforge-classic-parity.md, grupo "Configurações
        // aplicáveis ao integrado") — igual ao standalone, independente de perfil de trabalho.
        // Inicializado a partir de _settings.Sizes (carregado no construtor) e substituído por
        // inteiro quando SizeConfigWindow salva — nunca editado parcialmente aqui, só lido pelos
        // consumidores existentes (RefreshAdvancedSlots/BuildProcessingRequest/SizeSummaryBuilder/
        // OnExtractFromLink).
        private SizeConfig _sizeConfig = SizeConfig.Default();

        private string _lastOutputText = "";
        private List<Dictionary<string, string>> _lastOrders = new();
        private List<int> _issueLines = new();
        private readonly OutputFreshnessTracker _freshness = new();
        private string? _currentInputFile;
        private bool _hasUnsavedChanges;
        private bool _isProcessing;
        private bool _isSavingAdvanced;
        private int _lastProcessedRowCount;

        // Histórico de processamentos (ver docs/listforge-classic-parity.md, grupo "Revisão e
        // resumos") — _currentSourceType rastreia explicitamente a origem da entrada atual
        // (arquivo aberto/link extraído/edição aplicada/texto colado), igual ao
        // MainViewModel._currentSourceType do standalone: só muda nos mesmos quatro pontos de
        // transição (abrir arquivo, extrair link, aplicar edição manual, "Limpar tudo"), nunca
        // só por digitar. Usado só para decidir o nome/tipo exibidos no histórico — nunca altera
        // o processamento em si.
        private string _currentSourceType = Integrations.ListForgeProcessingHistorySourceTypes.PastedText;
        private readonly Integrations.ListForgeProcessingHistoryService _historyService = new();

        // Edição manual da saída/JSON (ver docs/listforge-classic-parity.md, grupo "Geração,
        // cópia e exportação de resultados") — porta de MainViewModel.AllowOutputEditing/
        // HasPendingOutputEdit/HasPendingJsonEdit/ApplyOutputEdits/DiscardOutputEdits do
        // standalone (lido por completo antes de implementar). _allowOutputEditing é SÓ DESTA
        // SESSÃO, nunca persistida em _settings — mesma regra da referência (checkbox
        // "nesta sessão" em Configurações lá). _lastValidJsonText é o par de _lastOutputText
        // para o JSON: só muda em um processamento ou "Aplicar alterações" com sucesso — nunca
        // reflete texto editado ainda não aplicado. Isso é o que garante que "Copiar JSON"/
        // "Gerar JSON"/o JSON de "Salvar avançado" nunca usem JSON invalido/não revalidado (ver
        // OnCopyJson/OnGenerateJson/OnAdvancedSave) — mesma proteção do standalone, que também
        // nunca usa o JsonText (live) bruto para essas três ações, só _lastJson (canônico).
        private bool _allowOutputEditing;
        private bool _isTogglingAllowOutputEditing;
        private bool _isUpdatingGeneratedText;
        private bool _hasPendingOutputEdit;
        private bool _hasPendingJsonEdit;
        private bool _outputWasManuallyEdited;
        private string _lastValidJsonText = "";

        // Diagnóstico e suporte (ver docs/listforge-classic-parity.md, grupo "Configurações
        // aplicáveis ao integrado") — _corelVersion é só uma STRING capturada uma única vez no
        // construtor (nunca a referência COM em si é guardada aqui): esta janela continua sem
        // depender de nenhum objeto COM vivo depois de construída, mesma garantia documentada
        // no cabeçalho da classe.
        private readonly string? _corelVersion;
        private readonly Integrations.SupportPackageService _supportPackageService = new();

        public ClassicWorkspaceWindow() : this(null)
        {
        }

        public ClassicWorkspaceWindow(CorelApplication? app)
        {
            InitializeComponent();
            _workflow = new ProcessingWorkflowService(_licenseService);
            _previewService = new ProcessingPreviewService(_workflow);
            _keyboardRouter = new ThreadKeyboardRouter(this);
            _keyboardRouter.CountersChanged += UpdateKeyboardDiagText;
            _corelVersion = TryGetCorelVersion(app);

            CmbCaseMode.ItemsSource = new[] { "Original", "Tudo maiúsculo", "Tudo minúsculo" };
            CmbSortMode.ItemsSource = new[] { "Original", "Crescente", "Decrescente" };
            AdvancedSlotsList.ItemsSource = _advancedSlots;
            CmbWorkProfile.ItemsSource = _workProfiles;

            var loadResult = Integrations.ListForgeIntegratedSettingsStore.Load();
            _settings = loadResult.Settings;
            _settingsLoadNotice = loadResult.Notice;
            _sizeConfig = _settings.Sizes;
            ApplySettingsToUi();
            RefreshWorkProfilesUi();
            ApplyWorkProfileProcessingOptionsToUi(ActiveProfileSettings());

            UpdateAccessStatusText();
            UpdateSupportInfoText();
            Integrations.PxLogger.Info("Startup",
                $"ListForge Integrado iniciado. PX {Integrations.PxVersionInfo.PxToolBoxVersion}, " +
                $"núcleo ListForge {Integrations.PxVersionInfo.ListForgeCoreVersion}, " +
                $"CorelDRAW {_corelVersion ?? "não disponível"}.");

            PxApplyInitialMode();
        }

        // app?.Version lido uma única vez aqui, nunca guardado como referência COM — ver
        // comentário do campo _corelVersion. Qualquer falha ao ler (Corel em estado inválido,
        // RPC morto) nunca pode impedir esta janela de abrir — só a versão fica "não
        // disponível", igual ao caso de app == null.
        private static string? TryGetCorelVersion(CorelApplication? app)
        {
            if (app == null) return null;
            try
            {
                return app.Version.ToString();
            }
            catch
            {
                return null;
            }
        }

        // Aplica os campos de _settings que têm efeito imediato na UI e não pertencem a nenhum
        // perfil — chamado na abertura e de novo depois de "Salvar" em SettingsWindow. Tamanho
        // de fonte é puramente cosmético (não influencia o resultado do processamento) — não
        // marca o resultado como desatualizado. AdvancedSaveMode/pasta/nome padrão pertencem ao
        // perfil ATIVO (ver ActiveProfileSettings) — só são lidos no momento de usar
        // (OnAdvancedSave/TryPickExportPath/SettingsWindow), não têm estado próprio de UI aqui.
        private void ApplySettingsToUi()
        {
            InputTextBox.FontSize = _settings.EditorFontSize;
            OutputTextBox.FontSize = _settings.EditorFontSize;
            JsonTextBox.FontSize = _settings.EditorFontSize;
            _advancedSaveMode = ActiveProfileSettings().AdvancedSaveMode == Integrations.ListForgeIntegratedSettings.ModeZip
                ? AdvancedSaveMode.Zip
                : AdvancedSaveMode.LooseFiles;
        }

        // Configurações do perfil ativo (ver docs/listforge-classic-parity.md, grupo "Perfis de
        // trabalho") — nunca null: WorkProfileService.EnsureProfiles (chamado por
        // ListForgeIntegratedSettings.Normalize, sempre executado ao carregar/salvar) garante
        // que sempre exista pelo menos o perfil "Padrão". O fallback para
        // CaptureOfficialDefaultSettings() aqui é só uma segunda camada defensiva — nunca deve
        // de fato ser necessário.
        private WorkProfileSettings ActiveProfileSettings() =>
            _workProfileService.GetActiveProfile(_settings)?.Settings ?? WorkProfileService.CaptureOfficialDefaultSettings();

        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            var dialog = new SettingsWindow(_settings, _settingsLoadNotice) { Owner = this };
            // O aviso de configuração corrompida (se houve) só precisa aparecer uma vez, na
            // primeira vez que a tela abrir depois do carregamento.
            _settingsLoadNotice = "";

            if (dialog.ShowDialog() != true || dialog.SavedSettings == null)
            {
                SetStatus("Configurações não alteradas.");
                return;
            }

            _settings = dialog.SavedSettings;
            // "Importar configurações" em SettingsWindow pode ter trocado a lista de perfis, o
            // perfil ativo e os tamanhos customizados por completo — reaplica tudo na UI/estado
            // principal por segurança, mesmo que o caso comum (só editar fonte/Salvar avançado/
            // pasta/nome) não precisasse disso.
            _sizeConfig = _settings.Sizes;
            RefreshWorkProfilesUi();
            ApplySettingsToUi();
            ApplyWorkProfileProcessingOptionsToUi(ActiveProfileSettings());
            MarkInputStale();
            SetStatus("Configurações salvas.");
        }

        // Tamanhos customizados (ver docs/listforge-classic-parity.md, grupo "Configurações
        // aplicáveis ao integrado") — porta de SizeGroupBindings/RestoreDefaultSizes do
        // standalone. Independente de perfil de trabalho (vale para todos os perfis, igual
        // lá) — por isso fica num botão próprio, fora do combo/CRUD de perfil.
        private void OnOpenSizeConfig(object sender, RoutedEventArgs e)
        {
            var dialog = new SizeConfigWindow(_settings) { Owner = this };

            if (dialog.ShowDialog() != true || dialog.SavedSettings == null)
            {
                SetStatus("Tamanhos customizados não alterados.");
                return;
            }

            _settings = dialog.SavedSettings;
            _sizeConfig = _settings.Sizes;

            // Tamanhos afetam reconhecimento/validação/ordenação/exportação — o resultado atual
            // (se houver) pode ter sido calculado com tamanhos diferentes. Mesmo mecanismo de
            // invalidação já usado por qualquer outra opção de processamento (MarkInputStale),
            // nunca silenciosamente aceito como ainda válido.
            MarkInputStale();
            RefreshAdvancedSlots();
            SetStatus("Tamanhos customizados salvos — processe novamente para aplicar.");
        }

        // =================================================================================
        // Perfis de trabalho (ver docs/listforge-classic-parity.md, grupo "Perfis de
        // trabalho") — porta de WorkProfileService + dos métodos homônimos de
        // MainViewModel.cs do standalone (CreateWorkProfile/SaveActiveWorkProfile/
        // DiscardWorkProfileChanges/RenameWorkProfile/DuplicateWorkProfile/DeleteWorkProfile/
        // RestoreDefaultWorkProfile/ChangeSelectedWorkProfile). Mesmas regras: o perfil "Padrão"
        // nunca pode ser renomeado nem excluído; nomes duplicados (sem diferenciar maiúsculas/
        // minúsculas) são rejeitados; trocar de perfil com alterações não salvas pergunta
        // Sim/Não/Cancelar (salvar/descartar/continuar no perfil atual); excluir pede
        // confirmação; excluir o perfil ativo cai para o Padrão.
        //
        // O que entra num perfil aqui: separador, capitalização, ordenação, lista avançada (e
        // sua ordem de tipos de peça), formato do Salvar avançado e pasta/nome padrão de
        // exportação — mesmo conjunto de WorkProfileSettings do standalone. Trocar de perfil
        // nunca apaga o texto da lista em InputTextBox — só os controles de OPÇÃO são
        // sobrescritos.
        private void RefreshWorkProfilesUi()
        {
            _isApplyingWorkProfile = true;
            try
            {
                var activeId = _settings.ActiveWorkProfileId;
                _workProfiles.Clear();
                foreach (var profile in _settings.WorkProfiles)
                {
                    profile.DisplayName = string.Equals(profile.Id, activeId, StringComparison.Ordinal) && _hasUnsavedWorkProfileChanges
                        ? $"{profile.Name}*"
                        : profile.Name;
                    _workProfiles.Add(profile);
                }

                CmbWorkProfile.SelectedItem = _workProfiles.FirstOrDefault(p => string.Equals(p.Id, activeId, StringComparison.Ordinal));
            }
            finally
            {
                _isApplyingWorkProfile = false;
            }

            var active = CmbWorkProfile.SelectedItem as WorkProfile;
            BtnRenameWorkProfile.IsEnabled = active != null && !active.IsDefault;
            BtnDeleteWorkProfile.IsEnabled = active != null && !active.IsDefault;
            BtnSaveWorkProfile.IsEnabled = _hasUnsavedWorkProfileChanges;
            BtnDiscardWorkProfile.IsEnabled = _hasUnsavedWorkProfileChanges;
            TxtWorkProfileStatus.Text = _hasUnsavedWorkProfileChanges
                ? "Alterações não salvas no perfil."
                : "Nenhuma alteração pendente.";
        }

        // Chamado por OnProcessingOptionChanged/OnAdvancedListToggled/seleção de slot avançado —
        // qualquer edição das opções de processamento que compõem um perfil. Equivalente a
        // MarkWorkProfileChanged() do standalone.
        private void MarkWorkProfileChanged()
        {
            // Mesma guarda defensiva de RefreshAdvancedSlots/UpdateOutputStaleWarning: o valor
            // inicial de TxtSeparator.Text="," no XAML dispara OnProcessingOptionChanged (e,
            // por ele, este método) DURANTE a própria construção da árvore de elementos do
            // InitializeComponent, antes de ChkAdvancedList/CmbWorkProfile (declarados mais
            // abaixo/acima no XAML, mas conectados em outra ordem de chamada) estarem
            // garantidamente prontos — ver o comentário extenso em UpdateOutputStaleWarning
            // sobre a causa real do crash do CorelDRAW que motivou esta guarda em toda a janela.
            if (_isApplyingWorkProfile || ChkAdvancedList == null || CmbWorkProfile == null) return;

            _hasUnsavedWorkProfileChanges = _workProfileService.HasUnsavedChanges(_settings, CaptureWorkProfileProcessingOptionsFromUi());
            RefreshWorkProfilesUi();
        }

        // Monta um WorkProfileSettings completo a partir do estado atual: separador/
        // capitalização/ordenação/lista avançada vêm da UI principal (editáveis aqui); formato
        // do Salvar avançado e pasta/nome padrão vêm do que já está gravado no perfil ativo
        // (só editáveis via "Configurações...", que grava direto no perfil — ver
        // SettingsWindow.TryBuildSettingsFromControls).
        private WorkProfileSettings CaptureWorkProfileProcessingOptionsFromUi()
        {
            var active = ActiveProfileSettings();
            return new WorkProfileSettings
            {
                DefaultInputSeparator = TxtSeparator.Text,
                DefaultCaseMode = CaseModeFromLabel(CmbCaseMode.SelectedItem as string),
                EditorSortMode = CmbSortMode.SelectedItem as string ?? "Original",
                UseAdvancedJsonPieceMapping = ChkAdvancedList.IsChecked == true,
                AdvancedJsonPieceOrder = _advancedSlots.Select(slot => slot.SelectedKey).ToList(),
                AdvancedSaveMode = active.AdvancedSaveMode,
                UseDefaultOutputDir = active.UseDefaultOutputDir,
                OutputDir = active.OutputDir,
                UseDefaultListName = active.UseDefaultListName,
                DefaultListName = active.DefaultListName,
            };
        }

        // Aplica separador/capitalização/ordenação/lista avançada de um perfil à UI principal —
        // nunca toca em InputTextBox/OutputTextBox/JsonTextBox (a lista e os resultados não são
        // parte de nenhum perfil). Também invalida o resultado atual, já que estas opções
        // afetam o processamento (mesma regra de MarkInputStale já usada pelos controles
        // individuais).
        private void ApplyWorkProfileProcessingOptionsToUi(WorkProfileSettings settings)
        {
            _isApplyingWorkProfile = true;
            try
            {
                TxtSeparator.Text = string.IsNullOrEmpty(settings.DefaultInputSeparator) ? "," : settings.DefaultInputSeparator;
                CmbCaseMode.SelectedItem = CaseModeToLabel(settings.DefaultCaseMode);
                CmbSortMode.SelectedItem = settings.EditorSortMode;
                ChkAdvancedList.IsChecked = settings.UseAdvancedJsonPieceMapping;
                RefreshAdvancedSlots();

                var order = settings.AdvancedJsonPieceOrder;
                for (var i = 0; i < _advancedSlots.Count; i++)
                    _advancedSlots[i].SelectedKey = i < order.Count ? order[i] : "";
            }
            finally
            {
                _isApplyingWorkProfile = false;
            }

            MarkInputStale();
        }

        private void OnWorkProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isApplyingWorkProfile) return;

            var newProfile = CmbWorkProfile.SelectedItem as WorkProfile;
            if (newProfile == null) return;

            var previousId = _settings.ActiveWorkProfileId;

            if (_hasUnsavedWorkProfileChanges)
            {
                var choice = MessageBox.Show(
                    "Existem alterações não salvas no perfil atual.\n\nClique em Sim para salvar, Não para descartar ou Cancelar para continuar no perfil atual.",
                    "ListForge Integrado",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Cancel)
                {
                    RevertWorkProfileSelection(previousId);
                    return;
                }

                if (choice == MessageBoxResult.Yes && !TrySaveActiveWorkProfile())
                {
                    RevertWorkProfileSelection(previousId);
                    return;
                }

                _hasUnsavedWorkProfileChanges = false;
            }

            _settings.ActiveWorkProfileId = newProfile.Id;
            ApplyWorkProfileProcessingOptionsToUi(newProfile.Settings);
            _hasUnsavedWorkProfileChanges = false;

            // A troca de perfil já foi aplicada em _settings/na tela acima — nunca desfeita
            // silenciosamente só porque a gravação falhou (ver SaveSettingsOrShowError).
            // RefreshWorkProfilesUi sempre roda, para a lista refletir o estado em memória.
            var saved = SaveSettingsOrShowError();
            RefreshWorkProfilesUi();
            SetStatus(saved
                ? $"Perfil aplicado: {newProfile.Name}"
                : $"Perfil aplicado: {newProfile.Name} (NÃO salvo — veja o erro exibido).");
        }

        private void RevertWorkProfileSelection(string previousId)
        {
            _isApplyingWorkProfile = true;
            try
            {
                CmbWorkProfile.SelectedItem = _workProfiles.FirstOrDefault(p => string.Equals(p.Id, previousId, StringComparison.Ordinal));
            }
            finally
            {
                _isApplyingWorkProfile = false;
            }
        }

        private void OnCreateWorkProfile(object sender, RoutedEventArgs e)
        {
            var name = PromptForText("Nome do novo perfil:", "");
            if (name == null) return;

            var result = _workProfileService.CreateProfile(_settings, name, CaptureWorkProfileProcessingOptionsFromUi());
            if (!result.Success || result.Value == null)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // O perfil novo já existe em _settings.WorkProfiles (CreateProfile já mutou o
            // objeto) — nunca escondido da interface só porque a gravação falhou.
            var saved = SaveSettingsOrShowError();
            _hasUnsavedWorkProfileChanges = false;
            RefreshWorkProfilesUi();
            SetStatus(saved
                ? $"Perfil criado: {result.Value.Name}"
                : $"Perfil criado: {result.Value.Name} (NÃO salvo — veja o erro exibido).");
        }

        private void OnSaveWorkProfile(object sender, RoutedEventArgs e)
        {
            SetStatus(TrySaveActiveWorkProfile()
                ? "Alterações salvas no perfil."
                : "Alterações aplicadas ao perfil, mas NÃO salvas em disco — veja o erro exibido.");
        }

        /// <summary>Retorna se a gravação em disco teve sucesso — mas a edição em si (capturada
        /// de <see cref="CaptureWorkProfileProcessingOptionsFromUi"/>) já foi aplicada ao perfil
        /// ativo em <see cref="_settings"/> antes da tentativa de salvar, e continua lá mesmo
        /// quando este método retorna false (nunca descartada silenciosamente).</summary>
        private bool TrySaveActiveWorkProfile()
        {
            var result = _workProfileService.SaveActiveProfile(_settings, CaptureWorkProfileProcessingOptionsFromUi());
            if (!result.Success)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var saved = SaveSettingsOrShowError();
            if (saved)
                _hasUnsavedWorkProfileChanges = false;
            RefreshWorkProfilesUi();
            return saved;
        }

        private void OnDiscardWorkProfileChanges(object sender, RoutedEventArgs e)
        {
            var active = _workProfileService.GetActiveProfile(_settings);
            if (active == null) return;

            ApplyWorkProfileProcessingOptionsToUi(active.Settings);
            _hasUnsavedWorkProfileChanges = false;
            RefreshWorkProfilesUi();
            SetStatus("Alterações do perfil descartadas.");
        }

        private void OnRenameWorkProfile(object sender, RoutedEventArgs e)
        {
            var active = CmbWorkProfile.SelectedItem as WorkProfile;
            if (active == null) return;

            var name = PromptForText("Novo nome do perfil:", active.Name);
            if (name == null) return;

            var result = _workProfileService.RenameProfile(_settings, active.Id, name);
            if (!result.Success)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saved = SaveSettingsOrShowError();
            RefreshWorkProfilesUi();
            SetStatus(saved ? "Perfil renomeado." : "Perfil renomeado, mas NÃO salvo em disco — veja o erro exibido.");
        }

        private void OnDuplicateWorkProfile(object sender, RoutedEventArgs e)
        {
            var active = CmbWorkProfile.SelectedItem as WorkProfile;
            if (active == null) return;

            var result = _workProfileService.DuplicateProfile(_settings, active.Id);
            if (!result.Success || result.Value == null)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // DuplicateProfile já torna a cópia o perfil ativo — aplica as opções dela (iguais
            // às do original, mas em um perfil independente) na UI principal, mesmo se a
            // gravação em disco falhar a seguir (nunca escondida da interface).
            ApplyWorkProfileProcessingOptionsToUi(result.Value.Settings);
            _hasUnsavedWorkProfileChanges = false;
            var saved = SaveSettingsOrShowError();
            RefreshWorkProfilesUi();
            SetStatus(saved
                ? $"Perfil duplicado: {result.Value.Name}"
                : $"Perfil duplicado: {result.Value.Name} (NÃO salvo — veja o erro exibido).");
        }

        private void OnDeleteWorkProfile(object sender, RoutedEventArgs e)
        {
            var active = CmbWorkProfile.SelectedItem as WorkProfile;
            if (active == null) return;

            if (MessageBox.Show($"Deseja excluir o perfil \"{active.Name}\"?", "ListForge Integrado", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var result = _workProfileService.DeleteProfile(_settings, active.Id);
            if (!result.Success)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newActive = _workProfileService.GetActiveProfile(_settings);
            ApplyWorkProfileProcessingOptionsToUi(newActive?.Settings ?? WorkProfileService.CaptureOfficialDefaultSettings());
            _hasUnsavedWorkProfileChanges = false;
            var saved = SaveSettingsOrShowError();
            RefreshWorkProfilesUi();
            SetStatus(saved ? "Perfil excluído." : "Perfil excluído da tela, mas NÃO salvo em disco — veja o erro exibido.");
        }

        private void OnRestoreDefaultWorkProfile(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Restaurar o perfil Padrão para as configurações oficiais?\n\nIsso não afeta nenhum outro perfil.",
                    "ListForge Integrado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var result = _workProfileService.RestoreDefaultProfile(_settings);
            if (!result.Success)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var active = _workProfileService.GetActiveProfile(_settings);
            ApplyWorkProfileProcessingOptionsToUi(active?.Settings ?? WorkProfileService.CaptureOfficialDefaultSettings());
            _hasUnsavedWorkProfileChanges = false;
            var saved = SaveSettingsOrShowError();
            RefreshWorkProfilesUi();
            SetStatus(saved ? "Perfil Padrão restaurado." : "Perfil Padrão restaurado na tela, mas NÃO salvo em disco — veja o erro exibido.");
        }

        /// <summary>
        /// Nunca lança: se a gravação falhar, a alteração já aplicada em <see cref="_settings"/>
        /// (perfil criado/renomeado/duplicado/excluído, opção alterada etc.) permanece em
        /// memória — quem chama é responsável por continuar refletindo isso na interface (ex.:
        /// chamando RefreshWorkProfilesUi mesmo quando este método retorna false) em vez de
        /// descartar silenciosamente o que o usuário já fez. A mensagem deixa explícito que a
        /// mudança NÃO foi persistida, para nunca ser confundida com "perfil salvo" ao reabrir.
        /// </summary>
        private bool SaveSettingsOrShowError()
        {
            try
            {
                Integrations.ListForgeIntegratedSettingsStore.Save(_settings);
                return true;
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("WorkProfileSettings", "Falha ao salvar perfis de trabalho.", ex, Integrations.ListForgeIntegratedSettingsStore.FilePathForDisplay);
                MessageBox.Show(
                    $"Não foi possível salvar os perfis de trabalho: {ex.Message}\n\n" +
                    "A alteração que você acabou de fazer continua visível nesta tela, mas NÃO foi salva em disco — " +
                    "ela será perdida se o PX Tool Box for fechado antes de uma gravação bem-sucedida.",
                    "ListForge Integrado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        // InputDialog próprio não existe neste host (o do standalone, ListForge.UI.Views.
        // InputDialog, é uma Window do projeto standalone, fora do núcleo compartilhado) — usa
        // o mesmo mecanismo simples já aceitável para o resto desta janela (Microsoft.VisualBasic
        // não está disponível em net48 WPF sem referência extra; em vez disso, uma pequena
        // InputBox própria, com o mesmo roteador de teclado das demais janelas).
        private string? PromptForText(string label, string initialValue)
        {
            var dialog = new WorkProfileNameDialog(label, initialValue) { Owner = this };
            return dialog.ShowDialog() == true ? dialog.ResultText : null;
        }

        private static string CaseModeToLabel(string mode) => mode switch
        {
            "upper" => "Tudo maiúsculo",
            "lower" => "Tudo minúsculo",
            _ => "Original",
        };

        // =================================================================================
        // Seção 1 — teclado e foco
        //
        // Causa do CONFLITO original: esta janela corre no mesmo thread/Dispatcher da UI do
        // CorelDRAW (o host "wpfhost" do Corel carrega o docker principal nesse mesmo thread —
        // ver docs/ARCHITECTURE.md, seção 3). O loop nativo do Corel chama
        // TranslateAccelerator(hWndCorel, hAccel, &msg) para TODA mensagem retirada da fila do
        // thread — e, pela própria documentação da API, TranslateAccelerator decide só pelo
        // código de tecla/estado do teclado em "msg", NÃO verifica se "msg.hwnd" pertence à
        // janela do Corel ou a uma janela descendente dela. Por isso, mesmo esta janela sendo
        // uma Window própria e separada (não um child HWND do Corel), uma tecla que combina com
        // a tabela de aceleradores do Corel é consumida por TranslateAccelerator antes que
        // TranslateMessage/DispatchMessage rodem para ela — por isso o caractere nunca chega à
        // caixa de texto, só o comando da ferramenta é executado.
        //
        // Causa de a 1ª CORREÇÃO não ter funcionado: ela assinava
        // ComponentDispatcher.ThreadFilterMessage — só dispara de DENTRO do loop de mensagens do
        // Dispatcher do WPF, que este add-on nunca chama (é o loop nativo do Corel que bombeia
        // as mensagens deste thread).
        //
        // Causa de a 2ª CORREÇÃO (hook WH_GETMESSAGE) não ter funcionado, segundo o usuário: o
        // diagnóstico mostrou 0 teclas observadas E 0 roteadas — ou seja, o hook em si nunca foi
        // atingido nem uma vez. Essa versão anterior nunca verificava se SetWindowsHookEx teve
        // sucesso (nem registrava o erro nativo em caso de falha), e nunca confirmava que esta
        // janela de fato recebia o foco nativo do Win32 (GetFocus()) — só o foco lógico do WPF.
        // Duas causas candidatas foram tratadas na correção desta rodada (ver
        // ThreadKeyboardRouter, que concentra agora toda a mecânica, compartilhada com a janela
        // mínima de diagnóstico KeyboardDiagMiniWindow para isolamento):
        //   1. hMod inválido: Marshal.GetHINSTANCE(...) sobre um assembly carregado por reflexão
        //      pelo host "wpfhost" do Corel (não via LoadLibrary a partir de um arquivo em
        //      disco) pode retornar um handle que SetWindowsHookEx recusa — trocado para
        //      IntPtr.Zero, valor documentado como válido para hook de THREAD quando a thread de
        //      dwThreadId pertence ao processo atual (nosso caso, já que o add-on roda dentro do
        //      processo do Corel).
        //   2. Falta de foco nativo: Window.Show() nesta Window não garante GetFocus() apontando
        //      para o HWND desta janela — se continuar apontando para a janela do Corel, toda
        //      WM_KEYDOWN chega ao hook com msg.hwnd do Corel, nunca desta janela, e forçar
        //      TranslateMessage/DispatchMessage sobre essa mensagem entregaria ao WndProc do
        //      Corel, não ao nosso editor (exatamente o sintoma relatado: nada digitado).
        //      ThreadKeyboardRouter.Install()/ReassertFocus() chamam Activate()+SetFocus()
        //      nativamente, e Window_Activated chama ReassertFocus() de novo a cada ativação
        //      (o Corel pode retomar o foco sozinho entre uma ativação e outra).
        // O contador abaixo (KeyboardDiagText) agora expõe cada elo da cadeia separadamente —
        // sucesso/erro da instalação do hook, hwnd próprio, foco nativo após Activate(), teclas
        // vistas na thread (com qualquer hwnd) vs. só as desta janela vs. roteadas — para nunca
        // tratar "zero eventos" como prova automática de uma causa só dentro do Corel real.
        //
        // Mecanismo (inalterado em sua lógica, agora em ThreadKeyboardRouter): hook de THREAD
        // (restrito ao id da própria thread de UI, dentro do próprio processo), NÃO um hook
        // global. WH_GETMESSAGE é chamado pelo sistema operacional dentro de GetMessage/
        // PeekMessage, então dispara independente de QUEM chama GetMessage — inclusive o loop
        // nativo do Corel —, permitindo agir ANTES de TranslateAccelerator: (1) só quando a
        // mensagem é WM_KEYDOWN com hwnd desta janela, a janela está ativa, o foco lógico do WPF
        // está num TextBox/ComboBox, e a tecla não está na lista de exclusão; (2) chamamos
        // TranslateMessage+DispatchMessage NÓS MESMOS sobre uma cópia, gerando o WM_CHAR e
        // entregando ao WndProc desta janela; (3) zeramos a mensagem original (WM_NULL) antes de
        // devolvê-la, para o loop do Corel não processá-la de novo. Isso não desativa nem remove
        // nenhum atalho do Corel: eles continuam funcionando normalmente assim que o foco sai de
        // um campo editável desta janela. O hook é instalado em Window_Loaded e removido em
        // Window_Closed — vive só enquanto esta janela existe.
        // =================================================================================

        private readonly ThreadKeyboardRouter _keyboardRouter;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _keyboardRouter.Install();
            InputTextBox.Focus();
            UpdateKeyboardDiagText();

            // Configuração corrompida/ilegível na abertura — avisa já na barra de status (além
            // de aparecer de novo, uma vez, se o usuário abrir "Configurações..."). Nunca
            // silenciosa: _settingsLoadNotice só vem preenchido quando o arquivo existia e algo
            // deu errado nele (ver ListForgeIntegratedSettingsStore.Load).
            if (!string.IsNullOrEmpty(_settingsLoadNotice))
                SetStatus(_settingsLoadNotice);
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            _keyboardRouter.ReassertFocus();
            UpdateKeyboardDiagText();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            // Remove o hook — sem isso, cada abertura da janela (ControlUI mantém um botão que
            // reabre uma nova instância se a anterior foi fechada) acumularia mais um hook vivo
            // na mesma thread de UI do Corel.
            _keyboardRouter.Uninstall();
            _linkListImportService.Dispose();
            Integrations.PxLogger.Info("Shutdown", "ListForge Integrado encerrado.");
        }

        private void OnOpenKeyboardDiagMiniWindow(object sender, RoutedEventArgs e)
        {
            // Janela de diagnóstico TEMPORÁRIA, pedida para isolar o mecanismo de interceptação
            // do resto da UI do modo Clássico (ver comentário em KeyboardDiagMiniWindow.xaml).
            var mini = new KeyboardDiagMiniWindow { Owner = this };
            mini.Show();
        }

        private void UpdateKeyboardDiagText()
        {
            KeyboardDiagText.Text = "Teclado (hook de thread, não global) — diagnóstico detalhado:\n"
                + _keyboardRouter.DescribeState();
        }

        // =================================================================================
        // Seção 4 — texto técnico: a área principal mostra só o estado de acesso necessário ao
        // usuário; a explicação interna (licenciamento pendente, referência de arquitetura) vai
        // só para o Diagnóstico.
        // =================================================================================
        private void UpdateAccessStatusText()
        {
            AccessStatusText.Text = _licenseService.CanProcess
                ? "Edição: Integrado (DEV) — processamento disponível."
                : "Edição: Integrado (DEV) — processamento bloqueado.";
            DiagnosticsText.Text =
                $"Licenciamento: {_licenseService.Edition}{_licenseService.ProcessingStatusSuffix}\n" +
                "Núcleo de processamento compartilhado: ListForge.Core (ver docs/ARCHITECTURE.md, seção 10).";
        }

        // Texto de "Copiar informações de suporte" — mesmas versões/caminhos do pacote de
        // suporte (Integrations.SupportPackageService), nunca dado de lista.
        private void UpdateSupportInfoText()
        {
            SupportInfoText.Text =
                $"PX Tool Box DEV: {Integrations.PxVersionInfo.PxToolBoxVersion}\n" +
                $"Núcleo ListForge (ListForge.Core): {Integrations.PxVersionInfo.ListForgeCoreVersion}\n" +
                $"CorelDRAW: {_corelVersion ?? "não disponível"}\n" +
                $"Runtime: {Integrations.PxVersionInfo.RuntimeDescription}\n" +
                $"Pasta de configuração: {Integrations.ListForgeIntegratedSettingsStore.ConfigDirectoryForDisplay}\n" +
                $"Pasta de logs: {Integrations.PxLogger.LogDirectory} (retenção: máx. {Integrations.PxLogger.MaxLogAgeDays} dias / {Integrations.PxLogger.MaxTotalLogBytes} bytes)";
        }

        private void OnCopySupportInfo(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(SupportInfoText.Text);
                SetStatus("Informações de suporte copiadas.");
            }
            catch (Exception ex)
            {
                // Nunca afirma sucesso se a área de transferência falhar (ex.: outro processo
                // com o clipboard aberto) — mesma regra já aplicada pelo inspetor Shape.Properties.
                SetStatus($"Não foi possível copiar: {ex.Message}");
            }
        }

        private void OnOpenLogsFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Integrations.PxLogger.LogDirectory);
                System.Diagnostics.Process.Start("explorer.exe", Integrations.PxLogger.LogDirectory);
            }
            catch (Exception ex)
            {
                SetStatus($"Não foi possível abrir a pasta de logs: {ex.Message}");
            }
        }

        private void OnOpenBackupsFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Integrations.ListForgeInputBackupService.BackupDirectory);
                System.Diagnostics.Process.Start("explorer.exe", Integrations.ListForgeInputBackupService.BackupDirectory);
            }
            catch (Exception ex)
            {
                SetStatus($"Não foi possível abrir a pasta de backups: {ex.Message}");
            }
        }

        // Mostra o conteúdo antes de perguntar o destino (pedido explícito da tarefa) — nunca
        // envia nada automaticamente; só gera o arquivo onde o usuário escolher.
        private void OnGenerateSupportPackage(object sender, RoutedEventArgs e)
        {
            var choice = MessageBox.Show(
                "O pacote de suporte incluirá:\n\n" +
                "• Versões do PX Tool Box DEV, do núcleo ListForge e do CorelDRAW (quando disponível), runtime/arquitetura;\n" +
                "• Um resumo seguro das configurações (tamanho de fonte, opções de processamento do perfil ativo, tamanhos customizados) — sem nomes de perfil, pasta ou nome de lista configurados;\n" +
                "• Os logs recentes deste módulo (dentro de um limite de arquivos/tamanho).\n\n" +
                "NUNCA inclui: a lista digitada, nomes/números de jogadores, área de transferência, credenciais, documentos do Corel ou arquivos de produção.\n\n" +
                "Continuar e escolher onde salvar?",
                "ListForge Integrado — gerar pacote de suporte",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (choice != MessageBoxResult.Yes)
            {
                SetStatus("Geração do pacote de suporte cancelada.");
                return;
            }

            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Escolha onde salvar o pacote de suporte",
            };
            var ownerHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (dialog.ShowDialog(new Win32WindowWrapper(ownerHandle)) != System.Windows.Forms.DialogResult.OK)
            {
                SetStatus("Geração do pacote de suporte cancelada.");
                return;
            }

            var result = _supportPackageService.Generate(dialog.SelectedPath, _settings, _corelVersion);
            if (!result.Success || result.Value == null)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Falha ao gerar pacote de suporte.");
                return;
            }

            MessageBox.Show($"Pacote de suporte gerado:\n{result.Value}\n\n{result.UserMessage}",
                "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Information);
            SetStatus($"Pacote de suporte gerado: {System.IO.Path.GetFileName(result.Value)}");
        }

        private void OnResetSeparator(object sender, RoutedEventArgs e) => TxtSeparator.Text = ",";

        private void OnInputTextChanged(object sender, TextChangedEventArgs e)
        {
            _hasUnsavedChanges = true;
            MarkInputStale();
            RefreshAdvancedSlots();
        }

        // Opções de processamento (separador/capitalização/ordenação) também invalidam a saída
        // atual — o resultado depende delas tanto quanto do texto de entrada (ver
        // ProcessingWorkflowRequest em OnProcess). Dois overloads porque TextChanged e
        // SelectionChanged são delegates distintos (TextChangedEventHandler/
        // SelectionChangedEventHandler) mesmo os dois derivando de RoutedEventArgs. Só o
        // separador (TextChanged, de TxtSeparator) também afeta quantos slots a lista avançada
        // precisa (EstimateRequiredSlots usa o separador para repartir a linha) — capitalização/
        // ordenação (SelectionChanged) não mudam a contagem de tamanhos por linha.
        private void OnProcessingOptionChanged(object sender, TextChangedEventArgs e)
        {
            MarkInputStale();
            RefreshAdvancedSlots();
            MarkWorkProfileChanged();
        }

        private void OnProcessingOptionChanged(object sender, SelectionChangedEventArgs e)
        {
            MarkInputStale();
            MarkWorkProfileChanged();
        }

        // Seletor de tipo de peça de um slot individual da lista avançada (AdvancedSlotsList) —
        // também compõe AdvancedJsonPieceOrder do perfil ativo, então precisa do mesmo par
        // MarkInputStale/MarkWorkProfileChanged dos demais controles de opção.
        private void OnAdvancedSlotChanged(object sender, SelectionChangedEventArgs e)
        {
            MarkInputStale();
            MarkWorkProfileChanged();
        }

        /// <summary>
        /// Bloqueio de resultado desatualizado (ver docs/listforge-classic-parity.md, grupo
        /// "Modos e opções de processamento" — equivalente ao par _inputRevision/
        /// _comparisonInputRevision do ListForge standalone). Só marca a saída como obsoleta;
        /// não processa nem bloqueia nada por si só — isso é feito por <see cref="ConfirmIfStale"/>
        /// nos botões que copiam/exportam.
        /// </summary>
        private void MarkInputStale()
        {
            _freshness.MarkInputChanged();
            UpdateOutputStaleWarning();
        }

        private void UpdateOutputStaleWarning()
        {
            // Causa real do crash do CorelDRAW ao abrir esta janela (confirmado pelo usuário,
            // reproduzia sempre): TxtSeparator.Text="," no XAML é o ÚNICO elemento desta janela
            // com valor inicial + handler da mesma propriedade (TextChanged="OnProcessingOptionChanged").
            // Esse valor inicial dispara TextChanged DURANTE a própria construção da árvore de
            // elementos do InitializeComponent — MUITO antes de OutputStaleWarning (declarado no
            // fim do XAML, dentro do painel de Saída) ser conectado ao campo. Sem esta guarda,
            // essa NullReferenceException não era capturada por nada no caminho (o host "wpfhost"
            // do Corel invoca o construtor via reflexão, na própria thread de UI nativa do Corel,
            // durante o tratamento do clique que abre a janela) — o resultado observado não foi
            // uma exceção .NET visível, foi o próprio CorelDRAW.exe encerrando (WER: APPCRASH,
            // 0xc0000005 dentro de VGCore.dll). RefreshAdvancedSlots() já tinha a mesma guarda por
            // este mesmo motivo; faltava aqui.
            if (OutputStaleWarning == null) return;

            OutputStaleWarning.Visibility = _freshness.IsStale && !string.IsNullOrEmpty(_lastOutputText)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Antes de copiar/salvar/gerar a partir de um resultado possivelmente desatualizado,
        /// avisa explicitamente e pede confirmação — nunca entrega o resultado antigo em
        /// silêncio (pedido explícito: "a interface não pode exportar silenciosamente uma saída
        /// anterior como se correspondesse aos dados atuais"). Não impede reprocessar: só exige
        /// uma decisão explícita do usuário para continuar com o resultado antigo.
        /// </summary>
        private bool ConfirmIfStale(string action)
        {
            if (!_freshness.IsStale) return true;

            var choice = MessageBox.Show(
                $"A entrada ou as opções de processamento mudaram desde o último \"Processar\" — {action} usaria um resultado desatualizado.\n\n" +
                "Processar de novo antes de continuar (recomendado), ou continuar mesmo assim?",
                "ListForge Integrado — resultado desatualizado",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // Yes = "continuar mesmo assim" (segue com o resultado antigo); No = cancela a ação.
            return choice == MessageBoxResult.Yes;
        }

        // Lista avançada (ver docs/listforge-classic-parity.md, grupo "Modos e opções de
        // processamento") — mapeamento de peça por slot JSON. O serviço
        // (JsonPieceMappingService.EstimateRequiredSlots) e o modelo (JsonPieceMappingOptions)
        // já são compartilhados; só a UI de slots é desta entrega.
        private void OnAdvancedListToggled(object sender, RoutedEventArgs e)
        {
            RefreshAdvancedSlots();
            MarkInputStale();
            MarkWorkProfileChanged();
        }

        private void RefreshAdvancedSlots()
        {
            // Guarda defensiva: TxtSeparator.Text=","  (valor inicial no XAML) dispara
            // OnProcessingOptionChanged/RefreshAdvancedSlots durante a própria construção da
            // árvore de elementos do InitializeComponent, antes de ChkAdvancedList/
            // AdvancedSlotsList (declarados mais abaixo no XAML) estarem conectados aos campos
            // — sem isso, a primeira digitação do separador na inicialização lançaria
            // NullReferenceException.
            if (ChkAdvancedList == null || AdvancedSlotsList == null) return;

            if (ChkAdvancedList.IsChecked != true)
            {
                AdvancedSlotsList.Visibility = Visibility.Collapsed;
                return;
            }

            AdvancedSlotsList.Visibility = Visibility.Visible;

            var required = JsonPieceMappingService.ClampSlotCount(
                string.IsNullOrWhiteSpace(InputTextBox.Text)
                    ? Math.Max(1, _advancedSlots.Count)
                    : _pieceMappingService.EstimateRequiredSlots(InputTextBox.Text, TxtSeparator.Text, _sizeConfig));

            while (_advancedSlots.Count > required)
                _advancedSlots.RemoveAt(_advancedSlots.Count - 1);

            for (var i = _advancedSlots.Count; i < required; i++)
                _advancedSlots.Add(new AdvancedPieceSlot { Position = i + 1 });
        }

        private static string CaseModeFromLabel(string? label) => label switch
        {
            "Tudo maiúsculo" => "upper",
            "Tudo minúsculo" => "lower",
            _ => "original",
        };

        private static ListSortMode SortModeFromLabel(string? label) => label switch
        {
            "Crescente" => ListSortMode.Ascending,
            "Decrescente" => ListSortMode.Descending,
            _ => ListSortMode.Original,
        };

        // Montagem da request compartilhada entre "Processar rápido" (OnProcess) e "Processar"
        // com prévia (OnProcessWithPreview) — mesmos campos de entrada/opções nos dois, só o
        // ConsumeTrialCredit e o que cada fluxo faz com o resultado mudam.
        private ProcessingWorkflowRequest BuildProcessingRequest(bool consumeTrialCredit) =>
            BuildProcessingRequest(consumeTrialCredit, InputTextBox.Text);

        // Sobrecarga usada por ApplyOutputEdits: mesmas opções de processamento (separador/
        // tamanhos/capitalização/ordenação/mapeamento de peça) da requisição normal, mas com o
        // conteúdo editado (saída ou JSON convertido para texto de lista) como entrada — nunca
        // construída com `with` sobre o record (evita exigir IsExternalInit no consumidor net48;
        // ListForgeIntegratedTests já cobre esse tipo de incompatibilidade em outras classes).
        private ProcessingWorkflowRequest BuildProcessingRequest(bool consumeTrialCredit, string inputText)
        {
            var pieceMapping = ChkAdvancedList.IsChecked == true
                ? new JsonPieceMappingOptions(true, _advancedSlots.Select(slot => slot.SelectedKey).ToList())
                : JsonPieceMappingOptions.Disabled;

            return new ProcessingWorkflowRequest(
                inputText,
                TxtSeparator.Text,
                _sizeConfig,
                CaseModeFromLabel(CmbCaseMode.SelectedItem as string),
                SortModeFromLabel(CmbSortMode.SelectedItem as string),
                pieceMapping,
                consumeTrialCredit);
        }

        private async void OnProcess(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            _isProcessing = true;
            BtnProcess.IsEnabled = false;
            BtnProcessWithPreview.IsEnabled = false;

            // Nunca descarta silenciosamente uma edição manual pendente ao reprocessar — porta
            // de MainViewModel.EnsureNoPendingOutputOrJsonEdits do standalone, chamado antes de
            // montar a requisição, igual lá.
            if (!EnsureNoPendingOutputOrJsonEdits())
            {
                SetStatus("Processamento cancelado — aplique ou descarte as alterações pendentes antes de reprocessar.");
                _isProcessing = false;
                BtnProcess.IsEnabled = true;
                BtnProcessWithPreview.IsEnabled = true;
                return;
            }

            SetStatus("Processando...");

            var request = BuildProcessingRequest(consumeTrialCredit: true);

            try
            {
                var result = await Task.Run(() => _workflow.Execute(request));
                ApplyResult(result, request);
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("Process", "Falha inesperada ao processar.", ex);
                SetStatus($"Falha inesperada ao processar: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                BtnProcess.IsEnabled = true;
                BtnProcessWithPreview.IsEnabled = true;
                UpdateAccessStatusText();
            }
        }

        // "Processar" com prévia (ver docs/listforge-classic-parity.md, grupo "Modos e opções de
        // processamento") — porta de MainViewModel.AnalyzeAndProcessWithPreview do standalone:
        // Analyze() (sem consumir crédito) → mostra ProcessingPreviewWindow → só chama
        // ExecuteConfirmed() (que processa de verdade, consumindo crédito) se o usuário confirmar
        // na janela. "Processar rápido" (OnProcess, acima) continua disponível para quem não quer
        // ver a prévia a cada vez — mesmas duas opções do standalone (ProcessCommand vs.
        // ProcessQuickCommand).
        private async void OnProcessWithPreview(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;
            _isProcessing = true;
            BtnProcess.IsEnabled = false;
            BtnProcessWithPreview.IsEnabled = false;

            if (!EnsureNoPendingOutputOrJsonEdits())
            {
                SetStatus("Processamento cancelado — aplique ou descarte as alterações pendentes antes de reprocessar.");
                _isProcessing = false;
                BtnProcess.IsEnabled = true;
                BtnProcessWithPreview.IsEnabled = true;
                return;
            }

            SetStatus("Analisando...");

            var snapshot = new ProcessingPreviewSnapshot(
                BuildProcessingRequest(consumeTrialCredit: false),
                ActiveWorkProfileName: (CmbWorkProfile.SelectedItem as WorkProfile)?.Name ?? WorkProfileService.DefaultProfileName,
                HasUnsavedWorkProfileChanges: _hasUnsavedWorkProfileChanges,
                AdvancedListEnabled: ChkAdvancedList.IsChecked == true,
                OutputDirectoryDescription: "escolhida ao salvar",
                OutputFileDescription: "escolhido ao salvar");

            try
            {
                var preview = await Task.Run(() => _previewService.Analyze(snapshot));

                if (preview.AnalysisResult.Status == ProcessingWorkflowStatus.ValidationFailed)
                {
                    IssuesList.ItemsSource = preview.AnalysisResult.ValidationIssues
                        .Select(issue => $"Linha {issue.LineNumber}: {issue.Message}")
                        .ToList();
                    _issueLines = preview.AnalysisResult.ValidationIssues.Select(issue => issue.LineNumber).ToList();
                }
                else
                {
                    // Mesmo comportamento de ClearValidationHighlights() no standalone — sem
                    // isso, avisos de uma análise anterior (entrada já corrigida) ficariam
                    // visíveis atrás da janela de prévia até o usuário confirmar.
                    IssuesList.ItemsSource = null;
                    _issueLines = new();
                }

                SetStatus($"Prévia: {preview.TotalRecords} registro(s), {preview.InvalidRecords} inválido(s), {preview.WarningRecords} aviso(s).");

                if (!ProcessingPreviewWindow.ShowDialog(this, preview))
                {
                    SetStatus("Processamento aguardando confirmação.");
                    return;
                }

                var result = await Task.Run(() => _previewService.ExecuteConfirmed(preview));
                ApplyResult(result, preview.Snapshot.Request);
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("ProcessWithPreview", "Falha inesperada ao analisar/processar.", ex);
                SetStatus($"Falha inesperada ao analisar/processar: {ex.Message}");
            }
            finally
            {
                _isProcessing = false;
                BtnProcess.IsEnabled = true;
                BtnProcessWithPreview.IsEnabled = true;
                UpdateAccessStatusText();
            }
        }

        private void ApplyResult(ProcessingWorkflowResult result, ProcessingWorkflowRequest request)
        {
            IssuesList.ItemsSource = result.ValidationIssues
                .Select(issue => $"Linha {issue.LineNumber}: {issue.Message}")
                .ToList();
            _issueLines = result.ValidationIssues.Select(issue => issue.LineNumber).ToList();

            switch (result.Status)
            {
                case ProcessingWorkflowStatus.EmptyInput:
                    SetStatus("Informe uma lista antes de processar.");
                    SizeSummaryText.Text = "";
                    return;
                case ProcessingWorkflowStatus.ValidationFailed:
                    SetStatus($"Validação encontrou {result.ValidationIssues.Count} problema(s) — corrija antes de exportar.");
                    SizeSummaryText.Text = "";
                    return;
                case ProcessingWorkflowStatus.NoRows:
                    SetStatus("Nenhuma linha reconhecida na lista informada.");
                    SizeSummaryText.Text = "";
                    return;
                case ProcessingWorkflowStatus.TrialLimitReached:
                    SetStatus("Limite de processamentos atingido.");
                    SizeSummaryText.Text = "";
                    return;
            }

            _lastOrders = result.Orders;
            SetGeneratedTexts(result.OutputText, result.JsonPreview);
            _outputWasManuallyEdited = false;
            // SetGeneratedTexts já chamou UpdateManualEditUi(), mas antes de
            // _outputWasManuallyEdited virar false — chama de novo para o indicador de edição
            // manual não ficar visível depois de um reprocessamento normal.
            UpdateManualEditUi();
            _freshness.MarkProcessed();
            UpdateOutputStaleWarning();
            SizeSummaryText.Text = SizeSummaryBuilder.Build(result.Rows, _sizeConfig);
            _lastProcessedRowCount = result.Rows.Count;
            CaptureComparisonSnapshot(request, result);
            SetStatus($"Processado: {result.Rows.Count} linha(s).");
        }

        // Ponto único onde OutputTextBox/JsonTextBox recebem um resultado CANÔNICO (vindo de um
        // processamento com sucesso ou de "Aplicar alterações" bem-sucedido) — nunca chamado com
        // texto editado ainda não validado. _isUpdatingGeneratedText evita que a própria
        // atribuição (que dispara TextChanged) seja lida como uma edição manual do usuário —
        // mesmo padrão do standalone (MainViewModel.SetGeneratedTexts/_isUpdatingGeneratedText).
        private void SetGeneratedTexts(string output, string json)
        {
            _isUpdatingGeneratedText = true;
            try
            {
                OutputTextBox.Text = output;
                JsonTextBox.Text = json;
                _lastOutputText = output;
                _lastValidJsonText = json;
                _hasPendingOutputEdit = false;
                _hasPendingJsonEdit = false;
            }
            finally
            {
                _isUpdatingGeneratedText = false;
            }

            UpdateManualEditUi();
        }

        // Checkbox "Permitir edição da saída e do JSON nesta sessão" (ver comentário dos campos
        // _allowOutputEditing acima) — porta de MainViewModel.AllowOutputEditing (setter)
        // do standalone: desligar com alterações pendentes nunca descarta/ignora silenciosamente
        // — pede Sim (aplica)/Não (descarta)/Cancelar (continua editando), exatamente como lá.
        private void OnAllowOutputEditingToggled(object sender, RoutedEventArgs e)
        {
            if (_isTogglingAllowOutputEditing) return;

            var newValue = ChkAllowOutputEditing.IsChecked == true;
            if (!newValue && (_hasPendingOutputEdit || _hasPendingJsonEdit))
            {
                var choice = MessageBox.Show(
                    "Existem alterações na saída ou no JSON. Clique em Sim para aplicar, Não para descartar ou Cancelar para continuar editando.",
                    "ListForge Integrado",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Cancel)
                {
                    RevertAllowOutputEditingCheckbox(true);
                    return;
                }

                if (choice == MessageBoxResult.Yes && !ApplyOutputEdits())
                {
                    RevertAllowOutputEditingCheckbox(true);
                    return;
                }

                if (choice == MessageBoxResult.No)
                    DiscardOutputEdits();
            }

            _allowOutputEditing = newValue;
            OutputTextBox.IsReadOnly = !_allowOutputEditing;
            JsonTextBox.IsReadOnly = !_allowOutputEditing;
            UpdateManualEditUi();
        }

        private void RevertAllowOutputEditingCheckbox(bool value)
        {
            _isTogglingAllowOutputEditing = true;
            try { ChkAllowOutputEditing.IsChecked = value; }
            finally { _isTogglingAllowOutputEditing = false; }
        }

        // Rastreiam edição manual — só contam como edição de verdade quando a edição está
        // habilitada e a mudança não veio de SetGeneratedTexts (_isUpdatingGeneratedText). Tocar
        // numa caixa zera o "pendente" da outra: só uma fonte (saída OU JSON) é usada por
        // "Aplicar alterações" por vez — mesma regra exata do standalone (OutputText/JsonText,
        // setters).
        private void OnOutputTextManuallyEdited(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingGeneratedText || !_allowOutputEditing) return;

            _hasPendingOutputEdit = !string.Equals(OutputTextBox.Text, _lastOutputText, StringComparison.Ordinal);
            if (_hasPendingOutputEdit)
                _hasPendingJsonEdit = false;
            UpdateManualEditUi();
        }

        private void OnJsonTextManuallyEdited(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingGeneratedText || !_allowOutputEditing) return;

            _hasPendingJsonEdit = !string.Equals(JsonTextBox.Text, _lastValidJsonText, StringComparison.Ordinal);
            if (_hasPendingJsonEdit)
                _hasPendingOutputEdit = false;
            UpdateManualEditUi();
        }

        private void UpdateManualEditUi()
        {
            var hasPending = _hasPendingOutputEdit || _hasPendingJsonEdit;
            BtnApplyOutputEdits.IsEnabled = _allowOutputEditing && hasPending;
            BtnDiscardOutputEdits.IsEnabled = _allowOutputEditing && hasPending;

            if (hasPending)
            {
                ManualEditIndicator.Text = "Alterações manuais pendentes — clique em \"Aplicar alterações\" " +
                    "para validar e usar, ou \"Descartar alterações\" para voltar ao último resultado válido.";
                ManualEditIndicator.Visibility = Visibility.Visible;
            }
            else if (_outputWasManuallyEdited)
            {
                ManualEditIndicator.Text = "Resultado editado manualmente (alterações já aplicadas e revalidadas).";
                ManualEditIndicator.Visibility = Visibility.Visible;
            }
            else
            {
                ManualEditIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void OnApplyOutputEdits(object sender, RoutedEventArgs e) => ApplyOutputEdits();

        private void OnDiscardOutputEdits(object sender, RoutedEventArgs e) => DiscardOutputEdits();

        // "Aplicar alterações" (ver docs/listforge-classic-parity.md) — porta de
        // MainViewModel.ApplyOutputEdits do standalone: o texto editado (saída OU JSON, nunca os
        // dois) é tratado como uma nova ENTRADA e passa pelo MESMO ProcessingWorkflowService do
        // resto da janela — nunca aceita a edição "como está"; se a validação falhar, o texto
        // editado permanece intacto na tela (nunca é apagado) e uma mensagem explica o problema.
        // Quando a edição é do JSON, ListProcessor.ExtractListTextFromJsonData (núcleo
        // compartilhado) converte o JSON de volta para texto de lista antes de reprocessar —
        // mesma função usada pelo standalone, nenhuma reimplementação.
        private bool ApplyOutputEdits()
        {
            if (!_hasPendingOutputEdit && !_hasPendingJsonEdit)
                return true;

            var wasJsonEdit = _hasPendingJsonEdit;
            try
            {
                var input = wasJsonEdit
                    ? ListProcessor.ExtractListTextFromJsonData(
                        Newtonsoft.Json.Linq.JObject.Parse(JsonTextBox.Text), TxtSeparator.Text, includeHeader: true)
                    : OutputTextBox.Text;

                var request = BuildProcessingRequest(consumeTrialCredit: false, input);
                var result = _workflow.Execute(request);

                if (result.Status == ProcessingWorkflowStatus.ValidationFailed)
                {
                    var issue = result.ValidationIssues.FirstOrDefault();
                    var message = wasJsonEdit
                        ? "O JSON contém erros e não pôde ser aplicado."
                        : issue != null
                            ? $"A lista de saída contém dados inválidos na linha {issue.LineNumber}."
                            : "A lista de saída contém dados inválidos.";
                    SetStatus(message);
                    MessageBox.Show(message, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (result.Status != ProcessingWorkflowStatus.Success)
                {
                    var message = wasJsonEdit
                        ? "O JSON contém erros e não pôde ser aplicado."
                        : "A lista de saída não pôde ser aplicada.";
                    SetStatus(message);
                    MessageBox.Show(message, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                var existingComparisonSnapshot = _comparisonSnapshot;
                var canUpdateComparisonSnapshot = existingComparisonSnapshot != null
                    && !_freshness.IsStale
                    && string.Equals(InputTextBox.Text, existingComparisonSnapshot.InputText, StringComparison.Ordinal);

                _lastOrders = result.Orders;
                SetGeneratedTexts(result.OutputText, result.JsonPreview);
                _outputWasManuallyEdited = true;
                _currentSourceType = wasJsonEdit
                    ? Integrations.ListForgeProcessingHistorySourceTypes.EditedJson
                    : Integrations.ListForgeProcessingHistorySourceTypes.EditedOutput;
                // SetGeneratedTexts já chamou UpdateManualEditUi(), mas antes de
                // _outputWasManuallyEdited virar true — chama de novo para o indicador refletir
                // "resultado editado manualmente" imediatamente.
                UpdateManualEditUi();
                SizeSummaryText.Text = SizeSummaryBuilder.Build(result.Rows, _sizeConfig);
                _lastProcessedRowCount = result.Rows.Count;

                if (canUpdateComparisonSnapshot)
                {
                    try
                    {
                        _comparisonSnapshot = _comparisonService.UpdateOutput(existingComparisonSnapshot!, result, outputWasManuallyEdited: true);
                    }
                    catch (Exception)
                    {
                        _comparisonSnapshot = null;
                    }
                }
                else
                {
                    _comparisonSnapshot = null;
                }

                SetStatus("Alterações aplicadas.");
                return true;
            }
            catch (Newtonsoft.Json.JsonReaderException ex)
            {
                Integrations.PxLogger.Error("ApplyOutputEdits", "JSON editado possui erro de sintaxe.", ex);
                var message = $"O JSON contém erros e não pôde ser aplicado. Linha {ex.LineNumber}, posição {ex.LinePosition}.";
                SetStatus(message);
                MessageBox.Show(message, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("ApplyOutputEdits", "Falha ao aplicar edição manual da saída/JSON.", ex);
                var message = wasJsonEdit
                    ? "O JSON contém erros e não pôde ser aplicado."
                    : "A lista de saída contém dados inválidos.";
                SetStatus(message);
                MessageBox.Show(message, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        // "Descartar alterações" — porta de MainViewModel.DiscardOutputEdits: volta ao último
        // resultado VÁLIDO (processado ou aplicado), nunca ao que o usuário tinha digitado.
        private void DiscardOutputEdits()
        {
            SetGeneratedTexts(_lastOutputText, _lastValidJsonText);
            SetStatus("Alterações descartadas.");
        }

        // Porta de MainViewModel.EnsureNoPendingOutputOrJsonEdits do standalone — chamado no
        // início de "Processar"/"Processar com prévia", ANTES de montar a requisição: nunca
        // descarta uma edição pendente silenciosamente ao reprocessar. Sim = aplica primeiro (e
        // só continua se a aplicação for bem-sucedida); Não = descarta e continua; Cancelar =
        // aborta o reprocessamento, mantendo a edição pendente intacta na tela.
        private bool EnsureNoPendingOutputOrJsonEdits()
        {
            if (!_hasPendingOutputEdit && !_hasPendingJsonEdit)
                return true;

            var choice = MessageBox.Show(
                "Existem alterações pendentes na saída ou no JSON. Clique em Sim para aplicar, Não para descartar ou Cancelar para continuar editando.",
                "ListForge Integrado",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel)
                return false;

            if (choice == MessageBoxResult.Yes)
                return ApplyOutputEdits();

            DiscardOutputEdits();
            return true;
        }

        // Comparar entrada/saída (ver docs/listforge-classic-parity.md, grupo "Revisão e
        // resumos") — porta de MainViewModel.CaptureComparisonSnapshot do standalone: captura o
        // estado (entrada, opções e resultado) exatamente no momento de um processamento com
        // sucesso, igual ao standalone (só no caminho de sucesso — nunca em EmptyInput/
        // ValidationFailed/NoRows/TrialLimitReached, que retornam antes de chegar aqui). Se a
        // entrada mudar depois, `_freshness.IsStale` (já usado pelo bloqueio de resultado
        // desatualizado) impede comparar o snapshot antigo com a entrada atual — ver
        // OnCompareInputOutput, abaixo.
        private void CaptureComparisonSnapshot(ProcessingWorkflowRequest request, ProcessingWorkflowResult result)
        {
            try
            {
                _comparisonSnapshot = _comparisonService.CreateSnapshot(
                    request,
                    result,
                    (CmbWorkProfile.SelectedItem as WorkProfile)?.Name ?? WorkProfileService.DefaultProfileName,
                    ChkAdvancedList.IsChecked == true);
            }
            catch (Exception)
            {
                // Mesmo comportamento do standalone: uma falha ao capturar o snapshot não pode
                // interromper o processamento em si — só desabilita a comparação até o próximo
                // processamento com sucesso.
                _comparisonSnapshot = null;
            }
        }

        // "Comparar entrada/saída" (ver docs/listforge-classic-parity.md): nunca compara
        // silenciosamente a entrada atual com uma saída antiga — usa exatamente o mesmo guard de
        // desatualização (`_freshness.IsStale`) já empregado por Copiar/Salvar/Gerar JSON. Se a
        // entrada ou as opções mudaram desde o processamento que gerou o snapshot, informa e pede
        // para processar de novo, em vez de comparar.
        private void OnCompareInputOutput(object sender, RoutedEventArgs e)
        {
            if (_comparisonSnapshot == null)
            {
                SetStatus("Não há dados suficientes para comparação. Processe a lista antes.");
                return;
            }

            if (_freshness.IsStale)
            {
                SetStatus("A entrada ou as opções foram alteradas desde o último processamento. Processe novamente antes de comparar.");
                return;
            }

            // Mesma regra de CanCompareInputOutput do standalone: nunca compara enquanto há uma
            // edição manual pendente (ainda não revalidada) — aplique ou descarte primeiro.
            if (_hasPendingOutputEdit || _hasPendingJsonEdit)
            {
                SetStatus("Existem alterações manuais pendentes — aplique ou descarte antes de comparar.");
                return;
            }

            try
            {
                var comparison = _comparisonService.Compare(_comparisonSnapshot);
                var window = new ComparisonWindow(comparison) { Owner = this };
                window.ShowDialog();
                SetStatus(comparison.Summary.HasCriticalDifferences
                    ? "Comparação concluída com diferenças para revisão."
                    : "Comparação concluída. Nenhuma diferença crítica foi encontrada.");
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("CompareInputOutput", "Falha ao comparar a entrada e a saída.", ex);
                SetStatus($"Não foi possível comparar a entrada e a saída: {ex.Message}");
            }
        }

        // Histórico de processamentos (ver docs/listforge-classic-parity.md, grupo "Revisão e
        // resumos") — porta de HistoryView.xaml/MainViewModel.OpenSelectedHistoryOutput/
        // ClearProcessingHistory do standalone. Janela própria (igual a ComparisonWindow/
        // SizeConfigWindow), recarregada do disco a cada abertura — não mantém lista viva nesta
        // janela.
        private void OnOpenProcessingHistory(object sender, RoutedEventArgs e)
        {
            var window = new ProcessingHistoryWindow(_historyService) { Owner = this };
            window.ShowDialog();
        }

        // Salvar avançado (ver docs/listforge-classic-parity.md, grupo "Geração, cópia e
        // exportação de resultados") — porta de MainViewModel.AdvancedSave() do standalone: os
        // mesmos dois portões antes de perguntar nome/pasta ("Ative Lista avançada antes de usar
        // o Salvar avançado."/"Processe a lista antes de usar o Salvar avançado.") e o mesmo
        // bloqueio de resultado desatualizado já usado por Copiar/Salvar/Gerar JSON. A segunda
        // verificação do standalone ("Ative os recursos de JSON...") não se aplica aqui: no
        // integrado a aba/geração de JSON nunca fica escondida (não é condicionada a "Lista
        // avançada" como no standalone — ShowJsonSection == AdvancedListEnabled lá).
        //
        // Nunca sobrescreve silenciosamente: AdvancedSaveService.ResolveAvailableBaseName (núcleo
        // compartilhado) detecta colisão de nome e acrescenta "_v2"/"_v3"... automaticamente —
        // mesmo mecanismo real do standalone (que também nunca sobrescreve) em vez de inventar
        // uma confirmação de sobrescrita que não existe na referência. O nome final (já com
        // sufixo, se houve colisão) aparece no status/mensagem de sucesso, então a troca nunca é
        // silenciosa para o usuário. Cancelar o diálogo não grava nada (nenhum arquivo é tocado
        // antes de "Salvar" no diálogo ser confirmado).
        private async void OnAdvancedSave(object sender, RoutedEventArgs e)
        {
            if (_isSavingAdvanced) return;

            if (ChkAdvancedList.IsChecked != true)
            {
                SetStatus("Ative Lista avançada antes de usar o Salvar avançado.");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputTextBox.Text) || _lastOrders.Count == 0)
            {
                SetStatus("Processe a lista antes de usar o Salvar avançado.");
                return;
            }

            if (!ConfirmIfStale("Salvar avançado"))
            {
                SetStatus("Salvar avançado cancelado — processe de novo antes de salvar.");
                return;
            }

            var activeProfile = ActiveProfileSettings();
            var suggestedName = _currentInputFile != null
                ? System.IO.Path.GetFileNameWithoutExtension(_currentInputFile)
                : (activeProfile.UseDefaultListName ? activeProfile.DefaultListName : "lista");
            var suggestedDir = activeProfile.UseDefaultOutputDir ? activeProfile.OutputDir : "";

            var dialog = new AdvancedSaveDialog(suggestedName, _advancedSaveMode, suggestedDir) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                SetStatus("Salvar avançado cancelado.");
                return;
            }

            // Modo (soltos/ZIP) usado nesta chamada não é salvo de volta em _settings
            // automaticamente — o padrão só muda de verdade pela tela de Configurações; isto
            // aqui só lembra a última escolha para a PRÓXIMA vez que esta janela (ainda aberta)
            // chamar "Salvar avançado", sem reescrever o arquivo de configuração sozinho.
            _advancedSaveMode = dialog.Mode;
            _isSavingAdvanced = true;
            BtnAdvancedSave.IsEnabled = false;
            SetStatus("Salvando...");

            try
            {
                // Saída: texto vivo (igual ao standalone, AdvancedSave() usa OutputText). JSON:
                // _lastValidJsonText, canônico — nunca o JsonTextBox.Text (vivo), mesma proteção
                // de "Copiar JSON"/"Gerar JSON" contra incluir JSON editado ainda não revalidado
                // num arquivo pronto para SISBolt.
                var request = new AdvancedSaveRequest(
                    dialog.OutputDirectory,
                    dialog.BaseName,
                    InputTextBox.Text,
                    OutputTextBox.Text,
                    _lastValidJsonText,
                    dialog.Mode);

                var result = await Task.Run(() => _advancedSaveService.Save(request));

                if (!result.Success || result.Value == null)
                {
                    // Cobre pasta sem acesso/inexistente (a mensagem do serviço distingue cada
                    // causa real via ex.Message), arquivo em uso por outro processo, etc. — só
                    // reporta sucesso depois que Save() de fato terminou de gravar, nunca antes.
                    MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetStatus("Falha ao salvar avançado.");
                    return;
                }

                var saved = result.Value;
                RegisterProcessingHistory(ResolveAdvancedSaveHistoryPath(saved), _lastOrders.Count);
                var message = saved.Mode == AdvancedSaveMode.Zip
                    ? $"Salvar avançado concluído:\n{saved.ZipPath}"
                    : "Salvar avançado concluído:\n" + string.Join("\n", saved.FilePaths);
                MessageBox.Show(message, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Information);

                SetStatus(saved.Mode == AdvancedSaveMode.Zip
                    ? $"Salvar avançado gerado: {System.IO.Path.GetFileName(saved.ZipPath)}"
                    : $"Salvar avançado gerado: {saved.FilePaths.Count} arquivo(s).");
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("AdvancedSave", "Falha inesperada ao salvar avançado.", ex);
                SetStatus($"Falha inesperada ao salvar avançado: {ex.Message}");
            }
            finally
            {
                _isSavingAdvanced = false;
                BtnAdvancedSave.IsEnabled = true;
            }
        }

        private void OnIssueSelected(object sender, SelectionChangedEventArgs e)
        {
            var index = IssuesList.SelectedIndex;
            if (index < 0 || index >= _issueLines.Count) return;

            var lineNumber = _issueLines[index];
            var start = TextLineLocator.GetLineStartIndex(InputTextBox.Text, lineNumber);
            var length = TextLineLocator.GetLineLength(InputTextBox.Text, lineNumber);
            InputTextBox.Focus();
            InputTextBox.Select(start, length);
            InputTextBox.ScrollToLine(Math.Max(0, lineNumber - 1));

            // No modo PX, InputTextBox vive na etapa Lista (ver PxSlotInput) — sem isto, a
            // seleção acima aconteceria fora de tela enquanto o usuário está na etapa Revisão.
            // Clássico não tem "etapas", então _pxCurrentMode nunca é PxModePx lá.
            if (_pxCurrentMode == PxModePx)
                GoToPxStep("lista");
        }

        // Usa OutputTextBox.Text (vivo), igual ao standalone (SaveOutput() usa OutputText) — a
        // lista organizada não tem um formato "inválido" possível (é texto livre), então salvar
        // o que está exibido, mesmo com uma edição ainda não aplicada, é seguro e corresponde
        // exatamente ao que o usuário vê na tela.
        private void OnSaveOutput(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OutputTextBox.Text))
            {
                SetStatus("Processe a lista antes de salvar a saída.");
                return;
            }

            if (!ConfirmIfStale("Salvar saída"))
            {
                SetStatus("Salvar saída cancelado — processe de novo antes de salvar.");
                return;
            }

            if (!TryPickExportPath("Salvar saída organizada", "Arquivo de texto (*.txt)|*.txt", out var directory, out var baseName))
                return;

            var result = _exportService.SaveOutputText(OutputTextBox.Text, directory, baseName);
            if (!result.Success || result.Value == null)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _hasUnsavedChanges = false;
            RegisterProcessingHistory(result.Value, _lastProcessedRowCount);
            SetStatus($"Saída salva: {result.Value}");
        }

        private void OnGenerateJson(object sender, RoutedEventArgs e)
        {
            if (_lastOrders.Count == 0)
            {
                SetStatus("Processe a lista antes de gerar o JSON.");
                return;
            }

            if (!ConfirmIfStale("Gerar JSON"))
            {
                SetStatus("Gerar JSON cancelado — processe de novo antes de gerar.");
                return;
            }

            if (!TryPickExportPath("Gerar JSON", "Arquivo JSON (*.json)|*.json", out var directory, out var baseName))
                return;

            var result = _exportService.SaveJson(_lastOrders, directory, baseName);
            if (!result.Success || result.Value == null)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _hasUnsavedChanges = false;
            RegisterProcessingHistory(result.Value, _lastOrders.Count);
            SetStatus($"JSON gerado: {result.Value}");
        }

        // Histórico de processamentos (ver docs/listforge-classic-parity.md, grupo "Revisão e
        // resumos") — porta de MainViewModel.RegisterProcessingHistory do standalone: só chamado
        // depois que um ARQUIVO já foi gravado com sucesso (nunca antes, nunca só por
        // processar). Uma falha ao atualizar o histórico nunca desfaz/invalida a exportação que
        // já foi concluída — só registra no log e segue sem o registro (mesmo tratamento do
        // standalone, que também só loga a falha, sem MessageBox). Persistência própria do PX
        // DEV (Integrations/ListForgeProcessingHistoryService) — nunca o histórico do ListForge
        // Standalone.
        private void RegisterProcessingHistory(string? outputPath, int processedLineCount)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                return;

            var source = Integrations.ListForgeProcessingHistoryService.BuildSource(_currentSourceType, _currentInputFile);
            var result = _historyService.Add(new Integrations.ListForgeProcessingHistoryEntry
            {
                SourceDisplayName = source.DisplayName,
                SourceType = source.SourceType,
                ProcessedLineCount = Math.Max(0, processedLineCount),
                OutputPath = outputPath!,
            });

            if (!result.Success)
                Integrations.PxLogger.Error("ProcessingHistory", "Falha ao atualizar histórico de processamentos.", result.Exception);
        }

        private static string? ResolveAdvancedSaveHistoryPath(AdvancedSaveResult saved)
        {
            if (!string.IsNullOrWhiteSpace(saved.ZipPath))
                return saved.ZipPath;

            return saved.FilePaths.FirstOrDefault(path => string.Equals(System.IO.Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                ?? saved.FilePaths.FirstOrDefault();
        }

        // Pasta/nome de saída padrão (ver docs/listforge-classic-parity.md, grupo
        // "Configurações") — equivalente a ResolveOutputDir()/ResolveOutputName() do standalone.
        // Diferença honesta: o standalone tem dois prompts INDEPENDENTES (pasta e nome), cada um
        // pulável separadamente; o integrado já usava um único SaveFileDialog combinado (decisão
        // de rodadas anteriores). Por isso só pula o diálogo por completo quando os DOIS padrões
        // estão ativos — com só um ativo, o diálogo ainda abre, mas pré-preenchido com o valor
        // configurado (pasta inicial e/ou nome sugerido), continuando editável.
        private bool TryPickExportPath(string title, string filter, out string directory, out string baseName)
        {
            var activeProfile = ActiveProfileSettings();

            if (activeProfile.UseDefaultOutputDir && activeProfile.UseDefaultListName)
            {
                directory = activeProfile.OutputDir;
                baseName = FileNameHelper.SanitizeBaseFilename(activeProfile.DefaultListName);
                try
                {
                    System.IO.Directory.CreateDirectory(directory);
                    return true;
                }
                catch (Exception)
                {
                    // Pasta padrão configurada ficou inválida (removida/sem permissão) — cai
                    // para o diálogo normal em vez de falhar silenciosamente.
                }
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = title,
                Filter = filter,
                OverwritePrompt = false,
                FileName = activeProfile.UseDefaultListName
                    ? FileNameHelper.SanitizeBaseFilename(activeProfile.DefaultListName)
                    : "lista",
                InitialDirectory = activeProfile.UseDefaultOutputDir && System.IO.Directory.Exists(activeProfile.OutputDir)
                    ? activeProfile.OutputDir
                    : null,
            };

            if (dialog.ShowDialog() != true)
            {
                directory = "";
                baseName = "";
                return false;
            }

            directory = System.IO.Path.GetDirectoryName(dialog.FileName) ?? "";
            baseName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            return true;
        }

        // =================================================================================
        // Seção 3 — funções de entrada/revisão já viáveis sem novo suporte do núcleo.
        // "Abrir Lista" — texto puro continua lido direto (sem passar pelo núcleo); PDF/Word/
        // Excel passam por ListForge.Core.Services.FileImportService (movido do standalone
        // nesta rodada — ver docs/listforge-classic-parity.md, "Importação de documentos").
        // Imagem/OCR (Tesseract — binários nativos + tessdata) permanece exclusiva do
        // standalone: reconhecida aqui só para dar uma mensagem específica, nunca tentada.
        // =================================================================================
        private static readonly HashSet<string> ImageOnlyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp" };

        private readonly ListForge.Services.FileImportService _fileImportService = new();
        private bool _isImportingDocument;

        private async void OnOpenInputFile(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Abrir lista",
                Filter =
                    "Arquivos compatíveis|*.txt;*.csv;*.list;*.pdf;*.docx;*.doc;*.xlsx;*.xlsm;*.xls|" +
                    "Texto|*.txt;*.csv;*.list|PDF|*.pdf|Word|*.doc;*.docx|Excel|*.xls;*.xlsx;*.xlsm|" +
                    "Todos os arquivos|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog() != true) return;

            var ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();

            if (ListForge.Core.FileImporter.TextExtensions.Contains(ext))
            {
                OpenPlainTextFile(dlg.FileName);
                return;
            }

            if (ImageOnlyExtensions.Contains(ext))
            {
                SetStatus("Importação de imagens via OCR não está disponível nesta distribuição " +
                    "(depende de binários nativos e arquivos de idioma que não são instalados automaticamente). " +
                    "Use o ListForge Standalone para importar essa imagem, ou abra/cole o texto já reconhecido aqui.");
                return;
            }

            var isDocument = ListForge.Core.FileImporter.PdfExtensions.Contains(ext)
                || ListForge.Core.FileImporter.WordExtensions.Contains(ext)
                || ListForge.Core.FileImporter.ExcelExtensions.Contains(ext);
            if (!isDocument)
            {
                SetStatus($"Formato não suportado: {ext}");
                return;
            }

            await ImportDocumentAsync(dlg.FileName);
        }

        private void OpenPlainTextFile(string path)
        {
            try
            {
                InputTextBox.Text = System.IO.File.ReadAllText(path);
                _currentInputFile = path;
                _currentSourceType = Integrations.ListForgeProcessingHistorySourceTypes.File;
                _hasUnsavedChanges = false;
                SetStatus($"Lista aberta: {path}");
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("OpenInputFile", "Falha ao abrir arquivo de entrada.", ex, path);
                SetStatus($"Falha ao abrir arquivo: {ex.Message}");
            }
        }

        // Importação de documentos (PDF/Word/Excel) — ver docs/listforge-classic-parity.md,
        // "Importação de documentos". Mesma regra do standalone: lê o arquivo inteiro de uma vez
        // (sem escolha de página/planilha — a referência também não oferece isso), mostra o
        // conteúdo extraído na própria caixa de entrada (editável) para revisão, nunca inicia
        // processamento/exportação por conta própria. Reforço desta entrega, além da referência:
        // pede confirmação antes de substituir uma entrada com alterações não salvas (a
        // referência substitui sem perguntar) — importações de PDF/Word/Excel podem produzir
        // texto bem diferente do original, então vale um segundo cuidado aqui. Extração roda em
        // Task.Run (nunca na thread de UI) — nenhuma chamada COM do Corel acontece dentro dela,
        // só análise do arquivo em disco.
        private async Task ImportDocumentAsync(string path)
        {
            if (_isImportingDocument) return;

            if (_hasUnsavedChanges)
            {
                var choice = MessageBox.Show(
                    "Há uma lista com alterações que ainda não foram salvas ou exportadas.\n\n" +
                    "Importar este arquivo vai substituir o conteúdo atual. Deseja continuar e perder essas alterações?",
                    "ListForge Integrado",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (choice != MessageBoxResult.Yes)
                {
                    SetStatus("Importação cancelada — a entrada atual foi preservada.");
                    return;
                }
            }

            _isImportingDocument = true;
            BtnOpenInputFile.IsEnabled = false;
            SetStatus($"Importando {System.IO.Path.GetFileName(path)}...");

            try
            {
                var result = await Task.Run(() => _fileImportService.ImportInputFile(path));

                if (!result.Success || result.Value == null)
                {
                    // Nunca toca na entrada atual numa falha — distingue extração vazia (mensagem
                    // específica do núcleo, ex.: PDF escaneado sem texto) de falha de leitura
                    // (arquivo corrompido/incompatível), ambas já reportadas por
                    // FileImportService com uma mensagem própria, nunca genérica demais.
                    MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetStatus($"Falha ao importar: {System.IO.Path.GetFileName(path)}");
                    return;
                }

                var imported = result.Value;
                InputTextBox.Text = imported.Text;
                _currentInputFile = null;
                _currentSourceType = Integrations.ListForgeProcessingHistorySourceTypes.ImportedFile;
                _hasUnsavedChanges = true;
                SetStatus(imported.StatusMessage);
                IssuesList.ItemsSource = null;
                _issueLines = new();

                if (!string.IsNullOrWhiteSpace(imported.ReviewMessage))
                    MessageBox.Show(imported.ReviewMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // Falha inesperada fora do OperationResult (ex.: biblioteca de terceiros lançando
                // algo não capturado por FileImportService) — mesma garantia: entrada atual
                // preservada, nunca um estado parcial.
                Integrations.PxLogger.Error("ImportDocument", "Falha inesperada ao importar documento.", ex, path);
                MessageBox.Show($"Falha ao importar arquivo:\n\n{ex.Message}", "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus($"Falha ao importar: {System.IO.Path.GetFileName(path)}");
            }
            finally
            {
                _isImportingDocument = false;
                BtnOpenInputFile.IsEnabled = true;
            }
        }

        // Backup automático antes de sobrescrever (ver docs/listforge-classic-parity.md, grupo
        // "Entrada e importação") — porta de ConfigManager.CreateBackup +
        // MainViewModel.SaveInputFile do standalone: só cria backup quando o conteúdo em disco é
        // DIFERENTE do que está sendo salvo (evita backup inútil ao salvar duas vezes sem
        // mudar nada). Se o arquivo não existe mais em disco (apagado externamente), não há o
        // que fazer backup — mesma guarda do standalone (`if (File.Exists(_currentFile))`).
        // Nunca escreve o arquivo novo antes do backup ter sido confirmado: uma falha de backup
        // aborta a operação inteira, sem tocar no arquivo original.
        private void OnSaveInputFile(object sender, RoutedEventArgs e)
        {
            if (_currentInputFile == null)
            {
                OnSaveInputFileAs(sender, e);
                return;
            }

            try
            {
                if (System.IO.File.Exists(_currentInputFile))
                {
                    string onDisk;
                    try
                    {
                        onDisk = System.IO.File.ReadAllText(_currentInputFile);
                    }
                    catch (Exception ex)
                    {
                        // Não dá para comparar nem confirmar que o backup preservaria o
                        // conteúdo real — mesma regra do standalone: aborta sem salvar, nunca
                        // sobrescreve sem saber o que estava lá antes.
                        SetStatus($"Falha ao verificar o arquivo antes de salvar: {ex.Message}");
                        return;
                    }

                    if (onDisk != InputTextBox.Text)
                    {
                        try
                        {
                            Integrations.ListForgeInputBackupService.CreateBackup(_currentInputFile);
                        }
                        catch (Exception ex)
                        {
                            Integrations.PxLogger.Error("InputBackup", "Falha ao criar backup antes de salvar a entrada.", ex, _currentInputFile);
                            SetStatus($"Falha ao criar backup — a entrada NÃO foi salva, o arquivo original não foi tocado: {ex.Message}");
                            return;
                        }
                    }
                }

                System.IO.File.WriteAllText(_currentInputFile, InputTextBox.Text);
                _hasUnsavedChanges = false;
                SetStatus($"Entrada salva: {_currentInputFile}");
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("SaveInputFile", "Falha ao salvar a entrada.", ex, _currentInputFile);
                SetStatus($"Falha ao salvar entrada: {ex.Message}");
            }
        }

        // "Salvar como" sempre faz backup do destino escolhido se ele já existir — sem comparar
        // conteúdo (pode ser um arquivo não relacionado ao atual) — mesma regra do standalone
        // (MainViewModel.SaveInputAsFile: `if (File.Exists(dlg.FileName)) ConfigManager.
        // CreateBackup(dlg.FileName);`, incondicional).
        private void OnSaveInputFileAs(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Salvar entrada como",
                Filter = "Arquivo de texto (*.txt)|*.txt",
                FileName = "lista.txt",
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                if (System.IO.File.Exists(dlg.FileName))
                {
                    try
                    {
                        Integrations.ListForgeInputBackupService.CreateBackup(dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        Integrations.PxLogger.Error("InputBackup", "Falha ao criar backup antes de salvar a entrada como novo arquivo.", ex, dlg.FileName);
                        SetStatus($"Falha ao criar backup — a entrada NÃO foi salva, o arquivo de destino não foi tocado: {ex.Message}");
                        return;
                    }
                }

                System.IO.File.WriteAllText(dlg.FileName, InputTextBox.Text);
                _currentInputFile = dlg.FileName;
                _hasUnsavedChanges = false;
                SetStatus($"Entrada salva: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                Integrations.PxLogger.Error("SaveInputFileAs", "Falha ao salvar a entrada como novo arquivo.", ex, dlg.FileName);
                SetStatus($"Falha ao salvar entrada: {ex.Message}");
            }
        }

        // Extrair lista de link (ver docs/listforge-classic-parity.md, grupo "Entrada e
        // importação") — mesma lógica de extração do standalone
        // (ListForge.Core.Services.LinkListImportService, movido para o núcleo compartilhado
        // nesta entrega: só dependia de ConfigManager.AppName, usado num cabeçalho HTTP sem
        // efeito observável no resultado).
        private async void OnExtractFromLink(object sender, RoutedEventArgs e)
        {
            var dialog = new LinkImportDialog { Owner = this };
            if (dialog.ShowDialog() != true) return;

            var append = dialog.AppendToCurrentList;
            SetStatus("Extraindo lista do link...");

            var result = await _linkListImportService.ExtractAsync(dialog.Url, TxtSeparator.Text, _sizeConfig);
            if (!result.Success || result.Value == null)
            {
                MessageBox.Show(result.UserMessage, "ListForge Integrado", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Falha ao extrair lista do link.");
                return;
            }

            if (append && !string.IsNullOrEmpty(InputTextBox.Text))
            {
                var needsNewline = !InputTextBox.Text.EndsWith("\n");
                InputTextBox.Text += (needsNewline ? Environment.NewLine : "") + result.Value.Text;
            }
            else
            {
                InputTextBox.Text = result.Value.Text;
            }

            _hasUnsavedChanges = true;
            _currentSourceType = Integrations.ListForgeProcessingHistorySourceTypes.Link;
            SetStatus($"Lista extraída do link: {result.Value.LineCount} registro(s).");
        }

        private void OnCleanSpaces(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                SetStatus("Não há texto para limpar.");
                return;
            }

            try
            {
                InputTextBox.Text = ListProcessor.CleanTextBySeparator(InputTextBox.Text, TxtSeparator.Text);
                SetStatus("Espaços removidos.");
            }
            catch (Exception ex)
            {
                SetStatus($"Falha ao limpar espaços: {ex.Message}");
            }
        }

        private void OnClearAll(object sender, RoutedEventArgs e)
        {
            InputTextBox.Text = "";
            SetGeneratedTexts("", "");
            IssuesList.ItemsSource = null;
            _lastOrders = new();
            _issueLines = new();
            _currentInputFile = null;
            _currentSourceType = Integrations.ListForgeProcessingHistorySourceTypes.PastedText;
            _hasUnsavedChanges = false;
            _comparisonSnapshot = null;
            // "Limpar tudo" é um descarte explícito e óbvio — igual ao MainViewModel.ClearAll do
            // standalone, nunca pede confirmação de edição pendente (diferente de reprocessar/
            // desligar a edição, que sempre perguntam antes de descartar).
            _outputWasManuallyEdited = false;
            UpdateManualEditUi();
            // InputTextBox.Text = "" acima já disparou OnInputTextChanged/MarkInputStale com o
            // _lastOutputText ainda antigo — recalcula aqui, já com tudo limpo, para o aviso não
            // ficar visível sem saída nenhuma para avisar sobre.
            UpdateOutputStaleWarning();
            SizeSummaryText.Text = "";
            SetStatus("Tudo limpo.");
        }

        private void OnCopyOutput(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OutputTextBox.Text))
            {
                SetStatus("Não há saída para copiar.");
                return;
            }

            if (!ConfirmIfStale("Copiar saída"))
            {
                SetStatus("Copiar saída cancelado — processe de novo antes de copiar.");
                return;
            }

            Clipboard.SetText(OutputTextBox.Text);
            SetStatus("Saída organizada copiada.");
        }

        // Usa _lastValidJsonText (canônico), nunca JsonTextBox.Text (vivo) — mesma regra do
        // standalone (CopyJson() usa _lastJson): protege contra copiar um JSON digitado/editado
        // ainda não validado por "Aplicar alterações" como se já fosse o resultado atual.
        private void OnCopyJson(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastValidJsonText))
            {
                SetStatus("Ainda não há prévia do JSON. Clique em Processar.");
                return;
            }

            if (!ConfirmIfStale("Copiar JSON"))
            {
                SetStatus("Copiar JSON cancelado — processe de novo antes de copiar.");
                return;
            }

            Clipboard.SetText(_lastValidJsonText);
            SetStatus("JSON copiado.");
        }

        // Localizar/Substituir: a UI (navegação relativa ao cursor, com wraparound) é própria
        // deste host, mas a matemática de "o que conta como ocorrência" e "como substituir" vem
        // de ListForge.Core.TextSearchHelper (núcleo compartilhado) — antes desta correção, esta
        // janela reimplementava essa lógica à mão (IndexOf/LastIndexOf/StringBuilder), uma cópia
        // local que podia divergir do standalone sem ninguém notar. TextSearchHelper.cs foi
        // movido do projeto do standalone para o projeto compartilhado nesta mesma entrega (ver
        // docs/listforge-classic-parity.md) — tinha zero dependência de AppLogger/ConfigManager,
        // então a extração não exigiu nenhuma decisão de produto; validado reconstruindo e
        // testando o próprio repositório do ListForge standalone (281/281) além deste.
        private void OnFindNext(object sender, RoutedEventArgs e) => MoveFindSelection(forward: true);

        private void OnFindPrevious(object sender, RoutedEventArgs e) => MoveFindSelection(forward: false);

        private void MoveFindSelection(bool forward)
        {
            var needle = TxtFind.Text;
            if (string.IsNullOrEmpty(needle)) return;

            var matchCase = ChkMatchCase.IsChecked == true;
            var matches = TextSearchHelper.FindMatches(InputTextBox.Text, needle, matchCase);
            if (matches.Count == 0)
            {
                SetStatus("Texto não encontrado.");
                return;
            }

            var caret = InputTextBox.SelectionStart;
            var caretEnd = caret + InputTextBox.SelectionLength;

            // Padrão: primeiro match (avançando) ou último (retrocedendo) — mesmo efeito de
            // "voltar ao início/fim" (wraparound) do laço IndexOf/LastIndexOf anterior, se não
            // houver nenhum match mais perto na direção pedida.
            var match = forward ? matches[0] : matches[matches.Count - 1];

            if (forward)
            {
                foreach (var candidate in matches)
                {
                    if (candidate.start >= caretEnd) { match = candidate; break; }
                }
            }
            else
            {
                foreach (var candidate in matches)
                {
                    if (candidate.start >= caret) break;
                    match = candidate;
                }
            }

            InputTextBox.Focus();
            InputTextBox.Select(match.start, match.length);
        }

        private void OnReplaceCurrent(object sender, RoutedEventArgs e)
        {
            var needle = TxtFind.Text;
            if (string.IsNullOrEmpty(needle)) return;

            var comparison = ChkMatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (string.Equals(InputTextBox.SelectedText, needle, comparison))
            {
                var start = InputTextBox.SelectionStart;
                var length = InputTextBox.SelectedText.Length;
                InputTextBox.Text = TextSearchHelper.ReplaceAt(InputTextBox.Text, start, length, TxtReplace.Text);
                InputTextBox.Select(start, TxtReplace.Text.Length);
            }

            MoveFindSelection(forward: true);
        }

        private void OnReplaceAll(object sender, RoutedEventArgs e)
        {
            var needle = TxtFind.Text;
            if (string.IsNullOrEmpty(needle)) return;

            var matchCase = ChkMatchCase.IsChecked == true;
            var count = TextSearchHelper.FindMatches(InputTextBox.Text, needle, matchCase).Count;
            InputTextBox.Text = TextSearchHelper.ReplaceAll(InputTextBox.Text, needle, TxtReplace.Text, matchCase);
            SetStatus($"{count} ocorrência(s) substituída(s).");
        }

        private void SetStatus(string message) =>
            StatusText.Text = $"{DateTime.Now:HH:mm:ss}  {message}";

        /// <summary>
        /// Fecha sem perda silenciosa: se há alterações desde o último salvamento/exportação,
        /// pergunta explicitamente — nunca descarta por conta própria nem perde a lista sem
        /// avisar.
        /// </summary>
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_hasUnsavedChanges) return;

            var choice = MessageBox.Show(
                "Há uma lista com alterações que ainda não foram salvas ou exportadas.\n\n" +
                "Deseja continuar editando (cancela o fechamento) ou descartar e fechar?",
                "ListForge Integrado — alterações não salvas",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            // Yes = "Continuar editando" (cancela o fechamento); No = "Descartar e fechar".
            if (choice == MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
        }
    }
}
