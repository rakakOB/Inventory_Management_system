using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.PanelInventory;

public class IndexModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<PanelComponent> Components { get; private set; } = new();

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(PanelComponent.SheetName);

        Components = new List<PanelComponent>();
        for (int i = 1; i < rows.Count; i++)
            Components.Add(PanelComponent.FromRow(rows[i], i + 1));
    }
}
