using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.ModulesInventory;

public class DeleteModel : PageModel
{
    private const string SheetName = "Modules_Inventory";

    private readonly GoogleSheetsService _sheets;

    public InventoryItem Item { get; private set; } = new();

    public DeleteModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var item = await FindAsync(id);
        if (item is null)
            return NotFound();
        Item = item;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id is not > 0)
            return NotFound();

        await _sheets.DeleteRowAsync(SheetName, id.Value);

        TempData["Success"] = "Inventory row deleted.";
        return RedirectToPage("./Index");
    }

    private async Task<InventoryItem?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(SheetName);
        return id.Value <= rows.Count ? InventoryItem.FromRow(rows[id.Value - 1], id.Value) : null;
    }
}
