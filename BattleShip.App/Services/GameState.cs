using BattleShip.Models;
using System.Net.Http.Json;
using BattleShip.API.Protos;
using Microsoft.AspNetCore.Components;

namespace BattleShip.App.Services;

public class GameState
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BattleshipService.BattleshipServiceClient _grpcClient;
    private NavigationManager? _navigationManager;

    public event Action? OnStateChanged;

    public void SetNavigationManager(NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;
    }

    public string? GameId { get; set; }
    public Cell[,]? PlayerBoard { get; set; }
    public Cell[,]? OpponentBoard { get; set; }
    public bool GameOver { get; set; }
    public bool PlayerWon { get; set; }
    public int HitCount { get; set; }
    public string Message { get; set; } = "";
    public List<AttackHistory> History { get; set; } = new();

    private void NotifyStateChanged() => OnStateChanged?.Invoke();

    public GameState(IHttpClientFactory httpClientFactory, BattleshipService.BattleshipServiceClient grpcClient)
    {
        _httpClientFactory = httpClientFactory;
        _grpcClient = grpcClient;
    }

    public async Task StartNewGame()
    {

        var httpClient = _httpClientFactory.CreateClient("BattleShipAPI");
        var response = await httpClient.PostAsync("/game/start", null);
        var result = await response.Content.ReadFromJsonAsync<StartGameResponse>();

        if (result != null)
        {
            GameId = result.GameId;
            PlayerBoard = ConvertToBoard(result.PlayerBoard);
            OpponentBoard = new Cell[Board.Size, Board.Size];

            for (int x = 0; x < Board.Size; x++)
            {
                for (int y = 0; y < Board.Size; y++)
                {
                    OpponentBoard[x, y] = new Cell(x, y);
                }
            }

            GameOver = false;
            PlayerWon = false;
            HitCount = 0;
            Message = "Partie démarrée ! Cliquez sur une case pour attaquer.";
            History = new List<AttackHistory>();
        }
    }

    public async Task<bool> TryRestoreGame(string? gameId)
    {
        if (string.IsNullOrEmpty(gameId))
        {
            return false;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("BattleShipAPI");
            var response = await httpClient.GetAsync($"/game/{gameId}/state");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GameStateResponse>();
                if (result != null)
                {
                    GameId = result.GameId;
                    PlayerBoard = ConvertToBoard(result.PlayerBoard);
                    OpponentBoard = ConvertToBoard(result.OpponentBoard);
                    GameOver = result.GameOver;
                    PlayerWon = result.PlayerWon;
                    HitCount = result.HitCount;
                    Message = result.Message;
                    History = result.History.Select(h => new AttackHistory(h.X, h.Y, h.Hit, h.IsPlayer)
                    {
                        Timestamp = h.Timestamp
                    }).ToList();

                    NotifyStateChanged();
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la restauration de la partie: {ex.Message}");
        }

        return false;
    }

    public async Task StartNewGameWithPlacements(List<ShipPlacement> placements, int gridSize = 10)
    {
        PlayerBoard = new Cell[gridSize, gridSize];
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                PlayerBoard[x, y] = new Cell(x, y);
            }
        }

        var playerBoardModel = new Board(gridSize);
        foreach (var placement in placements)
        {
            playerBoardModel.PlaceShip(placement.X, placement.Y, placement.Size, placement.IsHorizontal, placement.ShipType);

            for (int i = 0; i < placement.Size; i++)
            {
                int posX = placement.IsHorizontal ? placement.X + i : placement.X;
                int posY = placement.IsHorizontal ? placement.Y : placement.Y + i;

                if (posX < gridSize && posY < gridSize)
                {
                    PlayerBoard[posX, posY].HasShip = true;
                    PlayerBoard[posX, posY].ShipType = placement.ShipType;
                    PlayerBoard[posX, posY].IsHorizontal = placement.IsHorizontal;
                    if (i == 0) PlayerBoard[posX, posY].IsShipStart = true;
                    Console.WriteLine($"Placing ship at [{posX}, {posY}]");
                }
            }
        }

        var httpClient = _httpClientFactory.CreateClient("BattleShipAPI");

        var response = await httpClient.PostAsync($"/game/start?gridSize={gridSize}", null);
        var result = await response.Content.ReadFromJsonAsync<StartGameResponse>();

        if (result != null)
        {
            GameId = result.GameId;

            _navigationManager?.NavigateTo($"/game/{GameId}", false);

            var placementResponse = await httpClient.PostAsJsonAsync($"/game/{GameId}/place-ships", placements);
            if (!placementResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Erreur lors de l'envoi des placements: {placementResponse.StatusCode}");
            }
            else
            {
                Console.WriteLine($"Placements envoyés avec succès pour GameId: {GameId}");
            }

            OpponentBoard = new Cell[gridSize, gridSize];

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    OpponentBoard[x, y] = new Cell(x, y);
                }
            }

            GameOver = false;
            PlayerWon = false;
            HitCount = 0;
            Message = "Partie démarrée ! Cliquez sur une case pour attaquer.";
            History = new List<AttackHistory>();
        }
    }

    public async Task<bool> Attack(int x, int y)
    {
        if (GameId == null || GameOver) return false;

        try
        {
            var request = new AttackRequestGRPC
            {
                GameId = GameId,
                X = x,
                Y = y
            };

            var response = await _grpcClient.AttackAsync(request);

            UpdateBoardFromGrpc(OpponentBoard!, response.OpponentBoard, OpponentBoard!.GetLength(0));
            UpdateBoardFromGrpc(PlayerBoard!, response.PlayerBoard, PlayerBoard!.GetLength(0));

            GameOver = response.GameOver;
            PlayerWon = response.PlayerWon;
            HitCount = response.HitCount;
            Message = response.Message;

            History.Add(new AttackHistory(x, y, response.Hit, true));
            if (response.AiAttack != null)
            {
                History.Add(new AttackHistory(response.AiAttack.X, response.AiAttack.Y, response.AiAttack.Hit, false));
            }

            NotifyStateChanged();
            return true;
        }
        catch (Grpc.Core.RpcException ex)
        {
            Message = "Erreur gRPC: " + ex.Status.Detail;
            return false;
        }
        catch (Exception ex)
        {
            Message = "Erreur: " + ex.Message;
            return false;
        }
    }

    private void UpdateBoardFromGrpc(Cell[,] board, API.Protos.BoardDto boardDto, int gridSize)
    {
        foreach (var cell in boardDto.Cells)
        {
            if (cell.X < gridSize && cell.Y < gridSize)
            {
                board[cell.X, cell.Y].IsHit = cell.IsHit;
                board[cell.X, cell.Y].HasShip = cell.HasShip;
                board[cell.X, cell.Y].IsSunk = cell.IsSunk;
                board[cell.X, cell.Y].IsShipStart = cell.IsShipStart;
                board[cell.X, cell.Y].IsHorizontal = cell.IsHorizontal;
                if (cell.ShipType >= 0)
                {
                    board[cell.X, cell.Y].ShipType = (ShipType)cell.ShipType;
                }
            }
        }
    }

    private Cell[,] ConvertToBoard(BoardDto boardDto)
    {
        int boardSize = (int)Math.Sqrt(boardDto.Cells.Count);
        var board = new Cell[boardSize, boardSize];

        foreach (var cell in boardDto.Cells)
        {
            board[cell.X, cell.Y] = new Cell(cell.X, cell.Y)
            {
                HasShip = cell.HasShip,
                IsHit = cell.IsHit,
                IsSunk = cell.IsSunk,
                IsShipStart = cell.IsShipStart,
                IsHorizontal = cell.IsHorizontal,
                ShipType = cell.ShipType >= 0 ? (ShipType)cell.ShipType : null
            };
        }

        return board;
    }

    private Cell[,] ConvertGrpcBoardToArray(API.Protos.BoardDto boardDto)
    {
        var board = new Cell[Board.Size, Board.Size];

        foreach (var cell in boardDto.Cells)
        {
            board[cell.X, cell.Y] = new Cell(cell.X, cell.Y)
            {
                HasShip = cell.HasShip,
                IsHit = cell.IsHit,
                IsSunk = cell.IsSunk,
                IsShipStart = cell.IsShipStart,
                IsHorizontal = cell.IsHorizontal,
                ShipType = cell.ShipType >= 0 ? (ShipType)cell.ShipType : null
            };
        }

        return board;
    }

    private class StartGameResponse
    {
        public string GameId { get; set; } = "";
        public BoardDto PlayerBoard { get; set; } = new();
    }

    private class GameStateResponse
    {
        public string GameId { get; set; } = "";
        public BoardDto PlayerBoard { get; set; } = new();
        public BoardDto OpponentBoard { get; set; } = new();
        public bool GameOver { get; set; }
        public bool PlayerWon { get; set; }
        public int HitCount { get; set; }
        public List<AttackHistoryDto> History { get; set; } = new();
        public string Message { get; set; } = "";
    }

    private class AttackHistoryDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool Hit { get; set; }
        public bool IsPlayer { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private class BoardDto
    {
        public List<CellDto> Cells { get; set; } = new();
    }

    private class CellDto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool HasShip { get; set; }
        public bool IsHit { get; set; }
        public bool IsSunk { get; set; }
        public int ShipType { get; set; } = -1;
        public bool IsShipStart { get; set; }
        public bool IsHorizontal { get; set; }
    }
}