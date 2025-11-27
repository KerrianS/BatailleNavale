namespace BattleShip.Models;

public class ShipPlacement
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Size { get; set; }
    public bool IsHorizontal { get; set; }
    public ShipType ShipType { get; set; }
}

public enum ShipType
{
    Carrier = 1,        // Porte-avions (5 cases)
    Battleship = 2,     // Croiseur (4 cases)
    Cruiser = 3,        // Contre-torpilleur (3 cases)
    Submarine = 4,      // Sous-marin (3 cases)
    Destroyer = 5       // Torpilleur (2 cases)
}
