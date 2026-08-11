using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>One row of the Panel_Inventory sheet.</summary>
public sealed class PanelComponent : InventoryItemBase
{
    public const string SheetName = "Panel_Inventory";
    public const int ColumnCount = 11;

    // 0-based column indexes, matching the sheet's header row.
    private const int ColSlNo = 0;
    private const int ColCategory = 1;
    private const int ColComponentName = 2;
    private const int ColTotalQuantity = 3;
    private const int ColRemaining = 4;
    private const int ColInvoiceNo = 5;
    private const int ColCostPerUnit = 6;
    private const int ColTotalCost = 7;
    private const int ColSupplier = 8;
    private const int ColDateOfPurchase = 9;
    private const int ColRemarks = 10;

    [Display(Name = "Category")]
    public string Category { get; set; } = "";

    [Display(Name = "Component Name")]
    [Required(ErrorMessage = "Component name is required.")]
    public string ComponentName { get; set; } = "";

    public static PanelComponent FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        SlNo = Cell(row, ColSlNo),
        Category = Cell(row, ColCategory),
        ComponentName = Cell(row, ColComponentName),
        TotalQuantity = CellInt(row, ColTotalQuantity),
        Remaining = CellInt(row, ColRemaining),
        InvoiceNo = Cell(row, ColInvoiceNo),
        CostPerUnit = CellDec(row, ColCostPerUnit),
        TotalCost = CellDec(row, ColTotalCost),
        Supplier = Cell(row, ColSupplier),
        DateOfPurchase = Cell(row, ColDateOfPurchase),
        Remarks = Cell(row, ColRemarks),
    };

    public List<object> ToRow() =>
    [
        SlNo, Category, ComponentName, TotalQuantity, Remaining, InvoiceNo,
        CostPerUnit, TotalCost, Supplier, DateOfPurchase, Remarks,
    ];

    /// <summary>No GST for panels: TotalCost = TotalQuantity × CostPerUnit.</summary>
    public void RecalculateCosts()
    {
        TotalCost = Math.Round(TotalQuantity * CostPerUnit, 2);
    }
}
