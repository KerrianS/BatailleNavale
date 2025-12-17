namespace BattleShip.Models;

public class Game
{
    public string Id { get; set; }
    public Board PlayerBoard { get; set; }
    public Board OpponentBoard { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<AttackHistory> History { get; set; }

    public Game()
    {
        Id = Guid.NewGuid().ToString();
        PlayerBoard = new Board();
        OpponentBoard = new Board();
        CreatedAt = DateTime.UtcNow;
        History = new List<AttackHistory>();
    }
}