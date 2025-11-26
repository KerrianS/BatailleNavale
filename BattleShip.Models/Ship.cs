namespace BattleShip.Models;

public class Ship
{
    public int Size { get; set; }
    public List<(int X, int Y)> Positions { get; set; } = new();
    public ShipType Type { get; set; }

    public bool IsSunk()
    {
        // A ship is sunk when all its positions are hit
        // This would require tracking which positions are hit
        // For now, return false as it requires more board state tracking
        return false;
    }
}
