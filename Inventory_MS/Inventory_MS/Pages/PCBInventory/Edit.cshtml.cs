using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.PCBInventory;

public class EditModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    public PcbComponent Component { get; set; } = new();

    public EditModel(GoogleSheetsService sheets) => _sheets = sheets;

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

        Component.RowIndex = id.Value;
        if (!ModelState.IsValid)
            return Page();

        // Guard against a row being deleted while the edit form was open.
        var rows = await _sheets.GetRowsAsync(PcbComponent.SheetName);
        if (id.Value > rows.Count)
            return NotFound();

        Component.RecalculateCosts();
        await _sheets.UpdateRowAsync(PcbComponent.SheetName, id.Value, Component.ToRow());

        TempData["Success"] = $"Updated \"{Component.ComponentName}\".";
        return RedirectToPage("./Index");
    }

    private async Task<PcbComponent?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(PcbComponent.SheetName);
        return id.Value <= rows.Count ? PcbComponent.FromRow(rows[id.Value - 1], id.Value) : null;
    }
}
