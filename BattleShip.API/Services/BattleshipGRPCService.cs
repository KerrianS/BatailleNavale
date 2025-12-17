using Grpc.Core;
using BattleShip.API.Protos;
using BattleShip.Models;
using System.Collections.Concurrent;

namespace BattleShip.API.Services;

public class BattleshipGRPCService : BattleshipService.BattleshipServiceBase
{
    private readonly ILogger<BattleshipGRPCService> _logger;
    private readonly ConcurrentDictionary<string, Game> _games;
    private static readonly ConcurrentDictionary<string, SmartAI> _aiInstances = new();

    public BattleshipGRPCService(ILogger<BattleshipGRPCService> logger, ConcurrentDictionary<string, Game> games)
    {
        _logger = logger;
        _games = games;
    }

    public override Task<AttackResponseGRPC> Attack(AttackRequestGRPC request, ServerCallContext context)
    {
        _logger.LogInformation("[gRPC ATTAQUE] Position ({X}, {Y}) - Game ID: {GameId}", request.X, request.Y, request.GameId);

        if (!_games.TryGetValue(request.GameId, out var game))
        {
            _logger.LogWarning("[gRPC ATTAQUE] Partie non trouvee: {GameId}", request.GameId);
            throw new RpcException(new Status(StatusCode.NotFound, "Partie non trouvée"));
        }

        int boardSize = game.OpponentBoard.CurrentSize;

        if (request.X < 0 || request.X >= boardSize || request.Y < 0 || request.Y >= boardSize)
        {
            _logger.LogWarning("[gRPC ATTAQUE] Coordonnees invalides: ({X}, {Y}) - Taille grille: {Size}", request.X, request.Y, boardSize);
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Coordonnées invalides. X et Y doivent être entre 0 et {boardSize - 1}."));
        }

        var (hit, alreadyHit) = game.OpponentBoard.Attack(request.X, request.Y);

        if (alreadyHit)
        {
            _logger.LogWarning("[gRPC ATTAQUE] Case ({X}, {Y}) deja attaquee", request.X, request.Y);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Case déjà attaquée"));
        }

        game.History.Add(new AttackHistory(request.X, request.Y, hit, true));

        List<Ship> previousPlayerSunkShips = game.OpponentBoard.Ships
            .Where(ship => ship.IsSunk(game.OpponentBoard.Grid))
            .ToList();
        int previousPlayerSunkCount = previousPlayerSunkShips.Count;

        List<Ship> sunkShips = game.OpponentBoard.Ships
            .Where(ship => ship.IsSunk(game.OpponentBoard.Grid))
            .ToList();

        int playerHitCount = CountHits(game.OpponentBoard);
        int playerSunkShipsCount = sunkShips.Count;
        bool playerWon = playerSunkShipsCount >= 5;
        string message = "";

        if (hit)
        {
            if (playerWon)
            {
                message = "Touché-Coulé ! Vous avez gagné !";
            }
            else if (sunkShips.Count > previousPlayerSunkCount)
            {
                message = $"Coulé ! Vous avez coulé {sunkShips.Count}/5 bateaux.";
            }
            else
            {
                message = "Touché !";
            }
            _logger.LogInformation("[gRPC ATTAQUE] TOUCHE ! Position ({X}, {Y}) - Bateaux coules joueur: {SunkCount}/5", request.X, request.Y, playerSunkShipsCount);
        }
        else
        {
            message = "Raté";
            _logger.LogInformation("[gRPC ATTAQUE] Rate a la position ({X}, {Y}) - Bateaux coules joueur: {SunkCount}/5", request.X, request.Y, playerSunkShipsCount);
        }

        bool aiHit = false;
        int aiX = 0, aiY = 0;
        bool aiWon = false;
        bool foundTarget = false;

        if (!playerWon)
        {

            var ai = _aiInstances.GetOrAdd(request.GameId, _ => {
                _logger.LogInformation("[gRPC IA] Création d'une nouvelle instance SmartAI pour GameId: {GameId}", request.GameId);
                return new SmartAI();
            });

            _logger.LogInformation("[gRPC IA DEBUG AVANT] GameId: {GameId}, Mode: {Mode}, LastHits: {HitsCount}, PendingTargets: {PendingCount}",
                request.GameId, ai.GetCurrentMode(), ai.GetLastHitsCount(), ai.GetPendingTargetsCount());

            bool[,] attackedCells = new bool[game.PlayerBoard.CurrentSize, game.PlayerBoard.CurrentSize];
            for (int x = 0; x < game.PlayerBoard.CurrentSize; x++)
            {
                for (int y = 0; y < game.PlayerBoard.CurrentSize; y++)
                {
                    attackedCells[x, y] = game.PlayerBoard.Grid[x, y].IsHit;
                }
            }

            (aiX, aiY) = ai.GetNextAttack(attackedCells, game.PlayerBoard.CurrentSize);
            foundTarget = true;

            if (foundTarget)
            {
                var (aiHitResult, _) = game.PlayerBoard.Attack(aiX, aiY);
                aiHit = aiHitResult;

                ai.RegisterHit(aiX, aiY, aiHit);

                _logger.LogInformation("[gRPC IA DEBUG APRES] Mode: {Mode}, LastHits: {HitsCount}, PendingTargets: {PendingCount}",
                    ai.GetCurrentMode(), ai.GetLastHitsCount(), ai.GetPendingTargetsCount());

                game.History.Add(new AttackHistory(aiX, aiY, aiHit, false));

                List<Ship> previousAISunkShips = game.PlayerBoard.Ships
                    .Where(ship => ship.IsSunk(game.PlayerBoard.Grid))
                    .ToList();
                int previousAISunkCount = previousAISunkShips.Count;

                List<Ship> aiSunkShips = game.PlayerBoard.Ships
                    .Where(ship => ship.IsSunk(game.PlayerBoard.Grid))
                    .ToList();

                if (aiSunkShips.Count > previousAISunkCount)
                {
                    _logger.LogInformation("[gRPC IA] L'IA a coulé un bateau ! Total: {SunkCount}/5", aiSunkShips.Count);
                    ai.OnShipSunk();
                }

                int aiHitCount = CountHits(game.PlayerBoard);
                int aiSunkShipsCount = aiSunkShips.Count;
                aiWon = aiSunkShipsCount >= 5;

                _logger.LogInformation("[gRPC IA] L'IA attaque position ({X}, {Y}) - {Result}", aiX, aiY, aiHit ? "TOUCHE" : "Rate");
                _logger.LogInformation("[gRPC IA] Bateaux coules IA: {SunkCount}/5", aiSunkShipsCount);

                if (aiWon)
                {
                    _logger.LogInformation("[gRPC DEFAITE] L'IA a gagne avec {SunkCount} bateaux coules !", aiSunkShipsCount);
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
            _logger.LogInformation("[gRPC FIN DE PARTIE] Partie terminee - Gagnant: {Winner}", playerWon ? "Joueur" : "IA");
        }

        var response = new AttackResponseGRPC
        {
            Hit = hit,
            Message = message,
            HitCount = playerHitCount,
            GameOver = gameOver,
            PlayerWon = playerWon,
            OpponentBoard = ConvertBoardToDto(game.OpponentBoard, false, sunkShips),
            PlayerBoard = ConvertBoardToDto(game.PlayerBoard, true, new List<Ship>())
        };

        if (foundTarget)
        {
            response.AiAttack = new AIAttackDto
            {
                X = aiX,
                Y = aiY,
                Hit = aiHit
            };
        }

        return Task.FromResult(response);
    }

    private int CountHits(Board board)
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

    private BoardDto ConvertBoardToDto(Board board, bool showShips, List<Ship> sunkShips)
    {
        var boardDto = new BoardDto();
        var sunkPositions = new HashSet<(int X, int Y)>();

        foreach (var ship in sunkShips)
        {
            foreach (var pos in ship.Positions)
            {
                sunkPositions.Add(pos);
            }
        }

        for (int x = 0; x < board.CurrentSize; x++)
        {
            for (int y = 0; y < board.CurrentSize; y++)
            {
                var cell = board.Grid[x, y];
                bool isSunkPosition = sunkPositions.Contains((x, y));

                boardDto.Cells.Add(new CellDto
                {
                    X = cell.X,
                    Y = cell.Y,
                    HasShip = showShips ? cell.HasShip : (isSunkPosition || (cell.IsHit && cell.HasShip)),
                    IsHit = cell.IsHit,
                    IsSunk = isSunkPosition,
                    ShipType = cell.ShipType.HasValue ? (int)cell.ShipType.Value : -1,
                    IsShipStart = cell.IsShipStart,
                    IsHorizontal = cell.IsHorizontal
                });
            }
        }
        return boardDto;
    }
}