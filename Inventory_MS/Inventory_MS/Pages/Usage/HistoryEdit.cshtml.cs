using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Usage;

public class HistoryEditModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    public UsedItem Record { get; set; } = new();

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

        var rows = await _sheets.GetRowsAsync(UsedItem.SheetName);
        if (id.Value > rows.Count)
            return NotFound();

        var original = UsedItem.FromRow(rows[id.Value - 1], id.Value);
        Record.RowIndex = id.Value;

        if (!ModelState.IsValid)
            return Page();

        // If the quantity changed, add the previously deducted amount back to
        // stock, then deduct the new amount (validated against availability).
        var sheetName = InventoryItem.SheetNameFor(original.Category);
        if (sheetName.Length > 0)
        {
            var invRows = await _sheets.GetRowsAsync(sheetName);
            var item = FindBatch(invRows, original.UniqueCode, original.BatchPurchaseDate);
            if (item is not null)
            {
                item.Remaining += original.QuantityUsed;
                if (Record.QuantityUsed > item.Remaining)
                {
                    ModelState.AddModelError(string.Empty,
                        $"Only {item.Remaining} unit(s) of \"{item.ComponentName}\" are available after reversing the previous usage.");
                    return Page();
                }
                item.Remaining -= Record.QuantityUsed;
                await _sheets.UpdateRowAsync(sheetName, item.RowIndex, item.ToRow());
            }
        }

        await _sheets.UpdateRowAsync(UsedItem.SheetName, id.Value, Record.ToRow());

        TempData["Success"] = $"Updated the usage record for \"{Record.ComponentName}\".";
        return RedirectToPage("./History");
    }

    private async Task<UsedItem?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(UsedItem.SheetName);
        return id.Value <= rows.Count ? UsedItem.FromRow(rows[id.Value - 1], id.Value) : null;
    }

    /// <summary>
    /// Locates the batch row a history record came from.
    ///
    /// v2.2: a category sheet may hold several rows per UniqueCode, so the code
    /// alone no longer identifies a row. The log carries the batch's purchase
    /// date, so that is used as the discriminator. If nothing matches on both
    /// (the batch row was edited or removed), fall back to the first row with the
    /// same code so the stock reversal still lands somewhere sensible instead of
    /// being silently dropped.
    /// </summary>
    private static InventoryItem? FindBatch(
        IList<IList<object>> rows, string uniqueCode, string batchPurchaseDate)
    {
        InventoryItem? codeMatch = null;

        for (int i = 1; i < rows.Count; i++)
        {
            var item = InventoryItem.FromRow(rows[i], i + 1);
            if (!string.Equals(item.UniqueCode, uniqueCode, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(item.DateOfPurchase, batchPurchaseDate, StringComparison.OrdinalIgnoreCase))
                return item;

            codeMatch ??= item;
        }

        return codeMatch;
    }
}
