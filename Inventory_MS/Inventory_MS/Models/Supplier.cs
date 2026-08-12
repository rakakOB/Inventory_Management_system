using System.ComponentModel.DataAnnotations;

namespace InventoryManagement.Models;

/// <summary>One row of the Suppliers sheet.</summary>
public sealed class Supplier
{
    public const string SheetName = "Suppliers";
    public const int ColumnCount = 2;

    // 0-based column indexes, matching the sheet's header row.
    private const int ColSupplierName = 0;
    private const int ColContactInfo = 1;

    /// <summary>1-based row index in the spreadsheet (row 1 = header).</summary>
    public int RowIndex { get; set; }

    [Display(Name = "Supplier Name")]
    [Required(ErrorMessage = "Supplier name is required.")]
    public string SupplierName { get; set; } = "";

    [Display(Name = "Contact Info")]
    public string ContactInfo { get; set; } = "";

    public static Supplier FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        SupplierName = SheetCell.Cell(row, ColSupplierName),
        ContactInfo = SheetCell.Cell(row, ColContactInfo),
    };

    public List<object> ToRow() =>
    [
        SupplierName, ContactInfo,
    ];
}
