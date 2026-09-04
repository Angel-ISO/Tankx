namespace backend.api.Hubs.Runtime;

internal sealed class GameMap
{
    private static readonly int[][] BattleCity =
    [
        [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1],
        [1,0,0,0,0,2,2,0,0,0,1,0,0,0,2,2,0,0,0,0,0,0,0,1],
        [1,0,0,0,0,2,2,0,1,0,1,0,2,2,2,2,0,1,1,1,0,2,0,1],
        [1,0,0,0,0,0,0,0,1,0,0,0,2,0,0,2,0,0,0,1,0,2,0,1],
        [1,2,2,1,0,1,1,0,1,1,1,0,2,0,0,2,0,1,0,1,0,2,0,1],
        [1,2,2,0,0,0,0,0,0,0,0,0,2,2,2,2,0,1,0,0,0,0,0,1],
        [1,0,0,0,1,1,0,2,2,2,0,0,0,0,0,0,0,1,0,2,2,2,0,1],
        [1,0,1,0,0,0,0,2,0,2,0,1,1,1,1,0,0,0,0,2,0,2,0,1],
        [1,0,1,0,2,2,0,2,0,2,0,1,0,0,1,0,2,2,0,2,0,2,0,1],
        [1,0,0,0,2,2,0,2,2,2,0,1,0,0,1,0,2,2,0,2,2,2,0,1],
        [1,1,1,0,0,0,0,0,0,0,0,1,0,0,1,0,0,0,0,0,0,0,0,1],
        [1,0,0,0,1,1,1,0,2,2,0,0,0,0,0,0,2,2,0,1,1,1,0,1],
        [1,0,2,0,1,0,0,0,2,2,0,1,1,1,1,0,2,2,0,0,0,1,0,1],
        [1,0,2,0,0,0,2,2,0,0,0,1,0,0,1,0,0,0,2,2,0,0,0,1],
        [1,0,2,2,2,0,2,2,0,1,1,1,0,0,1,1,1,0,2,2,0,2,0,1],
        [1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1],
        [1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1]
    ];

    private readonly int[][] cells = BattleCity.Select(row => row.ToArray()).ToArray();

    public int Width => cells[0].Length * GameConstants.CellSize;
    public int Height => cells.Length * GameConstants.CellSize;

    public static bool IsSupported(string mapName) =>
        string.Equals(mapName, "battle-city", StringComparison.OrdinalIgnoreCase);

    public bool CollidesWithTank(double x, double y)
    {
        if (x < 0 || y < 0 || x + GameConstants.TankSize > Width || y + GameConstants.TankSize > Height)
            return true;

        var firstColumn = (int)Math.Floor(x / GameConstants.CellSize);
        var lastColumn = (int)Math.Floor((x + GameConstants.TankSize - 1) / GameConstants.CellSize);
        var firstRow = (int)Math.Floor(y / GameConstants.CellSize);
        var lastRow = (int)Math.Floor((y + GameConstants.TankSize - 1) / GameConstants.CellSize);

        for (var row = firstRow; row <= lastRow; row++)
        for (var column = firstColumn; column <= lastColumn; column++)
            if (cells[row][column] != 0)
                return true;

        return false;
    }

    public int GetCell(double x, double y)
    {
        var row = (int)Math.Floor(y / GameConstants.CellSize);
        var column = (int)Math.Floor(x / GameConstants.CellSize);
        return row < 0 || column < 0 || row >= cells.Length || column >= cells[0].Length
            ? 1
            : cells[row][column];
    }

    public (int Row, int Column) GetCoordinates(double x, double y) =>
        ((int)Math.Floor(y / GameConstants.CellSize), (int)Math.Floor(x / GameConstants.CellSize));

    public bool Destroy(int row, int column)
    {
        if (row < 0 || column < 0 || row >= cells.Length || column >= cells[0].Length || cells[row][column] != 2)
            return false;

        cells[row][column] = 0;
        return true;
    }
}
