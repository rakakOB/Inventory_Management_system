using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages;

public class IndexModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public int PcbCount { get; private set; }
    public int ToolsCount { get; private set; }
    public int PanelCount { get; private set; }
    public int DamageCount { get; private set; }

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        // All four sheets load in parallel; a failing sheet degrades to 0.
        var pcb = CountRowsAsync(PcbComponent.SheetName);
        var tools = CountRowsAsync(Tool.SheetName);
        var panel = CountRowsAsync(PanelComponent.SheetName);
        var damage = CountRowsAsync(DamageRecord.SheetName);

        await Task.WhenAll(pcb, tools, panel, damage);

        PcbCount = pcb.Result;
        ToolsCount = tools.Result;
        PanelCount = panel.Result;
        DamageCount = damage.Result;
    }

    private async Task<int> CountRowsAsync(string sheetName)
    {
        try
        {
            var rows = await _sheets.GetRowsAsync(sheetName);
            return Math.Max(0, rows.Count - 1); // minus the header row
        }
        catch
        {
            return 0;
        }
    }
}
