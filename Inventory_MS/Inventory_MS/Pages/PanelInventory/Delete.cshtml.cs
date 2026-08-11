using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.PanelInventory;

public class DeleteModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public PanelComponent Component { get; private set; } = new();

    public DeleteModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var item = await FindAsync(id);
        if (item is null)
            return NotFound();
        Component = item;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id is not > 0)
            return NotFound();

        await _sheets.DeleteRowAsync(PanelComponent.SheetName, id.Value);

        TempData["Success"] = "Component deleted. The remaining rows were re-numbered.";
        return RedirectToPage("./Index");
    }

    private async Task<PanelComponent?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(PanelComponent.SheetName);
        return id.Value <= rows.Count ? PanelComponent.FromRow(rows[id.Value - 1], id.Value) : null;
    }
}
