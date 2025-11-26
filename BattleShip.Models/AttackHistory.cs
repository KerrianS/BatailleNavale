namespace BattleShip.Models;

public class AttackHistory
{
    public int X { get; set; }
    public int Y { get; set; }
    public bool Hit { get; set; }
    public bool IsPlayer { get; set; }
    public DateTime Timestamp { get; set; }
    
    public AttackHistory()
    {
        Timestamp = DateTime.UtcNow;
    }
    
    public AttackHistory(int x, int y, bool hit, bool isPlayer)
    {
        X = x;
        Y = y;
        Hit = hit;
        IsPlayer = isPlayer;
        Timestamp = DateTime.UtcNow;
    }
}
