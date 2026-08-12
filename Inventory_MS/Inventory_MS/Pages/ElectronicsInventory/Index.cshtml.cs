using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.ElectronicsInventory;

public class IndexModel : PageModel
{
    private const string SheetName = "Electronics_Inventory";

    private readonly GoogleSheetsService _sheets;

    public List<InventoryItem> Items { get; private set; } = new();

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(SheetName);

        Items = new List<InventoryItem>();
        for (int i = 1; i < rows.Count; i++)
            Items.Add(InventoryItem.FromRow(rows[i], i + 1));

        Items = Items.OrderBy(item => item.ComponentName, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
