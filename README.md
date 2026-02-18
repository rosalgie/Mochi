# Mochi

Mochi is a virtual pet desktop app where you care for a puppy over time and track the cost of care through a coin economy, transaction history, and reports.

Built for FBLA: Introduction to Programming (2025-2026).

## Features
- **Customization**: choose a pet name, difficulty, and personality
- **Care actions**: feed, play, clean, and sleep with cooldowns
- **Reactions**: mood changes based on wellness (hunger/energy/happiness)
- **Economy**: earn coins through care, spend coins in the store for helpful items
- **Cost of care reporting**: totals, categories, and full transaction history
- **Persistence**: save/load pet state and history

## How to Run
### Requirements
- .NET SDK 9

### Run from command line
```bash
dotnet restore
dotnet run
````

### Build

```bash
dotnet build -c Release
```

### Build single .exe (large, includes .NET 9 Runtime)
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## How to Use

1. On first launch, choose a **name, difficulty, and personality**.
2. Use the **Care** tab to perform actions and improve stats.
3. Use the **Store** tab to buy items (expenses) that help care for your pet.
4. Use the **Report** tab to view the cost of care: totals, categories, and transaction history.
5. Use the **Help & Guide** tab for a full explanation of stats, mood, cooldowns, economy, and reporting.

## Documentation

- In-app user guide in the "❓ Help" page
- Code is organized using **MVVM**:
  - `Views/` (UI)
  - `ViewModels/` (UI state + commands)
  - `Services/` (simulation, persistence, business logic)
  - `Domain/` (data models)

## Credits / Attribution

See **CREDITS.md** for libraries and any external resources used.