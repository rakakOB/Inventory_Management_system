using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InventoryManagement.Pages.ModulesInventory;

public class EditModel : PageModel
{
    private const string SheetName = "Modules_Inventory";

    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    public InventoryItem Item { get; set; } = new();

    public IEnumerable<SelectListItem> SupplierOptions { get; private set; } = new List<SelectListItem>();

    public EditModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var item = await FindAsync(id);
        if (item is null)
            return NotFound();
        Item = item;
        await LoadSuppliersAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id is not > 0)
            return NotFound();

        Item.RowIndex = id.Value;
        if (!ModelState.IsValid)
        {
            await LoadSuppliersAsync();
            return Page();
        }

        // Guard against a row being deleted while the edit form was open.
        var rows = await _sheets.GetRowsAsync(SheetName);
        if (id.Value > rows.Count)
            return NotFound();

        Item.RecalculateCosts();
        await _sheets.UpdateRowAsync(SheetName, id.Value, Item.ToRow());

        TempData["Success"] = $"Updated \"{Item.ComponentName}\" ({Item.UniqueCode}).";
        return RedirectToPage("./Index");
    }

    private async Task<InventoryItem?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(SheetName);
        return id.Value <= rows.Count ? InventoryItem.FromRow(rows[id.Value - 1], id.Value) : null;
    }

    private async Task LoadSuppliersAsync()
    {
        var rows = await _sheets.GetRowsAsync(Supplier.SheetName);
        var names = new List<string>();
        for (int i = 1; i < rows.Count; i++)
            names.Add(Supplier.FromRow(rows[i], i + 1).SupplierName);

        // Keep the current supplier visible even if it was since removed from the Suppliers sheet.
        if (!string.IsNullOrWhiteSpace(Item.Supplier)
            && !names.Contains(Item.Supplier, StringComparer.OrdinalIgnoreCase))
        {
            names.Insert(0, Item.Supplier);
        }

        SupplierOptions = names
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new SelectListItem(n, n));
    }
}
