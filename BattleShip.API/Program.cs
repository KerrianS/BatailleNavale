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

app.MapPost("/game/start", (ILogger<Program> logger) =>
{
    logger.LogInformation("[NOUVELLE PARTIE] Demarrage d'une nouvelle partie...");
    
    var game = new Game();
    GenerateOpponentBoard(game.OpponentBoard);
    games[game.Id] = game;
    
    int shipCount = CountShips(game.OpponentBoard);
    logger.LogInformation("[NOUVELLE PARTIE] Partie creee avec ID: {GameId}", game.Id);
    logger.LogInformation("[NOUVELLE PARTIE] {ShipCount} bateaux places sur la grille adverse", shipCount);
    logger.LogInformation("[NOUVELLE PARTIE] Total de cases avec bateaux: 17 (5+4+3+3+2)");
    
    return TypedResults.Ok(new 
    { 
        gameId = game.Id,
        playerBoard = ConvertBoardToDto(game.PlayerBoard, true)
    });
});

app.MapPost("/game/{gameId}/attack", ([Microsoft.AspNetCore.Mvc.FromRoute] string gameId, [Microsoft.AspNetCore.Mvc.FromQuery] int x, [Microsoft.AspNetCore.Mvc.FromQuery] int y, ILogger<Program> logger) =>
{
    logger.LogInformation("[ATTAQUE] Joueur attaque position ({X}, {Y}) - Game ID: {GameId}", x, y, gameId);
    
    if (!games.TryGetValue(gameId, out var game))
    {
        logger.LogWarning("[ATTAQUE] Partie non trouvee: {GameId}", gameId);
        return Results.NotFound(new { message = "Partie non trouvée" });
    }

    var (hit, alreadyHit) = game.OpponentBoard.Attack(x, y);
    
    if (alreadyHit)
    {
        logger.LogWarning("[ATTAQUE] Case ({X}, {Y}) deja attaquee", x, y);
        return Results.BadRequest(new { message = "Case déjà attaquée" });
    }

    int hitCount = CountHits(game.OpponentBoard);
    bool gameOver = hitCount >= 13;
    string message = "";

    if (hit)
    {
        message = gameOver ? "Touché-Coulé ! Vous avez gagné !" : "Touché !";
        logger.LogInformation("[ATTAQUE] TOUCHE ! Position ({X}, {Y}) - Coups reussis: {HitCount}/13", x, y, hitCount);
        
        if (gameOver)
        {
            logger.LogInformation("[VICTOIRE] Le joueur a gagne avec {HitCount} coups reussis !", hitCount);
        }
    }
    else
    {
        message = "Raté";
        logger.LogInformation("[ATTAQUE] Rate a la position ({X}, {Y}) - Coups reussis: {HitCount}/13", x, y, hitCount);
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
        int attempts = 0;
        while (!placed)
        {
            attempts++;
            int x = random.Next(0, Board.Size);
            int y = random.Next(0, Board.Size);
            bool isHorizontal = random.Next(2) == 0;
            
            if (board.CanPlaceShip(x, y, shipSize, isHorizontal))
            {
                board.PlaceShip(x, y, shipSize, isHorizontal);
                placed = true;
                Console.WriteLine($"[PLACEMENT] Bateau de taille {shipSize} place a ({x},{y}) - {(isHorizontal ? "Horizontal" : "Vertical")} (Tentatives: {attempts})");
            }
        }
    }
}

int CountShips(Board board)
{
    int count = 0;
    for (int x = 0; x < Board.Size; x++)
    {
        for (int y = 0; y < Board.Size; y++)
        {
            if (board.Grid[x, y].HasShip)
                count++;
        }
    }
    return count;
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
                hasShip = showShips ? cell.HasShip : (cell.IsHit && cell.HasShip),
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
