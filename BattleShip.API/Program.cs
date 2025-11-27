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

app.MapPost("/game/start", (ILogger<Program> logger, ConcurrentDictionary<string, Game> games, int? gridSize) =>
{
    int size = gridSize ?? 10;
    logger.LogInformation("[NOUVELLE PARTIE] Demarrage d'une nouvelle partie avec grille {Size}x{Size}...", size, size);
    
    var game = new Game
    {
        PlayerBoard = new Board(size),
        OpponentBoard = new Board(size)
    };
    
    // Ne pas générer les bateaux du joueur - ils seront placés via /game/{gameId}/place-ships
    logger.LogInformation("[NOUVELLE PARTIE] Plateau du joueur vide - en attente des placements");
    
    GenerateOpponentBoard(game.OpponentBoard, size);
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

app.MapPost("/game/{gameId}/place-ships", ([Microsoft.AspNetCore.Mvc.FromRoute] string gameId, [Microsoft.AspNetCore.Mvc.FromBody] List<ShipPlacement> placements, ILogger<Program> logger, ConcurrentDictionary<string, Game> games) =>
{
    logger.LogInformation("[PLACEMENT] Reception de {Count} placements pour Game ID: {GameId}", placements.Count, gameId);

    if (!games.TryGetValue(gameId, out var game))
    {
        logger.LogWarning("[PLACEMENT] Partie non trouvee: {GameId}", gameId);
        return Results.NotFound(new { message = "Partie non trouvée" });
    }

    // Réinitialiser complètement le PlayerBoard pour éviter les bateaux aléatoires
    int boardSize = game.PlayerBoard.CurrentSize;
    game.PlayerBoard = new Board(boardSize);
    logger.LogInformation("[PLACEMENT] Plateau du joueur reinitialise");

    // Placer les bateaux du joueur
    foreach (var placement in placements)
    {
        game.PlayerBoard.PlaceShip(placement.X, placement.Y, placement.Size, placement.IsHorizontal);
        logger.LogInformation("[PLACEMENT] Bateau de taille {Size} place a ({X},{Y}) - {Orientation}", 
            placement.Size, placement.X, placement.Y, placement.IsHorizontal ? "Horizontal" : "Vertical");
    }

    int shipCount = CountShips(game.PlayerBoard);
    logger.LogInformation("[PLACEMENT] Total: {ShipCount} cases avec bateaux sur le plateau du joueur", shipCount);

    return Results.Ok(new { success = true, shipCount });
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

    // Vérifier les bateaux coulés par le joueur
    List<Ship> previousPlayerSunkShips = game.OpponentBoard.Ships
        .Where(ship => ship.IsSunk(game.OpponentBoard.Grid))
        .ToList();
    int previousPlayerSunkCount = previousPlayerSunkShips.Count;

    List<Ship> playerSunkShips = game.OpponentBoard.Ships
        .Where(ship => ship.IsSunk(game.OpponentBoard.Grid))
        .ToList();

    int playerHitCount = CountHits(game.OpponentBoard);
    int playerSunkShipsCount = playerSunkShips.Count;
    bool playerWon = playerSunkShipsCount >= 5;
    string message = "";

    if (hit)
    {
        if (playerWon)
        {
            message = "Touché-Coulé ! Vous avez gagné !";
        }
        else if (playerSunkShips.Count > previousPlayerSunkCount)
        {
            message = $"Coulé ! Vous avez coulé {playerSunkShips.Count}/5 bateaux.";
        }
        else
        {
            message = "Touché !";
        }
        logger.LogInformation("[ATTAQUE] TOUCHE ! Position ({X}, {Y}) - Bateaux coules joueur: {SunkCount}/5", request.X, request.Y, playerSunkShipsCount);
    }
    else
    {
        message = "Raté";
        logger.LogInformation("[ATTAQUE] Rate a la position ({X}, {Y}) - Bateaux coules joueur: {SunkCount}/5", request.X, request.Y, playerSunkShipsCount);
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
            aiX = random.Next(0, game.PlayerBoard.CurrentSize);
            aiY = random.Next(0, game.PlayerBoard.CurrentSize);

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

            // Vérifier les bateaux coulés par l'IA
            List<Ship> previousAISunkShips = game.PlayerBoard.Ships
                .Where(ship => ship.IsSunk(game.PlayerBoard.Grid))
                .ToList();
            int previousAISunkCount = previousAISunkShips.Count;

            List<Ship> aiSunkShips = game.PlayerBoard.Ships
                .Where(ship => ship.IsSunk(game.PlayerBoard.Grid))
                .ToList();

            int aiHitCount = CountHits(game.PlayerBoard);
            int aiSunkShipsCount = aiSunkShips.Count;
            aiWon = aiSunkShipsCount >= 5;

            logger.LogInformation("[IA] L'IA attaque position ({X}, {Y}) - {Result}", aiX, aiY, aiHit ? "TOUCHE" : "Rate");
            logger.LogInformation("[IA] Bateaux coules IA: {SunkCount}/5", aiSunkShipsCount);

            if (aiWon)
            {
                logger.LogInformation("[DEFAITE] L'IA a gagne avec {SunkCount} bateaux coules !", aiSunkShipsCount);
                message = "L'IA a gagné ! Vous avez perdu.";
            }
            else if (aiHit)
            {
                if (aiSunkShips.Count > previousAISunkCount)
                {
                    message += $" - L'IA a coulé votre bateau ! ({aiSunkShipsCount}/5)";
                }
                else
                {
                    message += " - L'IA a touché votre bateau !";
                }
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

void GenerateOpponentBoard(Board board, int gridSize)
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
            int x = random.Next(0, gridSize);
            int y = random.Next(0, gridSize);
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
    for (int x = 0; x < board.CurrentSize; x++)
    {
        for (int y = 0; y < board.CurrentSize; y++)
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
    for (int x = 0; x < board.CurrentSize; x++)
    {
        for (int y = 0; y < board.CurrentSize; y++)
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

int CountHits(Board board)
{
    int count = 0;
    for (int x = 0; x < board.CurrentSize; x++)
    {
        for (int y = 0; y < board.CurrentSize; y++)
        {
            if (board.Grid[x, y].HasShip && board.Grid[x, y].IsHit)
                count++;
        }
    }
    return count;
}
