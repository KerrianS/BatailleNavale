using System.Linq;

namespace BattleShip.Models;

public class SmartAI
{
    private enum AIMode
    {
        Hunt,      // Mode recherche : tir aléatoire
        Target,    // Mode cible : exploiter un touché
        Destroy    // Mode destruction : suivre la ligne du bateau
    }

    private AIMode _currentMode = AIMode.Hunt;
    private List<(int x, int y)> _lastHits = new();
    private List<(int x, int y)> _pendingTargets = new();
    private bool _isHorizontal = true;

    // Méthodes de debug
    public string GetCurrentMode() => _currentMode.ToString();
    public int GetLastHitsCount() => _lastHits.Count;
    public int GetPendingTargetsCount() => _pendingTargets.Count;

    public (int x, int y) GetNextAttack(bool[,] attackedCells, int boardSize)
    {
        switch (_currentMode)
        {
            case AIMode.Destroy:
                return GetDestroyModeAttack(attackedCells, boardSize);
            case AIMode.Target:
                return GetTargetModeAttack(attackedCells, boardSize);
            default:
                return GetHuntModeAttack(attackedCells, boardSize);
        }
    }

    public void RegisterHit(int x, int y, bool hit)
    {
        if (hit)
        {
            _lastHits.Add((x, y));

            if (_lastHits.Count == 1)
            {
                // Premier touché : passer en mode Target
                _currentMode = AIMode.Target;
                AddAdjacentCells(x, y);
            }
            else if (_lastHits.Count == 2)
            {
                // Vérifier si les 2 touchés sont adjacents (même bateau)
                var first = _lastHits[0];
                var second = _lastHits[1];
                
                int dx = Math.Abs(first.x - second.x);
                int dy = Math.Abs(first.y - second.y);
                
                // Si adjacents, passer en mode Destroy
                if ((dx == 1 && dy == 0) || (dx == 0 && dy == 1))
                {
                    _currentMode = AIMode.Destroy;
                    DetermineOrientation();
                }
                else
                {
                    // Pas adjacents, ce sont des bateaux différents
                    // Garder uniquement le dernier touché et explorer autour
                    _lastHits.Clear();
                    _lastHits.Add((x, y));
                    _currentMode = AIMode.Target;
                    AddAdjacentCells(x, y);
                }
            }
            else if (_lastHits.Count >= 3)
            {
                // Vérifier que le nouveau touché est aligné avec les précédents
                if (!IsAligned())
                {
                    // Nouveau bateau détecté, recommencer avec ce touché
                    _lastHits.Clear();
                    _lastHits.Add((x, y));
                    _currentMode = AIMode.Target;
                    AddAdjacentCells(x, y);
                }
                else
                {
                    _currentMode = AIMode.Destroy;
                }
            }
        }
        else
        {
            // Raté
            if (_currentMode == AIMode.Destroy && _lastHits.Count >= 2)
            {
                // On a raté en suivant la ligne, essayer l'autre direction
                // ou revenir en mode Target s'il reste des cibles pendantes
                if (_pendingTargets.Count > 0)
                {
                    _currentMode = AIMode.Target;
                }
            }
            else if (_currentMode == AIMode.Target && _pendingTargets.Count == 0)
            {
                // Plus de cibles à explorer, retour en mode Hunt
                ResetToHuntMode();
            }
        }
    }

    private bool IsAligned()
    {
        if (_lastHits.Count < 2) return true;
        
        var first = _lastHits[0];
        bool isHorizontal = _lastHits.All(h => h.y == first.y);
        bool isVertical = _lastHits.All(h => h.x == first.x);
        
        return isHorizontal || isVertical;
    }

    public void OnShipSunk()
    {
        // Un bateau a été coulé, réinitialiser pour chercher le prochain
        ResetToHuntMode();
    }

    private void ResetToHuntMode()
    {
        _currentMode = AIMode.Hunt;
        _lastHits.Clear();
        _pendingTargets.Clear();
    }

    private (int x, int y) GetHuntModeAttack(bool[,] attackedCells, int boardSize)
    {
        // Stratégie en damier pour être plus efficace
        var random = new Random();
        var availableCells = new List<(int x, int y)>();

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                if (!attackedCells[x, y])
                {
                    // Privilégier les cases en damier (x + y) pair pour optimiser
                    if ((x + y) % 2 == 0)
                    {
                        availableCells.Add((x, y));
                    }
                }
            }
        }

        // Si aucune case en damier disponible, prendre n'importe quelle case
        if (availableCells.Count == 0)
        {
            for (int x = 0; x < boardSize; x++)
            {
                for (int y = 0; y < boardSize; y++)
                {
                    if (!attackedCells[x, y])
                    {
                        availableCells.Add((x, y));
                    }
                }
            }
        }

        if (availableCells.Count > 0)
        {
            return availableCells[random.Next(availableCells.Count)];
        }

        // Fallback : chercher n'importe quelle case disponible
        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                if (!attackedCells[x, y])
                    return (x, y);
            }
        }

        return (0, 0);
    }

    private (int x, int y) GetTargetModeAttack(bool[,] attackedCells, int boardSize)
    {
        // Attaquer les cellules adjacentes au dernier touché
        while (_pendingTargets.Count > 0)
        {
            var target = _pendingTargets[0];
            _pendingTargets.RemoveAt(0);

            if (IsValidCell(target.x, target.y, boardSize) && !attackedCells[target.x, target.y])
            {
                return target;
            }
        }

        // Plus de cibles, retour en mode Hunt
        ResetToHuntMode();
        return GetHuntModeAttack(attackedCells, boardSize);
    }

    private (int x, int y) GetDestroyModeAttack(bool[,] attackedCells, int boardSize)
    {
        if (_lastHits.Count < 2) 
        {
            return GetTargetModeAttack(attackedCells, boardSize);
        }

        // Continuer dans la direction établie
        var firstHit = _lastHits[0];
        var lastHit = _lastHits[_lastHits.Count - 1];

        (int x, int y) nextCell;

        if (_isHorizontal)
        {
            // Essayer de continuer vers la droite
            nextCell = (lastHit.x + 1, lastHit.y);
            if (IsValidCell(nextCell.x, nextCell.y, boardSize) && !attackedCells[nextCell.x, nextCell.y])
            {
                return nextCell;
            }

            // Sinon essayer vers la gauche (depuis le premier touché)
            nextCell = (firstHit.x - 1, firstHit.y);
            if (IsValidCell(nextCell.x, nextCell.y, boardSize) && !attackedCells[nextCell.x, nextCell.y])
            {
                return nextCell;
            }
        }
        else
        {
            // Essayer de continuer vers le bas
            nextCell = (lastHit.x, lastHit.y + 1);
            if (IsValidCell(nextCell.x, nextCell.y, boardSize) && !attackedCells[nextCell.x, nextCell.y])
            {
                return nextCell;
            }

            // Sinon essayer vers le haut (depuis le premier touché)
            nextCell = (firstHit.x, firstHit.y - 1);
            if (IsValidCell(nextCell.x, nextCell.y, boardSize) && !attackedCells[nextCell.x, nextCell.y])
            {
                return nextCell;
            }
        }

        // Toutes les directions sont bloquées
        // Vérifier s'il reste des cellules adjacentes à explorer des anciennes touches
        if (_pendingTargets.Count > 0)
        {
            // Retourner en mode Target pour explorer les autres cellules adjacentes
            _currentMode = AIMode.Target;
            _lastHits.Clear();
            return GetTargetModeAttack(attackedCells, boardSize);
        }

        // Plus rien à explorer, continuer en mode Hunt
        // Le service gRPC appellera OnShipSunk() quand le bateau sera vraiment coulé
        _currentMode = AIMode.Hunt;
        _lastHits.Clear();
        return GetHuntModeAttack(attackedCells, boardSize);
    }

    private void DetermineOrientation()
    {
        if (_lastHits.Count < 2) return;

        var first = _lastHits[0];
        var second = _lastHits[1];

        _isHorizontal = first.y == second.y;
    }

    private void AddAdjacentCells(int x, int y)
    {
        // Ajouter les 4 cellules adjacentes (haut, bas, gauche, droite)
        _pendingTargets.Clear();
        _pendingTargets.Add((x, y - 1)); // Haut
        _pendingTargets.Add((x, y + 1)); // Bas
        _pendingTargets.Add((x - 1, y)); // Gauche
        _pendingTargets.Add((x + 1, y)); // Droite
    }

    private bool IsValidCell(int x, int y, int boardSize)
    {
        return x >= 0 && x < boardSize && y >= 0 && y < boardSize;
    }
}
