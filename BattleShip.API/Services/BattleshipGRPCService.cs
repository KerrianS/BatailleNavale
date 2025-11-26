using Grpc.Core;
using BattleShip.API.Protos;
using BattleShip.Models;
using System.Collections.Concurrent;

namespace BattleShip.API.Services;

public class BattleshipGRPCService : BattleshipService.BattleshipServiceBase
{
    private readonly ILogger<BattleshipGRPCService> _logger;
    private readonly ConcurrentDictionary<string, Game> _games;

    public BattleshipGRPCService(ILogger<BattleshipGRPCService> logger, ConcurrentDictionary<string, Game> games)
    {
        _logger = logger;
        _games = games;
    }

    public override Task<AttackResponseGRPC> Attack(AttackRequestGRPC request, ServerCallContext context)
    {
        _logger.LogInformation("[gRPC ATTAQUE] Position ({X}, {Y}) - Game ID: {GameId}", request.X, request.Y, request.GameId);

        if (request.X < 0 || request.X >= Board.Size || request.Y < 0 || request.Y >= Board.Size)
        {
            _logger.LogWarning("[gRPC ATTAQUE] Coordonnees invalides: ({X}, {Y})", request.X, request.Y);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Coordonnées invalides. X et Y doivent être entre 0 et 9."));
        }

        if (!_games.TryGetValue(request.GameId, out var game))
        {
            _logger.LogWarning("[gRPC ATTAQUE] Partie non trouvee: {GameId}", request.GameId);
            throw new RpcException(new Status(StatusCode.NotFound, "Partie non trouvée"));
        }

        var (hit, alreadyHit) = game.OpponentBoard.Attack(request.X, request.Y);

        if (alreadyHit)
        {
            _logger.LogWarning("[gRPC ATTAQUE] Case ({X}, {Y}) deja attaquee", request.X, request.Y);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Case déjà attaquée"));
        }

        int playerHitCount = CountHits(game.OpponentBoard);
        bool playerWon = playerHitCount >= 13;
        string message = "";

        if (hit)
        {
            message = playerWon ? "Touché-Coulé ! Vous avez gagné !" : "Touché !";
            _logger.LogInformation("[gRPC ATTAQUE] TOUCHE ! Position ({X}, {Y}) - Coups reussis joueur: {HitCount}/13", request.X, request.Y, playerHitCount);
        }
        else
        {
            message = "Raté";
            _logger.LogInformation("[gRPC ATTAQUE] Rate a la position ({X}, {Y}) - Coups reussis joueur: {HitCount}/13", request.X, request.Y, playerHitCount);
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

                _logger.LogInformation("[gRPC IA] L'IA attaque position ({X}, {Y}) - {Result}", aiX, aiY, aiHit ? "TOUCHE" : "Rate");
                _logger.LogInformation("[gRPC IA] Coups reussis IA: {HitCount}/13", aiHitCount);

                if (aiWon)
                {
                    _logger.LogInformation("[gRPC DEFAITE] L'IA a gagne avec {HitCount} coups reussis !", aiHitCount);
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
            _logger.LogInformation("[gRPC FIN DE PARTIE] Partie terminee - Gagnant: {Winner}", playerWon ? "Joueur" : "IA");
        }

        var response = new AttackResponseGRPC
        {
            Hit = hit,
            Message = message,
            HitCount = playerHitCount,
            GameOver = gameOver,
            PlayerWon = playerWon,
            OpponentBoard = ConvertBoardToDto(game.OpponentBoard, false),
            PlayerBoard = ConvertBoardToDto(game.PlayerBoard, true)
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

    private BoardDto ConvertBoardToDto(Board board, bool showShips)
    {
        var boardDto = new BoardDto();
        for (int x = 0; x < Board.Size; x++)
        {
            for (int y = 0; y < Board.Size; y++)
            {
                var cell = board.Grid[x, y];
                boardDto.Cells.Add(new CellDto
                {
                    X = cell.X,
                    Y = cell.Y,
                    HasShip = showShips ? cell.HasShip : (cell.IsHit && cell.HasShip),
                    IsHit = cell.IsHit
                });
            }
        }
        return boardDto;
    }
}
