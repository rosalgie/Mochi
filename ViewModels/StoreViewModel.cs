using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mochi.Domain;
using Mochi.Services;

namespace Mochi.ViewModels;

public partial class StoreViewModel : ViewModelBase
{
    private readonly AppStateService _appState;
    private readonly SaveData _save;
    [ObservableProperty] private string _statusMessage = "Choose an item to buy!";

    [ObservableProperty] private int _walletBalance;

    public StoreViewModel(SaveData save, AppStateService appState)
    {
        _save = save;
        _appState = appState;
        _walletBalance = save.WalletBalance;
    }

    /// <summary>Design-time constructor.</summary>
    public StoreViewModel() : this(SaveData.CreateDefault(), new AppStateService())
    {
    }

    public IReadOnlyList<StoreItemDefinition> StoreItems => GameBalance.StoreItems;

    [RelayCommand]
    private async Task Buy(StoreItemDefinition item)
    {
        if (_save.WalletBalance < item.Price)
        {
            StatusMessage = $"Not enough coins! Need ${item.Price}.";
            return;
        }

        _save.WalletBalance -= item.Price;
        WalletBalance = _save.WalletBalance;

        // Find existing buff or create new one
        ActiveBuff? existing = _save.ActiveBuffs.FirstOrDefault(b => b.ItemName == item.Name);
        bool isRefresh = existing != null;

        if (existing != null)
            existing.ExpiresUtc = DateTime.UtcNow.AddDays(item.DurationDays);
        else
            _save.ActiveBuffs.Add(new ActiveBuff(
                item.Name,
                DateTime.UtcNow.AddDays(item.DurationDays),
                item.HungerDecayMult,
                item.EnergyDecayMult,
                item.HappinessDecayMult));

        _save.Transactions.Add(new Transaction(
            item.Price, true, item.Category,
            $"Bought {item.Name}"));

        _save.History.Add(new HistoryEntry(
            $"Bought {item.Name} for ${item.Price}"));

        StatusMessage = isRefresh
            ? $"Refreshed {item.Name}! ({item.DurationDays}d)"
            : $"Bought {item.Name}! ({item.DurationDays}d)";

        await _appState.SaveSaveAsync(_save);
    }

    public void RefreshBalance()
    {
        WalletBalance = _save.WalletBalance;
    }
}