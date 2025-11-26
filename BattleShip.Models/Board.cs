namespace BattleShip.Models;

public class Board
{
    public Cell[,] Grid { get; set; }
    public const int Size = 10;

    public Board()
    {
        Grid = new Cell[Size, Size];
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Grid[x, y] = new Cell(x, y);
            }
        }
    }

    public void PlaceShip(int x, int y, int size, bool isHorizontal)
    {
        for (int i = 0; i < size; i++)
        {
            int posX = isHorizontal ? x + i : x;
            int posY = isHorizontal ? y : y + i;
            
            if (posX < Size && posY < Size)
            {
                Grid[posX, posY].HasShip = true;
            }
        }
    }

    public bool CanPlaceShip(int x, int y, int size, bool isHorizontal)
    {
        if (isHorizontal && x + size > Size) return false;
        if (!isHorizontal && y + size > Size) return false;

        for (int i = 0; i < size; i++)
        {
            int posX = isHorizontal ? x + i : x;
            int posY = isHorizontal ? y : y + i;
            
            if (Grid[posX, posY].HasShip) return false;
        }

        return true;
    }

    public (bool hit, bool alreadyHit) Attack(int x, int y)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size)
            return (false, false);

        var cell = Grid[x, y];
        
        if (cell.IsHit)
            return (cell.HasShip, true);

        cell.IsHit = true;
        return (cell.HasShip, false);
    }
}
