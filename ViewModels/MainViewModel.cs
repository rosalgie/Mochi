using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mochi.Domain;
using Mochi.Services;

namespace Mochi.ViewModels;

/// <summary>
///     Shell ViewModel that owns bottom-tab navigation and all child page ViewModels.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly AppStateService _appState;
    private readonly PetCareService _careService;
    private readonly DispatcherTimer _decayTimer;
    private readonly SaveData _save;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHomeSelected))]
    [NotifyPropertyChangedFor(nameof(IsCareSelected))]
    [NotifyPropertyChangedFor(nameof(IsStoreSelected))]
    [NotifyPropertyChangedFor(nameof(IsReportSelected))]
    [NotifyPropertyChangedFor(nameof(IsHelpSelected))]
    private ViewModelBase _currentPage;

    private int _tickCount;

    public MainViewModel(AppConfig config, SaveData save, AppStateService appState)
    {
        _appState = appState;
        _save = save;
        _careService = new PetCareService(save.Pet, config);

        HomeVm = new HomeViewModel(config, save);
        CareVm = new CareViewModel(config, save, appState, _careService);
        StoreVm = new StoreViewModel(save, appState);
        ReportVm = new ReportViewModel(config, save);
        HelpVm = new HelpViewModel();

        _currentPage = HomeVm;

        _decayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(GameBalance.DecayTickIntervalSeconds)
        };
        _decayTimer.Tick += OnDecayTick;
        _decayTimer.Start();
    }

    /// <summary>Design-time constructor.</summary>
    public MainViewModel() : this(
        new AppConfig { PetName = "Designer" },
        SaveData.CreateDefault(),
        new AppStateService())
    {
    }

    public HomeViewModel HomeVm { get; }
    public CareViewModel CareVm { get; }
    public StoreViewModel StoreVm { get; }
    public ReportViewModel ReportVm { get; }
    public HelpViewModel HelpVm { get; }

    public bool IsHomeSelected => CurrentPage == HomeVm;
    public bool IsCareSelected => CurrentPage == CareVm;
    public bool IsStoreSelected => CurrentPage == StoreVm;
    public bool IsReportSelected => CurrentPage == ReportVm;
    public bool IsHelpSelected => CurrentPage == HelpVm;

    private async void OnDecayTick(object? sender, EventArgs e)
    {
        _careService.ApplyDecay(_save.ActiveBuffs);
        _tickCount++;

        if (_tickCount % GameBalance.SaveEveryNTicks == 0) await _appState.SaveSaveAsync(_save);
    }

    [RelayCommand]
    private void NavigateHome()
    {
        CurrentPage = HomeVm;
    }

    [RelayCommand]
    private void NavigateCare()
    {
        CurrentPage = CareVm;
    }

    [RelayCommand]
    private void NavigateStore()
    {
        StoreVm.RefreshBalance();
        CurrentPage = StoreVm;
    }

    [RelayCommand]
    private void NavigateReport()
    {
        ReportVm.Refresh();
        CurrentPage = ReportVm;
    }

    [RelayCommand]
    private void NavigateHelp()
    {
        CurrentPage = HelpVm;
    }
}