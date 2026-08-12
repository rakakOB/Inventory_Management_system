using System.Globalization;

namespace InventoryManagement.Models;

/// <summary>
/// Cell-parsing helpers shared by all row models. The Sheets API returns
/// numbers as double and omits empty trailing cells, so every read goes
/// through these safe converters.
/// </summary>
public static class SheetCell
{
    public static string Cell(IList<object> row, int index)
        => index < row.Count ? row[index]?.ToString()?.Trim() ?? string.Empty : string.Empty;

    /// <summary>Parses a cell as an integer; 0 when empty or unparseable.</summary>
    public static int SafeInt(IList<object> row, int index)
        => decimal.TryParse(Cell(row, index), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? (int)v
            : 0;

    /// <summary>Parses a cell as a decimal; 0m when empty or unparseable.</summary>
    public static decimal SafeDecimal(IList<object> row, int index)
        => decimal.TryParse(Cell(row, index), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0m;
}
