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
    Carrier,        // Porte-avions (5)
    Battleship,     // Croiseur (4)
    Cruiser,        // Contre-torpilleur (3)
    Submarine,      // Sous-marin (3)
    Destroyer       // Torpilleur (2)
}

public static class ShipTypeExtensions
{
    public static int GetSize(this ShipType type)
    {
        return type switch
        {
            ShipType.Carrier => 5,
            ShipType.Battleship => 4,
            ShipType.Cruiser => 3,
            ShipType.Submarine => 3,
            ShipType.Destroyer => 2,
            _ => 1
        };
    }
}
