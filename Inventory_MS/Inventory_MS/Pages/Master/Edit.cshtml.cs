using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Master;

public class EditModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    public MasterItem Item { get; set; } = new();

    public EditModel(GoogleSheetsService sheets) => _sheets = sheets;

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

        Item.RowIndex = id.Value;
        if (!ModelState.IsValid)
            return Page();

        // Guard against a row being deleted while the edit form was open.
        var rows = await _sheets.GetRowsAsync(MasterItem.SheetName);
        if (id.Value > rows.Count)
            return NotFound();

        await _sheets.UpdateRowAsync(MasterItem.SheetName, id.Value, Item.ToRow());

        TempData["Success"] = $"Updated \"{Item.ComponentName}\" ({Item.UniqueCode}).";
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
