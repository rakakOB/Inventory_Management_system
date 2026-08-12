using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>One row of the Damaged_Components sheet (the damage log).</summary>
public sealed class DamagedItem
{
    public const string SheetName = "Damaged_Components";
    public const int ColumnCount = 9;

    // 0-based column indexes, matching the sheet's header row.
    private const int ColUniqueCode = 0;
    private const int ColComponentName = 1;
    private const int ColCategory = 2;
    private const int ColBatchPurchaseDate = 3;
    private const int ColDamageDate = 4;
    private const int ColQuantityDamaged = 5;
    private const int ColInvoiceNo = 6;
    private const int ColCostPerUnit = 7;
    private const int ColRemarks = 8;

    /// <summary>1-based row index in the spreadsheet (row 1 = header).</summary>
    public int RowIndex { get; set; }

    [Display(Name = "Unique Code")]
    public string UniqueCode { get; set; } = "";

    [Display(Name = "Component Name")]
    public string ComponentName { get; set; } = "";

    [Display(Name = "Category")]
    public string Category { get; set; } = "";

    [Display(Name = "Batch Purchase Date")]
    public string BatchPurchaseDate { get; set; } = "";

    [Display(Name = "Damage Date")]
    public string DamageDate { get; set; } = "";

    [Display(Name = "Quantity Damaged")]
    [Range(1, 999999, ErrorMessage = "Quantity damaged must be at least 1.")]
    public int QuantityDamaged { get; set; }

    [Display(Name = "Invoice No.")]
    public string InvoiceNo { get; set; } = "";

    [Display(Name = "Cost per Unit (₹)")]
    [Range(0, 99999999, ErrorMessage = "Cost per unit cannot be negative.")]
    public decimal CostPerUnit { get; set; }

    [Display(Name = "Remarks")]
    public string Remarks { get; set; } = "";

    public static DamagedItem FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        UniqueCode = SheetCell.Cell(row, ColUniqueCode),
        ComponentName = SheetCell.Cell(row, ColComponentName),
        Category = SheetCell.Cell(row, ColCategory),
        BatchPurchaseDate = SheetCell.Cell(row, ColBatchPurchaseDate),
        DamageDate = SheetCell.Cell(row, ColDamageDate),
        QuantityDamaged = SheetCell.SafeInt(row, ColQuantityDamaged),
        InvoiceNo = SheetCell.Cell(row, ColInvoiceNo),
        CostPerUnit = SheetCell.SafeDecimal(row, ColCostPerUnit),
        Remarks = SheetCell.Cell(row, ColRemarks),
    };

    public List<object> ToRow() =>
    [
        UniqueCode, ComponentName, Category, BatchPurchaseDate, DamageDate,
        QuantityDamaged, InvoiceNo, CostPerUnit, Remarks,
    ];
}
