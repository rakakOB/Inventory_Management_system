using System.ComponentModel.DataAnnotations;
using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SupplierModel = InventoryManagement.Models.Supplier;

namespace InventoryManagement.Pages.ToolsInventory;

/// <summary>
/// Records a new stock purchase.
///
/// v2.2 CHANGE — every submission APPENDS a new row. Up to v2.1 this page
/// upserted: an existing row for the same UniqueCode was updated in place,
/// which enforced "one row per UniqueCode per category sheet" but meant a
/// restock overwrote the previous batch's cost and supplier while keeping its
/// invoice and purchase date, so per-batch history was unrecoverable.
///
/// That invariant is deliberately gone. A category sheet now holds one row per
/// batch and several rows may share a UniqueCode. Anything that needs to act on
/// a single batch addresses it by RowIndex.
/// </summary>
public class AddStockModel : PageModel
{
    private const string SheetName = "Tools_Inventory";
    private const string Category = "Tools";

    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    [Required(ErrorMessage = "Please select a component.")]
    [Display(Name = "Component")]
    public string? UniqueCode { get; set; }

    [BindProperty]
    [Range(1, 999999, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }

    [BindProperty]
    [Display(Name = "Invoice No.")]
    public string InvoiceNo { get; set; } = "";

    [BindProperty]
    [Range(0.01, 99999999, ErrorMessage = "Cost per unit is required and cannot be negative.")]
    [Display(Name = "Cost per Unit (₹)")]
    public decimal CostPerUnit { get; set; }

    [BindProperty]
    [Display(Name = "Supplier")]
    public string Supplier { get; set; } = "";

    [BindProperty]
    [Display(Name = "Date of Purchase")]
    public string DateOfPurchase { get; set; } = "";

    [BindProperty]
    [Display(Name = "Remarks")]
    public string Remarks { get; set; } = "";

    public List<MasterItem> MasterItems { get; private set; } = new();
    public List<Supplier> Suppliers { get; private set; } = new();

    public IEnumerable<SelectListItem> ComponentOptions =>
        MasterItems
            .OrderBy(m => m.UniqueCode, UniqueCodeComparer.Instance)
            .Select(m => new SelectListItem($"{m.UniqueCode} – {m.ComponentName}", m.UniqueCode));

    public IEnumerable<SelectListItem> SupplierOptions =>
        Suppliers
            .OrderBy(s => s.SupplierName, StringComparer.OrdinalIgnoreCase)
            .Select(s => new SelectListItem(s.SupplierName, s.SupplierName));

    public AddStockModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        await LoadLookupsAsync();
        if (string.IsNullOrWhiteSpace(DateOfPurchase))
            DateOfPurchase = DateTime.Today.ToString("yyyy-MM-dd");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();
        if (string.IsNullOrWhiteSpace(DateOfPurchase))
            DateOfPurchase = DateTime.Today.ToString("yyyy-MM-dd");

        if (!ModelState.IsValid)
            return Page();

        // 1. Resolve the selected Master item.
        var master = MasterItems.FirstOrDefault(m =>
            string.Equals(m.UniqueCode, UniqueCode, StringComparison.OrdinalIgnoreCase));
        if (master is null)
        {
            ModelState.AddModelError(string.Empty,
                "The selected component could not be found — it may have been deleted from the Master list.");
            return Page();
        }

        // 2. Always append: this batch is its own row, independent of any earlier
        //    batch of the same component.
        var item = new InventoryItem
        {
            UniqueCode = master.UniqueCode,
            ComponentName = master.ComponentName,
            Brand = string.IsNullOrWhiteSpace(master.Brand) ? "-" : master.Brand,
            TotalQuantity = Quantity,
            Remaining = Quantity,
            InvoiceNo = InvoiceNo.Trim(),
            CostPerUnit = CostPerUnit,
            Supplier = Supplier.Trim(),
            DateOfPurchase = DateOfPurchase.Trim(),
            Remarks = Remarks.Trim(),
        };
        item.RecalculateCosts();

        await _sheets.AppendRowAsync(SheetName, item.ToRow());

        TempData["Success"] =
            $"Added a new batch of {Quantity} unit(s) of \"{master.ComponentName}\" ({master.UniqueCode}).";
        return RedirectToPage("./Index");
    }

    private async Task LoadLookupsAsync()
    {
        var masterTask = _sheets.GetRowsAsync(MasterItem.SheetName);
        var suppliersTask = _sheets.GetRowsAsync(SupplierModel.SheetName);
        await Task.WhenAll(masterTask, suppliersTask);

        MasterItems = new List<MasterItem>();
        for (int i = 1; i < masterTask.Result.Count; i++)
        {
            var item = MasterItem.FromRow(masterTask.Result[i], i + 1);
            if (string.Equals(item.Category, Category, StringComparison.OrdinalIgnoreCase))
                MasterItems.Add(item);
        }

        Suppliers = new List<Supplier>();
        for (int i = 1; i < suppliersTask.Result.Count; i++)
            Suppliers.Add(SupplierModel.FromRow(suppliersTask.Result[i], i + 1));
    }
}
