namespace BattleShip.Models;

public class MultiplayerGame : Game
{
    public string Player1Id { get; set; } = "";
    public string Player2Id { get; set; } = "";
    public string Player1Name { get; set; } = "Joueur 1";
    public string Player2Name { get; set; } = "Joueur 2";
    public string CurrentTurnPlayerId { get; set; } = "";
    public bool Player1Ready { get; set; }
    public bool Player2Ready { get; set; }
    public GameMode Mode { get; set; } = GameMode.VsAI;
    public Board Player2Board { get; set; } = new Board();
}

public enum GameMode
{
    VsAI,
    Multiplayer
}
