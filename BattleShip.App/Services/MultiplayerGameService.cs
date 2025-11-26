using BattleShip.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace BattleShip.App.Services;

public class MultiplayerGameService
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl = "http://localhost:5001/gamehub";

    public event Action<string>? OnWaitingForOpponent;
    public event Action<string, string>? OnGameJoined;
    public event Action<string, string>? OnOpponentJoined;
    public event Action? OnOpponentReady;
    public event Action<string>? OnGameStarted;
    public event Action<int, int, bool, bool, bool>? OnAttackResult;
    public event Action<int, int, bool, bool, bool>? OnOpponentAttacked;
    public event Action? OnYourTurn;
    public event Action? OnGameWon;
    public event Action? OnGameLost;
    public event Action? OnOpponentDisconnected;

    public string? GameId { get; private set; }
    public string? OpponentName { get; private set; }
    public bool IsMyTurn { get; private set; }
    public Board? MyBoard { get; private set; }
    public Board? OpponentBoard { get; private set; }
    public List<AttackHistory> History { get; private set; } = new();

    public async Task InitializeAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On("WaitingForOpponent", () =>
        {
            OnWaitingForOpponent?.Invoke("En attente d'un adversaire...");
        });

        _hubConnection.On<string, string>("GameJoined", (gameId, opponentName) =>
        {
            GameId = gameId;
            OpponentName = opponentName;
            OnGameJoined?.Invoke(gameId, opponentName);
        });

        _hubConnection.On<string, string>("OpponentJoined", (gameId, opponentName) =>
        {
            GameId = gameId;
            OpponentName = opponentName;
            OnOpponentJoined?.Invoke(gameId, opponentName);
        });

        _hubConnection.On("OpponentReady", () =>
        {
            OnOpponentReady?.Invoke();
        });

        _hubConnection.On<string>("GameStarted", (currentTurnPlayerId) =>
        {
            IsMyTurn = currentTurnPlayerId == _hubConnection.ConnectionId;
            OnGameStarted?.Invoke(currentTurnPlayerId);
        });

        _hubConnection.On<int, int, bool, bool, bool>("AttackResult", (x, y, hit, sunk, gameOver) =>
        {
            if (OpponentBoard != null)
            {
                OpponentBoard.Grid[x, y].IsHit = true;
                if (hit)
                {
                    OpponentBoard.Grid[x, y].HasShip = true;
                }
            }

            History.Add(new AttackHistory
            {
                X = x,
                Y = y,
                Hit = hit,
                IsPlayer = true,
                Timestamp = DateTime.UtcNow
            });

            if (!gameOver)
            {
                IsMyTurn = false;
            }

            OnAttackResult?.Invoke(x, y, hit, sunk, gameOver);
        });

        _hubConnection.On<int, int, bool, bool, bool>("OpponentAttacked", (x, y, hit, sunk, gameOver) =>
        {
            if (MyBoard != null)
            {
                MyBoard.Grid[x, y].IsHit = true;
            }

            History.Add(new AttackHistory
            {
                X = x,
                Y = y,
                Hit = hit,
                IsPlayer = false,
                Timestamp = DateTime.UtcNow
            });

            OnOpponentAttacked?.Invoke(x, y, hit, sunk, gameOver);
        });

        _hubConnection.On("YourTurn", () =>
        {
            IsMyTurn = true;
            OnYourTurn?.Invoke();
        });

        _hubConnection.On("GameWon", () =>
        {
            OnGameWon?.Invoke();
        });

        _hubConnection.On("GameLost", () =>
        {
            OnGameLost?.Invoke();
        });

        _hubConnection.On("OpponentDisconnected", () =>
        {
            OnOpponentDisconnected?.Invoke();
        });

        await _hubConnection.StartAsync();
    }

    public async Task<string> JoinGameAsync(string playerName)
    {
        if (_hubConnection == null)
            throw new InvalidOperationException("Hub connection not initialized");

        var gameId = await _hubConnection.InvokeAsync<string>("JoinGame", playerName);
        return gameId;
    }

    public async Task PlaceShipsAsync(List<ShipPlacement> placements)
    {
        if (_hubConnection == null || string.IsNullOrEmpty(GameId))
            throw new InvalidOperationException("Not connected to a game");

        MyBoard = new Board();
        foreach (var placement in placements)
        {
            MyBoard.PlaceShip(placement.X, placement.Y, placement.Size, placement.IsHorizontal);
        }

        OpponentBoard = new Board();

        await _hubConnection.InvokeAsync("PlaceShips", GameId, placements);
    }

    public async Task AttackAsync(int x, int y)
    {
        if (_hubConnection == null || string.IsNullOrEmpty(GameId))
            throw new InvalidOperationException("Not connected to a game");

        if (!IsMyTurn)
            throw new InvalidOperationException("Not your turn");

        await _hubConnection.InvokeAsync("Attack", GameId, x, y);
    }

    public async Task DisconnectAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
        }

        GameId = null;
        OpponentName = null;
        IsMyTurn = false;
        MyBoard = null;
        OpponentBoard = null;
        History.Clear();
    }
}
