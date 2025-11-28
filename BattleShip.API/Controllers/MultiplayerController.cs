using BattleShip.API.Services;
using BattleShip.Models;
using Microsoft.AspNetCore.Mvc;

namespace BattleShip.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MultiplayerController : ControllerBase
{
    private readonly MultiplayerGameService _multiplayerService;

    public MultiplayerController(MultiplayerGameService multiplayerService)
    {
        _multiplayerService = multiplayerService;
    }

    [HttpPost("join")]
    public async Task<ActionResult<string>> JoinGame([FromBody] JoinGameRequest request)
    {
        // For API calls, we need to generate a temporary player ID
        string playerId = Guid.NewGuid().ToString();
        string gameId = await _multiplayerService.JoinGameAsync(playerId, request.PlayerName);
        
        return Ok(new { GameId = gameId, PlayerId = playerId });
    }

    [HttpPost("place-ships")]
    public async Task<ActionResult> PlaceShips([FromBody] PlaceShipsRequest request)
    {
        await _multiplayerService.PlaceShipsAsync(request.PlayerId, request.GameId, request.Placements);
        return Ok();
    }

    [HttpPost("attack")]
    public async Task<ActionResult<AttackResponse>> Attack([FromBody] MultiplayerAttackRequest request)
    {
        var result = await _multiplayerService.AttackAsync(request.PlayerId, request.GameId, request.X, request.Y);
        
        return Ok(new AttackResponse
        {
            Hit = result.hit,
            Sunk = result.sunk,
            GameOver = result.gameOver
        });
    }

    [HttpGet("game/{gameId}")]
    public ActionResult<MultiplayerGameResponse> GetGame(string gameId)
    {
        var game = _multiplayerService.GetGame(gameId);
        if (game == null)
            return NotFound();

        return Ok(new MultiplayerGameResponse
        {
            GameId = game.Id,
            Player1Name = game.Player1Name,
            Player2Name = game.Player2Name,
            CurrentTurnPlayerId = game.CurrentTurnPlayerId,
            Player1Ready = game.Player1Ready,
            Player2Ready = game.Player2Ready,
            GameOver = game.GameOver,
            Winner = game.Winner,
            History = game.History
        });
    }

    [HttpGet("player/{playerId}/game")]
    public ActionResult<MultiplayerGameResponse> GetPlayerGame(string playerId)
    {
        var game = _multiplayerService.GetPlayerGame(playerId);
        if (game == null)
            return NotFound();

        return Ok(new MultiplayerGameResponse
        {
            GameId = game.Id,
            Player1Name = game.Player1Name,
            Player2Name = game.Player2Name,
            CurrentTurnPlayerId = game.CurrentTurnPlayerId,
            Player1Ready = game.Player1Ready,
            Player2Ready = game.Player2Ready,
            GameOver = game.GameOver,
            Winner = game.Winner,
            History = game.History,
            MyBoard = playerId == game.Player1Id ? game.PlayerBoard?.Grid : game.Player2Board?.Grid,
            OpponentBoard = playerId == game.Player1Id ? game.Player2Board?.Grid : game.PlayerBoard?.Grid
        });
    }
}

// DTOs
public class JoinGameRequest
{
    public string PlayerName { get; set; } = "";
}

public class PlaceShipsRequest
{
    public string PlayerId { get; set; } = "";
    public string GameId { get; set; } = "";
    public List<ShipPlacement> Placements { get; set; } = new();
}

public class MultiplayerAttackRequest
{
    public string PlayerId { get; set; } = "";
    public string GameId { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
}

public class AttackResponse
{
    public bool Hit { get; set; }
    public bool Sunk { get; set; }
    public bool GameOver { get; set; }
}

public class MultiplayerGameResponse
{
    public string GameId { get; set; } = "";
    public string Player1Name { get; set; } = "";
    public string Player2Name { get; set; } = "";
    public string CurrentTurnPlayerId { get; set; } = "";
    public bool Player1Ready { get; set; }
    public bool Player2Ready { get; set; }
    public bool GameOver { get; set; }
    public string? Winner { get; set; }
    public List<AttackHistory> History { get; set; } = new();
    public Cell[,]? MyBoard { get; set; }
    public Cell[,]? OpponentBoard { get; set; }
}