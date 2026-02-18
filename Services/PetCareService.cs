using System;
using System.Collections.Generic;
using Mochi.Domain;

namespace Mochi.Services;

public class PetCareService(PetState pet, AppConfig config)
{
    private readonly Dictionary<CareAction, DateTime> _lastActionTimes = new();

    /// <summary>
    ///     Performs a care action. Returns a UI message, or null if the action cannot be performed.
    /// </summary>
    public string? PerformAction(CareAction action, string petName)
    {
        // Sleep doubles as Wake Up when pet is asleep
        if (action == CareAction.Sleep && pet.IsAsleep)
        {
            pet.IsAsleep = false;
            _lastActionTimes[action] = DateTime.UtcNow;
            return $"{petName} woke up!";
        }

        // Can't interact with sleeping pet (except Sleep/WakeUp above)
        if (pet.IsAsleep)
            return null;

        if (!CanPerformAction(action))
            return null;

        // Apply base effects scaled by difficulty
        (int baseH, int baseE, int baseHap) = GameBalance.ActionEffects[action];
        (double actionMult, _, _) = GameBalance.DifficultySettings[config.Difficulty];

        int deltaH = (int)Math.Round(baseH * actionMult);
        int deltaE = (int)Math.Round(baseE * actionMult);
        int deltaHap = (int)Math.Round(baseHap * actionMult);

        // Apply personality bonuses (flat, after difficulty scaling)
        (Personality Personality, CareAction action) key = (config.Personality, action);
        if (GameBalance.PersonalityActionBonuses.TryGetValue(key, out (int Hunger, int Energy, int Happiness) bonus))
        {
            deltaH += bonus.Hunger;
            deltaE += bonus.Energy;
            deltaHap += bonus.Happiness;
        }

        pet.Hunger = Math.Clamp(pet.Hunger + deltaH, 0, 100);
        pet.Energy = Math.Clamp(pet.Energy + deltaE, 0, 100);
        pet.Happiness = Math.Clamp(pet.Happiness + deltaHap, 0, 100);

        // Sleep action puts pet to sleep
        if (action == CareAction.Sleep)
            pet.IsAsleep = true;

        _lastActionTimes[action] = DateTime.UtcNow;

        return action switch
        {
            CareAction.Feed => $"You fed {petName}!",
            CareAction.Play => $"You played with {petName}!",
            CareAction.Sleep => $"{petName} fell asleep...",
            CareAction.Clean => $"{petName} is all clean!",
            _ => null
        };
    }

    public bool CanPerformAction(CareAction action)
    {
        // Sleep always available as wake-up toggle when asleep
        if (action == CareAction.Sleep && pet.IsAsleep)
            return true;

        if (!_lastActionTimes.TryGetValue(action, out DateTime lastUse))
            return true;

        int cooldown = GameBalance.GetEffectiveCooldown(action, config.Difficulty);
        return (DateTime.UtcNow - lastUse).TotalSeconds >= cooldown;
    }

    public int GetCooldownRemainingSeconds(CareAction action)
    {
        if (action == CareAction.Sleep && pet.IsAsleep)
            return 0;

        if (!_lastActionTimes.TryGetValue(action, out DateTime lastUse))
            return 0;

        int cooldown = GameBalance.GetEffectiveCooldown(action, config.Difficulty);
        double elapsed = (DateTime.UtcNow - lastUse).TotalSeconds;
        return Math.Max((int)Math.Ceiling(cooldown - elapsed), 0);
    }

    /// <summary>
    ///     Called every tick by the background decay timer.
    /// </summary>
    public void ApplyDecay(List<ActiveBuff> activeBuffs)
    {
        // Remove expired buffs
        activeBuffs.RemoveAll(b => b.ExpiresUtc <= DateTime.UtcNow);

        // Compute combined buff multipliers
        double buffHunger = 1.0, buffEnergy = 1.0, buffHappiness = 1.0;
        foreach (ActiveBuff buff in activeBuffs)
        {
            buffHunger *= buff.HungerDecayMult;
            buffEnergy *= buff.EnergyDecayMult;
            buffHappiness *= buff.HappinessDecayMult;
        }

        (_, double diffDecay, _) = GameBalance.DifficultySettings[config.Difficulty];
        (double Hunger, double Energy, double Happiness) persDecay =
            GameBalance.PersonalityDecayMods[config.Personality];

        if (pet.IsAsleep)
        {
            pet.Energy = Math.Clamp(pet.Energy +
                                    (int)Math.Round(GameBalance.SleepEnergyRecoveryPerTick * diffDecay *
                                                    persDecay.Energy), 0, 100);
            pet.Hunger = Math.Clamp(pet.Hunger +
                                    (int)Math.Round(GameBalance.SleepHungerPerTick * diffDecay * persDecay.Hunger *
                                                    buffHunger), 0, 100);

            // Auto-wake when energy is full
            if (pet.Energy >= 100)
                pet.IsAsleep = false;
        }
        else
        {
            pet.Hunger = Math.Clamp(pet.Hunger +
                                    (int)Math.Round(GameBalance.BaseHungerDecayPerTick * diffDecay * persDecay.Hunger *
                                                    buffHunger), 0, 100);
            pet.Energy = Math.Clamp(pet.Energy +
                                    (int)Math.Round(GameBalance.BaseEnergyDecayPerTick * diffDecay * persDecay.Energy *
                                                    buffEnergy), 0, 100);
            pet.Happiness = Math.Clamp(pet.Happiness +
                                       (int)Math.Round(GameBalance.BaseHappinessDecayPerTick * diffDecay *
                                                       persDecay.Happiness * buffHappiness), 0, 100);
        }
    }

    /// <summary>
    ///     Applies stat decay for time the user was away. Called once on app launch.
    /// </summary>
    public static void ApplyTimeAwayDecay(PetState pet, AppConfig config, TimeSpan timeAway)
    {
        double minutes = Math.Min(timeAway.TotalMinutes, GameBalance.TimeAwayMaxMinutes);
        if (minutes < 1) return;

        (_, double diffDecay, _) = GameBalance.DifficultySettings[config.Difficulty];
        (double Hunger, double Energy, double Happiness) persDecay =
            GameBalance.PersonalityDecayMods[config.Personality];

        pet.Hunger = Math.Clamp(pet.Hunger +
                                (int)Math.Round(minutes * GameBalance.TimeAwayHungerPerMinute * diffDecay *
                                                persDecay.Hunger), 0, 100);
        pet.Energy = Math.Clamp(pet.Energy +
                                (int)Math.Round(minutes * GameBalance.TimeAwayEnergyPerMinute * diffDecay *
                                                persDecay.Energy), 0, 100);
        pet.Happiness = Math.Clamp(pet.Happiness +
                                   (int)Math.Round(minutes * GameBalance.TimeAwayHappinessPerMinute * diffDecay *
                                                   persDecay.Happiness), 0, 100);

        // Wake pet up on return
        pet.IsAsleep = false;
    }

    /// <summary>
    ///     Derives the current mood from pet stats.
    /// </summary>
    public static Mood CalculateMood(PetState pet)
    {
        if (pet.IsAsleep) return Mood.Sleeping;

        double wellness = (pet.Energy + pet.Happiness + (100 - pet.Hunger)) / 3.0;

        return wellness switch
        {
            >= GameBalance.MoodEcstaticThreshold => Mood.Ecstatic,
            >= GameBalance.MoodHappyThreshold => Mood.Happy,
            >= GameBalance.MoodContentThreshold => Mood.Content,
            >= GameBalance.MoodSadThreshold => Mood.Sad,
            _ => Mood.Miserable
        };
    }
}