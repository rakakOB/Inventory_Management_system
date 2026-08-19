using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Damage;

public class HistoryModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<DamagedItem> Records { get; private set; } = new();

    public HistoryModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(DamagedItem.SheetName);

        Records = new List<DamagedItem>();
        for (int i = 1; i < rows.Count; i++)
            Records.Add(DamagedItem.FromRow(rows[i], i + 1));

        // v2.2: grouped by UniqueCode ascending (was ComponentName), newest damage
        // first within each code.
        Records = Records
            .OrderBy(r => r.UniqueCode, UniqueCodeComparer.Instance)
            .ThenByDescending(r => r.DamageDate, StringComparer.Ordinal)
            .ToList();
    }
}
