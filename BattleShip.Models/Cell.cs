namespace BattleShip.Models;

public class Cell
{
    public int X { get; set; }
    public int Y { get; set; }
    public bool HasShip { get; set; }
    public bool IsHit { get; set; }
    public bool IsSunk { get; set; }
    
    public Cell(int x, int y)
    {
        X = x;
        Y = y;
        HasShip = false;
        IsHit = false;
        IsSunk = false;
    }
}
