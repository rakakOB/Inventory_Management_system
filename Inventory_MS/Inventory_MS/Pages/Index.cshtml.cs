using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages;

public class IndexModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public int MasterCount { get; private set; }
    public int LowStockCount { get; private set; }
    public int ElectronicsCount { get; private set; }
    public int ElectricalCount { get; private set; }
    public int ToolsCount { get; private set; }
    public int ModulesCount { get; private set; }

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        // All sheets load in parallel; a failing sheet degrades to empty.
        var masterTask = LoadRowsAsync(MasterItem.SheetName);
        var electronicsTask = LoadRowsAsync(InventoryItem.SheetNameFor(InventoryItem.Electronics));
        var electricalTask = LoadRowsAsync(InventoryItem.SheetNameFor(InventoryItem.Electrical));
        var toolsTask = LoadRowsAsync(InventoryItem.SheetNameFor(InventoryItem.Tools));
        var modulesTask = LoadRowsAsync(InventoryItem.SheetNameFor(InventoryItem.Modules));

        await Task.WhenAll(masterTask, electronicsTask, electricalTask, toolsTask, modulesTask);

        var masterRows = masterTask.Result;
        MasterCount = Math.Max(0, masterRows.Count - 1);

        // UniqueCode -> MinStockAlert lookup built from the Master sheet.
        var alerts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < masterRows.Count; i++)
        {
            var item = MasterItem.FromRow(masterRows[i], i + 1);
            if (!string.IsNullOrWhiteSpace(item.UniqueCode))
                alerts[item.UniqueCode] = item.MinStockAlert;
        }

        ElectronicsCount = Math.Max(0, electronicsTask.Result.Count - 1);
        ElectricalCount = Math.Max(0, electricalTask.Result.Count - 1);
        ToolsCount = Math.Max(0, toolsTask.Result.Count - 1);
        ModulesCount = Math.Max(0, modulesTask.Result.Count - 1);

        // Count items whose remaining stock is below the Master minimum alert.
        foreach (var rows in new[] { electronicsTask.Result, electricalTask.Result, toolsTask.Result, modulesTask.Result })
        {
            for (int i = 1; i < rows.Count; i++)
            {
                var item = InventoryItem.FromRow(rows[i], i + 1);
                if (alerts.TryGetValue(item.UniqueCode, out var minStock) && item.Remaining < minStock)
                    LowStockCount++;
            }
        }
    }

    private async Task<IList<IList<object>>> LoadRowsAsync(string sheetName)
    {
        try
        {
            return await _sheets.GetRowsAsync(sheetName);
        }
        catch
        {
            return new List<IList<object>>();
        }
    }
}
