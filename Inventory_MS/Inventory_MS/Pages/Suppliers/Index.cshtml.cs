using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Suppliers;

public class IndexModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<Supplier> Suppliers { get; private set; } = new();

    public IndexModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(Supplier.SheetName);

        Suppliers = new List<Supplier>();
        for (int i = 1; i < rows.Count; i++)
            Suppliers.Add(Supplier.FromRow(rows[i], i + 1));

        Suppliers = Suppliers.OrderBy(s => s.SupplierName, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
