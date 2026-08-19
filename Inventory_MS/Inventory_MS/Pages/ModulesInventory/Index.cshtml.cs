using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.ModulesInventory;

public class IndexModel : PageModel
{
    private const string SheetName = "Modules_Inventory";

    private readonly GoogleSheetsService _sheets;

    public List<InventoryItem> Items { get; private set; } = new();

    /// <summary>
    /// Codes whose total remaining stock across every batch is under the Master
    /// minimum. Their rows are highlighted in the table.
    /// </summary>
    public HashSet<string> LowStockCodes { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Distinct components represented by the batch rows.</summary>
    public int ComponentCount => Items
        .Select(i => i.UniqueCode)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rowsTask = _sheets.GetRowsAsync(SheetName);
        var minStockTask = StockAlerts.LoadMinStockAsync(_sheets);
        await Task.WhenAll(rowsTask, minStockTask);

        var rows = rowsTask.Result;

        Items = new List<InventoryItem>();
        for (int i = 1; i < rows.Count; i++)
            Items.Add(InventoryItem.FromRow(rows[i], i + 1));

        // v2.2: a component can now own several batch rows. Group them together
        // by code, oldest purchase first, so a new batch lands directly beneath
        // the earlier ones for the same component.
        Items = Items
            .OrderBy(item => item.UniqueCode, UniqueCodeComparer.Instance)
            .ThenBy(item => item.DateOfPurchase, StringComparer.Ordinal)
            .ToList();

        LowStockCodes = StockAlerts.LowStockCodes(Items, minStockTask.Result);
    }
}
