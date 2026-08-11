using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>One row of the Damages_Components sheet (read-only in the UI).</summary>
public sealed class DamageRecord
{
    public const string SheetName = "Damages_Components";
    public const int ColumnCount = 9;

    // 0-based column indexes, matching the sheet's header row.
    private const int ColSlNo = 0;
    private const int ColDate = 1;
    private const int ColComponentName = 2;
    private const int ColCategory = 3;
    private const int ColQuantityDamaged = 4;
    private const int ColReason = 5;
    private const int ColInvoiceNo = 6;
    private const int ColCostPerUnit = 7;
    private const int ColRemarks = 8;

    /// <summary>1-based row index in the spreadsheet (row 1 = header).</summary>
    public int RowIndex { get; set; }

    [Display(Name = "Sl. No.")]
    public string SlNo { get; set; } = "";

    [Display(Name = "Date")]
    public string Date { get; set; } = "";

    [Display(Name = "Component Name")]
    public string ComponentName { get; set; } = "";

    [Display(Name = "Category")]
    public string Category { get; set; } = "";

    [Display(Name = "Quantity Damaged")]
    public int QuantityDamaged { get; set; }

    [Display(Name = "Reason for Damage")]
    public string Reason { get; set; } = "";

    [Display(Name = "Invoice No.")]
    public string InvoiceNo { get; set; } = "";

    [Display(Name = "Cost per Unit (₹)")]
    public decimal CostPerUnit { get; set; }

    [Display(Name = "Remarks")]
    public string Remarks { get; set; } = "";

    public static DamageRecord FromRow(IList<object> row, int rowIndex)
    {
        string Cell(int index)
            => index < row.Count ? row[index]?.ToString()?.Trim() ?? string.Empty : string.Empty;

        int CellInt(int index)
            => int.TryParse(Cell(index), out var v) ? v : 0;

        decimal CellDec(int index)
            => decimal.TryParse(Cell(index), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0m;

        return new DamageRecord
        {
            RowIndex = rowIndex,
            SlNo = Cell(ColSlNo),
            Date = Cell(ColDate),
            ComponentName = Cell(ColComponentName),
            Category = Cell(ColCategory),
            QuantityDamaged = CellInt(ColQuantityDamaged),
            Reason = Cell(ColReason),
            InvoiceNo = Cell(ColInvoiceNo),
            CostPerUnit = CellDec(ColCostPerUnit),
            Remarks = Cell(ColRemarks),
        };
    }

    public List<object> ToRow() =>
    [
        SlNo, Date, ComponentName, Category, QuantityDamaged, Reason,
        InvoiceNo, CostPerUnit, Remarks,
    ];
}
