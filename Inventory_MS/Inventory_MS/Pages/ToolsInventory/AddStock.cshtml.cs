using System.ComponentModel.DataAnnotations;
using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Pages.ToolsInventory;

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
            .OrderBy(m => m.ComponentName, StringComparer.OrdinalIgnoreCase)
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

        // 2. Look for an existing inventory row with the same UniqueCode.
        var rows = await _sheets.GetRowsAsync(SheetName);
        InventoryItem? existing = null;
        int existingRow = 0;
        for (int i = 1; i < rows.Count; i++)
        {
            var item = InventoryItem.FromRow(rows[i], i + 1);
            if (string.Equals(item.UniqueCode, master.UniqueCode, StringComparison.OrdinalIgnoreCase))
            {
                existing = item;
                existingRow = i + 1;
                break;
            }
        }

        if (existing is not null)
        {
            // Increment stock and overwrite cost/supplier with the new batch values.
            existing.TotalQuantity += Quantity;
            existing.Remaining += Quantity;
            existing.CostPerUnit = CostPerUnit;
            existing.Supplier = Supplier.Trim();
            existing.RecalculateCosts();
            await _sheets.UpdateRowAsync(SheetName, existingRow, existing.ToRow());
        }
        else
        {
            // First stock for this code: seed the row from the Master item.
            var item = new InventoryItem
            {
                UniqueCode = master.UniqueCode,
                ComponentName = master.ComponentName,
                Brand = master.Brand,
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
        }

        TempData["Success"] = $"Added {Quantity} unit(s) of \"{master.ComponentName}\" to stock.";
        return RedirectToPage("./Index");
    }

    private async Task LoadLookupsAsync()
    {
        var masterTask = _sheets.GetRowsAsync(MasterItem.SheetName);
        var suppliersTask = _sheets.GetRowsAsync(Supplier.SheetName);
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
            Suppliers.Add(Supplier.FromRow(suppliersTask.Result[i], i + 1));
    }
}
