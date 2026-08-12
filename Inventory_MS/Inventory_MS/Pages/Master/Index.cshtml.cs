using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Master;

public class IndexModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<MasterItem> Items { get; private set; } = new();

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(MasterItem.SheetName);

        Items = new List<MasterItem>();
        for (int i = 1; i < rows.Count; i++)
            Items.Add(MasterItem.FromRow(rows[i], i + 1));

        Items = Items.OrderBy(m => m.ComponentName, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
