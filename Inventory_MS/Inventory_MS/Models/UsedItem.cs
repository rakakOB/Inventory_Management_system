using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>One row of the Used_Components sheet (the usage log).</summary>
public sealed class UsedItem
{
    public const string SheetName = "Used_Components";
    public const int ColumnCount = 7;

    // 0-based column indexes, matching the sheet's header row.
    private const int ColUniqueCode = 0;
    private const int ColComponentName = 1;
    private const int ColCategory = 2;
    private const int ColBatchPurchaseDate = 3;
    private const int ColUsedDate = 4;
    private const int ColQuantityUsed = 5;
    private const int ColRemarks = 6;

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

    [Display(Name = "Used Date")]
    public string UsedDate { get; set; } = "";

    [Display(Name = "Quantity Used")]
    [Range(1, 999999, ErrorMessage = "Quantity used must be at least 1.")]
    public int QuantityUsed { get; set; }

    [Display(Name = "Remarks")]
    public string Remarks { get; set; } = "";

    public static UsedItem FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        UniqueCode = SheetCell.Cell(row, ColUniqueCode),
        ComponentName = SheetCell.Cell(row, ColComponentName),
        Category = SheetCell.Cell(row, ColCategory),
        BatchPurchaseDate = SheetCell.Cell(row, ColBatchPurchaseDate),
        UsedDate = SheetCell.Cell(row, ColUsedDate),
        QuantityUsed = SheetCell.SafeInt(row, ColQuantityUsed),
        Remarks = SheetCell.Cell(row, ColRemarks),
    };

    public List<object> ToRow() =>
    [
        UniqueCode, ComponentName, Category, BatchPurchaseDate, UsedDate,
        QuantityUsed, Remarks,
    ];
}
