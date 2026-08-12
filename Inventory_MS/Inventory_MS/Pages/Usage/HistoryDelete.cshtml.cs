using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Usage;

public class HistoryDeleteModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public UsedItem Record { get; private set; } = new();

    public HistoryDeleteModel(GoogleSheetsService sheets) => _sheets = sheets;

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

        var rows = await _sheets.GetRowsAsync(UsedItem.SheetName);
        if (id.Value > rows.Count)
            return NotFound();

        var record = UsedItem.FromRow(rows[id.Value - 1], id.Value);

        // Return the used quantity to stock, then remove the log entry.
        var sheetName = InventoryItem.SheetNameFor(record.Category);
        if (sheetName.Length > 0)
        {
            var invRows = await _sheets.GetRowsAsync(sheetName);
            var item = FindByCode(invRows, record.UniqueCode);
            if (item is not null)
            {
                item.Remaining += record.QuantityUsed;
                await _sheets.UpdateRowAsync(sheetName, item.RowIndex, item.ToRow());
            }
        }

        await _sheets.DeleteRowAsync(UsedItem.SheetName, id.Value);

        TempData["Success"] = "Usage record deleted and the quantity returned to stock.";
        return RedirectToPage("./History");
    }

    private async Task<UsedItem?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(UsedItem.SheetName);
        return id.Value <= rows.Count ? UsedItem.FromRow(rows[id.Value - 1], id.Value) : null;
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
