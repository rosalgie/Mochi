using System;
using System.Collections.Generic;
using Mochi.Domain;

namespace Mochi.Services;

public static class GameBalance
{
    // background decay (per 3-minute tick)
    public const double BaseHungerDecayPerTick = 2.0;
    public const double BaseEnergyDecayPerTick = -2.0;
    public const double BaseHappinessDecayPerTick = -1.5;

    // while asleep: energy recovers, hunger still rises, happiness stable
    public const double SleepEnergyRecoveryPerTick = 12.0;
    public const double SleepHungerPerTick = 3.0;

    // time-away decay (per minute away)
    public const double TimeAwayHungerPerMinute = 0.15;
    public const double TimeAwayEnergyPerMinute = -0.1;
    public const double TimeAwayHappinessPerMinute = -0.08;
    public const int TimeAwayMaxMinutes = 1440; // Cap at 24 hours

    // decay tick interval and save frequency
    public const int DecayTickIntervalSeconds = 180;
    public const int SaveEveryNTicks = 2; // Save every 2 ticks (~6 min)

    // mood thresholds (wellness score)
    public const double MoodEcstaticThreshold = 80.0;
    public const double MoodHappyThreshold = 60.0;
    public const double MoodContentThreshold = 40.0;

    public const double MoodSadThreshold = 20.0;

    // economy: return bonus
    public const int ReturnBonusCoinsPerInterval = 1;
    public const int ReturnBonusIntervalMinutes = 5;
    public const int ReturnBonusMaxCoinsBase = 50;

    // per-difficulty return bonus caps
    public static readonly Dictionary<Difficulty, int> ReturnBonusMaxCoins = new()
    {
        [Difficulty.Easy] = 75,
        [Difficulty.Normal] = 35,
        [Difficulty.Hard] = 20
    };

    // care action base effects
    // (HungerDelta, EnergyDelta, HappinessDelta)
    // Negative hunger = less hungry (good). Positive energy = more energy (good).
    public static readonly Dictionary<CareAction, (int Hunger, int Energy, int Happiness)> ActionEffects = new()
    {
        [CareAction.Feed] = (-25, +5, +5),
        [CareAction.Play] = (+10, -15, +20),
        [CareAction.Sleep] = (+5, +10, +5),
        [CareAction.Clean] = (0, -5, +15)
    };

    // cooldown durations (seconds) — base values, scaled by difficulty
    public static readonly Dictionary<CareAction, int> CooldownSeconds = new()
    {
        [CareAction.Feed] = 20,
        [CareAction.Play] = 40,
        [CareAction.Sleep] = 45,
        [CareAction.Clean] = 20
    };

    // difficulty cooldown multiplier (higher = longer cooldowns = slower income)
    public static readonly Dictionary<Difficulty, double> CooldownMultiplier = new()
    {
        [Difficulty.Easy] = 0.85,
        [Difficulty.Normal] = 1.0,
        [Difficulty.Hard] = 1.2
    };

    // starting wallet per difficulty
    public static readonly Dictionary<Difficulty, int> StartingWallet = new()
    {
        [Difficulty.Easy] = 15,
        [Difficulty.Normal] = 10,
        [Difficulty.Hard] = 5
    };

    // diff config
    // ActionMult: scales care action effectiveness (higher = more effective)
    // DecayMult: scales how fast stats worsen (higher = faster decay)
    // CoinMult: scales coin rewards (higher = more coins)
    public static readonly Dictionary<Difficulty, (double ActionMult, double DecayMult, double CoinMult)>
        DifficultySettings = new()
        {
            [Difficulty.Easy] = (1.3, 0.7, 1.3),
            [Difficulty.Normal] = (1.0, 1.0, 1.0),
            [Difficulty.Hard] = (0.7, 1.3, 0.5)
        };

    // personality action bonuses
    // flat bonuses added to specific actions AFTER difficulty scaling
    public static readonly Dictionary<(Personality, CareAction), (int Hunger, int Energy, int Happiness)>
        PersonalityActionBonuses = new()
        {
            [(Personality.Chill, CareAction.Sleep)] = (0, +5, 0),
            [(Personality.Energetic, CareAction.Play)] = (0, -5, +5),
            [(Personality.Anxious, CareAction.Clean)] = (0, 0, +5),
            [(Personality.Anxious, CareAction.Sleep)] = (0, +5, 0),
            [(Personality.Independent, CareAction.Feed)] = (-5, 0, 0),
            [(Personality.Independent, CareAction.Play)] = (0, 0, -5)
        };

    // personality decay modifiers
    // per-stat multipliers applied on top of difficulty decay
    public static readonly Dictionary<Personality, (double Hunger, double Energy, double Happiness)>
        PersonalityDecayMods = new()
        {
            [Personality.Chill] = (1.0, 1.0, 0.85),
            [Personality.Energetic] = (1.2, 1.2, 1.0),
            [Personality.Anxious] = (1.0, 1.0, 1.25),
            [Personality.Independent] = (0.85, 0.85, 0.85)
        };

    // economy: coins earned per care action (base, scaled by CoinMult)
    public static readonly Dictionary<CareAction, int> CareActionRewards = new()
    {
        [CareAction.Feed] = 4,
        [CareAction.Play] = 6,
        [CareAction.Sleep] = 2,
        [CareAction.Clean] = 4
    };

    // economy: Store items (temporary decay modifier buffs)
    public static readonly IReadOnlyList<StoreItemDefinition> StoreItems =
    [
        new("Premium Food", 15, "Food", 2, 0.5, 1.0, 1.0),
        new("Toy", 20, "Toys", 2, 1.0, 1.0, 0.5),
        new("Comfy Bed", 35, "Comfort", 3, 1.0, 0.5, 1.0),
        new("Medicine", 30, "Health", 1, 0.7, 0.7, 0.7)
    ];

    /// <summary>
    ///     Returns the effective cooldown for an action, scaled by difficulty.
    /// </summary>
    public static int GetEffectiveCooldown(CareAction action, Difficulty difficulty)
    {
        int baseCooldown = CooldownSeconds[action];
        double mult = CooldownMultiplier[difficulty];
        return Math.Max(1, (int)Math.Round(baseCooldown * mult));
    }
}