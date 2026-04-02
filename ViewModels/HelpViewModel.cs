using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mochi.Domain;
using Mochi.Services;

namespace Mochi.ViewModels;

public partial class HelpViewModel : ViewModelBase
{
    private readonly AppConfig _config;
    private readonly Action _navigateCare;
    private readonly Action _navigateHome;
    private readonly Action _navigateReport;
    private readonly Action _navigateStore;
    private readonly SaveData _save;

    [ObservableProperty] private string _askMochiAnswer =
        "Ask Mochi reads the current pet stats, buffs, and coins. Tap a question to get a live answer instantly.";

    [ObservableProperty] private string _askMochiPrompt = "Ask Mochi";

    private AskMochiTopic _selectedTopic = AskMochiTopic.None;
    private HelpDestination _suggestedDestination = HelpDestination.None;
    [ObservableProperty] private string _suggestedNavigationLabel = string.Empty;

    public HelpViewModel(
        AppConfig config,
        SaveData save,
        Action? navigateHome = null,
        Action? navigateCare = null,
        Action? navigateStore = null,
        Action? navigateReport = null)
    {
        _config = config;
        _save = save;
        _navigateHome = navigateHome ?? (() => { });
        _navigateCare = navigateCare ?? (() => { });
        _navigateStore = navigateStore ?? (() => { });
        _navigateReport = navigateReport ?? (() => { });

        _save.Pet.PropertyChanged += OnPetStateChanged;

        RefreshFromState();
    }

    /// <summary>Design-time constructor.</summary>
    public HelpViewModel() : this(new AppConfig { PetName = "Designer" }, SaveData.CreateDefault())
    {
    }

    public string LiveStatusSummary =>
        $"{_config.PetName} is {CurrentMoodText}. Hunger {_save.Pet.Hunger}/100, Energy {_save.Pet.Energy}/100, Happiness {_save.Pet.Happiness}/100.";

    public string LiveEconomySummary =>
        $"Wellness {WellnessScore:0.#} | Wallet ${_save.WalletBalance} | Transactions {_save.Transactions.Count}";

    public string ActiveBuffSummary => BuildActiveBuffSummary();

    public bool HasSuggestedNavigation => _suggestedDestination != HelpDestination.None;

    private string CurrentMoodText => PetCareService.CalculateMood(_save.Pet).ToString();

    private double WellnessScore => (_save.Pet.Energy + _save.Pet.Happiness + (100 - _save.Pet.Hunger)) / 3.0;

    [RelayCommand]
    private void AskWhatShouldIDoNext()
    {
        SetAnswer(AskMochiTopic.NextStep);
    }

    [RelayCommand]
    private void AskWhyIsMochiSad()
    {
        SetAnswer(AskMochiTopic.SadReason);
    }

    [RelayCommand]
    private void AskHowDoBuffsWork()
    {
        SetAnswer(AskMochiTopic.Buffs);
    }

    [RelayCommand]
    private void AskHowIsMoodCalculated()
    {
        SetAnswer(AskMochiTopic.Mood);
    }

    [RelayCommand]
    private void AskHowDoesCostOfCareWork()
    {
        SetAnswer(AskMochiTopic.CostOfCare);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateSuggestedPage))]
    private void NavigateSuggestedPage()
    {
        switch (_suggestedDestination)
        {
            case HelpDestination.Home:
                _navigateHome();
                break;
            case HelpDestination.Care:
                _navigateCare();
                break;
            case HelpDestination.Store:
                _navigateStore();
                break;
            case HelpDestination.Report:
                _navigateReport();
                break;
        }
    }

    public void RefreshFromState()
    {
        OnPropertyChanged(nameof(LiveStatusSummary));
        OnPropertyChanged(nameof(LiveEconomySummary));
        OnPropertyChanged(nameof(ActiveBuffSummary));
        OnPropertyChanged(nameof(HasSuggestedNavigation));

        if (_selectedTopic != AskMochiTopic.None) UpdateAnswer();
    }

    private bool CanNavigateSuggestedPage()
    {
        return _suggestedDestination != HelpDestination.None;
    }

    private void OnPetStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshFromState();
    }

    private void SetAnswer(AskMochiTopic topic)
    {
        _selectedTopic = topic;
        UpdateAnswer();
    }

    private void UpdateAnswer()
    {
        (string prompt, string answer, HelpDestination destination, string buttonLabel) response = _selectedTopic switch
        {
            AskMochiTopic.NextStep => BuildNextStepAnswer(),
            AskMochiTopic.SadReason => BuildSadAnswer(),
            AskMochiTopic.Buffs => BuildBuffAnswer(),
            AskMochiTopic.Mood => BuildMoodAnswer(),
            AskMochiTopic.CostOfCare => BuildCostOfCareAnswer(),
            _ => ("Ask Mochi", AskMochiAnswer, HelpDestination.None, string.Empty)
        };

        AskMochiPrompt = response.prompt;
        AskMochiAnswer = response.answer;
        _suggestedDestination = response.destination;
        SuggestedNavigationLabel = response.buttonLabel;

        OnPropertyChanged(nameof(HasSuggestedNavigation));
        NavigateSuggestedPageCommand.NotifyCanExecuteChanged();
    }

    private (string Prompt, string Answer, HelpDestination Destination, string ButtonLabel) BuildNextStepAnswer()
    {
        PetState pet = _save.Pet;

        if (pet.IsAsleep)
        {
            string sleepingAnswer = pet.Energy >= 100
                ? $"{_config.PetName} is asleep but already fully rested. Open Care and tap Sleep once to wake up, then handle the next lowest stat."
                : $"{_config.PetName} is asleep with Energy {pet.Energy}/100. The best move is to stay on Care and let Sleep keep restoring energy before doing anything else.";

            return ("What should I do next?", sleepingAnswer, HelpDestination.Care, "Open Care Tab");
        }

        if (pet is { Hunger: <= 35, Energy: >= 65, Happiness: >= 65 })
            return (
                "What should I do next?",
                $"{_config.PetName} is in good shape overall, so Play is the best next move. It keeps Happiness high and gives the best coin reward while your stats are stable.",
                HelpDestination.Care,
                "Open Care Tab");

        int hungerNeed = pet.Hunger;
        int energyNeed = 100 - pet.Energy;
        int happinessNeed = 100 - pet.Happiness;

        if (hungerNeed >= energyNeed && hungerNeed >= happinessNeed)
            return (
                "What should I do next?",
                $"Feed is the best next move. Hunger is {pet.Hunger}/100, which is {_config.PetName}'s biggest problem right now. Open Care and tap Feed to bring it down fast.",
                HelpDestination.Care,
                "Open Care Tab");

        if (energyNeed >= happinessNeed)
            return (
                "What should I do next?",
                $"Sleep is the best next move. Energy is only {pet.Energy}/100, so rest will stabilize mood and make later play sessions safer.",
                HelpDestination.Care,
                "Open Care Tab");

        if (pet.Energy < 35)
            return (
                "What should I do next?",
                $"Clean is the safest next move. Happiness is {pet.Happiness}/100, but Energy is only {pet.Energy}/100, so cleaning helps without the bigger energy hit from playing.",
                HelpDestination.Care,
                "Open Care Tab");

        return (
            "What should I do next?",
            $"Play is the best next move. Happiness is {pet.Happiness}/100 and Energy is healthy at {pet.Energy}/100, so {_config.PetName} can afford a play session.",
            HelpDestination.Care,
            "Open Care Tab");
    }

    private (string Prompt, string Answer, HelpDestination Destination, string ButtonLabel) BuildSadAnswer()
    {
        PetState pet = _save.Pet;
        Mood mood = PetCareService.CalculateMood(pet);
        List<string> weakStats = [];

        if (pet.Hunger >= 60) weakStats.Add($"Hunger is high at {pet.Hunger}/100");
        if (pet.Energy <= 40) weakStats.Add($"Energy is low at {pet.Energy}/100");
        if (pet.Happiness <= 40) weakStats.Add($"Happiness is low at {pet.Happiness}/100");

        string causes = weakStats.Count > 0
            ? string.Join(", ", weakStats)
            : $"the weakest area right now is {DescribeWeakestNeed()}";

        string answer = mood switch
        {
            Mood.Sad or Mood.Miserable =>
                $"{_config.PetName} is {mood} because wellness is only {WellnessScore:0.#}. Right now {causes}. Fix that on Care with {BuildFixPlan()}.",
            Mood.Sleeping =>
                $"{_config.PetName} is sleeping, not sad. Under the hood the weakest area is still {DescribeWeakestNeed()}, so that is what you should fix after waking up.",
            _ =>
                $"{_config.PetName} is {mood}, not sad right now. If mood drops later, it will be because {causes}. The fastest fix would be {BuildFixPlan()}."
        };

        return ("Why is Mochi sad?", answer, HelpDestination.Care, "Open Care Tab");
    }

    private (string Prompt, string Answer, HelpDestination Destination, string ButtonLabel) BuildBuffAnswer()
    {
        List<ActiveBuff> activeBuffs = GetActiveBuffs();
        string activeBuffLine = activeBuffs.Count == 0
            ? $"{_config.PetName} has no active buffs right now."
            : $"Active right now: {string.Join(", ", activeBuffs.Select(FormatBuffStatus))}.";

        string combinedLine = activeBuffs.Count == 0
            ? "Buy buffs on the Store tab when you want slower decay."
            : BuildCombinedBuffSummary(activeBuffs);

        string answer =
            $"Buffs come from Store purchases and slow stat decay for a limited time. Different buffs stack by multiplying their decay rates together, but buying the same item again only refreshes its timer. {activeBuffLine} {combinedLine} You can also see the current active buff list in the Live Snapshot above.";

        return ("How do buffs work?", answer, HelpDestination.Store, "Open Store Tab");
    }

    private (string Prompt, string Answer, HelpDestination Destination, string ButtonLabel) BuildMoodAnswer()
    {
        PetState pet = _save.Pet;
        int fullness = 100 - pet.Hunger;
        Mood mood = PetCareService.CalculateMood(pet);

        string answer = pet.IsAsleep
            ? $"{_config.PetName} is showing Sleeping because sleep overrides the normal mood label. The wellness formula still uses Energy {pet.Energy}, Happiness {pet.Happiness}, and Fullness {fullness}, which comes out to {WellnessScore:0.#}. If {_config.PetName} were awake, that score would read as {MoodFromWellness(WellnessScore)}."
            : $"{_config.PetName}'s mood is {mood}. Wellness is the average of Energy {pet.Energy}, Happiness {pet.Happiness}, and Fullness {fullness} (that is 100 minus Hunger). That gives {WellnessScore:0.#}, which lands in the {MoodRangeLabel(mood)} range.";

        return ("How is mood calculated?", answer, HelpDestination.Home, "Open Home Tab");
    }

    private (string Prompt, string Answer, HelpDestination Destination, string ButtonLabel) BuildCostOfCareAnswer()
    {
        int totalEarned = _save.Transactions.Where(t => !t.IsExpense).Sum(t => t.Amount);
        int totalSpent = _save.Transactions.Where(t => t.IsExpense).Sum(t => t.Amount);
        int transactionCount = _save.Transactions.Count;
        var topCategory = _save.Transactions
            .Where(t => t.IsExpense)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(g => g.Total)
            .FirstOrDefault();

        string spendingLine = topCategory == null
            ? "You have not spent coins in the Store yet."
            : $"Your biggest spending category so far is {topCategory.Category} at ${topCategory.Total}.";

        string answer =
            $"Care actions and return bonuses earn coins, Store items spend them, and the Report tab shows the full history. Right now your wallet is ${_save.WalletBalance}, total earned is ${totalEarned}, total spent is ${totalSpent}, and there are {transactionCount} recorded transactions. {spendingLine}";

        return ("How does cost of care work?", answer, HelpDestination.Report, "Open Report Tab");
    }

    private string BuildActiveBuffSummary()
    {
        List<ActiveBuff> activeBuffs = GetActiveBuffs();
        return activeBuffs.Count == 0
            ? "Active buffs: none"
            : $"Active buffs: {string.Join(" | ", activeBuffs.Select(FormatBuffStatus))}";
    }

    private string BuildCombinedBuffSummary(IReadOnlyCollection<ActiveBuff> activeBuffs)
    {
        double hungerMult = 1.0;
        double energyMult = 1.0;
        double happinessMult = 1.0;

        foreach (ActiveBuff buff in activeBuffs)
        {
            hungerMult *= buff.HungerDecayMult;
            energyMult *= buff.EnergyDecayMult;
            happinessMult *= buff.HappinessDecayMult;
        }

        return
            $"Combined active multipliers right now are Hunger {hungerMult:0.##}x, Energy {energyMult:0.##}x, and Happiness {happinessMult:0.##}x decay.";
    }

    private string BuildFixPlan()
    {
        PetState pet = _save.Pet;

        if (pet.Hunger >= 60) return "feeding first";
        if (pet.Energy <= 40) return "sleeping first";
        if (pet is { Happiness: <= 40, Energy: < 35 }) return "cleaning first, then playing when energy is safer";
        if (pet.Happiness <= 40) return "playing first";

        return $"{BuildSimpleActionName(GetRecommendedAction())} first";
    }

    private string DescribeWeakestNeed()
    {
        PetState pet = _save.Pet;
        int hungerNeed = pet.Hunger;
        int energyNeed = 100 - pet.Energy;
        int happinessNeed = 100 - pet.Happiness;

        if (hungerNeed >= energyNeed && hungerNeed >= happinessNeed)
            return $"hunger at {pet.Hunger}/100";

        if (energyNeed >= happinessNeed)
            return $"energy at {pet.Energy}/100";

        return $"happiness at {pet.Happiness}/100";
    }

    private CareAction GetRecommendedAction()
    {
        PetState pet = _save.Pet;

        if (pet.IsAsleep || pet is { Hunger: <= 35, Energy: >= 65, Happiness: >= 65 })
            return CareAction.Play;

        int hungerNeed = pet.Hunger;
        int energyNeed = 100 - pet.Energy;
        int happinessNeed = 100 - pet.Happiness;

        if (hungerNeed >= energyNeed && hungerNeed >= happinessNeed) return CareAction.Feed;
        if (energyNeed >= happinessNeed) return CareAction.Sleep;
        return pet.Energy < 35 ? CareAction.Clean : CareAction.Play;
    }

    private List<ActiveBuff> GetActiveBuffs()
    {
        return _save.ActiveBuffs
            .Where(b => b.ExpiresUtc > DateTime.UtcNow)
            .OrderBy(b => b.ExpiresUtc)
            .ToList();
    }

    private string FormatBuffStatus(ActiveBuff buff)
    {
        TimeSpan remaining = buff.ExpiresUtc - DateTime.UtcNow;
        return $"{buff.ItemName} ({FormatTimeRemaining(remaining)} left)";
    }

    private static string BuildSimpleActionName(CareAction action)
    {
        return action switch
        {
            CareAction.Feed => "feeding",
            CareAction.Play => "playing",
            CareAction.Sleep => "sleeping",
            CareAction.Clean => "cleaning",
            _ => "checking the Care tab"
        };
    }

    private static string FormatTimeRemaining(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero) return "under 1m";
        if (remaining.TotalDays >= 1) return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
        if (remaining.TotalHours >= 1) return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"{Math.Max(1, remaining.Minutes)}m";
    }

    private static string MoodRangeLabel(Mood mood)
    {
        return mood switch
        {
            Mood.Ecstatic => "80+ wellness",
            Mood.Happy => "60-79 wellness",
            Mood.Content => "40-59 wellness",
            Mood.Sad => "20-39 wellness",
            Mood.Miserable => "below 20 wellness",
            _ => "sleeping"
        };
    }

    private static Mood MoodFromWellness(double wellness)
    {
        return wellness switch
        {
            >= GameBalance.MoodEcstaticThreshold => Mood.Ecstatic,
            >= GameBalance.MoodHappyThreshold => Mood.Happy,
            >= GameBalance.MoodContentThreshold => Mood.Content,
            >= GameBalance.MoodSadThreshold => Mood.Sad,
            _ => Mood.Miserable
        };
    }

    private enum AskMochiTopic
    {
        None,
        NextStep,
        SadReason,
        Buffs,
        Mood,
        CostOfCare
    }

    private enum HelpDestination
    {
        None,
        Home,
        Care,
        Store,
        Report
    }
}