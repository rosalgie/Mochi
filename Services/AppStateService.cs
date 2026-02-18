using System;
using System.Threading.Tasks;
using Mochi.Domain;

namespace Mochi.Services;

public class AppStateService
{
    private readonly JsonFileStore _store = new();

    public AppConfig? CurrentConfig { get; private set; }
    public SaveData? CurrentSave { get; private set; }

    public async Task<AppConfig?> LoadConfigOrNullAsync()
    {
        AppPaths.EnsureAppFolderExists();
        CurrentConfig = await JsonFileStore.LoadAsync<AppConfig>(AppPaths.ConfigPath);
        return CurrentConfig;
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        AppPaths.EnsureAppFolderExists();
        await JsonFileStore.SaveAsync(AppPaths.ConfigPath, config);
        CurrentConfig = config;
    }

    public async Task<SaveData> LoadSaveOrCreateDefaultAsync()
    {
        AppPaths.EnsureAppFolderExists();

        SaveData? save = await JsonFileStore.LoadAsync<SaveData>(AppPaths.SavePath);

        if (save == null)
        {
            int startingWallet = CurrentConfig != null
                ? GameBalance.StartingWallet[CurrentConfig.Difficulty]
                : 10;
            save = SaveData.CreateDefault(startingWallet);
        }
        else
        {
            // Compute time away and add history entry for time user was away from pet
            DateTime now = DateTime.UtcNow;
            TimeSpan away = now - save.LastOpenedUtc;

            if (away.TotalMinutes >= 1)
            {
                string awayMessage = FormatTimeAway(away);
                save.History.Add(new HistoryEntry($"Returned after {awayMessage}"));

                // Apply stat decay for time away
                if (CurrentConfig != null)
                {
                    PetCareService.ApplyTimeAwayDecay(save.Pet, CurrentConfig, away);

                    // Return bonus coins (scaled by difficulty)
                    double coinMult = GameBalance.DifficultySettings[CurrentConfig.Difficulty].CoinMult;
                    int maxBonus = GameBalance.ReturnBonusMaxCoins[CurrentConfig.Difficulty];
                    int rawBonus = (int)(away.TotalMinutes / GameBalance.ReturnBonusIntervalMinutes)
                        * GameBalance.ReturnBonusCoinsPerInterval;
                    int bonus = Math.Min((int)Math.Round(rawBonus * coinMult), maxBonus);

                    if (bonus > 0)
                    {
                        save.WalletBalance += bonus;
                        save.Transactions.Add(new Transaction(
                            bonus, false, "ReturnBonus",
                            $"Welcome back bonus ({awayMessage} away)"));
                        save.History.Add(new HistoryEntry($"Earned {bonus} coins for returning"));
                    }
                }
            }

            // Update last opened time
            save.LastOpenedUtc = now;
        }

        await SaveSaveAsync(save);

        CurrentSave = save;
        return save;
    }

    public async Task SaveSaveAsync(SaveData save)
    {
        AppPaths.EnsureAppFolderExists();
        await JsonFileStore.SaveAsync(AppPaths.SavePath, save);
        CurrentSave = save;
    }

    private static string FormatTimeAway(TimeSpan away)
    {
        if (away.TotalDays >= 1)
        {
            int days = (int)away.TotalDays;
            return days == 1 ? "1 day" : $"{days} days";
        }

        if (away.TotalHours >= 1)
        {
            int hours = (int)away.TotalHours;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        int minutes = (int)away.TotalMinutes;
        return minutes == 1 ? "1 minute" : $"{minutes} minutes";
    }
}