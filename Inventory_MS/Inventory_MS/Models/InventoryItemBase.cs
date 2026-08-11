using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace InventoryManagement.Models;

/// <summary>
/// Fields and cell-parsing helpers shared by the three inventory sheets
/// (PCB_Inventory, Tools_Inventory, Panel_Inventory).
/// </summary>
public abstract class InventoryItemBase
{
    /// <summary>
    /// 1-based row index in the spreadsheet, including the header row
    /// (row 1 = header). Used to locate the row for edit / delete / damage.
    /// </summary>
    public int RowIndex { get; set; }

    [Display(Name = "Sl. No.")]
    public string SlNo { get; set; } = "";

    [Display(Name = "Total Quantity")]
    [Range(1, 999999, ErrorMessage = "Total quantity must be at least 1.")]
    public int TotalQuantity { get; set; }

    [Display(Name = "Remaining")]
    [Range(0, 999999, ErrorMessage = "Remaining cannot be negative.")]
    public int Remaining { get; set; }

    [Display(Name = "Invoice No.")]
    public string InvoiceNo { get; set; } = "";

    [Display(Name = "Cost per Unit (₹)")]
    [Range(0, 99999999, ErrorMessage = "Cost per unit cannot be negative.")]
    public decimal CostPerUnit { get; set; }

    [Display(Name = "Total Cost (₹)")]
    public decimal TotalCost { get; set; }

    [Display(Name = "Supplier")]
    public string Supplier { get; set; } = "";

    [Display(Name = "Date of Purchase")]
    public string DateOfPurchase { get; set; } = "";

    [Display(Name = "Remarks")]
    public string Remarks { get; set; } = "";

    /// <summary>Rows at or below this stock level are highlighted in the UI.</summary>
    public const int LowStockThreshold = 5;

    public bool IsLowStock => Remaining < LowStockThreshold;

    // ---- Cell parsing helpers -------------------------------------------------
    // Sheets API returns numbers as double, empty trailing cells are omitted.

    protected static string Cell(IList<object> row, int index)
        => index < row.Count ? row[index]?.ToString()?.Trim() ?? string.Empty : string.Empty;

    protected static int CellInt(IList<object> row, int index)
    {
        var text = Cell(row, index);
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? (int)v
            : 0;
    }

    protected static decimal CellDec(IList<object> row, int index)
        => decimal.TryParse(Cell(row, index), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0m;
}
