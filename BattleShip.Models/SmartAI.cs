using System.Linq;

namespace BattleShip.Models;

public class SmartAI
{
    private enum AIMode
    {
        Hunt,
        Target,
        Destroy
    }

    private AIMode _currentMode = AIMode.Hunt;
    private List<(int x, int y)> _lastHits = new();
    private List<(int x, int y)> _pendingTargets = new();
    private bool _isHorizontal = true;

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

                _currentMode = AIMode.Target;
                AddAdjacentCells(x, y);
            }
            else if (_lastHits.Count == 2)
            {

                var first = _lastHits[0];
                var second = _lastHits[1];

                int dx = Math.Abs(first.x - second.x);
                int dy = Math.Abs(first.y - second.y);

                if ((dx == 1 && dy == 0) || (dx == 0 && dy == 1))
                {
                    _currentMode = AIMode.Destroy;
                    DetermineOrientation();
                }
                else
                {

                    _lastHits.Clear();
                    _lastHits.Add((x, y));
                    _currentMode = AIMode.Target;
                    AddAdjacentCells(x, y);
                }
            }
            else if (_lastHits.Count >= 3)
            {

                if (!IsAligned())
                {

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

            if (_currentMode == AIMode.Destroy && _lastHits.Count >= 2)
            {

                if (_pendingTargets.Count > 0)
                {
                    _currentMode = AIMode.Target;
                }
            }
            else if (_currentMode == AIMode.Target && _pendingTargets.Count == 0)
            {

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

        var random = new Random();
        var availableCells = new List<(int x, int y)>();

        for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                if (!attackedCells[x, y])
                {

                    if ((x + y) % 2 == 0)
                    {
                        availableCells.Add((x, y));
                    }
                }
            }
        }

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

        while (_pendingTargets.Count > 0)
        {
            var target = _pendingTargets[0];
            _pendingTargets.RemoveAt(0);

            if (IsValidCell(target.x, target.y, boardSize) && !attackedCells[target.x, target.y])
            {
                return target;
            }
        }

        ResetToHuntMode();
        return GetHuntModeAttack(attackedCells, boardSize);
    }

    private (int x, int y) GetDestroyModeAttack(bool[,] attackedCells, int boardSize)
    {
        if (_lastHits.Count < 2)
        {
            return GetTargetModeAttack(attackedCells, boardSize);
        }

        var firstHit = _lastHits[0];
        var lastHit = _lastHits[_lastHits.Count - 1];

        (int x, int y) nextCell;

        if (_isHorizontal)
        {

            nextCell = (lastHit.x + 1, lastHit.y);
            if (IsValidCell(nextCell.x, nextCell.y, boardSize) && !attackedCells[nextCell.x, nextCell.y])
            {
                return nextCell;
            }

            nextCell = (firstHit.x - 1, firstHit.y);
            if (IsValidCell(nextCell.x, nextCell.y, boardSize) && !attackedCells[nextCell.x, nextCell.y])
            {
                return nextCell;
            }
        }
        else
        {

            nextCell = (lastHit.x, lastHit.y + 1);
            if (IsValidCell(nextCell.x, nextCell.y, boardSize) && !attackedCells[nextCell.x, nextCell.y])
            {
                return nextCell;
            }

            nextCell = (firstHit.x, firstHit.y - 1);
            if (IsValidCell(nextCell.x, nextCell.y, boardSize) && !attackedCells[nextCell.x, nextCell.y])
            {
                return nextCell;
            }
        }

        if (_pendingTargets.Count > 0)
        {

            _currentMode = AIMode.Target;
            _lastHits.Clear();
            return GetTargetModeAttack(attackedCells, boardSize);
        }

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

        _pendingTargets.Clear();
        _pendingTargets.Add((x, y - 1));
        _pendingTargets.Add((x, y + 1));
        _pendingTargets.Add((x - 1, y));
        _pendingTargets.Add((x + 1, y));
    }

    private bool IsValidCell(int x, int y, int boardSize)
    {
        return x >= 0 && x < boardSize && y >= 0 && y < boardSize;
    }
}