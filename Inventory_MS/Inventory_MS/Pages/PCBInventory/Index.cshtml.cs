using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.PCBInventory;

public class IndexModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<PcbComponent> Components { get; private set; } = new();

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(PcbComponent.SheetName);

        Components = new List<PcbComponent>();
        for (int i = 1; i < rows.Count; i++)
            Components.Add(PcbComponent.FromRow(rows[i], i + 1));
    }
}
