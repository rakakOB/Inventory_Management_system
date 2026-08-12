using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>
/// One row of a category inventory sheet (Electronics_Inventory,
/// Electrical_Inventory, Tools_Inventory or Modules_Inventory). All four
/// sheets share the same column layout, so a single model covers them.
/// </summary>
public sealed class InventoryItem
{
    public const string Electronics = "Electronics";
    public const string Electrical = "Electrical";
    public const string Tools = "Tools";
    public const string Modules = "Modules";

    public const int ColumnCount = 11;

    // 0-based column indexes, matching the sheets' header rows.
    private const int ColUniqueCode = 0;
    private const int ColComponentName = 1;
    private const int ColBrand = 2;
    private const int ColTotalQuantity = 3;
    private const int ColRemaining = 4;
    private const int ColInvoiceNo = 5;
    private const int ColCostPerUnit = 6;
    private const int ColTotalCost = 7;
    private const int ColSupplier = 8;
    private const int ColDateOfPurchase = 9;
    private const int ColRemarks = 10;

    /// <summary>1-based row index in the spreadsheet (row 1 = header).</summary>
    public int RowIndex { get; set; }

    [Display(Name = "Unique Code")]
    public string UniqueCode { get; set; } = "";

    [Display(Name = "Component Name")]
    public string ComponentName { get; set; } = "";

    [Display(Name = "Brand")]
    public string Brand { get; set; } = "";

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

    // ---- Category mapping ------------------------------------------------------

    /// <summary>Maps a category name to its inventory tab, e.g. "Electronics" → "Electronics_Inventory".</summary>
    public static string SheetNameFor(string category) => category switch
    {
        Electronics => "Electronics_Inventory",
        Electrical => "Electrical_Inventory",
        Tools => "Tools_Inventory",
        Modules => "Modules_Inventory",
        _ => string.Empty,
    };

    /// <summary>Maps a category name to the UniqueCode prefix used by the Master sheet.</summary>
    public static string CodePrefixFor(string category) => category switch
    {
        Electronics => "E-",
        Electrical => "EL-",
        Tools => "T-",
        Modules => "M-",
        _ => string.Empty,
    };

    public static InventoryItem FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        UniqueCode = SheetCell.Cell(row, ColUniqueCode),
        ComponentName = SheetCell.Cell(row, ColComponentName),
        Brand = SheetCell.Cell(row, ColBrand),
        TotalQuantity = SheetCell.SafeInt(row, ColTotalQuantity),
        Remaining = SheetCell.SafeInt(row, ColRemaining),
        InvoiceNo = SheetCell.Cell(row, ColInvoiceNo),
        CostPerUnit = SheetCell.SafeDecimal(row, ColCostPerUnit),
        TotalCost = SheetCell.SafeDecimal(row, ColTotalCost),
        Supplier = SheetCell.Cell(row, ColSupplier),
        DateOfPurchase = SheetCell.Cell(row, ColDateOfPurchase),
        Remarks = SheetCell.Cell(row, ColRemarks),
    };

    public List<object> ToRow() =>
    [
        UniqueCode, ComponentName, Brand, TotalQuantity, Remaining, InvoiceNo,
        CostPerUnit, TotalCost, Supplier, DateOfPurchase, Remarks,
    ];

    /// <summary>Costs are tax-inclusive: TotalCost = TotalQuantity × CostPerUnit.</summary>
    public void RecalculateCosts()
    {
        TotalCost = Math.Round(TotalQuantity * CostPerUnit, 2);
    }
}
