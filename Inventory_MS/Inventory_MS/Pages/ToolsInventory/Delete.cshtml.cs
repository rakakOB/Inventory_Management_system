using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.ToolsInventory;

public class DeleteModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public Tool Component { get; private set; } = new();

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

        await _sheets.DeleteRowAsync(Tool.SheetName, id.Value);

        TempData["Success"] = "Tool deleted. The remaining rows were re-numbered.";
        return RedirectToPage("./Index");
    }

    private async Task<Tool?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(Tool.SheetName);
        return id.Value <= rows.Count ? Tool.FromRow(rows[id.Value - 1], id.Value) : null;
    }
}
