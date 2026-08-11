using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>One row of the PCB_Inventory sheet.</summary>
public sealed class PcbComponent : InventoryItemBase
{
    public const string SheetName = "PCB_Inventory";
    public const int ColumnCount = 13;

    // 0-based column indexes, matching the sheet's header row.
    private const int ColSlNo = 0;
    private const int ColCategory = 1;
    private const int ColComponentName = 2;
    private const int ColTotalQuantity = 3;
    private const int ColRemaining = 4;
    private const int ColInvoiceNo = 5;
    private const int ColCostPerUnit = 6;
    private const int ColBaseCost = 7;
    private const int ColGstAmount = 8;
    private const int ColTotalCost = 9;
    private const int ColSupplier = 10;
    private const int ColDateOfPurchase = 11;
    private const int ColRemarks = 12;

    [Display(Name = "Category")]
    public string Category { get; set; } = "";

    [Display(Name = "Component Name")]
    [Required(ErrorMessage = "Component name is required.")]
    public string ComponentName { get; set; } = "";

    [Display(Name = "Base Cost (₹)")]
    public decimal BaseCost { get; set; }

    [Display(Name = "GST 18% (₹)")]
    public decimal GstAmount { get; set; }

    public static PcbComponent FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        SlNo = Cell(row, ColSlNo),
        Category = Cell(row, ColCategory),
        ComponentName = Cell(row, ColComponentName),
        TotalQuantity = CellInt(row, ColTotalQuantity),
        Remaining = CellInt(row, ColRemaining),
        InvoiceNo = Cell(row, ColInvoiceNo),
        CostPerUnit = CellDec(row, ColCostPerUnit),
        BaseCost = CellDec(row, ColBaseCost),
        GstAmount = CellDec(row, ColGstAmount),
        TotalCost = CellDec(row, ColTotalCost),
        Supplier = Cell(row, ColSupplier),
        DateOfPurchase = Cell(row, ColDateOfPurchase),
        Remarks = Cell(row, ColRemarks),
    };

    public List<object> ToRow() =>
    [
        SlNo, Category, ComponentName, TotalQuantity, Remaining, InvoiceNo,
        CostPerUnit, BaseCost, GstAmount, TotalCost, Supplier, DateOfPurchase, Remarks,
    ];

    /// <summary>
    /// BaseCost = TotalQuantity × CostPerUnit;
    /// GST 18% = BaseCost × 0.18;
    /// TotalCost = BaseCost + GST.
    /// </summary>
    public void RecalculateCosts()
    {
        BaseCost = Math.Round(TotalQuantity * CostPerUnit, 2);
        GstAmount = Math.Round(BaseCost * 0.18m, 2);
        TotalCost = BaseCost + GstAmount;
    }
}
