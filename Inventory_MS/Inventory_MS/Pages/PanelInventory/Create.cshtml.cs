using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.PanelInventory;

public class CreateModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    public PanelComponent Component { get; set; } = new();

    public CreateModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // No GST for panels: total is computed from quantity and unit price.
        Component.RecalculateCosts();
        Component.Remaining = Component.TotalQuantity;

        var rows = await _sheets.GetRowsAsync(PanelComponent.SheetName);
        Component.SlNo = (Math.Max(0, rows.Count - 1) + 1).ToString();

        await _sheets.AppendRowAsync(PanelComponent.SheetName, Component.ToRow());

        TempData["Success"] = $"Added \"{Component.ComponentName}\" to the panel inventory.";
        return RedirectToPage("./Index");
    }
}
