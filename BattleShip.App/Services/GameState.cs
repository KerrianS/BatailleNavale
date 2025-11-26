using BattleShip.Models;
using System.Net.Http.Json;

namespace BattleShip.App.Services;

public class GameState
{
    private readonly HttpClient _httpClient;
    
    public string? GameId { get; set; }
    public Cell[,]? PlayerBoard { get; set; }
    public Cell[,]? OpponentBoard { get; set; }
    public bool GameOver { get; set; }
    public bool PlayerWon { get; set; }
    public int HitCount { get; set; }
    public string Message { get; set; } = "";

    public GameState(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5290");
    }

    public async Task StartNewGame()
    {
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

    var request = new BattleShip.Models.AttackRequest { X = x, Y = y };
    var response = await _httpClient.PostAsJsonAsync($"/game/{GameId}/attack", request);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Message = "Erreur: " + error;
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<AttackResponse>();
        
        if (result != null)
        {
            // Mettre à jour la grille adverse
            OpponentBoard = ConvertToBoard(result.OpponentBoard);
            
            // Mettre à jour la grille du joueur (attaque de l'IA)
            if (result.PlayerBoard != null)
            {
                PlayerBoard = ConvertToBoard(result.PlayerBoard);
            }
            
            GameOver = result.GameOver;
            PlayerWon = result.PlayerWon;
            HitCount = result.HitCount;
            Message = result.Message;
            return true;
        }

        return false;
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

    private class StartGameResponse
    {
        public string GameId { get; set; } = "";
        public BoardDto PlayerBoard { get; set; } = new();
    }

    private class AttackResponse
    {
        public bool Hit { get; set; }
        public string Message { get; set; } = "";
        public int HitCount { get; set; }
        public BoardDto OpponentBoard { get; set; } = new();
        public BoardDto? PlayerBoard { get; set; }
        public bool GameOver { get; set; }
        public bool PlayerWon { get; set; }
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
