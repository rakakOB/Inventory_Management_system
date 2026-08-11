using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>One row of the Tools_Inventory sheet.</summary>
public sealed class Tool : InventoryItemBase
{
    public const string SheetName = "Tools_Inventory";
    public const int ColumnCount = 11;

    // 0-based column indexes, matching the sheet's header row.
    private const int ColSlNo = 0;
    private const int ColToolName = 1;
    private const int ColCategory = 2;
    private const int ColTotalQuantity = 3;
    private const int ColAvailable = 4;
    private const int ColInvoiceNo = 5;
    private const int ColCostPerUnit = 6;
    private const int ColTotalCost = 7;
    private const int ColSupplier = 8;
    private const int ColDateOfPurchase = 9;
    private const int ColRemarks = 10;

    [Display(Name = "Tool Name")]
    [Required(ErrorMessage = "Tool name is required.")]
    public string ToolName { get; set; } = "";

    [Display(Name = "Category")]
    public string Category { get; set; } = "";

    /// <summary>The Tools sheet calls its stock column "Available" instead of "Remaining".</summary>
    [Display(Name = "Available")]
    public int Available { get => Remaining; set => Remaining = value; }

    public static Tool FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        SlNo = Cell(row, ColSlNo),
        ToolName = Cell(row, ColToolName),
        Category = Cell(row, ColCategory),
        TotalQuantity = CellInt(row, ColTotalQuantity),
        Available = CellInt(row, ColAvailable),
        InvoiceNo = Cell(row, ColInvoiceNo),
        CostPerUnit = CellDec(row, ColCostPerUnit),
        TotalCost = CellDec(row, ColTotalCost),
        Supplier = Cell(row, ColSupplier),
        DateOfPurchase = Cell(row, ColDateOfPurchase),
        Remarks = Cell(row, ColRemarks),
    };

    public List<object> ToRow() =>
    [
        SlNo, ToolName, Category, TotalQuantity, Available, InvoiceNo,
        CostPerUnit, TotalCost, Supplier, DateOfPurchase, Remarks,
    ];

    /// <summary>No GST for tools: TotalCost = TotalQuantity × CostPerUnit.</summary>
    public void RecalculateCosts()
    {
        TotalCost = Math.Round(TotalQuantity * CostPerUnit, 2);
    }
}
