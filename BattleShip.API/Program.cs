using BattleShip.Models;
using FluentValidation;
using BattleShip.API.Validators;
using BattleShip.API.Services;
using BattleShip.API.Hubs;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5208", 
                "https://localhost:5208",
                "http://localhost:5001", 
                "https://localhost:5001",
                "http://localhost:5000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddScoped<IValidator<AttackRequest>, AttackRequestValidator>();

builder.Services.AddSignalR();
builder.Services.AddGrpc();

var games = new ConcurrentDictionary<string, Game>();
builder.Services.AddSingleton(games);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowBlazor");

app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.MapGrpcService<BattleshipGRPCService>().EnableGrpcWeb().RequireCors("AllowBlazor");
app.MapHub<GameHub>("/gamehub").RequireCors("AllowBlazor");

app.MapPost("/game/start", (ILogger<Program> logger, ConcurrentDictionary<string, Game> games) =>
{
    logger.LogInformation("[NOUVELLE PARTIE] Demarrage d'une nouvelle partie...");
    
    var game = new Game();
    
    GenerateOpponentBoard(game.PlayerBoard);
    logger.LogInformation("[NOUVELLE PARTIE] Bateaux du joueur places");
    
    GenerateOpponentBoard(game.OpponentBoard);
    logger.LogInformation("[NOUVELLE PARTIE] Bateaux de l'adversaire places");
    
    games[game.Id] = game;
    
    int shipCount = CountShips(game.OpponentBoard);
    logger.LogInformation("[NOUVELLE PARTIE] Partie creee avec ID: {GameId}", game.Id);
    logger.LogInformation("[NOUVELLE PARTIE] {ShipCount} cases avec bateaux sur chaque grille", shipCount);
    
    return TypedResults.Ok(new 
    { 
        gameId = game.Id,
        playerBoard = ConvertBoardToDto(game.PlayerBoard, true)
    });
}).RequireCors("AllowBlazor");

app.MapPost("/game/{gameId}/attack", ([Microsoft.AspNetCore.Mvc.FromRoute] string gameId, [Microsoft.AspNetCore.Mvc.FromBody] AttackRequest request, IValidator<AttackRequest> validator, ILogger<Program> logger, ConcurrentDictionary<string, Game> games) =>
{
    logger.LogInformation("[ATTAQUE] Joueur attaque position ({X}, {Y}) - Game ID: {GameId}", request.X, request.Y, gameId);

    if (!games.TryGetValue(gameId, out var game))
    {
        logger.LogWarning("[ATTAQUE] Partie non trouvee: {GameId}", gameId);
        return Results.NotFound(new { message = "Partie non trouvée" });
    }

    var validation = validator.Validate(request);
    if (!validation.IsValid)
    {
        var errors = validation.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }).ToList();
        logger.LogWarning("[ATTAQUE] Requete invalide: {@Errors}", errors);
        return Results.BadRequest(new { errors });
    }

    var (hit, alreadyHit) = game.OpponentBoard.Attack(request.X, request.Y);

    if (alreadyHit)
    {
        logger.LogWarning("[ATTAQUE] Case ({X}, {Y}) deja attaquee", request.X, request.Y);
        return Results.BadRequest(new { message = "Case déjà attaquée" });
    }

    int playerHitCount = CountHits(game.OpponentBoard);
    bool playerWon = playerHitCount >= 13;
    string message = "";

    if (hit)
    {
        message = playerWon ? "Touché-Coulé ! Vous avez gagné !" : "Touché !";
        logger.LogInformation("[ATTAQUE] TOUCHE ! Position ({X}, {Y}) - Coups reussis joueur: {HitCount}/13", request.X, request.Y, playerHitCount);
    }
    else
    {
        message = "Raté";
        logger.LogInformation("[ATTAQUE] Rate a la position ({X}, {Y}) - Coups reussis joueur: {HitCount}/13", request.X, request.Y, playerHitCount);
    }

    bool aiHit = false;
    int aiX = 0, aiY = 0;
    bool aiWon = false;
    bool foundTarget = false;

    if (!playerWon)
    {
        var random = new Random();
        int attempts = 0;

        while (!foundTarget && attempts < 100)
        {
            aiX = random.Next(0, Board.Size);
            aiY = random.Next(0, Board.Size);

            if (!game.PlayerBoard.Grid[aiX, aiY].IsHit)
            {
                foundTarget = true;
            }
            attempts++;
        }

        if (foundTarget)
        {
            var (aiHitResult, _) = game.PlayerBoard.Attack(aiX, aiY);
            aiHit = aiHitResult;

            int aiHitCount = CountHits(game.PlayerBoard);
            aiWon = aiHitCount >= 13;

            logger.LogInformation("[IA] L'IA attaque position ({X}, {Y}) - {Result}", aiX, aiY, aiHit ? "TOUCHE" : "Rate");
            logger.LogInformation("[IA] Coups reussis IA: {HitCount}/13", aiHitCount);

            if (aiWon)
            {
                logger.LogInformation("[DEFAITE] L'IA a gagne avec {HitCount} coups reussis !", aiHitCount);
                message = "L'IA a gagné ! Vous avez perdu.";
            }
            else if (aiHit)
            {
                message += " - L'IA a touché votre bateau !";
            }
            else
            {
                message += " - L'IA a raté";
            }
        }
    }

    bool gameOver = playerWon || aiWon;

    if (gameOver)
    {
        logger.LogInformation("[FIN DE PARTIE] Partie terminee - Gagnant: {Winner}", playerWon ? "Joueur" : "IA");
    }

    return Results.Ok(new 
    { 
        hit,
        message,
        hitCount = playerHitCount,
        opponentBoard = ConvertBoardToDto(game.OpponentBoard, false),
        playerBoard = ConvertBoardToDto(game.PlayerBoard, true),
        gameOver,
        playerWon,
        aiAttack = foundTarget ? new { x = aiX, y = aiY, hit = aiHit } : null
    });
}).RequireCors("AllowBlazor");

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
