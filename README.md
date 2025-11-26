# Bataille Navale - ASP.NET Core MVC

Application web de Bataille Navale développée en ASP.NET Core 8.0 avec le pattern MVC.

## Structure du projet

```
BatailleNavale/
├── Controllers/
│   ├── HomeController.cs
│   └── GameController.cs
├── Models/
│   ├── Enums/
│   │   ├── CellState.cs
│   │   ├── GameStatus.cs
│   │   ├── Orientation.cs
│   │   └── ShipType.cs
│   ├── Board.cs
│   ├── Cell.cs
│   ├── Game.cs
│   ├── Player.cs
│   └── Ship.cs
├── Services/
│   ├── Interfaces/
│   │   └── IGameService.cs
│   └── GameService.cs
├── ViewModels/
│   ├── AttackViewModel.cs
│   ├── GameViewModel.cs
│   └── PlaceShipViewModel.cs
├── Views/
│   ├── Game/
│   │   ├── Create.cshtml
│   │   ├── Index.cshtml
│   │   ├── Join.cshtml
│   │   ├── Play.cshtml
│   │   └── Setup.cshtml
│   ├── Home/
│   │   └── Index.cshtml
│   └── Shared/
│       └── _Layout.cshtml
├── wwwroot/
│   └── css/
│       └── site.css
├── Program.cs
└── appsettings.json
```

## Fonctionnalités

- Création de partie
- Rejoindre une partie existante
- Placement des navires (5 types de navires)
- Jeu en tour par tour
- Détection des coups réussis et ratés
- Détection des navires coulés
- Détermination du gagnant

## Types de navires

1. Porte-avions (5 cases)
2. Croiseur (4 cases)
3. Contre-torpilleur (3 cases)
4. Sous-marin (3 cases)
5. Torpilleur (2 cases)

## Lancement du projet

```bash
cd BatailleNavale
dotnet restore
dotnet build
dotnet run
```

L'application sera accessible sur https://localhost:5001 ou http://localhost:5000

## Technologies utilisées

- ASP.NET Core 8.0
- C# 12
- Razor Pages
- Bootstrap 5
- JavaScript vanilla
