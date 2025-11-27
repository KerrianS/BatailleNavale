namespace BattleShip.Models;

public class Ship
{
    public int Size { get; set; }
    public List<(int X, int Y)> Positions { get; set; } = new();
    public ShipType Type { get; set; }

    public bool IsSunk(Cell[,] grid)
    {
        // Un bateau est coulé si toutes ses positions sont touchées
        return Positions.All(pos => grid[pos.X, pos.Y].IsHit);
    }
}
