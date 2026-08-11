using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.ToolsInventory;

public class IndexModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<Tool> Tools { get; private set; } = new();

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(Tool.SheetName);

        Tools = new List<Tool>();
        for (int i = 1; i < rows.Count; i++)
            Tools.Add(Tool.FromRow(rows[i], i + 1));
    }
}
