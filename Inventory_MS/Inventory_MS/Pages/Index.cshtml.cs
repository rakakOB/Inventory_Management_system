using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages;

public class IndexModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public int MasterCount { get; private set; }

    /// <summary>
    /// Per-category totals. Since v2.2 a category sheet holds one row per stock
    /// batch, so the row count and the number of distinct components are no
    /// longer the same number and both are worth showing.
    /// </summary>
    public CategorySummary Electronics { get; private set; } = new();
    public CategorySummary Electrical { get; private set; } = new();
    public CategorySummary Tools { get; private set; } = new();
    public CategorySummary Modules { get; private set; } = new();

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

        MasterCount = Math.Max(0, masterTask.Result.Count - 1);

        // Low stock is no longer summarised here. It is surfaced where it is
        // actionable instead: rows are highlighted on each category page.
        Electronics = Summarise(electronicsTask.Result);
        Electrical = Summarise(electricalTask.Result);
        Tools = Summarise(toolsTask.Result);
        Modules = Summarise(modulesTask.Result);
    }

    private static CategorySummary Summarise(IList<IList<object>> rows)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var batches = 0;
        var remaining = 0;

        for (int i = 1; i < rows.Count; i++)
        {
            var item = InventoryItem.FromRow(rows[i], i + 1);
            batches++;
            remaining += item.Remaining;
            if (!string.IsNullOrWhiteSpace(item.UniqueCode))
                codes.Add(item.UniqueCode);
        }

        return new CategorySummary
        {
            Components = codes.Count,
            Batches = batches,
            RemainingUnits = remaining,
        };
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

    public sealed class CategorySummary
    {
        public int Components { get; init; }
        public int Batches { get; init; }
        public int RemainingUnits { get; init; }
    }
}
