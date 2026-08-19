using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Usage;

public class HistoryModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<UsedItem> Records { get; private set; } = new();

    public HistoryModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(UsedItem.SheetName);

        Records = new List<UsedItem>();
        for (int i = 1; i < rows.Count; i++)
            Records.Add(UsedItem.FromRow(rows[i], i + 1));

        // v2.2: grouped by UniqueCode ascending (was ComponentName), newest usage
        // first within each code.
        Records = Records
            .OrderBy(r => r.UniqueCode, UniqueCodeComparer.Instance)
            .ThenByDescending(r => r.UsedDate, StringComparer.Ordinal)
            .ToList();
    }
}
