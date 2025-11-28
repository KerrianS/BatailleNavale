using BattleShip.Models;
using Microsoft.AspNetCore.SignalR;
using BattleShip.API.Hubs;

namespace BattleShip.API.Services;

public class MultiplayerGameService
{
    private readonly IHubContext<GameHub> _hubContext;
    private static readonly Dictionary<string, MultiplayerGame> _games = new();
    private static readonly Dictionary<string, string> _playerToGame = new();
    private static readonly Dictionary<string, string> _playerNames = new();
    private static readonly Queue<string> _waitingPlayers = new();

    public MultiplayerGameService(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task<string> JoinGameAsync(string playerId, string playerName)
    {
        _playerNames[playerId] = playerName;

        if (_waitingPlayers.Count > 0)
        {
            // Match with waiting player
            string opponentId = _waitingPlayers.Dequeue();
            string gameId = Guid.NewGuid().ToString();
            string opponentName = _playerNames.GetValueOrDefault(opponentId, "Joueur 1");

            var game = new MultiplayerGame
            {
                Id = gameId,
                Player1Id = opponentId,
                Player2Id = playerId,
                Player1Name = opponentName,
                Player2Name = playerName,
                CurrentTurnPlayerId = opponentId,
                Mode = GameMode.Multiplayer,
                PlayerBoard = new Board(),
                Player2Board = new Board()
            };

            _games[gameId] = game;
            _playerToGame[opponentId] = gameId;
            _playerToGame[playerId] = gameId;

            await _hubContext.Clients.Client(opponentId).SendAsync("OpponentJoined", gameId, playerName);
            await _hubContext.Clients.Client(playerId).SendAsync("GameJoined", gameId, opponentName);

            return gameId;
        }
        else
        {
            // Wait for opponent
            _waitingPlayers.Enqueue(playerId);
            await _hubContext.Clients.Client(playerId).SendAsync("WaitingForOpponent");
            return "";
        }
    }

    public async Task PlaceShipsAsync(string playerId, string gameId, List<ShipPlacement> placements)
    {
        if (!_games.ContainsKey(gameId)) return;

        var game = _games[gameId];
        Board board = playerId == game.Player1Id ? game.PlayerBoard : game.Player2Board;

        // Clear existing ships
        board.Ships.Clear();
        for (int x = 0; x < board.CurrentSize; x++)
        {
            for (int y = 0; y < board.CurrentSize; y++)
            {
                board.Grid[x, y].HasShip = false;
                board.Grid[x, y].ShipType = null;
                board.Grid[x, y].IsShipStart = false;
            }
        }

        // Place ships on board
        foreach (var placement in placements)
        {
            board.PlaceShip(placement.X, placement.Y, placement.Size, placement.IsHorizontal, placement.ShipType);
        }

        // Mark player as ready
        if (playerId == game.Player1Id)
        {
            game.Player1Ready = true;
        }
        else
        {
            game.Player2Ready = true;
        }

        // Notify opponent
        string opponentId = playerId == game.Player1Id ? game.Player2Id : game.Player1Id;
        await _hubContext.Clients.Client(opponentId).SendAsync("OpponentReady");

        // If both ready, start game
        if (game.Player1Ready && game.Player2Ready)
        {
            await _hubContext.Clients.Clients(game.Player1Id, game.Player2Id)
                .SendAsync("GameStarted", game.CurrentTurnPlayerId);
        }
    }

    public async Task<(bool hit, bool sunk, bool gameOver)> AttackAsync(string playerId, string gameId, int x, int y)
    {
        if (!_games.ContainsKey(gameId))
            return (false, false, false);

        var game = _games[gameId];

        // Verify it's player's turn
        if (playerId != game.CurrentTurnPlayerId)
            return (false, false, false);

        // Determine which board to attack
        Board targetBoard = playerId == game.Player1Id ? game.Player2Board : game.PlayerBoard;

        // Process attack
        bool hit = targetBoard.ReceiveAttack(x, y);
        bool sunk = false;

        if (hit)
        {
            // Check if a ship is sunk
            foreach (var ship in targetBoard.Ships)
            {
                if (ship.Positions.Contains((x, y)) && ship.IsSunk(targetBoard.Grid))
                {
                    sunk = true;
                    // Mark ship as sunk
                    targetBoard.MarkShipAsSunk(x, y);
                    break;
                }
            }
        }

        // Record in history
        game.History.Add(new AttackHistory
        {
            X = x,
            Y = y,
            Hit = hit,
            IsPlayer = playerId == game.Player1Id,
            Timestamp = DateTime.UtcNow
        });

        // Check for winner
        bool gameOver = targetBoard.AllShipsSunk();

        // Prepare ship data if sunk
        object? sunkShipData = null;
        if (sunk && hit)
        {
            var cell = targetBoard.Grid[x, y];
            if (cell.ShipType.HasValue)
            {
                sunkShipData = new
                {
                    shipType = (int)cell.ShipType.Value,
                    isShipStart = cell.IsShipStart,
                    isHorizontal = cell.IsHorizontal
                };
            }
        }

        // Notify both players
        string opponentId = playerId == game.Player1Id ? game.Player2Id : game.Player1Id;

        await _hubContext.Clients.Client(playerId).SendAsync("AttackResult", x, y, hit, sunk, gameOver, sunkShipData);
        await _hubContext.Clients.Client(opponentId).SendAsync("OpponentAttacked", x, y, hit, sunk, gameOver, sunkShipData);

        if (!gameOver)
        {
            // Switch turn
            game.CurrentTurnPlayerId = opponentId;
            await _hubContext.Clients.Client(opponentId).SendAsync("YourTurn");
        }
        else
        {
            // Game over
            await _hubContext.Clients.Client(playerId).SendAsync("GameWon");
            await _hubContext.Clients.Client(opponentId).SendAsync("GameLost");
        }

        return (hit, sunk, gameOver);
    }

    public async Task<bool> ReconnectPlayerAsync(string newPlayerId, string gameId, string playerName)
    {
        if (!_games.ContainsKey(gameId))
            return false;

        var game = _games[gameId];
        
        // Trouver quel joueur se reconnecte basé sur le nom
        if (game.Player1Name == playerName)
        {
            // Mettre à jour l'ID de connexion du joueur 1
            string? oldPlayerId = game.Player1Id;
            game.Player1Id = newPlayerId;
            
            // Mettre à jour les mappings
            _playerToGame[newPlayerId] = gameId;
            _playerNames[newPlayerId] = playerName;
            
            // Supprimer l'ancien mapping si il existe
            if (oldPlayerId != null)
            {
                _playerToGame.Remove(oldPlayerId);
                _playerNames.Remove(oldPlayerId);
            }
            
            // Notifier l'adversaire que le joueur s'est reconnecté
            await _hubContext.Clients.Client(game.Player2Id).SendAsync("OpponentReconnected", playerName);
            return true;
        }
        else if (game.Player2Name == playerName)
        {
            // Mettre à jour l'ID de connexion du joueur 2
            string? oldPlayerId = game.Player2Id;
            game.Player2Id = newPlayerId;
            
            // Mettre à jour les mappings
            _playerToGame[newPlayerId] = gameId;
            _playerNames[newPlayerId] = playerName;
            
            // Supprimer l'ancien mapping si il existe
            if (oldPlayerId != null)
            {
                _playerToGame.Remove(oldPlayerId);
                _playerNames.Remove(oldPlayerId);
            }
            
            // Notifier l'adversaire que le joueur s'est reconnecté
            await _hubContext.Clients.Client(game.Player1Id).SendAsync("OpponentReconnected", playerName);
            return true;
        }
        
        return false;
    }

    public MultiplayerGame? GetGame(string gameId)
    {
        return _games.GetValueOrDefault(gameId);
    }

    public MultiplayerGame? GetPlayerGame(string playerId)
    {
        if (_playerToGame.TryGetValue(playerId, out var gameId))
        {
            return _games.GetValueOrDefault(gameId);
        }
        return null;
    }

    public async Task DisconnectPlayerAsync(string playerId)
    {
        // Remove from waiting queue
        if (_waitingPlayers.Contains(playerId))
        {
            var queue = new Queue<string>(_waitingPlayers.Where(p => p != playerId));
            _waitingPlayers.Clear();
            foreach (var p in queue)
            {
                _waitingPlayers.Enqueue(p);
            }
        }

        // Handle disconnection from active game
        if (_playerToGame.ContainsKey(playerId))
        {
            string gameId = _playerToGame[playerId];
            if (_games.ContainsKey(gameId))
            {
                var game = _games[gameId];
                string opponentId = playerId == game.Player1Id ? game.Player2Id : game.Player1Id;

                await _hubContext.Clients.Client(opponentId).SendAsync("OpponentDisconnected");

                // Clean up game
                _games.Remove(gameId);
                _playerToGame.Remove(playerId);
                _playerToGame.Remove(opponentId);
                _playerNames.Remove(playerId);
                _playerNames.Remove(opponentId);
            }
        }
        else
        {
            // Clean up player name
            _playerNames.Remove(playerId);
        }
    }
}