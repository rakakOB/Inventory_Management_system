using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Damage;

public class HistoryEditModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    public DamagedItem Record { get; set; } = new();

    public HistoryEditModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var record = await FindAsync(id);
        if (record is null)
            return NotFound();
        Record = record;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id is not > 0)
            return NotFound();

        var rows = await _sheets.GetRowsAsync(DamagedItem.SheetName);
        if (id.Value > rows.Count)
            return NotFound();

        var original = DamagedItem.FromRow(rows[id.Value - 1], id.Value);
        Record.RowIndex = id.Value;

        if (!ModelState.IsValid)
            return Page();

        // If the quantity changed, add the previously deducted amount back to
        // stock, then deduct the new amount (validated against availability).
        var sheetName = InventoryItem.SheetNameFor(original.Category);
        if (sheetName.Length > 0)
        {
            var invRows = await _sheets.GetRowsAsync(sheetName);
            var item = FindByCode(invRows, original.UniqueCode);
            if (item is not null)
            {
                item.Remaining += original.QuantityDamaged;
                if (Record.QuantityDamaged > item.Remaining)
                {
                    ModelState.AddModelError(string.Empty,
                        $"Only {item.Remaining} unit(s) of \"{item.ComponentName}\" are available after reversing the previous damage.");
                    return Page();
                }
                item.Remaining -= Record.QuantityDamaged;
                await _sheets.UpdateRowAsync(sheetName, item.RowIndex, item.ToRow());
            }
        }

        await _sheets.UpdateRowAsync(DamagedItem.SheetName, id.Value, Record.ToRow());

        TempData["Success"] = $"Updated the damage record for \"{Record.ComponentName}\".";
        return RedirectToPage("./History");
    }

    private async Task<DamagedItem?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(DamagedItem.SheetName);
        return id.Value <= rows.Count ? DamagedItem.FromRow(rows[id.Value - 1], id.Value) : null;
    }

    private static InventoryItem? FindByCode(IList<IList<object>> rows, string uniqueCode)
    {
        for (int i = 1; i < rows.Count; i++)
        {
            var item = InventoryItem.FromRow(rows[i], i + 1);
            if (string.Equals(item.UniqueCode, uniqueCode, StringComparison.OrdinalIgnoreCase))
                return item;
        }
        return null;
    }
}
