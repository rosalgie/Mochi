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

    // cooldown durations (seconds)
    public static readonly Dictionary<CareAction, int> CooldownSeconds = new()
    {
        [CareAction.Feed] = 15,
        [CareAction.Play] = 30,
        [CareAction.Sleep] = 60,
        [CareAction.Clean] = 15
    };

    // diff config
    // ActionMult: scales care action effectiveness (higher = more effective)
    // DecayMult: scales how fast stats worsen (higher = faster decay)
    public static readonly Dictionary<Difficulty, (double ActionMult, double DecayMult)> DifficultySettings = new()
    {
        [Difficulty.Easy] = (1.3, 0.7),
        [Difficulty.Normal] = (1.0, 1.0),
        [Difficulty.Hard] = (0.7, 1.3)
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
}