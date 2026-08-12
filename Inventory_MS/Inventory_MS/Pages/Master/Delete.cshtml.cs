using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Master;

public class DeleteModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public MasterItem Item { get; private set; } = new();

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

        await _sheets.DeleteRowAsync(MasterItem.SheetName, id.Value);

        TempData["Success"] = "Master item deleted.";
        return RedirectToPage("./Index");
    }

    private async Task<MasterItem?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(MasterItem.SheetName);
        return id.Value <= rows.Count ? MasterItem.FromRow(rows[id.Value - 1], id.Value) : null;
    }
}
