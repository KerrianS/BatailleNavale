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
    Carrier = 5,        // Porte-avions
    Battleship = 4,     // Croiseur
    Cruiser = 3,        // Contre-torpilleur
    Submarine = 3,      // Sous-marin
    Destroyer = 2       // Torpilleur
}
