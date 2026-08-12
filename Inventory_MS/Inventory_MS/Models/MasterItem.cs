using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>One row of the Master sheet (the catalogue of all components).</summary>
public sealed class MasterItem
{
    public const string SheetName = "Master";
    public const int ColumnCount = 7;

    // 0-based column indexes, matching the sheet's header row.
    private const int ColUniqueCode = 0;
    private const int ColComponentName = 1;
    private const int ColCategory = 2;
    private const int ColBrand = 3;
    private const int ColDescription = 4;
    private const int ColUnit = 5;
    private const int ColMinStockAlert = 6;

    /// <summary>1-based row index in the spreadsheet (row 1 = header).</summary>
    public int RowIndex { get; set; }

    [Display(Name = "Unique Code")]
    public string UniqueCode { get; set; } = "";

    [Display(Name = "Component Name")]
    [Required(ErrorMessage = "Component name is required.")]
    public string ComponentName { get; set; } = "";

    [Display(Name = "Category")]
    [Required(ErrorMessage = "Category is required.")]
    public string Category { get; set; } = "";

    [Display(Name = "Brand")]
    public string Brand { get; set; } = "";

    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Display(Name = "Unit")]
    public string Unit { get; set; } = "";

    [Display(Name = "Min. Stock Alert")]
    [Range(0, 999999, ErrorMessage = "Minimum stock alert cannot be negative.")]
    public int MinStockAlert { get; set; } = 5;

    public static MasterItem FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        UniqueCode = SheetCell.Cell(row, ColUniqueCode),
        ComponentName = SheetCell.Cell(row, ColComponentName),
        Category = SheetCell.Cell(row, ColCategory),
        Brand = SheetCell.Cell(row, ColBrand),
        Description = SheetCell.Cell(row, ColDescription),
        Unit = SheetCell.Cell(row, ColUnit),
        MinStockAlert = SheetCell.SafeInt(row, ColMinStockAlert),
    };

    public List<object> ToRow() =>
    [
        UniqueCode, ComponentName, Category, Brand, Description, Unit, MinStockAlert,
    ];
}
