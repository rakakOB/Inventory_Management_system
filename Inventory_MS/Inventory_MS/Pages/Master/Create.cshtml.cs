using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Master;

public class CreateModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    public MasterItem Item { get; set; } = new();

    public CreateModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var prefix = InventoryItem.CodePrefixFor(Item.Category);
        if (string.IsNullOrEmpty(prefix))
        {
            ModelState.AddModelError(nameof(Item.Category), "Please choose a valid category.");
            return Page();
        }

        Item.UniqueCode = await GenerateUniqueCodeAsync(prefix);
        await _sheets.AppendRowAsync(MasterItem.SheetName, Item.ToRow());

        TempData["Success"] = $"Added \"{Item.ComponentName}\" ({Item.UniqueCode}) to the Master list.";
        return RedirectToPage("./Index");
    }

    /// <summary>
    /// Builds the next UniqueCode for a category, e.g. "E-001" for Electronics.
    /// The numeric suffix continues from the highest existing code of that prefix.
    /// </summary>
    private async Task<string> GenerateUniqueCodeAsync(string prefix)
    {
        var rows = await _sheets.GetRowsAsync(MasterItem.SheetName);
        var max = 0;
        for (int i = 1; i < rows.Count; i++)
        {
            var code = MasterItem.FromRow(rows[i], i + 1).UniqueCode;
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(code[prefix.Length..], out var n)
                && n > max)
            {
                max = n;
            }
        }

        return $"{prefix}{max + 1:D3}";
    }
}
