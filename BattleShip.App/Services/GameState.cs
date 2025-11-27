using BattleShip.Models;
using System.Net.Http.Json;
using BattleShip.API.Protos;

namespace BattleShip.App.Services;

public class GameState
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BattleshipService.BattleshipServiceClient _grpcClient;
    
    public event Action? OnStateChanged;
    
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
        // Use REST endpoint for starting game (as specified in PDF)
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
        
        // Créer la partie
        var response = await httpClient.PostAsync($"/game/start?gridSize={gridSize}", null);
        var result = await response.Content.ReadFromJsonAsync<StartGameResponse>();
        
        if (result != null)
        {
            GameId = result.GameId;
            
            // Envoyer les placements du joueur à l'API
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
            
            // Mettre à jour les boards avec les données complètes du serveur (incluant IsSunk)
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
            }
        }
    }

    private Cell[,] ConvertToBoard(BoardDto boardDto)
    {
        var board = new Cell[Board.Size, Board.Size];
        
        foreach (var cell in boardDto.Cells)
        {
            board[cell.X, cell.Y] = new Cell(cell.X, cell.Y)
            {
                HasShip = cell.HasShip,
                IsHit = cell.IsHit
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
                IsHit = cell.IsHit
            };
        }
        
        return board;
    }

    private class StartGameResponse
    {
        public string GameId { get; set; } = "";
        public BoardDto PlayerBoard { get; set; } = new();
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
    }
}
