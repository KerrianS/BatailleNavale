namespace BattleShip.Models;

public class Board
{
    public Cell[,] Grid { get; set; }
    public const int Size = 10;
    public int CurrentSize { get; private set; }
    public List<Ship> Ships { get; set; } = new();

    public Board() : this(Size)
    {
    }

    public Board(int size)
    {
        CurrentSize = size;
        Grid = new Cell[size, size];
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        for (int x = 0; x < CurrentSize; x++)
        {
            for (int y = 0; y < CurrentSize; y++)
            {
                Grid[x, y] = new Cell(x, y);
            }
        }
    }

    public void PlaceShip(int x, int y, int size, bool isHorizontal, ShipType? shipType = null)
    {
        var ship = new Ship
        {
            Size = size,
            Type = (ShipType)size // Approximation basée sur la taille
        };

        for (int i = 0; i < size; i++)
        {
            int posX = isHorizontal ? x + i : x;
            int posY = isHorizontal ? y : y + i;
            
            if (posX < CurrentSize && posY < CurrentSize)
            {
                Grid[posX, posY].HasShip = true;
                Grid[posX, posY].ShipType = shipType;
                Grid[posX, posY].IsHorizontal = isHorizontal;
                if (i == 0)
                {
                    Grid[posX, posY].IsShipStart = true;
                }
                ship.Positions.Add((posX, posY));
            }
        }

        Ships.Add(ship);
    }

    public bool CanPlaceShip(int x, int y, int size, bool isHorizontal)
    {
        if (isHorizontal && x + size > CurrentSize) return false;
        if (!isHorizontal && y + size > CurrentSize) return false;

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
        if (x < 0 || x >= CurrentSize || y < 0 || y >= CurrentSize)
            return (false, false);

        var cell = Grid[x, y];
        
        if (cell.IsHit)
            return (cell.HasShip, true);

        cell.IsHit = true;
        return (cell.HasShip, false);
    }

    public bool ReceiveAttack(int x, int y)
    {
        if (x < 0 || x >= CurrentSize || y < 0 || y >= CurrentSize)
            return false;

        var cell = Grid[x, y];
        cell.IsHit = true;
        return cell.HasShip;
    }

    public Ship? GetShipAt(int x, int y)
    {
        // TODO: Implement ship tracking
        return null;
    }

    public bool AllShipsSunk()
    {
        for (int x = 0; x < CurrentSize; x++)
        {
            for (int y = 0; y < CurrentSize; y++)
            {
                if (Grid[x, y].HasShip && !Grid[x, y].IsHit)
                    return false;
            }
        }
        return true;
    }

    public void MarkShipAsSunk(int x, int y)
    {
        // Trouver le bateau qui contient cette cellule et marquer toutes ses cellules comme coulées
        var cell = Grid[x, y];
        if (!cell.HasShip) return;

        // Rechercher toutes les cellules du même bateau
        var shipCells = FindShipCells(x, y);
        
        // Marquer toutes les cellules du bateau comme coulées
        foreach (var (cellX, cellY) in shipCells)
        {
            Grid[cellX, cellY].IsSunk = true;
        }
    }

    private List<(int x, int y)> FindShipCells(int startX, int startY)
    {
        var shipCells = new List<(int x, int y)>();
        var cell = Grid[startX, startY];
        
        if (!cell.HasShip) return shipCells;

        // Essayer de détecter la direction du bateau
        bool isHorizontal = cell.IsHorizontal;
        
        if (isHorizontal)
        {
            // Bateau horizontal - chercher vers la gauche puis vers la droite
            int minX = startX;
            int maxX = startX;
            
            // Chercher vers la gauche
            while (minX > 0 && Grid[minX - 1, startY].HasShip)
                minX--;
            
            // Chercher vers la droite
            while (maxX < CurrentSize - 1 && Grid[maxX + 1, startY].HasShip)
                maxX++;
            
            // Ajouter toutes les cellules du bateau
            for (int x = minX; x <= maxX; x++)
            {
                shipCells.Add((x, startY));
            }
        }
        else
        {
            // Bateau vertical - chercher vers le haut puis vers le bas
            int minY = startY;
            int maxY = startY;
            
            // Chercher vers le haut
            while (minY > 0 && Grid[startX, minY - 1].HasShip)
                minY--;
            
            // Chercher vers le bas
            while (maxY < CurrentSize - 1 && Grid[startX, maxY + 1].HasShip)
                maxY++;
            
            // Ajouter toutes les cellules du bateau
            for (int y = minY; y <= maxY; y++)
            {
                shipCells.Add((startX, y));
            }
        }
        
        return shipCells;
    }
}
