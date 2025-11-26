using BattleShip.Models;
using System.Net.Http.Json;
using BattleShip.API.Protos;

namespace BattleShip.App.Services;

public class GameState
{
    private readonly HttpClient _httpClient;
    private readonly BattleshipService.BattleshipServiceClient _grpcClient;
    
    public string? GameId { get; set; }
    public Cell[,]? PlayerBoard { get; set; }
    public Cell[,]? OpponentBoard { get; set; }
    public bool GameOver { get; set; }
    public bool PlayerWon { get; set; }
    public int HitCount { get; set; }
    public string Message { get; set; } = "";

    public GameState(HttpClient httpClient, BattleshipService.BattleshipServiceClient grpcClient)
    {
        _httpClient = httpClient;
        _grpcClient = grpcClient;
    }

    public async Task StartNewGame()
    {
        // Use REST endpoint for starting game (as specified in PDF)
        var response = await _httpClient.PostAsync("/game/start", null);
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
            
            // Update opponent board
            OpponentBoard = ConvertGrpcBoardToArray(response.OpponentBoard);
            
            // Update player board (AI attack)
            PlayerBoard = ConvertGrpcBoardToArray(response.PlayerBoard);
            
            GameOver = response.GameOver;
            PlayerWon = response.PlayerWon;
            HitCount = response.HitCount;
            Message = response.Message;
            
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
