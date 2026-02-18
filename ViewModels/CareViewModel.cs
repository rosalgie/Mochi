using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mochi.Domain;
using Mochi.Services;

namespace Mochi.ViewModels;

public partial class CareViewModel : ViewModelBase
{
    private readonly AppStateService _appState;
    private readonly PetCareService _careService;
    private readonly AppConfig _config;
    private readonly DispatcherTimer _cooldownTimer;
    private readonly SaveData _save;
    [ObservableProperty] private string _cleanCooldownText = "";
    [ObservableProperty] private string _feedCooldownText = "";
    [ObservableProperty] private string _lastActionMessage = "Choose an action to care for your pet!";

    [ObservableProperty] private PetState _petState;
    [ObservableProperty] private string _playCooldownText = "";
    [ObservableProperty] private string _sleepCooldownText = "";

    public CareViewModel(AppConfig config, SaveData save, AppStateService appState, PetCareService careService)
    {
        _config = config;
        _save = save;
        _appState = appState;
        _careService = careService;
        _petState = save.Pet;

        _petState.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PetState.IsAsleep))
            {
                OnPropertyChanged(nameof(SleepButtonLabel));
                RefreshCanExecute();
            }
        };

        _cooldownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _cooldownTimer.Tick += OnCooldownTick;
        _cooldownTimer.Start();
    }

    /// <summary>Design-time constructor.</summary>
    public CareViewModel() : this(
        new AppConfig { PetName = "Designer" },
        SaveData.CreateDefault(),
        new AppStateService(),
        new PetCareService(new PetState(), new AppConfig()))
    {
    }

    public string PetName => _config.PetName;
    public string SleepButtonLabel => PetState.IsAsleep ? "Wake Up" : "Sleep";

    private void OnCooldownTick(object? sender, EventArgs e)
    {
        FeedCooldownText = FormatCooldown(CareAction.Feed);
        PlayCooldownText = FormatCooldown(CareAction.Play);
        SleepCooldownText = FormatCooldown(CareAction.Sleep);
        CleanCooldownText = FormatCooldown(CareAction.Clean);
        RefreshCanExecute();
    }

    private void RefreshCanExecute()
    {
        FeedCommand.NotifyCanExecuteChanged();
        PlayCommand.NotifyCanExecuteChanged();
        SleepCommand.NotifyCanExecuteChanged();
        CleanCommand.NotifyCanExecuteChanged();
    }

    private string FormatCooldown(CareAction action)
    {
        int seconds = _careService.GetCooldownRemainingSeconds(action);
        return seconds > 0 ? $"{seconds}s" : "";
    }

    private bool CanFeed()
    {
        return !PetState.IsAsleep && _careService.CanPerformAction(CareAction.Feed);
    }

    private bool CanPlay()
    {
        return !PetState.IsAsleep && _careService.CanPerformAction(CareAction.Play);
    }

    private bool CanSleep()
    {
        return _careService.CanPerformAction(CareAction.Sleep);
    }

    private bool CanClean()
    {
        return !PetState.IsAsleep && _careService.CanPerformAction(CareAction.Clean);
    }

    [RelayCommand(CanExecute = nameof(CanFeed))]
    private async Task Feed()
    {
        await PerformCareAsync(CareAction.Feed);
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private async Task Play()
    {
        await PerformCareAsync(CareAction.Play);
    }

    [RelayCommand(CanExecute = nameof(CanSleep))]
    private async Task Sleep()
    {
        await PerformCareAsync(CareAction.Sleep);
    }

    [RelayCommand(CanExecute = nameof(CanClean))]
    private async Task Clean()
    {
        await PerformCareAsync(CareAction.Clean);
    }

    private async Task PerformCareAsync(CareAction action)
    {
        string? msg = _careService.PerformAction(action, PetName);
        if (msg == null) return;

        double coinMult = GameBalance.DifficultySettings[_config.Difficulty].CoinMult;
        int reward = Math.Max(1, (int)Math.Round(GameBalance.CareActionRewards[action] * coinMult));
        _save.WalletBalance += reward;
        _save.Transactions.Add(new Transaction(reward, false, "CareReward", msg));

        LastActionMessage = $"{msg} (+{reward} coins)";
        _save.History.Add(new HistoryEntry(msg));
        await _appState.SaveSaveAsync(_save);
    }
}