using BattleShip.Models;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowBlazor");
app.UseHttpsRedirection();

var games = new ConcurrentDictionary<string, Game>();

app.MapPost("/game/start", () =>
{
    var game = new Game();
    GenerateOpponentBoard(game.OpponentBoard);
    games[game.Id] = game;
    
    return TypedResults.Ok(new 
    { 
        gameId = game.Id,
        playerBoard = ConvertBoardToDto(game.PlayerBoard, true)
    });
});

app.MapPost("/game/{gameId}/attack", ([Microsoft.AspNetCore.Mvc.FromRoute] string gameId, [Microsoft.AspNetCore.Mvc.FromQuery] int x, [Microsoft.AspNetCore.Mvc.FromQuery] int y) =>
{
    if (!games.TryGetValue(gameId, out var game))
        return Results.NotFound(new { message = "Partie non trouvée" });

    var (hit, alreadyHit) = game.OpponentBoard.Attack(x, y);
    
    if (alreadyHit)
        return Results.BadRequest(new { message = "Case déjà attaquée" });

    int hitCount = CountHits(game.OpponentBoard);
    bool gameOver = hitCount >= 13;
    string message = "";

    if (hit)
    {
        message = gameOver ? "Touché-Coulé ! Vous avez gagné !" : "Touché !";
    }
    else
    {
        message = "Raté";
    }

    return Results.Ok(new 
    { 
        hit,
        message,
        hitCount,
        opponentBoard = ConvertBoardToDto(game.OpponentBoard, false),
        gameOver,
        playerWon = gameOver
    });
});

app.Run();

void GenerateOpponentBoard(Board board)
{
    var random = new Random();
    var ships = new[] { 5, 4, 3, 3, 2 };
    
    foreach (var shipSize in ships)
    {
        bool placed = false;
        while (!placed)
        {
            int x = random.Next(0, Board.Size);
            int y = random.Next(0, Board.Size);
            bool isHorizontal = random.Next(2) == 0;
            
            if (board.CanPlaceShip(x, y, shipSize, isHorizontal))
            {
                board.PlaceShip(x, y, shipSize, isHorizontal);
                placed = true;
            }
        }
    }
}

object ConvertBoardToDto(Board board, bool showShips)
{
    var cells = new List<object>();
    for (int x = 0; x < Board.Size; x++)
    {
        for (int y = 0; y < Board.Size; y++)
        {
            var cell = board.Grid[x, y];
            cells.Add(new
            {
                x = cell.X,
                y = cell.Y,
                hasShip = showShips ? cell.HasShip : false,
                isHit = cell.IsHit
            });
        }
    }
    return new { cells };
}

bool IsGameOver(Board board)
{
    for (int x = 0; x < Board.Size; x++)
    {
        for (int y = 0; y < Board.Size; y++)
        {
            if (board.Grid[x, y].HasShip && !board.Grid[x, y].IsHit)
                return false;
        }
    }
    return true;
}

int CountHits(Board board)
{
    int count = 0;
    for (int x = 0; x < Board.Size; x++)
    {
        for (int y = 0; y < Board.Size; y++)
        {
            if (board.Grid[x, y].HasShip && board.Grid[x, y].IsHit)
                count++;
        }
    }
    return count;
}
