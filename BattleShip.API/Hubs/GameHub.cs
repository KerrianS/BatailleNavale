using BattleShip.Models;
using Microsoft.AspNetCore.SignalR;
using BattleShip.API.Services;

namespace BattleShip.API.Hubs;

public class GameHub : Hub
{
    private readonly MultiplayerGameService _multiplayerService;

    public GameHub(MultiplayerGameService multiplayerService)
    {
        _multiplayerService = multiplayerService;
    }

    public async Task<string> JoinGame(string playerName)
    {
        string playerId = Context.ConnectionId;
        return await _multiplayerService.JoinGameAsync(playerId, playerName);
    }

    public async Task PlaceShips(string gameId, List<ShipPlacement> placements)
    {
        string playerId = Context.ConnectionId;
        await _multiplayerService.PlaceShipsAsync(playerId, gameId, placements);
    }

    public async Task Attack(string gameId, int x, int y)
    {
        string playerId = Context.ConnectionId;
        await _multiplayerService.AttackAsync(playerId, gameId, x, y);
    }

    public async Task<bool> ReconnectToGame(string gameId, string playerName)
    {
        string playerId = Context.ConnectionId;
        return await _multiplayerService.ReconnectPlayerAsync(playerId, gameId, playerName);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string playerId = Context.ConnectionId;
        await _multiplayerService.DisconnectPlayerAsync(playerId);
        await base.OnDisconnectedAsync(exception);
    }
}
