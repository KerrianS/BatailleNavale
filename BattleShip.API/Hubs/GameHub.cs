using BattleShip.Models;
using Microsoft.AspNetCore.SignalR;

namespace BattleShip.API.Hubs;

public class GameHub : Hub
{
    private static readonly Dictionary<string, MultiplayerGame> Games = new();
    private static readonly Dictionary<string, string> PlayerToGame = new();
    private static readonly Queue<string> WaitingPlayers = new();

    public async Task<string> JoinGame(string playerName)
    {
        string playerId = Context.ConnectionId;
        
        if (WaitingPlayers.Count > 0)
        {
            // Match with waiting player
            string opponentId = WaitingPlayers.Dequeue();
            string gameId = Guid.NewGuid().ToString();
            
            var game = new MultiplayerGame
            {
                Id = gameId,
                Player1Id = opponentId,
                Player2Id = playerId,
                Player1Name = "Joueur 1",
                Player2Name = playerName,
                CurrentTurnPlayerId = opponentId,
                Mode = GameMode.Multiplayer,
                PlayerBoard = new Board(),
                Player2Board = new Board()
            };
            
            Games[gameId] = game;
            PlayerToGame[opponentId] = gameId;
            PlayerToGame[playerId] = gameId;
            
            await Clients.Client(opponentId).SendAsync("OpponentJoined", gameId, playerName);
            await Clients.Caller.SendAsync("GameJoined", gameId, game.Player1Name);
            
            return gameId;
        }
        else
        {
            // Wait for opponent
            WaitingPlayers.Enqueue(playerId);
            await Clients.Caller.SendAsync("WaitingForOpponent");
            return "";
        }
    }

    public async Task PlaceShips(string gameId, List<ShipPlacement> placements)
    {
        if (!Games.ContainsKey(gameId)) return;
        
        var game = Games[gameId];
        string playerId = Context.ConnectionId;
        
        Board board = playerId == game.Player1Id ? game.PlayerBoard : game.Player2Board;
        
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
        await Clients.Client(opponentId).SendAsync("OpponentReady");
        
        // If both ready, start game
        if (game.Player1Ready && game.Player2Ready)
        {
            await Clients.Clients(game.Player1Id, game.Player2Id).SendAsync("GameStarted", game.CurrentTurnPlayerId);
        }
    }

    public async Task Attack(string gameId, int x, int y)
    {
        if (!Games.ContainsKey(gameId)) return;
        
        var game = Games[gameId];
        string playerId = Context.ConnectionId;
        
        // Verify it's player's turn
        if (playerId != game.CurrentTurnPlayerId) return;
        
        // Determine which board to attack
        Board targetBoard = playerId == game.Player1Id ? game.Player2Board : game.PlayerBoard;
        
        // Process attack
        bool hit = targetBoard.ReceiveAttack(x, y);
        bool sunk = false;
        
        if (hit)
        {
            var ship = targetBoard.GetShipAt(x, y);
            if (ship != null && ship.IsSunk(targetBoard.Grid))
            {
                sunk = true;
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
        
        // Notify both players
        string opponentId = playerId == game.Player1Id ? game.Player2Id : game.Player1Id;
        
        await Clients.Caller.SendAsync("AttackResult", x, y, hit, sunk, gameOver);
        await Clients.Client(opponentId).SendAsync("OpponentAttacked", x, y, hit, sunk, gameOver);
        
        if (!gameOver)
        {
            // Switch turn
            game.CurrentTurnPlayerId = opponentId;
            await Clients.Client(opponentId).SendAsync("YourTurn");
        }
        else
        {
            // Game over
            await Clients.Caller.SendAsync("GameWon");
            await Clients.Client(opponentId).SendAsync("GameLost");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string playerId = Context.ConnectionId;
        
        // Remove from waiting queue
        if (WaitingPlayers.Contains(playerId))
        {
            var queue = new Queue<string>(WaitingPlayers.Where(p => p != playerId));
            WaitingPlayers.Clear();
            foreach (var p in queue)
            {
                WaitingPlayers.Enqueue(p);
            }
        }
        
        // Handle disconnection from active game
        if (PlayerToGame.ContainsKey(playerId))
        {
            string gameId = PlayerToGame[playerId];
            if (Games.ContainsKey(gameId))
            {
                var game = Games[gameId];
                string opponentId = playerId == game.Player1Id ? game.Player2Id : game.Player1Id;
                
                await Clients.Client(opponentId).SendAsync("OpponentDisconnected");
                
                // Clean up game
                Games.Remove(gameId);
                PlayerToGame.Remove(playerId);
                PlayerToGame.Remove(opponentId);
            }
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
