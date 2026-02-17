namespace Mochi.Domain;

public record StoreItemDefinition(
    string Name,
    int Price,
    string Category,
    int DurationDays,
    double HungerDecayMult,
    double EnergyDecayMult,
    double HappinessDecayMult
);