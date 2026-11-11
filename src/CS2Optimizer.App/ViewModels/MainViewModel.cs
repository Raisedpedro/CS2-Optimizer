using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CS2Optimizer.App.Dialogs;
using CS2Optimizer.Core.Diagnostics;
using CS2Optimizer.Core.Engine;
using CS2Optimizer.Core.Fixes;
using CS2Optimizer.Core.Mitigations;
using CS2Optimizer.Core.Models;
using CS2Optimizer.Core.Oosu10;
using CS2Optimizer.Core.Services;
using CS2Optimizer.Core.Updates;

namespace CS2Optimizer.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IAppInteraction _interaction;
    private readonly Dispatcher _dispatcher;
    private readonly string _windowsDefaultText;

    private FixContext? _context;
    private bool _isBusy;
    private bool _isLogExpanded;
    private int _selectedTabIndex;
    private string _status = "Pronto.";
    private string _statusDetail = "Aguardando leitura da máquina.";
    private string _lastScanText = "nunca";
    private string _osText = "—";
    private string _osDetail = "";
    private string _machineTitle = "";
    private string _cpuText = "—";
    private string _cpuDetail = "";
    private string _ramText = "—";
    private string _ramDetail = "";
    private string _gpuText = "—";
    private string _gpuDetail = "";
    private string _cs2Text = "—";
    private string _cs2Detail = "";
    private string _storageText = "—";
    private string _powerText = "—";
    private string _hvciText = "—";
    private string _mitigationSummary = "—";
    private Brush _mitigationSummaryBrush = UiBrushes.Muted;
    private string _mitigationStateText = "Faça uma leitura da máquina para ver o estado das mitigações.";
    private bool _needReboot;
    private string _recommendedSummary = "Aguardando leitura da máquina.";
    private FixItemViewModel? _selectedFix;
    private MitigationProfileViewModel? _selectedMitigation;
    private BackupInfo? _selectedBackup;
    private bool _suppressSelectionRefresh;

    private const string Oosu10AllGroups = "Todos os grupos";
    private string _oosu10Search = "";
    private string _oosu10Group = Oosu10AllGroups;
    private bool _oosu10OnlyPending;
    private Oosu10TweakViewModel? _selectedOosu10;
    private bool _suppressOosu10Refresh;
    private string _oosu10Summary = "Marque os ajustes que deseja aplicar ou use as sugestões para CS2.";

    private bool _isProgressIndeterminate = true;
    private double _progressValue;
    private double _progressMaximum = 100;
    private string _busyTitle = "";
    private string _busyDetail = "";
    private int _healthScore;
    private string _healthScoreDisplay = "-";
    private string _healthScoreLabel = "Aguardando leitura";
    private Brush _healthScoreBrush = UiBrushes.Muted;

    // ---- atualização ----
    private readonly UpdateSettings _updateSettings = UpdateSettings.Load();
    private bool _isCheckingUpdate;
    private bool _hasUpdate;
    private string _updateTitle = "";
    private string _updateDetail = "";
    private string _updateStatusText = "Ainda não verificado nesta sessão.";
    private string _updateUrl = UpdateChecker.ReleasesPageUrl(UpdateChecker.DefaultRepository);
    private string? _latestVersionTag;

    public MainViewModel(AppServices services, IAppInteraction interaction)
    {
        _services = services;
        _interaction = interaction;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _windowsDefaultText = services.Mitigation.GetWindowsDefaultText();

        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        ApplyRecommendedCommand = new AsyncRelayCommand(ApplyRecommendedAsync, () => !IsBusy && HasRecommendedWork);
        ApplySelectedCommand = new AsyncRelayCommand(ApplySelectedAsync, () => !IsBusy && SelectedCount > 0);
        SelectRecommendedCommand = new RelayCommand(SelectRecommended, () => !IsBusy && RecommendedCount > 0);
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => !IsBusy && SelectedCount > 0);
        ApplyMitigationsCommand = new AsyncRelayCommand(ApplyMitigationsAsync, () => !IsBusy && SelectedMitigationCount > 0);
        ResetMitigationsCommand = new AsyncRelayCommand(ResetMitigationsAsync, () => !IsBusy);
        ShowWindowsDefaultCommand = new RelayCommand(ShowWindowsDefault);
        ClearMitigationSelectionCommand = new RelayCommand(ClearMitigationSelection, () => !IsBusy && SelectedMitigationCount > 0);
        RefreshBackupsCommand = new RelayCommand(RefreshBackups);
        RestoreSelectedBackupCommand = new AsyncRelayCommand(RestoreSelectedBackupAsync, () => !IsBusy && SelectedBackup is not null);
        RestoreFromFolderCommand = new AsyncRelayCommand(RestoreFromFolderAsync, () => !IsBusy);
        OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
        SelectOosu10PresetCommand = new RelayCommand(SelectOosu10Preset, () => !IsBusy && Oosu10Tweaks.Count > 0);
        ClearOosu10SelectionCommand = new RelayCommand(ClearOosu10Selection, () => !IsBusy && Oosu10SelectedCount > 0);
        ApplyOosu10Command = new AsyncRelayCommand(ApplyOosu10Async, () => !IsBusy && Oosu10SelectedCount > 0);
        RestoreOosu10Command = new AsyncRelayCommand(RestoreOosu10Async, () => !IsBusy && Oosu10SelectedCount > 0);
        SaveReportCommand = new RelayCommand(SaveReport, () => _context is not null);
        ClearPendingCommand = new RelayCommand(ClearAllPending, () => !IsBusy && PendingChanges.Count > 0);
        OpenLogFileCommand = new RelayCommand(OpenLogFile);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        ToggleLogCommand = new RelayCommand(() => IsLogExpanded = !IsLogExpanded);
        CheckUpdatesCommand = new AsyncRelayCommand(() => CheckForUpdatesAsync(true), () => !IsCheckingUpdate);
        OpenUpdateCommand = new RelayCommand(OpenUpdatePage);
        DismissUpdateCommand = new RelayCommand(DismissUpdate);
        OpenProjectPageCommand = new RelayCommand(OpenProjectPage);
        SelectAllQuickCleanCommand = new RelayCommand(SelectAllQuickClean, () => !IsBusy && QuickClean.Count > 0);
        ClearQuickCleanCommand = new RelayCommand(ClearQuickClean, () => !IsBusy && QuickCleanSelectedCount > 0);
        ApplyQuickCleanCommand = new AsyncRelayCommand(ApplyQuickCleanAsync, () => !IsBusy && QuickCleanSelectedCount > 0);
        ShowTabCommand = new RelayCommand(parameter =>
            SelectedTabIndex = AppTabs.IndexOf(parameter as string));

        _services.Engine.StatusChanged += text => Post(() => Status = text);
        _services.Logger.EntryAdded += OnLogEntry;
        foreach (var entry in _services.Logger.Entries)
            LogItems.Add(new LogItemViewModel(entry));

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            IsAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            IsAdmin = false;
        }

        RefreshBackups();
        RefreshOosu10State();
    }

    public ObservableCollection<FixItemViewModel> Fixes { get; } = new();
    public ObservableCollection<MitigationProfileViewModel> Mitigations { get; } = new();
    public ObservableCollection<BackupInfo> Backups { get; } = new();
    public ObservableCollection<Oosu10TweakViewModel> Oosu10Tweaks { get; } = new();
    public ObservableCollection<Oosu10TweakViewModel> Oosu10Visible { get; } = new();
    public ObservableCollection<DiagnosticItemViewModel> Diagnostics { get; } = new();
    public ObservableCollection<ChecklistItemViewModel> Checklist { get; } = new();
    public ObservableCollection<string> Problems { get; } = new();
    public ObservableCollection<LogItemViewModel> LogItems { get; } = new();
    public ObservableCollection<PendingChangeViewModel> PendingChanges { get; } = new();
    public ObservableCollection<SessionChangeViewModel> SessionChanges { get; } = new();
    public ObservableCollection<QuickCleanItemViewModel> QuickClean { get; } = new();
    public IReadOnlyList<string> Tips => Cs2Tips.All;

    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand ApplyRecommendedCommand { get; }
    public AsyncRelayCommand ApplySelectedCommand { get; }
    public RelayCommand SelectRecommendedCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public AsyncRelayCommand ApplyMitigationsCommand { get; }
    public AsyncRelayCommand ResetMitigationsCommand { get; }
    public RelayCommand ShowWindowsDefaultCommand { get; }
    public RelayCommand ClearMitigationSelectionCommand { get; }
    public RelayCommand RefreshBackupsCommand { get; }
    public AsyncRelayCommand RestoreSelectedBackupCommand { get; }
    public AsyncRelayCommand RestoreFromFolderCommand { get; }
    public RelayCommand OpenBackupFolderCommand { get; }
    public RelayCommand SelectOosu10PresetCommand { get; }
    public RelayCommand ClearOosu10SelectionCommand { get; }
    public AsyncRelayCommand ApplyOosu10Command { get; }
    public AsyncRelayCommand RestoreOosu10Command { get; }
    public RelayCommand SaveReportCommand { get; }
    public RelayCommand ClearPendingCommand { get; }
    public RelayCommand OpenLogFileCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand ToggleLogCommand { get; }
    public RelayCommand ShowTabCommand { get; }

    // ---- atualização ----
    public AsyncRelayCommand CheckUpdatesCommand { get; }
    public RelayCommand OpenUpdateCommand { get; }
    public RelayCommand DismissUpdateCommand { get; }
    public RelayCommand OpenProjectPageCommand { get; }

    // ---- limpeza rápida ----
    public RelayCommand SelectAllQuickCleanCommand { get; }
    public RelayCommand ClearQuickCleanCommand { get; }
    public AsyncRelayCommand ApplyQuickCleanCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(IsProgressVisible));
                RefreshCommands();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool IsProgressVisible => IsBusy;

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set
        {
            if (SetProperty(ref _isProgressIndeterminate, value))
                OnPropertyChanged(nameof(ProgressPercentText));
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set
        {
            if (SetProperty(ref _progressValue, value))
                OnPropertyChanged(nameof(ProgressPercentText));
        }
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        private set
        {
            if (SetProperty(ref _progressMaximum, value))
                OnPropertyChanged(nameof(ProgressPercentText));
        }
    }

    public string ProgressPercentText => IsProgressIndeterminate || ProgressMaximum <= 0
        ? "aguarde..."
        : $"{ProgressValue:0} de {ProgressMaximum:0} · {Math.Round(ProgressValue * 100d / ProgressMaximum):0}%";

    public string BusyTitle
    {
        get => _busyTitle;
        private set => SetProperty(ref _busyTitle, value);
    }

    public string BusyDetail
    {
        get => _busyDetail;
        private set => SetProperty(ref _busyDetail, value);
    }

    public int HealthScore
    {
        get => _healthScore;
        private set
        {
            if (SetProperty(ref _healthScore, value))
                OnPropertyChanged(nameof(HealthScoreBar));
        }
    }

    public string HealthScoreDisplay
    {
        get => _healthScoreDisplay;
        private set => SetProperty(ref _healthScoreDisplay, value);
    }

    public string HealthScoreLabel
    {
        get => _healthScoreLabel;
        private set => SetProperty(ref _healthScoreLabel, value);
    }

    public Brush HealthScoreBrush
    {
        get => _healthScoreBrush;
        private set => SetProperty(ref _healthScoreBrush, value);
    }

    public double HealthScoreBar => HealthScore;

    public int PendingCount => PendingChanges.Count;
    public bool HasPendingChanges => PendingChanges.Count > 0;
    public bool HasNoPendingChanges => PendingChanges.Count == 0;
    public string PendingCountText => PendingCount == 0
        ? "Nada selecionado"
        : $"{PendingCount} alteração(ões) pronta(s) para aplicar";

    public bool HasSessionChanges => SessionChanges.Count > 0;
    public bool HasNoSessionChanges => SessionChanges.Count == 0;

    public bool HasFixes => Fixes.Count > 0;
    public bool HasNoFixes => Fixes.Count == 0;
    public bool HasMitigations => Mitigations.Count > 0;
    public bool HasNoMitigations => Mitigations.Count == 0;

    public bool HasScanned => _context is not null;
    public bool Step1Done => HasScanned;
    public string Step1Detail => HasScanned
        ? "Leitura feita às " + LastScanText
        : "Clique em \"Reler a máquina\" para começar.";
    public bool Step2Done => PendingCount > 0;
    public string Step2Detail => PendingCount > 0
        ? PendingCountText
        : "Marque correções, mitigações ou ajustes do O&O.";
    public bool Step3Done => NeedReboot;
    public string Step3Detail => NeedReboot
        ? "Reinicie o PC para concluir."
        : HasSessionChanges
            ? "Tudo aplicado nesta sessão."
            : "Aplique e reinicie se algo exigir.";


    public bool IsLogExpanded
    {
        get => _isLogExpanded;
        set => SetProperty(ref _isLogExpanded, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    public string LastScanText
    {
        get => _lastScanText;
        private set
        {
            if (SetProperty(ref _lastScanText, value))
            {
                OnPropertyChanged(nameof(LastScanDisplay));
                OnPropertyChanged(nameof(LastScanLabel));
                OnPropertyChanged(nameof(Step1Detail));
            }
        }
    }

    public string OsText { get => _osText; private set => SetProperty(ref _osText, value); }
    public string OsDetail { get => _osDetail; private set => SetProperty(ref _osDetail, value); }
    public string MachineTitle { get => _machineTitle; private set => SetProperty(ref _machineTitle, value); }
    public string CpuText { get => _cpuText; private set => SetProperty(ref _cpuText, value); }
    public string CpuDetail { get => _cpuDetail; private set => SetProperty(ref _cpuDetail, value); }
    public string RamText { get => _ramText; private set => SetProperty(ref _ramText, value); }
    public string RamDetail { get => _ramDetail; private set => SetProperty(ref _ramDetail, value); }
    public string GpuText { get => _gpuText; private set => SetProperty(ref _gpuText, value); }
    public string GpuDetail { get => _gpuDetail; private set => SetProperty(ref _gpuDetail, value); }
    public string Cs2Text { get => _cs2Text; private set => SetProperty(ref _cs2Text, value); }
    public string Cs2Detail { get => _cs2Detail; private set => SetProperty(ref _cs2Detail, value); }
    public string StorageText { get => _storageText; private set => SetProperty(ref _storageText, value); }
    public string PowerText { get => _powerText; private set => SetProperty(ref _powerText, value); }
    public string HvciText { get => _hvciText; private set => SetProperty(ref _hvciText, value); }
    public string MitigationSummary { get => _mitigationSummary; private set => SetProperty(ref _mitigationSummary, value); }
    public Brush MitigationSummaryBrush { get => _mitigationSummaryBrush; private set => SetProperty(ref _mitigationSummaryBrush, value); }
    public string MitigationStateText { get => _mitigationStateText; private set => SetProperty(ref _mitigationStateText, value); }
    public string RecommendedSummary { get => _recommendedSummary; private set => SetProperty(ref _recommendedSummary, value); }

    public bool NeedReboot
    {
        get => _needReboot;
        private set
        {
            if (SetProperty(ref _needReboot, value))
            {
                OnPropertyChanged(nameof(NeedRebootText));
                OnPropertyChanged(nameof(RebootNoticeText));
                OnPropertyChanged(nameof(Step3Done));
                OnPropertyChanged(nameof(Step3Detail));
            }
        }
    }

    public string NeedRebootText => NeedReboot ? "Reinício pendente" : "";

    public string RebootNoticeText => NeedReboot
        ? "Reinício pendente: parte do que foi aplicado só passa a valer depois de reiniciar o PC."
        : "";

    public bool IsAdmin { get; }
    public string AdminText => IsAdmin ? "Administrador" : "SEM ADMINISTRADOR";
    public Brush AdminBrush => IsAdmin ? UiBrushes.Safe : UiBrushes.Risky;
    public string VersionText =>
        "v" + (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0");

    public static string ProjectUrl => $"https://github.com/{UpdateChecker.DefaultRepository}";

    /// <summary>Aparece a barra de "nova versão" no topo (nunca uma caixa de diálogo).</summary>
    public bool HasUpdate
    {
        get => _hasUpdate;
        private set => SetProperty(ref _hasUpdate, value);
    }

    public string UpdateTitle
    {
        get => _updateTitle;
        private set => SetProperty(ref _updateTitle, value);
    }

    public string UpdateDetail
    {
        get => _updateDetail;
        private set => SetProperty(ref _updateDetail, value);
    }

    /// <summary>Estado da última verificação, mostrado no cartão de atualizações.</summary>
    public string UpdateStatusText
    {
        get => _updateStatusText;
        private set => SetProperty(ref _updateStatusText, value);
    }

    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        private set
        {
            if (!SetProperty(ref _isCheckingUpdate, value)) return;
            CheckUpdatesCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Opção do usuário: verificar sozinho ao abrir (1x por dia).</summary>
    public bool AutoCheckUpdates
    {
        get => _updateSettings.CheckOnStart;
        set
        {
            if (_updateSettings.CheckOnStart == value) return;
            _updateSettings.CheckOnStart = value;
            _updateSettings.Save();
            OnPropertyChanged();
            UpdateStatusText = value
                ? "Verificação automática ligada (ao abrir, no máximo 1x por dia)."
                : "Verificação automática desligada. Use \"Verificar agora\" quando quiser.";
        }
    }

    public int FixCount => Fixes.Count;
    public int RecommendedCount => Fixes.Count(f => f.IsRecommended);

    // ---- limpeza rápida ----
    public bool QuickCleanEmpty => QuickClean.Count == 0;
    public int QuickCleanSelectedCount => QuickClean.Count(i => i.IsSelected);
    public string QuickCleanSummary => QuickClean.Count == 0
        ? "Nenhum cache encontrado para limpar."
        : $"{QuickCleanSelectedCount} de {QuickClean.Count} cache(s) marcado(s).";
    public string QuickCleanActionText => QuickCleanSelectedCount == 0
        ? "Nenhum cache marcado"
        : $"Limpar {QuickCleanSelectedCount} cache(s)";

    public int PendingRecommendedOosuCount =>
        Oosu10Tweaks.Count(t => t.InPreset && t.Advice != Oosu10Advice.Evitar && !t.IsActive);
    /// <summary>Itens do O&O sugeridos para CS2 (preset), sem os marcados como evitar.</summary>
    public int Oosu10RecommendedTotal =>
        Oosu10Tweaks.Count(t => t.InPreset && t.Advice != Oosu10Advice.Evitar);
    public bool HasRecommendedWork => RecommendedCount > 0 || PendingRecommendedOosuCount > 0;
    public string RecommendedActionText => HasRecommendedWork
        ? $"Aplicar recomendadas ({RecommendedCount + PendingRecommendedOosuCount})"
        : "Aplicar recomendadas";
    public int SelectedCount => Fixes.Count(f => f.IsSelected);
    public int ProblemCount => Problems.Count;
    public bool HasProblems => Problems.Count > 0;
    public int SelectedMitigationCount => Mitigations.Count(m => m.IsSelected && m.IsCompatible);
    public string SelectedCountText => $"Aplicar selecionadas ({SelectedCount})";
    public string SelectedMitigationCountText => $"Aplicar selecionadas ({SelectedMitigationCount})";
    public string ProblemCountText => $"{ProblemCount} ponto(s) de atenção.";
    public string LastScanDisplay => "Leitura: " + LastScanText;
    public string LastScanLabel => "Última leitura: " + LastScanText;

    public FixItemViewModel? SelectedFix
    {
        get => _selectedFix;
        set => SetProperty(ref _selectedFix, value);
    }

    public MitigationProfileViewModel? SelectedMitigation
    {
        get => _selectedMitigation;
        set
        {
            if (SetProperty(ref _selectedMitigation, value))
                OnPropertyChanged(nameof(SelectedMitigationExplanation));
        }
    }

    public string SelectedMitigationExplanation => SelectedMitigation?.Explanation ?? "";

    public BackupInfo? SelectedBackup
    {
        get => _selectedBackup;
        set
        {
            if (SetProperty(ref _selectedBackup, value))
            {
                OnPropertyChanged(nameof(HasSelectedBackup));
                RestoreSelectedBackupCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedBackup => SelectedBackup is not null;
    public bool HasBackups => Backups.Count > 0;
    public bool HasNoBackups => Backups.Count == 0;

    public IReadOnlyList<string> Oosu10GroupOptions { get; } =
        new[] { Oosu10AllGroups }.Concat(Oosu10Groups.Order).ToArray();

    public string Oosu10Search
    {
        get => _oosu10Search;
        set
        {
            if (SetProperty(ref _oosu10Search, value))
                ApplyOosu10Filter();
        }
    }

    public string Oosu10Group
    {
        get => _oosu10Group;
        set
        {
            if (SetProperty(ref _oosu10Group, value ?? Oosu10AllGroups))
                ApplyOosu10Filter();
        }
    }

    public bool Oosu10OnlyPending
    {
        get => _oosu10OnlyPending;
        set
        {
            if (SetProperty(ref _oosu10OnlyPending, value))
                ApplyOosu10Filter();
        }
    }

    public Oosu10TweakViewModel? SelectedOosu10
    {
        get => _selectedOosu10;
        set => SetProperty(ref _selectedOosu10, value);
    }

    public int Oosu10SelectedCount => Oosu10Tweaks.Count(t => t.IsSelected);
    public string Oosu10SelectedCountText => $"Aplicar selecionados ({Oosu10SelectedCount})";
    public int Oosu10ActiveCount => Oosu10Tweaks.Count(t => t.IsActive);
    public int Oosu10PresetCount => Oosu10Tweaks.Count(t => t.InPreset);
    public string Oosu10Summary
    {
        get => _oosu10Summary;
        private set => SetProperty(ref _oosu10Summary, value);
    }

    public bool HasOosu10Items => Oosu10Visible.Count > 0;
    public bool HasNoOosu10Items => Oosu10Visible.Count == 0;

    public async Task InitializeAsync()
    {
        await ScanAsync();

        // Depois da primeira leitura, sem travar a janela: a checagem de versão é discreta.
        _ = CheckForUpdatesAsync(false);
    }

    private void BeginOperation(string title, string detail, int? total = null)
    {
        BusyTitle = title;
        BusyDetail = detail;
        IsProgressIndeterminate = total is null;
        ProgressMaximum = total ?? 100;
        ProgressValue = 0;
        OnPropertyChanged(nameof(ProgressPercentText));
    }

    private void UpdateOperationDetail(string detail) => BusyDetail = detail;

    private void ReportProgress(int done, int total)
    {
        Post(() =>
        {
            IsProgressIndeterminate = false;
            ProgressMaximum = Math.Max(total, 1);
            ProgressValue = Math.Clamp(done, 0, ProgressMaximum);
            OnPropertyChanged(nameof(ProgressPercentText));
        });
    }

    private void EndOperation()
    {
        BusyTitle = "";
        BusyDetail = "";
        ProgressValue = 0;
        ProgressMaximum = 100;
        IsProgressIndeterminate = true;
        OnPropertyChanged(nameof(ProgressPercentText));
    }

    private void RefreshPendingChanges()
    {
        PendingChanges.Clear();

        foreach (var fix in Fixes.Where(f => f.IsSelected))
            PendingChanges.Add(new PendingChangeViewModel(
                "Correção", fix.Title, fix.LevelDisplay, fix.LevelBrush, AppTabs.Correcoes));

        foreach (var mitigation in Mitigations.Where(m => m.IsSelected && m.IsCompatible))
            PendingChanges.Add(new PendingChangeViewModel(
                "Mitigação", mitigation.Name, mitigation.SecurityDisplay, mitigation.SecurityBrush, AppTabs.Mitigacoes));

        // Ajuste do O&O já ativo não é "pendência": foi aplicado antes.
        // Sem isso, depois de aplicar o painel continuava dizendo que havia o que aplicar.
        foreach (var tweak in Oosu10Tweaks.Where(t => t.IsSelected && !t.IsActive))
            PendingChanges.Add(new PendingChangeViewModel(
                "O&O", tweak.Title, tweak.AdviceDisplay, tweak.AdviceBrush, AppTabs.Oosu10));

        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(HasNoPendingChanges));
        OnPropertyChanged(nameof(PendingCountText));
        OnPropertyChanged(nameof(Step2Done));
        OnPropertyChanged(nameof(Step2Detail));
        ClearPendingCommand.RaiseCanExecuteChanged();
    }

    private void ClearAllPending()
    {
        _suppressSelectionRefresh = true;
        _suppressOosu10Refresh = true;
        try
        {
            foreach (var fix in Fixes)
                fix.IsSelected = false;
            foreach (var mitigation in Mitigations)
                mitigation.IsSelected = false;
            foreach (var tweak in Oosu10Tweaks)
                tweak.IsSelected = false;
        }
        finally
        {
            _suppressSelectionRefresh = false;
            _suppressOosu10Refresh = false;
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(SelectedMitigationCount));
        OnPropertyChanged(nameof(SelectedMitigationCountText));
        UpdateOosu10Summary();
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void RecordSessionChange(string action, string summary, string detail, Brush brush)
    {
        SessionChanges.Insert(0, new SessionChangeViewModel(DateTime.Now, action, summary, detail, brush));
        OnPropertyChanged(nameof(HasSessionChanges));
        OnPropertyChanged(nameof(HasNoSessionChanges));
        OnPropertyChanged(nameof(Step3Detail));
    }

    /// <summary>Texto do cartão principal: o que ainda falta aplicar (correções + O&O).</summary>
    private void UpdateRecommendedSummary()
    {
        var pendentesFix = RecommendedCount;
        var pendentesOosu = PendingRecommendedOosuCount;

        RecommendedSummary = FixCount == 0 && Oosu10Tweaks.Count == 0
            ? "Aguardando leitura da máquina."
            : pendentesFix + pendentesOosu == 0
                ? "Nada pendente: correções e ajustes sugeridos já estão aplicados nesta máquina."
                : pendentesFix > 0 && pendentesOosu > 0
                    ? $"{pendentesFix} correção(ões) recomendada(s) e {pendentesOosu} ajuste(s) do O&O ainda pendentes."
                    : pendentesFix > 0
                        ? $"{pendentesFix} correção(ões) recomendada(s) de {FixCount} aplicável(is)."
                        : $"{pendentesOosu} ajuste(s) do O&O sugeridos ainda inativos.";
    }

    private void UpdateHealthScore()
    {
        if (_context is null)
        {
            HealthScore = 0;
            HealthScoreDisplay = "-";
            HealthScoreLabel = "Aguardando leitura";
            HealthScoreBrush = UiBrushes.Muted;
        }
        else
        {
            // A nota mede só o que o programa consegue resolver: quanto das correções
            // e dos ajustes sugeridos do O&O ainda está pendente. Os "pontos de atenção"
            // (overlays abertos, mitigação reduzida por escolha sua...) têm cartão e aba
            // próprios e NÃO entram aqui - senão a nota nunca chega a 100.
            var pendentes = RecommendedCount + PendingRecommendedOosuCount;
            var total = FixCount + Oosu10RecommendedTotal;
            var penalty = total == 0
                ? 0
                : (int)Math.Round(pendentes * 100d / total, MidpointRounding.AwayFromZero);
            var score = Math.Clamp(100 - penalty, 0, 100);

            HealthScore = score;
            HealthScoreDisplay = score.ToString();
            HealthScoreLabel = score switch
            {
                100 => "Tudo aplicado",
                >= 85 => "Excelente",
                >= 70 => "Bom",
                >= 50 => "Ajustes pendentes",
                _ => "Atenção"
            };
            HealthScoreBrush = score switch
            {
                >= 85 => UiBrushes.Safe,
                >= 70 => UiBrushes.Accent,
                >= 50 => UiBrushes.Caution,
                _ => UiBrushes.Risky
            };
        }

        OnPropertyChanged(nameof(HasScanned));
        OnPropertyChanged(nameof(Step1Done));
        OnPropertyChanged(nameof(Step1Detail));
    }


    private async Task ScanAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        BeginOperation("Lendo a máquina",
            "CPU, memória, GPU, discos, Steam, registro, BCD, energia e mitigações.");
        try
        {
            await ScanCoreAsync();
        }
        catch (Exception ex)
        {
            Status = "Falha na leitura.";
            StatusDetail = ex.Message;
            _services.Logger.Bad("Falha ao ler a máquina: " + ex.Message);
            _interaction.ShowMessage("Falha na leitura", ex.ToString(), true);
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private async Task ScanCoreAsync()
    {
        Status = "Lendo a máquina...";
        StatusDetail = "CPU, memória, GPU, discos, Steam, registro, BCD, energia e mitigações.";
        var result = await _services.Engine.ScanAsync();
        _context = result.Context;
        LastScanText = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        UpdateMachineCards(result.Context);
        UpdateDiagnostics(result.Diagnostics);
        UpdateFixes(result.Fixes);
        RefreshMitigationState();
        RefreshBackups();
        RefreshOosu10State();
        // Reinício pendente é da sessão inteira: a releitura não pode "esquecer" o aviso.
        NeedReboot = NeedReboot || _services.Engine.NeedReboot;
        UpdateHealthScore();
        RefreshPendingChanges();
        Status = "Diagnóstico concluído.";
        StatusDetail = $"{FixCount} correção(ões) aplicável(is), {RecommendedCount} recomendada(s).";
    }

    private void UpdateMachineCards(FixContext ctx)
    {
        MachineTitle = string.Join(" · ", new[] { ctx.Machine, ctx.Board }.Where(s => !string.IsNullOrWhiteSpace(s)));
        OsText = string.IsNullOrWhiteSpace(ctx.OsCaption) ? "Windows" : ctx.OsCaption;
        OsDetail = ctx.BuildNumber > 0
            ? $"build {ctx.BuildNumber} · {(ctx.IsWin11 ? "Windows 11" : "Windows 10")}"
            : "build desconhecido";

        CpuText = string.IsNullOrWhiteSpace(ctx.Cpu.Name) ? "Não detectada" : ctx.Cpu.Name;
        CpuDetail = $"{ctx.Cpu.Cores} núcleos / {ctx.Cpu.LogicalProcessors} threads · {ctx.Cpu.MaxClockMHz} MHz";

        var totalBytes = ctx.Ram.Aggregate(0UL, (total, module) => total + module.CapacityBytes);
        var totalGb = Math.Round(totalBytes / 1024d / 1024d / 1024d, 1);
        RamText = ctx.Ram.Count == 0 ? "Não detectada" : $"{totalGb:0.0} GB em {ctx.Ram.Count} pente(s)";
        RamDetail = ctx.Ram.Count == 0
            ? ""
            : string.Join(" · ", ctx.Ram.Select(m =>
                $"{m.CapacityBytes / 1024d / 1024d / 1024d:0} GB @ {m.ConfiguredMHz} MHz"));

        var firstGpu = ctx.Gpus.FirstOrDefault();
        GpuText = firstGpu is null
            ? "Não detectada"
            : firstGpu.Name + (ctx.Gpus.Count > 1 ? $" (+{ctx.Gpus.Count - 1})" : "");
        GpuDetail = firstGpu is null
            ? ""
            : $"driver {firstGpu.DriverVersion} · {firstGpu.HorizontalResolution}x{firstGpu.VerticalResolution} @ {firstGpu.RefreshRate} Hz";

        Cs2Text = string.IsNullOrWhiteSpace(ctx.Cs2Exe) ? "cs2.exe não encontrado" : ctx.Cs2Exe;
        Cs2Detail = string.IsNullOrWhiteSpace(ctx.Steam.SteamPath)
            ? "Steam não encontrada no registro"
            : "Steam: " + ctx.Steam.SteamPath;

        StorageText = ctx.Volumes.Count == 0
            ? "Não detectado"
            : string.Join("  ·  ", ctx.Volumes.Select(v =>
                $"{v.Letter}: {v.MediaType} {v.FreePercent:0}% livre"));

        PowerText = ctx.OnPerformancePowerPlan
            ? "Plano de alto desempenho ativo"
            : "Plano padrão/economia de energia";

        HvciText = !ctx.HvciRead
            ? "Estado não lido"
            : ctx.HvciActive
                ? "Integridade de memória ATIVA (custa alguns % de FPS)"
                : "Integridade de memória desligada";
    }

    private void UpdateDiagnostics(DiagnosticResult result)
    {
        Diagnostics.Clear();
        foreach (var entry in result.Entries)
            Diagnostics.Add(new DiagnosticItemViewModel(entry));

        Problems.Clear();
        foreach (var problem in result.Problems)
            Problems.Add(problem);

        // O checklist é reconstruído a cada leitura; preserva o que o usuário já marcou
        // (senão "Reler a máquina" apagava as marcas).
        var done = Checklist.Where(c => c.IsDone)
            .Select(c => c.Text)
            .ToHashSet(StringComparer.Ordinal);

        Checklist.Clear();
        foreach (var item in result.Manual)
            Checklist.Add(new ChecklistItemViewModel(item) { IsDone = done.Contains(item) });

        OnPropertyChanged(nameof(ProblemCount));
        OnPropertyChanged(nameof(HasProblems));
        OnPropertyChanged(nameof(ProblemCountText));
        UpdateHealthScore();
    }

    private void UpdateFixes(IReadOnlyList<FixDefinition> fixes)
    {
        foreach (var old in Fixes)
            old.PropertyChanged -= FixItem_PropertyChanged;

        Fixes.Clear();
        foreach (var definition in fixes)
        {
            var item = new FixItemViewModel(definition);
            item.PropertyChanged += FixItem_PropertyChanged;
            Fixes.Add(item);
        }

        SelectedFix = Fixes.FirstOrDefault();
        OnPropertyChanged(nameof(FixCount));
        OnPropertyChanged(nameof(RecommendedCount));
        OnPropertyChanged(nameof(HasRecommendedWork));
        OnPropertyChanged(nameof(RecommendedActionText));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(HasFixes));
        OnPropertyChanged(nameof(HasNoFixes));
        UpdateRecommendedSummary();
        RefreshPendingChanges();
        UpdateQuickClean();
        UpdateHealthScore();
        RefreshCommands();
    }

    private void FixItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSelectionRefresh) return;
        if (e.PropertyName != nameof(FixItemViewModel.IsSelected)) return;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void SelectRecommended()
    {
        _suppressSelectionRefresh = true;
        try
        {
            foreach (var item in Fixes)
                item.IsSelected = item.IsRecommended;
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void ClearSelection()
    {
        _suppressSelectionRefresh = true;
        try
        {
            foreach (var item in Fixes)
                item.IsSelected = false;
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        RefreshPendingChanges();
        RefreshCommands();
    }

    /// <summary>
    /// Ação única do programa: aplica TUDO o que é recomendado — correções recomendadas
    /// e os ajustes do O&O sugeridos para CS2 que ainda estão inativos. Nada opcional,
    /// de risco ou marcado como "evitar" entra aqui.
    /// </summary>
    private async Task ApplyRecommendedAsync()
    {
        if (IsBusy) return;

        var fixes = Fixes.Where(f => f.IsRecommended).Select(f => f.Definition).ToList();
        var tweaks = Oosu10Tweaks
            .Where(t => t.InPreset && t.Advice != Oosu10Advice.Evitar && !t.IsActive)
            .Select(t => t.Tweak)
            .ToList();

        if (fixes.Count == 0 && tweaks.Count == 0)
        {
            _interaction.ShowMessage("Aplicar recomendadas",
                "Nada recomendado está pendente nesta máquina.\n\n" +
                "As correções recomendadas já estão aplicadas e os ajustes do O&O sugeridos para CS2 já estão ativos.");
            return;
        }

        var question = new StringBuilder();
        question.AppendLine("Aplicar tudo o que é recomendado nesta máquina?");
        question.AppendLine();
        question.AppendLine($"• Correções recomendadas: {fixes.Count}");
        question.AppendLine($"• Ajustes do O&O sugeridos para CS2 (ainda inativos): {tweaks.Count}");
        question.AppendLine();
        question.AppendLine("Itens opcionais, de risco e os marcados como \"evitar\" ficam de fora.");
        question.AppendLine("Cada escrita passa pelo backup e pode ser desfeita em Backups / Desfazer.");
        question.AppendLine("Se algo exigir reinício, o aviso aparece no fim.");

        if (!_interaction.Confirm(question.ToString(), FixLevel.Safe)) return;

        var total = fixes.Count + tweaks.Count;
        var appliedFixes = new List<string>();
        var failedFixes = new List<string>();
        var skippedFixes = new List<string>();
        var appliedTweaks = new List<string>();
        var failedTweaks = new List<string>();
        var needReboot = false;

        IsBusy = true;
        BeginOperation("Aplicar recomendadas",
            $"{total} item(ns): {fixes.Count} correção(ões) + {tweaks.Count} ajuste(s) do O&O.", total);
        try
        {
            if (!await EnsureBackupAsync())
            {
                Status = "Cancelado.";
                StatusDetail = "Nada foi alterado.";
                return;
            }

            var fixesTotal = 0;
            if (fixes.Count > 0)
            {
                Status = "Aplicando correções recomendadas...";
                UpdateOperationDetail($"{fixes.Count} correção(ões) recomendada(s).");
                var result = await _services.Engine.ApplyAsync(
                    fixes, _interaction,
                    new ApplyOptions { ConfirmBatch = false, SkipBatchConfirmation = true },
                    progress: (done, stageTotal) =>
                    {
                        fixesTotal = stageTotal;
                        ReportProgress(done, stageTotal + tweaks.Count);
                    });

                appliedFixes.AddRange(result.Applied);
                failedFixes.AddRange(result.Failed);
                skippedFixes.AddRange(result.Skipped);
                needReboot |= result.NeedReboot;
            }

            if (tweaks.Count > 0)
            {
                Status = "Aplicando ajustes do O&O ShutUp10...";
                UpdateOperationDetail($"{tweaks.Count} ajuste(s) sugeridos para CS2.");
                var oosu = await Task.Run(() => _services.Oosu10.Apply(
                    tweaks, restore: false,
                    log: msg => _services.Logger.Log(msg),
                    progress: (done, stageTotal) => ReportProgress(fixesTotal + done, fixesTotal + stageTotal)));

                appliedTweaks.AddRange(oosu.Applied);
                failedTweaks.AddRange(oosu.Failed);
                needReboot |= tweaks.Any(t => t.RequiresReboot);
            }

            if (_services.Backup.CurrentBackupDirectory is not null)
                _services.Journal.SaveTo(_services.Backup.CurrentBackupDirectory, _services.Logger.Log);

            if (appliedFixes.Count > 0)
                RecordSessionChange("Aplicado", $"{appliedFixes.Count} correção(ões) recomendada(s)",
                    string.Join(" · ", appliedFixes.Take(5)) +
                    (appliedFixes.Count > 5 ? $" · +{appliedFixes.Count - 5}" : ""),
                    UiBrushes.Safe);

            if (appliedTweaks.Count > 0)
                RecordSessionChange("Aplicado", $"{appliedTweaks.Count} ajuste(s) do O&O",
                    string.Join(" · ", appliedTweaks.Take(5)) +
                    (appliedTweaks.Count > 5 ? $" · +{appliedTweaks.Count - 5}" : ""),
                    UiBrushes.Safe);

            NeedReboot = needReboot || _services.Engine.NeedReboot;

            var summary = new StringBuilder();
            summary.AppendLine($"Correções aplicadas: {appliedFixes.Count}");
            summary.AppendLine($"Ajustes do O&O aplicados: {appliedTweaks.Count}");
            if (failedFixes.Count + failedTweaks.Count > 0)
                summary.AppendLine($"Falhas: {failedFixes.Count + failedTweaks.Count}");
            if (skippedFixes.Count > 0)
                summary.AppendLine($"Não confirmadas: {skippedFixes.Count}");

            if (failedFixes.Count + failedTweaks.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Falhas:");
                foreach (var item in failedFixes.Concat(failedTweaks)) summary.AppendLine("  - " + item);
            }

            summary.AppendLine();
            summary.AppendLine(NeedReboot
                ? "REINICIE O PC: parte destas alterações só passa a valer depois do reinício."
                : "Tudo aplicado. Nenhum reinício necessário.");

            var changed = appliedFixes.Count + appliedTweaks.Count;
            Status = changed > 0 ? "Recomendadas aplicadas." : "Nada foi alterado.";
            StatusDetail = changed > 0
                ? "A máquina será relida para atualizar o diagnóstico."
                : "Cancelado ou sem itens confirmados.";

            _interaction.ShowMessage("Aplicar recomendadas", summary.ToString().Trim(),
                failedFixes.Count + failedTweaks.Count > 0);

            if (changed > 0)
            {
                BusyTitle = "Reler a máquina";
                BusyDetail = "Atualizando o diagnóstico com o novo estado.";
                IsProgressIndeterminate = true;
                await ScanCoreAsync();
                // A releitura recalcula o aviso; o que exige reinício tem de continuar avisado.
                NeedReboot = needReboot || _services.Engine.NeedReboot;
            }
        }
        catch (Exception ex)
        {
            Status = "Falha ao aplicar as recomendadas.";
            StatusDetail = ex.Message;
            _services.Logger.Bad("Falha ao aplicar recomendadas: " + ex.Message);
            _interaction.ShowMessage("Aplicar recomendadas", ex.ToString(), true);
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private Task ApplySelectedAsync()
    {
        var selected = Fixes.Where(f => f.IsSelected).Select(f => f.Definition).ToList();
        if (selected.Count == 0)
        {
            _interaction.ShowMessage("Aplicar selecionadas", "Marque ao menos uma correção na lista.");
            return Task.CompletedTask;
        }

        return ApplyFixesAsync(selected, "Aplicar selecionadas");
    }

    private async Task ApplyFixesAsync(IReadOnlyList<FixDefinition> fixes, string title)
    {
        if (IsBusy) return;

        IsBusy = true;
        BeginOperation(title, $"{fixes.Count} item(ns) selecionado(s). Confirme e aguarde o backup.", fixes.Count);
        try
        {
            Status = "Aplicando alterações...";
            StatusDetail = "Confirme os itens e aguarde o backup.";
            var result = await _services.Engine.ApplyAsync(
                fixes, _interaction, new ApplyOptions { ConfirmBatch = true },
                progress: ReportProgress);

            NeedReboot = _services.Engine.NeedReboot || result.NeedReboot;

            if (result.Applied.Count > 0)
                RecordSessionChange("Aplicado", $"{result.Applied.Count} correção(ões)",
                    string.Join(" · ", result.Applied.Take(5)) +
                    (result.Applied.Count > 5 ? $" · +{result.Applied.Count - 5}" : ""),
                    UiBrushes.Safe);

            var summary = new StringBuilder();
            summary.AppendLine($"Aplicadas: {result.Applied.Count}");
            if (result.Skipped.Count > 0) summary.AppendLine($"Puladas: {result.Skipped.Count}");
            if (result.Failed.Count > 0) summary.AppendLine($"Falhas: {result.Failed.Count}");

            if (result.Applied.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Aplicadas:");
                foreach (var item in result.Applied) summary.AppendLine("  - " + item);
            }

            if (result.Skipped.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Puladas (não confirmadas):");
                foreach (var item in result.Skipped) summary.AppendLine("  - " + item);
            }

            if (result.Failed.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine("Falhas:");
                foreach (var item in result.Failed) summary.AppendLine("  - " + item);
            }

            if (result.NeedReboot)
            {
                summary.AppendLine();
                summary.AppendLine("Reinicie o PC para concluir as alterações que exigem reinício.");
            }

            Status = result.Applied.Count > 0 ? "Alterações aplicadas." : "Nada foi alterado.";
            StatusDetail = result.Applied.Count > 0
                ? "A máquina será relida para atualizar o diagnóstico."
                : "Cancelado ou sem itens confirmados.";
            _interaction.ShowMessage(title, summary.ToString().Trim(), result.Failed.Count > 0);

            if (result.Applied.Count > 0)
            {
                BusyTitle = "Reler a máquina";
                BusyDetail = "Atualizando o diagnóstico com o novo estado.";
                IsProgressIndeterminate = true;
                await ScanCoreAsync();
            }
        }
        catch (Exception ex)
        {
            Status = "Falha ao aplicar.";
            StatusDetail = ex.Message;
            _services.Logger.Bad("Falha ao aplicar correções: " + ex.Message);
            _interaction.ShowMessage("Falha ao aplicar", ex.ToString(), true);
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private async Task<bool> EnsureBackupAsync()
    {
        if (_services.Backup.BackupMade) return true;

        var choice = _interaction.AskRestorePoint();
        if (choice == RestorePointChoice.Cancel) return false;

        Status = "Criando backup...";
        StatusDetail = "Salvando registro, BCD e plano de energia.";
        UpdateOperationDetail("Criando backup do registro, BCD e plano de energia...");
        await _services.Backup.CreateBackupAsync(
            BackupService.NewStamp(), choice == RestorePointChoice.Yes);
        return true;
    }

    private async Task ApplyMitigationsAsync()
    {
        if (IsBusy) return;

        var selected = Mitigations.Where(m => m.IsSelected && m.IsCompatible).ToList();
        if (selected.Count == 0)
        {
            _interaction.ShowMessage("Mitigações de CPU", "Marque ao menos um perfil de mitigação.");
            return;
        }

        var keys = selected.Select(m => m.Key).ToList();
        var resolution = MitigationService.Resolve(keys);
        if (!resolution.Success)
        {
            _interaction.ShowMessage("Combinação inválida", resolution.Error ?? "Não foi possível combinar os perfis.", true);
            return;
        }

        var level = resolution.Security == MitigationSecurity.Reduzida
            ? FixLevel.Risky
            : resolution.Security == MitigationSecurity.Maior
                ? FixLevel.Optional
                : FixLevel.Safe;

        var question = new StringBuilder();
        question.AppendLine("Aplicar este perfil de mitigação de CPU?");
        question.AppendLine();
        question.AppendLine(resolution.Name);
        question.AppendLine();
        question.AppendLine(resolution.Explanation);
        question.AppendLine();
        question.AppendLine("Só passa a valer depois de reiniciar. O Desfazer devolve o estado anterior.");

        if (!_interaction.Confirm(question.ToString(), level)) return;

        IsBusy = true;
        BeginOperation("Aplicando mitigações", resolution.Name);
        try
        {
            if (!await EnsureBackupAsync())
            {
                Status = "Cancelado.";
                StatusDetail = "Nenhuma mitigação foi alterada.";
                return;
            }

            _services.Mitigation.Apply(keys);
            if (_services.Backup.CurrentBackupDirectory is not null)
                _services.Journal.SaveTo(_services.Backup.CurrentBackupDirectory, _services.Logger.Log);

            NeedReboot = true;
            RecordSessionChange("Mitigação", resolution.Name,
                "Perfil aplicado; reinicie o PC para valer.", UiBrushes.Caution);
            _services.Logger.Ok("Mitigações aplicadas: " + resolution.Name);
            RefreshMitigationState();
            Status = "Mitigações aplicadas.";
            StatusDetail = "Reinicie o PC para valer.";
            _interaction.ShowMessage("Mitigações de CPU",
                "Perfil aplicado: " + resolution.Name +
                "\n\nReinicie o PC para valer.\nUse Backups / Desfazer para voltar ao estado anterior.");

            BusyTitle = "Reler a máquina";
            BusyDetail = "Atualizando o estado das mitigações.";
            IsProgressIndeterminate = true;
            await ScanCoreAsync();
            NeedReboot = true;
            Status = "Mitigações aplicadas.";
            StatusDetail = "Reinicie o PC para valer.";
        }
        catch (Exception ex)
        {
            Status = "Falha ao aplicar mitigações.";
            StatusDetail = ex.Message;
            _services.Logger.Bad("Falha ao aplicar mitigações: " + ex.Message);
            _interaction.ShowMessage("Mitigações de CPU", ex.ToString(), true);
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private async Task ResetMitigationsAsync()
    {
        if (IsBusy) return;

        var confirmed = _interaction.Confirm(
            "Voltar ao padrão do Windows?\n\n" +
            "Isso APAGA FeatureSettingsOverride, FeatureSettingsOverrideMask e MinVmVersionForCpuBasedMitigations. " +
            "Apagar é diferente de gravar 0. O efeito só aparece depois de reiniciar.",
            FixLevel.Safe);
        if (!confirmed) return;

        IsBusy = true;
        BeginOperation("Voltando ao padrão", "Apagando as sobrescritas de mitigação do registro.");
        try
        {
            if (!await EnsureBackupAsync())
            {
                Status = "Cancelado.";
                StatusDetail = "Nenhuma mitigação foi alterada.";
                return;
            }

            _services.Mitigation.Apply(new[] { "padrao" });
            if (_services.Backup.CurrentBackupDirectory is not null)
                _services.Journal.SaveTo(_services.Backup.CurrentBackupDirectory, _services.Logger.Log);

            NeedReboot = true;
            RecordSessionChange("Padrão", "Mitigações voltaram ao padrão do Windows",
                "As sobrescritas foram apagadas; reinicie o PC.", UiBrushes.Accent);
            _services.Logger.Ok("Mitigações voltaram ao padrão do Windows.");
            RefreshMitigationState();
            Status = "Mitigações voltaram ao padrão.";
            StatusDetail = "Reinicie o PC para valer.";
            _interaction.ShowMessage("Mitigações de CPU",
                "As sobrescritas foram apagadas. O Windows volta a decidir sozinho.\n\n" +
                "Reinicie o PC para valer.");

            BusyTitle = "Reler a máquina";
            BusyDetail = "Atualizando o estado das mitigações.";
            IsProgressIndeterminate = true;
            await ScanCoreAsync();
            NeedReboot = true;
            Status = "Mitigações voltaram ao padrão.";
            StatusDetail = "Reinicie o PC para valer.";
        }
        catch (Exception ex)
        {
            Status = "Falha ao voltar ao padrão.";
            StatusDetail = ex.Message;
            _services.Logger.Bad("Falha ao voltar as mitigações ao padrão: " + ex.Message);
            _interaction.ShowMessage("Mitigações de CPU", ex.ToString(), true);
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private void RefreshMitigationState()
    {
        if (_context is null) return;

        var state = _services.Mitigation.GetState(_context.Cpu);
        MitigationStateText = _services.Mitigation.GetStateText(state);
        MitigationSummary = state.IsDefault
            ? "Padrão do Windows"
            : state.IsReduced
                ? "Proteção REDUZIDA"
                : "Personalizada";
        MitigationSummaryBrush = state.IsReduced
            ? UiBrushes.Risky
            : state.IsDefault
                ? UiBrushes.Safe
                : UiBrushes.Warning;

        var selectedKeys = Mitigations
            .Where(m => m.IsSelected)
            .Select(m => m.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _suppressSelectionRefresh = true;
        try
        {
            foreach (var old in Mitigations)
                old.PropertyChanged -= MitigationItem_PropertyChanged;

            Mitigations.Clear();
            foreach (var profile in MitigationService.Profiles)
            {
                var compatible = state.Vendor == "Desconhecido" ||
                                 profile.AppliesTo.Contains(state.Vendor, StringComparer.OrdinalIgnoreCase);
                var explanation = _services.Mitigation.GetProfileText(profile, state);
                var item = new MitigationProfileViewModel(profile, compatible, explanation);
                item.PropertyChanged += MitigationItem_PropertyChanged;
                if (compatible && selectedKeys.Contains(profile.Key))
                    item.IsSelected = true;
                Mitigations.Add(item);
            }
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        SelectedMitigation = Mitigations.FirstOrDefault(m => m.IsSelected) ?? Mitigations.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedMitigationCount));
        OnPropertyChanged(nameof(SelectedMitigationCountText));
        OnPropertyChanged(nameof(HasMitigations));
        OnPropertyChanged(nameof(HasNoMitigations));
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void MitigationItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSelectionRefresh) return;
        if (e.PropertyName != nameof(MitigationProfileViewModel.IsSelected)) return;
        OnPropertyChanged(nameof(SelectedMitigationCount));
        OnPropertyChanged(nameof(SelectedMitigationCountText));
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void ShowWindowsDefault() =>
        _interaction.ShowMessage("Padrão do Windows", _windowsDefaultText);

    private void ClearMitigationSelection()
    {
        _suppressSelectionRefresh = true;
        try
        {
            foreach (var item in Mitigations)
                item.IsSelected = false;
        }
        finally
        {
            _suppressSelectionRefresh = false;
        }

        OnPropertyChanged(nameof(SelectedMitigationCount));
        OnPropertyChanged(nameof(SelectedMitigationCountText));
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void RefreshOosu10State()
    {
        var selectedIds = Oosu10Tweaks
            .Where(t => t.IsSelected)
            .Select(t => t.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var keepId = SelectedOosu10?.Id;

        _suppressOosu10Refresh = true;
        try
        {
            foreach (var old in Oosu10Tweaks)
                old.PropertyChanged -= Oosu10Item_PropertyChanged;

            Oosu10Tweaks.Clear();
            foreach (var tweak in _services.Oosu10.All)
            {
                var item = new Oosu10TweakViewModel(tweak, _services.Oosu10.GetState(tweak));
                item.PropertyChanged += Oosu10Item_PropertyChanged;
                item.IsSelected = selectedIds.Contains(tweak.Id);
                Oosu10Tweaks.Add(item);
            }
        }
        finally
        {
            _suppressOosu10Refresh = false;
        }

        ApplyOosu10Filter(keepId);
        UpdateOosu10Summary();
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void ApplyOosu10Filter(string? preferredId = null)
    {
        var keepId = preferredId ?? SelectedOosu10?.Id;
        var search = (_oosu10Search ?? "").Trim();
        var group = _oosu10Group ?? Oosu10AllGroups;

        Oosu10Visible.Clear();
        foreach (var item in Oosu10Tweaks)
        {
            if (group != Oosu10AllGroups &&
                !string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase))
                continue;

            if (_oosu10OnlyPending && !item.IsPending)
                continue;

            if (search.Length > 0 &&
                item.Title.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                item.Detail.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                item.Group.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Oosu10Visible.Add(item);
        }

        SelectedOosu10 = Oosu10Visible.FirstOrDefault(i =>
                            string.Equals(i.Id, keepId, StringComparison.OrdinalIgnoreCase))
                        ?? Oosu10Visible.FirstOrDefault();

        OnPropertyChanged(nameof(HasOosu10Items));
        OnPropertyChanged(nameof(HasNoOosu10Items));
    }

    private void Oosu10Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressOosu10Refresh) return;
        if (e.PropertyName != nameof(Oosu10TweakViewModel.IsSelected)) return;
        UpdateOosu10Summary();
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void SelectOosu10Preset()
    {
        _suppressOosu10Refresh = true;
        try
        {
            foreach (var item in Oosu10Tweaks)
                item.IsSelected = item.InPreset && item.Advice != Oosu10Advice.Evitar;
        }
        finally
        {
            _suppressOosu10Refresh = false;
        }

        UpdateOosu10Summary();
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void ClearOosu10Selection()
    {
        _suppressOosu10Refresh = true;
        try
        {
            foreach (var item in Oosu10Tweaks)
                item.IsSelected = false;
        }
        finally
        {
            _suppressOosu10Refresh = false;
        }

        UpdateOosu10Summary();
        RefreshPendingChanges();
        RefreshCommands();
    }

    private void UpdateOosu10Summary()
    {
        OnPropertyChanged(nameof(Oosu10SelectedCount));
        OnPropertyChanged(nameof(Oosu10SelectedCountText));
        OnPropertyChanged(nameof(Oosu10ActiveCount));
        OnPropertyChanged(nameof(Oosu10PresetCount));
        OnPropertyChanged(nameof(PendingRecommendedOosuCount));
        OnPropertyChanged(nameof(HasRecommendedWork));
        OnPropertyChanged(nameof(RecommendedActionText));
        OnPropertyChanged(nameof(Oosu10RecommendedTotal));
        UpdateRecommendedSummary();
        UpdateHealthScore();
        Oosu10Summary = Oosu10Tweaks.Count == 0
            ? "Nenhum ajuste disponível."
            : $"{Oosu10ActiveCount} de {Oosu10Tweaks.Count} ajustes já estão ativos · " +
              $"{Oosu10PresetCount} sugeridos para CS2 · {Oosu10SelectedCount} selecionado(s).";
    }

    private async Task ApplyOosu10Async()
    {
        if (IsBusy) return;

        var selected = Oosu10Tweaks.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _interaction.ShowMessage("O&O ShutUp10", "Marque ao menos um ajuste na lista.");
            return;
        }

        if (!IsAdmin && selected.Any(t => t.NeedsAdmin))
        {
            _interaction.ShowMessage("O&O ShutUp10",
                "Há itens que gravam em HKLM (para todos os usuários).\n\n" +
                "Feche o programa e abra como administrador para aplicá-los, ou desmarque esses itens.",
                true);
            return;
        }

        var risky = selected.Count(t => t.Advice == Oosu10Advice.Evitar);
        var question = new StringBuilder();
        question.AppendLine($"Aplicar {selected.Count} ajuste(s) do O&O ShutUp10?");
        question.AppendLine();
        question.AppendLine("Recomendados: " + selected.Count(t => t.Advice == Oosu10Advice.Recomendado));
        question.AppendLine("Opcionais: " + selected.Count(t => t.Advice == Oosu10Advice.Opcional));
        if (risky > 0)
        {
            question.AppendLine();
            question.AppendLine($"ATENÇÃO: {risky} item(ns) marcados como \"evitar\" foram selecionados.");
            question.AppendLine("Eles reduzem proteção do Windows. Só prossiga se souber o que está fazendo.");
        }

        question.AppendLine();
        question.AppendLine("Tudo é salvo no backup antes e pode ser revertido.");

        if (!_interaction.Confirm(question.ToString(), risky > 0 ? FixLevel.Risky : FixLevel.Safe))
            return;

        IsBusy = true;
        BeginOperation("Aplicando O&O ShutUp10",
            $"{selected.Count} ajuste(s) selecionado(s). Confirme e aguarde o backup.", selected.Count);
        try
        {
            if (!await EnsureBackupAsync())
            {
                Status = "Cancelado.";
                StatusDetail = "Nenhum ajuste do O&O foi alterado.";
                return;
            }

            Status = "Aplicando ajustes do O&O ShutUp10...";
            StatusDetail = $"{selected.Count} ajuste(s) selecionado(s).";
            var tweaksToApply = selected.Select(t => t.Tweak).ToList();
            var result = await Task.Run(() => _services.Oosu10.Apply(
                tweaksToApply, restore: false,
                log: msg => _services.Logger.Log(msg),
                progress: ReportProgress));
            if (_services.Backup.CurrentBackupDirectory is not null)
                _services.Journal.SaveTo(_services.Backup.CurrentBackupDirectory, _services.Logger.Log);

            if (result.Applied.Count > 0)
            {
                _services.Logger.Ok($"O&O ShutUp10: {result.Applied.Count} ajuste(s) aplicado(s).");
                RecordSessionChange("Aplicado", $"{result.Applied.Count} ajuste(s) do O&O",
                    string.Join(" · ", result.Applied.Take(5)) +
                    (result.Applied.Count > 5 ? $" · +{result.Applied.Count - 5}" : ""),
                    UiBrushes.Safe);
            }

            NeedReboot = selected.Any(t => t.RequiresReboot) || NeedReboot;
            RefreshOosu10State();

            Status = result.Applied.Count > 0 ? "Ajustes do O&O aplicados." : "Nada foi alterado.";
            StatusDetail = result.Failed.Count > 0
                ? $"{result.Failed.Count} ajuste(s) falharam."
                : "Estado relido do registro.";

            _interaction.ShowMessage("O&O ShutUp10", BuildOosu10Report(result, "Aplicados"), result.Failed.Count > 0);
        }
        catch (Exception ex)
        {
            Status = "Falha ao aplicar ajustes do O&O.";
            StatusDetail = ex.Message;
            _services.Logger.Bad("Falha ao aplicar ajustes do O&O: " + ex.Message);
            _interaction.ShowMessage("O&O ShutUp10", ex.ToString(), true);
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private async Task RestoreOosu10Async()
    {
        if (IsBusy) return;

        var selected = Oosu10Tweaks.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            _interaction.ShowMessage("O&O ShutUp10", "Marque ao menos um ajuste para reverter.");
            return;
        }

        if (!IsAdmin && selected.Any(t => t.NeedsAdmin))
        {
            _interaction.ShowMessage("O&O ShutUp10",
                "Há itens que gravam em HKLM (para todos os usuários).\n\n" +
                "Feche o programa e abra como administrador para revertê-los, ou desmarque esses itens.",
                true);
            return;
        }

        var confirmed = _interaction.Confirm(
            $"Reverter {selected.Count} ajuste(s) selecionado(s)?\n\n" +
            "Cada item volta ao estado alternativo do O&O ou, quando não existe, o valor é removido. " +
            "As alterações são salvas antes, no backup.",
            FixLevel.Safe);
        if (!confirmed) return;

        IsBusy = true;
        BeginOperation("Revertendo O&O ShutUp10",
            $"{selected.Count} ajuste(s) selecionado(s). Confirme e aguarde o backup.", selected.Count);
        try
        {
            if (!await EnsureBackupAsync())
            {
                Status = "Cancelado.";
                StatusDetail = "Nenhum ajuste do O&O foi alterado.";
                return;
            }

            Status = "Revertendo ajustes do O&O ShutUp10...";
            StatusDetail = $"{selected.Count} ajuste(s) selecionado(s).";
            var tweaksToRestore = selected.Select(t => t.Tweak).ToList();
            var result = await Task.Run(() => _services.Oosu10.Apply(
                tweaksToRestore, restore: true,
                log: msg => _services.Logger.Log(msg),
                progress: ReportProgress));
            if (_services.Backup.CurrentBackupDirectory is not null)
                _services.Journal.SaveTo(_services.Backup.CurrentBackupDirectory, _services.Logger.Log);

            if (result.Applied.Count > 0)
            {
                _services.Logger.Ok($"O&O ShutUp10: {result.Applied.Count} ajuste(s) revertido(s).");
                RecordSessionChange("Revertido", $"{result.Applied.Count} ajuste(s) do O&O",
                    string.Join(" · ", result.Applied.Take(5)) +
                    (result.Applied.Count > 5 ? $" · +{result.Applied.Count - 5}" : ""),
                    UiBrushes.Caution);
            }

            RefreshOosu10State();

            Status = result.Applied.Count > 0 ? "Ajustes do O&O revertidos." : "Nada foi alterado.";
            StatusDetail = result.Failed.Count > 0
                ? $"{result.Failed.Count} ajuste(s) falharam."
                : "Estado relido do registro.";

            _interaction.ShowMessage("O&O ShutUp10", BuildOosu10Report(result, "Revertidos"), result.Failed.Count > 0);
        }
        catch (Exception ex)
        {
            Status = "Falha ao reverter ajustes do O&O.";
            StatusDetail = ex.Message;
            _services.Logger.Bad("Falha ao reverter ajustes do O&O: " + ex.Message);
            _interaction.ShowMessage("O&O ShutUp10", ex.ToString(), true);
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private static string BuildOosu10Report(Oosu10ApplyResult result, string appliedLabel)
    {
        var text = new StringBuilder();
        text.AppendLine($"{appliedLabel}: {result.Applied.Count}");
        text.AppendLine($"Escritas no registro: {result.Writes}");
        if (result.Failed.Count > 0)
            text.AppendLine($"Falhas: {result.Failed.Count}");

        if (result.Applied.Count > 0)
        {
            text.AppendLine();
            text.AppendLine(appliedLabel + ":");
            foreach (var item in result.Applied)
                text.AppendLine("  - " + item);
        }

        if (result.Failed.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Falhas:");
            foreach (var item in result.Failed)
                text.AppendLine("  - " + item);
        }

        text.AppendLine();
        text.AppendLine("Use Backups / Desfazer para voltar ao estado anterior.");
        return text.ToString().Trim();
    }

    private void RefreshBackups()
    {
        var selectedPath = SelectedBackup?.Path;
        Backups.Clear();
        foreach (var backup in _services.Engine.GetBackups())
            Backups.Add(backup);

        SelectedBackup = Backups.FirstOrDefault(b =>
                            string.Equals(b.Path, selectedPath, StringComparison.OrdinalIgnoreCase))
                        ?? Backups.FirstOrDefault();

        OnPropertyChanged(nameof(HasBackups));
        OnPropertyChanged(nameof(HasNoBackups));
    }

    private Task RestoreSelectedBackupAsync()
    {
        if (SelectedBackup is null) return Task.CompletedTask;
        return RestoreBackupAsync(SelectedBackup.Path);
    }

    private async Task RestoreFromFolderAsync()
    {
        if (IsBusy) return;
        var folder = _interaction.PickFolder("Escolha a pasta do backup", _services.Backup.BackupRoot);
        if (string.IsNullOrWhiteSpace(folder)) return;
        await RestoreBackupAsync(folder);
    }

    private async Task RestoreBackupAsync(string path)
    {
        if (IsBusy) return;
        if (!_interaction.ConfirmRestoreBackup(path)) return;

        IsBusy = true;
        BeginOperation("Restaurando backup", path);
        try
        {
            Status = "Restaurando backup...";
            StatusDetail = path;
            await _services.Engine.RestoreBackupAsync(path);
            _services.Backup.ResetSession();
            _services.Journal.Clear();
            NeedReboot = true;
            RecordSessionChange("Restaurado", "Backup restaurado",
                path, UiBrushes.Accent);
            Status = "Backup restaurado.";
            StatusDetail = "Reinicie o PC para concluir.";
            _interaction.ShowMessage("Backup restaurado",
                "Backup restaurado com sucesso.\n\nReinicie o PC.\n\n" + path);
            BusyTitle = "Reler a máquina";
            BusyDetail = "Atualizando o diagnóstico com o estado restaurado.";
            IsProgressIndeterminate = true;
            await ScanCoreAsync();
        }
        catch (Exception ex)
        {
            Status = "Falha ao restaurar.";
            StatusDetail = ex.Message;
            _services.Logger.Bad("Falha ao restaurar backup: " + ex.Message);
            _interaction.ShowMessage("Restaurar backup", ex.ToString(), true);
        }
        finally
        {
            IsBusy = false;
            EndOperation();
        }
    }

    private void OpenBackupFolder()
    {
        var path = SelectedBackup?.Path ?? _services.Backup.BackupRoot;
        try
        {
            if (!Directory.Exists(path))
            {
                _interaction.ShowMessage("Backups", "A pasta ainda não existe:\n\n" + path);
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _interaction.ShowMessage("Backups", ex.Message, true);
        }
    }

    private void SaveReport()
    {
        if (_context is null)
        {
            _interaction.ShowMessage("Relatório", "Faça uma leitura da máquina antes de salvar o relatório.");
            return;
        }

        var path = _interaction.SaveFile(
            "Salvar relatório",
            $"CS2-Optimizer-Relatorio-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            "Arquivos de texto (*.txt)|*.txt|Todos os arquivos (*.*)|*.*",
            ".txt");
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            var text = new StringBuilder();
            text.AppendLine("CS2 / Windows Optimizer - relatório");
            text.AppendLine("Gerado em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            text.AppendLine();
            text.AppendLine("Sistema ........ " + OsText + " (" + OsDetail + ")");
            text.AppendLine("Máquina/placa .. " + MachineTitle);
            text.AppendLine("CPU ............ " + CpuText + " (" + CpuDetail + ")");
            text.AppendLine("RAM ............ " + RamText + " (" + RamDetail + ")");
            text.AppendLine("GPU ............ " + GpuText + " (" + GpuDetail + ")");
            text.AppendLine("CS2 ............ " + Cs2Text);
            text.AppendLine("Armazenamento .. " + StorageText);
            text.AppendLine("Energia ........ " + PowerText);
            text.AppendLine("Mitigações ..... " + MitigationSummary);
            text.AppendLine();
            text.AppendLine(Cs2Tips.BuildReport(
                Diagnostics.Select(d => new DiagnosticEntry(d.Kind, d.Text)).ToArray(),
                Checklist.Select(c => c.Text).ToArray()));

            if (Problems.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("PROBLEMAS ENCONTRADOS");
                foreach (var problem in Problems)
                    text.AppendLine("  - " + problem);
            }

            File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
            _services.Logger.Ok("Relatório salvo em: " + path);
            _interaction.ShowMessage("Relatório", "Relatório salvo em:\n\n" + path);
        }
        catch (Exception ex)
        {
            _interaction.ShowMessage("Relatório", ex.ToString(), true);
        }
    }

    private void OpenLogFile()
    {
        var file = _services.Logger.LogFile;
        try
        {
            if (!File.Exists(file))
            {
                _interaction.ShowMessage("Log", "O arquivo de log ainda não existe:\n\n" + file);
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _interaction.ShowMessage("Log", ex.Message, true);
        }
    }

    private void OpenLogFolder()
    {
        var folder = Path.GetDirectoryName(_services.Logger.LogFile);
        if (string.IsNullOrWhiteSpace(folder)) return;
        try
        {
            if (!Directory.Exists(folder))
            {
                _interaction.ShowMessage("Log", "A pasta ainda não existe:\n\n" + folder);
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _interaction.ShowMessage("Log", ex.Message, true);
        }
    }

    private void OnLogEntry(LogEntry entry)
    {
        Post(() =>
        {
            LogItems.Add(new LogItemViewModel(entry));
            while (LogItems.Count > 3000)
                LogItems.RemoveAt(0);
        });
    }

    // ------------------------------------------------------------------ atualização

    private static Version AppVersion =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    /// <summary>
    /// Consulta a última release publicada. Nunca interrompe o usuário: o resultado
    /// aparece na barra do topo (nova versão) ou no cartão de atualizações.
    /// </summary>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (IsCheckingUpdate) return;

        if (!manual)
        {
            if (!_updateSettings.CheckOnStart) return;

            // automático no máximo 1x por dia, para não bater na API a cada abertura
            var ultima = _updateSettings.LastCheck;
            if (ultima is not null && DateTimeOffset.Now - ultima.Value < TimeSpan.FromHours(24)) return;
        }

        IsCheckingUpdate = true;
        UpdateStatusText = "Verificando atualizações...";
        try
        {
            var resultado = await UpdateChecker.CheckAsync(AppVersion);
            _updateSettings.LastCheck = DateTimeOffset.Now;
            _updateSettings.Save();
            ApplyUpdateResult(resultado, manual);
        }
        catch (Exception ex)
        {
            _services.Logger.Warn("Falha ao verificar atualização: " + ex.Message);
            UpdateStatusText = "Não foi possível verificar agora.";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    private void ApplyUpdateResult(UpdateCheckResult resultado, bool manual)
    {
        var quando = DateTime.Now.ToString("dd/MM 'às' HH:mm");

        switch (resultado.Status)
        {
            case UpdateCheckStatus.UpToDate:
                HasUpdate = false;
                _latestVersionTag = null;
                UpdateStatusText = $"Você está na versão mais recente ({VersionText}). Verificado em {quando}.";
                if (manual) _services.Logger.Ok("Nenhuma atualização disponível.");
                break;

            case UpdateCheckStatus.UpdateAvailable:
                var info = resultado.Update!;
                _latestVersionTag = info.Tag;
                _updateUrl = info.ReleaseUrl;

                if (string.Equals(_updateSettings.DismissedVersion, info.Tag, StringComparison.OrdinalIgnoreCase))
                {
                    HasUpdate = false;
                    UpdateStatusText = $"A versão {info.Tag} está disponível (aviso dispensado). Verificado em {quando}.";
                    break;
                }

                HasUpdate = true;
                UpdateTitle = $"Nova versão disponível: {info.Tag}";
                UpdateDetail = $"Você está na {VersionText}.";
                UpdateStatusText = $"Versão {info.Tag} disponível (você tem {VersionText}). Verificado em {quando}.";
                if (manual) _services.Logger.Ok($"Atualização disponível: {info.Tag}.");
                break;

            default:
                HasUpdate = false;
                UpdateStatusText = manual
                    ? "Não foi possível verificar agora (sem internet ou GitHub fora do ar). Tente de novo."
                    : "Verificação de atualização indisponível agora.";
                break;
        }
    }

    /// <summary>"Dispensar" esconde só esta versão; a próxima que sair aparece de novo.</summary>
    private void DismissUpdate()
    {
        if (!string.IsNullOrWhiteSpace(_latestVersionTag))
        {
            _updateSettings.DismissedVersion = _latestVersionTag;
            _updateSettings.Save();
        }

        HasUpdate = false;
        UpdateStatusText = "Aviso dispensado. Dá para baixar quando quiser em \"Abrir no GitHub\".";
    }

    private void OpenUpdatePage() => OpenUrl(_updateUrl);

    private void OpenProjectPage() => OpenUrl(ProjectUrl);

    private static void OpenUrl(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Sem navegador padrão: não é erro do programa.
        }
    }

    // ------------------------------------------------------------------ limpeza rápida

    /// <summary>Monta a lista da aba "Limpeza rápida" a partir do catálogo (categoria Limpeza).</summary>
    private void UpdateQuickClean()
    {
        // Preserva o que o usuário já tinha marcado/desmarcado antes de uma releitura.
        var marcados = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var antigo in QuickClean) marcados[antigo.Id] = antigo.IsSelected;

        foreach (var item in QuickClean) item.PropertyChanged -= OnQuickCleanItemChanged;
        QuickClean.Clear();

        foreach (var fix in Fixes)
        {
            var def = fix.Definition;
            if (!string.Equals(def.Category, FixCategories.Limpeza, StringComparison.Ordinal)) continue;
            if (string.Equals(def.Id, "trim", StringComparison.OrdinalIgnoreCase)) continue; // manutenção, não cache

            // Cache de shader existe na lista mas nunca vem marcado: apagar obriga o CS2 a recompilar.
            var selecionado = marcados.TryGetValue(def.Id, out var anterior)
                ? anterior
                : !string.Equals(def.Id, "shaders", StringComparison.OrdinalIgnoreCase);

            var novo = new QuickCleanItemViewModel(def, selecionado);
            novo.PropertyChanged += OnQuickCleanItemChanged;
            QuickClean.Add(novo);
        }

        RaiseQuickCleanChanged();
    }

    private void OnQuickCleanItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(QuickCleanItemViewModel.IsSelected) or null)
            RaiseQuickCleanChanged();
    }

    private void RaiseQuickCleanChanged()
    {
        OnPropertyChanged(nameof(QuickCleanEmpty));
        OnPropertyChanged(nameof(QuickCleanSelectedCount));
        OnPropertyChanged(nameof(QuickCleanSummary));
        OnPropertyChanged(nameof(QuickCleanActionText));
        ApplyQuickCleanCommand?.RaiseCanExecuteChanged();
        ClearQuickCleanCommand?.RaiseCanExecuteChanged();
        SelectAllQuickCleanCommand?.RaiseCanExecuteChanged();
    }

    private void SelectAllQuickClean()
    {
        foreach (var item in QuickClean) item.IsSelected = true;
    }

    private void ClearQuickClean()
    {
        foreach (var item in QuickClean) item.IsSelected = false;
    }

    private Task ApplyQuickCleanAsync()
    {
        var escolhidos = QuickClean.Where(i => i.IsSelected).Select(i => i.Definition).ToList();
        if (escolhidos.Count == 0)
        {
            _interaction.ShowMessage("Limpeza rápida", "Marque ao menos um cache para limpar.");
            return Task.CompletedTask;
        }

        return ApplyFixesAsync(escolhidos, "Limpeza rápida");
    }

    private void Post(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(new Action(action));
    }

    private void RefreshCommands()
    {
        ScanCommand.RaiseCanExecuteChanged();
        ApplyRecommendedCommand.RaiseCanExecuteChanged();
        ApplySelectedCommand.RaiseCanExecuteChanged();
        SelectRecommendedCommand.RaiseCanExecuteChanged();
        ClearSelectionCommand.RaiseCanExecuteChanged();
        ApplyMitigationsCommand.RaiseCanExecuteChanged();
        ResetMitigationsCommand.RaiseCanExecuteChanged();
        ClearMitigationSelectionCommand.RaiseCanExecuteChanged();
        RestoreSelectedBackupCommand.RaiseCanExecuteChanged();
        RestoreFromFolderCommand.RaiseCanExecuteChanged();
        SaveReportCommand.RaiseCanExecuteChanged();
        ClearPendingCommand.RaiseCanExecuteChanged();
        SelectOosu10PresetCommand.RaiseCanExecuteChanged();
        ClearOosu10SelectionCommand.RaiseCanExecuteChanged();
        ApplyOosu10Command.RaiseCanExecuteChanged();
        RestoreOosu10Command.RaiseCanExecuteChanged();
        ApplyQuickCleanCommand.RaiseCanExecuteChanged();
        ClearQuickCleanCommand.RaiseCanExecuteChanged();
        SelectAllQuickCleanCommand.RaiseCanExecuteChanged();
    }
}
