using BattleShip.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Microsoft.JSInterop;

namespace BattleShip.App.Services;

public class MultiplayerGameClient
{
    private HubConnection? _hubConnection;
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly string _hubUrl = "http://localhost:5001/gamehub";
    private readonly string _apiUrl = "http://localhost:5001/api/multiplayer";
    private const string GAME_STATE_KEY = "battleship_multiplayer_state";

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
    public string? PlayerId { get; private set; }
    public string? PlayerName { get; private set; }
    public string? OpponentName { get; private set; }
    public bool IsMyTurn { get; private set; }
    public Board? MyBoard { get; private set; }
    public Board? OpponentBoard { get; private set; }
    public List<AttackHistory> History { get; private set; } = new();

    public MultiplayerGameClient(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

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

        _hubConnection.On<string, string>("GameJoined", async (gameId, opponentName) =>
        {
            GameId = gameId;
            OpponentName = opponentName;
            await SaveGameStateAsync();
            OnGameJoined?.Invoke(gameId, opponentName);
        });

        _hubConnection.On<string, string>("OpponentJoined", async (gameId, opponentName) =>
        {
            GameId = gameId;
            OpponentName = opponentName;
            await SaveGameStateAsync();
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

        _hubConnection.On<int, int, bool, bool, bool, object?>("AttackResult", async (x, y, hit, sunk, gameOver, sunkShipData) =>
        {
            if (OpponentBoard != null)
            {
                OpponentBoard.Grid[x, y].IsHit = true;
                if (hit)
                {
                    OpponentBoard.Grid[x, y].HasShip = true;
                    if (sunk && sunkShipData != null)
                    {
                        // Mettre à jour les propriétés du bateau coulé pour le reveal
                        var shipDataJson = System.Text.Json.JsonSerializer.Serialize(sunkShipData);
                        var shipData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(shipDataJson);
                        if (shipData != null)
                        {
                            OpponentBoard.Grid[x, y].IsSunk = true;
                            if (shipData.ContainsKey("shipType"))
                            {
                                OpponentBoard.Grid[x, y].ShipType = (ShipType)((System.Text.Json.JsonElement)shipData["shipType"]).GetInt32();
                            }
                            if (shipData.ContainsKey("isShipStart"))
                            {
                                OpponentBoard.Grid[x, y].IsShipStart = ((System.Text.Json.JsonElement)shipData["isShipStart"]).GetBoolean();
                            }
                            if (shipData.ContainsKey("isHorizontal"))
                            {
                                OpponentBoard.Grid[x, y].IsHorizontal = ((System.Text.Json.JsonElement)shipData["isHorizontal"]).GetBoolean();
                            }
                        }
                        
                        // Marquer tout le bateau comme coulé
                        OpponentBoard.MarkShipAsSunk(x, y);
                    }
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

            await SaveGameStateAsync();
            OnAttackResult?.Invoke(x, y, hit, sunk, gameOver);
        });

        _hubConnection.On<int, int, bool, bool, bool, object?>("OpponentAttacked", (x, y, hit, sunk, gameOver, sunkShipData) =>
        {
            if (MyBoard != null)
            {
                MyBoard.Grid[x, y].IsHit = true;
                if (sunk && hit)
                {
                    // Marquer tout le bateau comme coulé
                    MyBoard.MarkShipAsSunk(x, y);
                }
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

        _hubConnection.On("YourTurn", async () =>
        {
            IsMyTurn = true;
            await SaveGameStateAsync();
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

    public async Task<bool> TryRestoreGameAsync()
    {
        try
        {
            // Restaurer l'état sauvegardé si il existe
            await RestoreGameStateAsync();
            
            // Si on a un jeu sauvegardé, essayer de le rejoindre
            if (!string.IsNullOrEmpty(GameId) && !string.IsNullOrEmpty(PlayerName))
            {
                await ReconnectToGameAsync();
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to restore/reconnect game: {ex.Message}");
            await ClearGameStateAsync();
        }
        
        return false;
    }

    public async Task<string> JoinGameAsync(string playerName)
    {
        if (_hubConnection == null)
            throw new InvalidOperationException("Hub connection not initialized");

        PlayerName = playerName;
        PlayerId = _hubConnection.ConnectionId;
        var gameId = await _hubConnection.InvokeAsync<string>("JoinGame", playerName);
        GameId = gameId;
        
        await SaveGameStateAsync();
        return gameId;
    }

    private async Task ReconnectToGameAsync()
    {
        if (_hubConnection == null || string.IsNullOrEmpty(GameId) || string.IsNullOrEmpty(PlayerName))
            return;

        PlayerId = _hubConnection.ConnectionId;
        
        // Essayer de rejoindre le jeu existant
        var success = await _hubConnection.InvokeAsync<bool>("ReconnectToGame", GameId, PlayerName);
        
        if (success)
        {
            await SaveGameStateAsync();
        }
        else
        {
            // Le jeu n'existe plus, effacer l'état
            await ClearGameStateAsync();
            GameId = null;
            OpponentName = null;
            MyBoard = null;
            OpponentBoard = null;
            History.Clear();
        }
    }

    public bool HasSavedGame()
    {
        return !string.IsNullOrEmpty(GameId);
    }

    public async Task<string?> GetStoredPlayerNameAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", GAME_STATE_KEY);
            if (!string.IsNullOrEmpty(json))
            {
                var gameState = JsonSerializer.Deserialize<GameStateDto>(json);
                return gameState?.PlayerName;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting stored player name: {ex.Message}");
        }
        
        return null;
    }

    public async Task PlaceShipsAsync(List<ShipPlacement> placements)
    {
        if (_hubConnection == null || string.IsNullOrEmpty(GameId))
            throw new InvalidOperationException("Not connected to a game");

        MyBoard = new Board();
        foreach (var placement in placements)
        {
            MyBoard.PlaceShip(placement.X, placement.Y, placement.Size, placement.IsHorizontal, placement.ShipType);
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

        // Effacer l'état sauvegardé
        await ClearGameStateAsync();

        GameId = null;
        OpponentName = null;
        IsMyTurn = false;
        MyBoard = null;
        OpponentBoard = null;
        History.Clear();
    }

    private async Task SaveGameStateAsync()
    {
        try
        {
            var gameState = new GameStateDto
            {
                GameId = GameId,
                PlayerId = PlayerId,
                PlayerName = PlayerName,
                OpponentName = OpponentName,
                IsMyTurn = IsMyTurn,
                MyBoard = SerializeBoard(MyBoard),
                OpponentBoard = SerializeBoard(OpponentBoard),
                History = History.ToList()
            };

            var json = JsonSerializer.Serialize(gameState);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", GAME_STATE_KEY, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving game state: {ex.Message}");
        }
    }

    private async Task RestoreGameStateAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", GAME_STATE_KEY);
            if (!string.IsNullOrEmpty(json))
            {
                var gameState = JsonSerializer.Deserialize<GameStateDto>(json);
                if (gameState != null)
                {
                    GameId = gameState.GameId;
                    PlayerId = gameState.PlayerId;
                    PlayerName = gameState.PlayerName;
                    OpponentName = gameState.OpponentName;
                    IsMyTurn = gameState.IsMyTurn;
                    MyBoard = DeserializeBoard(gameState.MyBoard);
                    OpponentBoard = DeserializeBoard(gameState.OpponentBoard);
                    History = gameState.History ?? new List<AttackHistory>();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error restoring game state: {ex.Message}");
        }
    }

    private async Task ClearGameStateAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", GAME_STATE_KEY);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error clearing game state: {ex.Message}");
        }
    }

    private object? SerializeBoard(Board? board)
    {
        if (board == null) return null;
        
        var cells = new List<object>();
        for (int x = 0; x < board.CurrentSize; x++)
        {
            for (int y = 0; y < board.CurrentSize; y++)
            {
                var cell = board.Grid[x, y];
                cells.Add(new
                {
                    x = cell.X,
                    y = cell.Y,
                    hasShip = cell.HasShip,
                    isHit = cell.IsHit,
                    isSunk = cell.IsSunk,
                    shipType = cell.ShipType.HasValue ? (int)cell.ShipType.Value : -1,
                    isShipStart = cell.IsShipStart,
                    isHorizontal = cell.IsHorizontal
                });
            }
        }
        
        return new { size = board.CurrentSize, cells };
    }

    private Board? DeserializeBoard(object? boardData)
    {
        if (boardData == null) return null;
        
        try
        {
            var json = JsonSerializer.Serialize(boardData);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (data.TryGetProperty("size", out var sizeElement) &&
                data.TryGetProperty("cells", out var cellsElement))
            {
                int size = sizeElement.GetInt32();
                var board = new Board(size);
                
                foreach (var cellElement in cellsElement.EnumerateArray())
                {
                    int x = cellElement.GetProperty("x").GetInt32();
                    int y = cellElement.GetProperty("y").GetInt32();
                    bool hasShip = cellElement.GetProperty("hasShip").GetBoolean();
                    bool isHit = cellElement.GetProperty("isHit").GetBoolean();
                    bool isSunk = cellElement.GetProperty("isSunk").GetBoolean();
                    int shipTypeInt = cellElement.GetProperty("shipType").GetInt32();
                    bool isShipStart = cellElement.GetProperty("isShipStart").GetBoolean();
                    bool isHorizontal = cellElement.GetProperty("isHorizontal").GetBoolean();
                    
                    var cell = board.Grid[x, y];
                    cell.HasShip = hasShip;
                    cell.IsHit = isHit;
                    cell.IsSunk = isSunk;
                    cell.ShipType = shipTypeInt >= 0 ? (ShipType)shipTypeInt : null;
                    cell.IsShipStart = isShipStart;
                    cell.IsHorizontal = isHorizontal;
                }
                
                return board;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deserializing board: {ex.Message}");
        }
        
        return null;
    }

    private class GameStateDto
    {
        public string? GameId { get; set; }
        public string? PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? OpponentName { get; set; }
        public bool IsMyTurn { get; set; }
        public object? MyBoard { get; set; }
        public object? OpponentBoard { get; set; }
        public List<AttackHistory>? History { get; set; }
    }
}
