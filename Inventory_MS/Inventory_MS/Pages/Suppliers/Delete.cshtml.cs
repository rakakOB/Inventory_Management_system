using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Suppliers;

public class DeleteModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public Supplier Supplier { get; private set; } = new();

    public DeleteModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var supplier = await FindAsync(id);
        if (supplier is null)
            return NotFound();
        Supplier = supplier;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id is not > 0)
            return NotFound();

        await _sheets.DeleteRowAsync(Supplier.SheetName, id.Value);

        TempData["Success"] = "Supplier deleted.";
        return RedirectToPage("./Index");
    }

    private async Task<Supplier?> FindAsync(int? id)
    {
        if (id is not > 0)
            return null;

        var rows = await _sheets.GetRowsAsync(Supplier.SheetName);
        return id.Value <= rows.Count ? Supplier.FromRow(rows[id.Value - 1], id.Value) : null;
    }
}
