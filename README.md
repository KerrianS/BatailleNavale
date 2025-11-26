# Bataille Navale - TP C#/.NET

## 👥 Groupe
- **SALAÜN Kerrian**
- **VIDAL Odilon**
- **DIMECK Raphaël**

## 📝 Description
Application de jeu de bataille navale développée en ASP.NET Core et Blazor WebAssembly avec communication gRPC-Web.

## 🏗️ Architecture
- **BattleShip.API** : API ASP.NET Core Minimal avec gRPC
- **BattleShip.App** : Client Blazor WebAssembly
- **BattleShip.Models** : Bibliothèque de modèles partagés

## 🚀 Technologies utilisées
- .NET 10.0
- ASP.NET Core Minimal API
- Blazor WebAssembly
- gRPC & gRPC-Web
- FluentValidation

## ✅ Fonctionnalités implémentées (Étapes 1-9)
- ✅ Placement aléatoire des bateaux (5 navires : 5,4,3,3,2 cases)
- ✅ Système d'attaque joueur vs IA
- ✅ Détection de fin de partie (13 coups réussis)
- ✅ Validation des coordonnées avec FluentValidation
- ✅ Communication REST + gRPC-Web
- ✅ Interface Blazor avec double grille (joueur + adversaire)
- ✅ IA avec attaque aléatoire
- ✅ Injection de dépendances pour le client gRPC

## 📋 Fonctionnalités à implémenter (TP)
- [ ] **Historique des batailles** - Afficher les coups joués et pouvoir revenir en arrière
- [ ] **Leaderboard** - Classement et statistiques des joueurs
- [ ] **Mode multi-joueur** - Entre deux joueurs humains avec SignalR
- [ ] **Recommencer une partie** - Bouton pour relancer sans recharger
- [ ] **Images des bateaux** - Remplacer les lettres par des sprites
- [ ] **Placement manuel** - Permettre au joueur de placer ses bateaux
- [ ] **Sécurité** - Authentification avec Auth0
- [ ] **IA améliorée** - Attaque intelligente par périmètre
- [ ] **Niveaux de difficulté** - Taille de grille et intelligence IA variables

## 🎮 Lancement
```bash
./start.sh
```

## 🌐 URLs
- **Application Blazor** : http://localhost:5208
- **API (gRPC + REST)** : http://localhost:5001
- **API (legacy REST)** : http://localhost:5224

## 📦 Structure du projet

```
BatailleNavale/
├── BattleShip.API/
│   ├── Services/
│   │   └── BattleshipGRPCService.cs
│   ├── Protos/
│   │   └── battleship.proto
│   ├── Validators/
│   │   └── AttackRequestValidator.cs
│   └── Program.cs
├── BattleShip.App/
│   ├── Services/
│   │   └── GameState.cs
│   ├── Pages/
│   │   └── Home.razor
│   └── Program.cs
├── BattleShip.Models/
│   ├── Cell.cs
│   ├── Board.cs
│   ├── Game.cs
│   └── AttackRequest.cs
├── start.sh
└── README.md
```

## 🎯 Règles du jeu
- Grille de 10x10
- 5 navires à placer : Porte-avions (5), Croiseur (4), Contre-torpilleur (3), Sous-marin (3), Torpilleur (2)
- **13 coups réussis** pour gagner (total des cases occupées)
- L'IA joue automatiquement après chaque coup du joueur

## 📧 Contact
Envoi du lien GitHub à : **contact@hts-learning.com**
