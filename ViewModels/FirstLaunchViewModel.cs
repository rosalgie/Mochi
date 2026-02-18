using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mochi.Domain;
using Mochi.Services;

namespace Mochi.ViewModels;

public partial class FirstLaunchViewModel(AppStateService appState) : ViewModelBase
{
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private string _petName = string.Empty;

    [ObservableProperty] private Difficulty _selectedDifficulty = Difficulty.Normal;

    [ObservableProperty] private Personality _selectedPersonality = Personality.Chill;

    [ObservableProperty] private string? _validationError;

    public FirstLaunchViewModel() : this(new AppStateService())
    {
    }

    public IReadOnlyList<Difficulty> Difficulties { get; } = Enum.GetValues<Difficulty>();
    public IReadOnlyList<Personality> Personalities { get; } = Enum.GetValues<Personality>();

    public event EventHandler? SetupCompleted;

    private bool CanStart()
    {
        return ValidatePetName(PetName) == null;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        string trimmedName = PetName.Trim();
        string? error = ValidatePetName(trimmedName);

        if (error != null)
        {
            ValidationError = error;
            return;
        }

        // Create and save config
        AppConfig config = new()
        {
            PetName = trimmedName,
            Difficulty = SelectedDifficulty,
            Personality = SelectedPersonality,
            CreatedUtc = DateTime.UtcNow
        };

        await appState.SaveConfigAsync(config);

        // Create and save default save data
        int startingWallet = GameBalance.StartingWallet[config.Difficulty];
        SaveData save = SaveData.CreateDefault(startingWallet);
        await appState.SaveSaveAsync(save);

        // Navigate to main
        SetupCompleted?.Invoke(this, EventArgs.Empty);
    }

    private static string? ValidatePetName(string name)
    {
        string trimmed = name.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
            return "Pet name cannot be empty.";

        if (trimmed.Length > 20)
            return "Pet name must be 20 characters or less.";

        return null;
    }

    partial void OnPetNameChanged(string value)
    {
        ValidationError = ValidatePetName(value);
    }
}