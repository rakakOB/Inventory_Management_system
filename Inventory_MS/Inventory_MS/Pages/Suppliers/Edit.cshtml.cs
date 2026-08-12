using System.ComponentModel.DataAnnotations;
using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Suppliers;

public class EditModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    [Display(Name = "Supplier Name")]
    [Required(ErrorMessage = "Supplier name is required.")]
    public string SupplierName { get; set; } = "";

    [BindProperty]
    [Display(Name = "Contact Info")]
    public string ContactInfo { get; set; } = "";

    /// <summary>Row index of the record being edited; round-trips through the form.</summary>
    [BindProperty]
    public int RowIndex { get; set; }

    public EditModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var supplier = await FindAsync(id);
        if (supplier is null)
            return NotFound();
        SupplierName = supplier.SupplierName;
        ContactInfo = supplier.ContactInfo;
        RowIndex = supplier.RowIndex;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        if (id is not > 0)
            return NotFound();

        if (!ModelState.IsValid)
            return Page();

        // Guard against a row being deleted while the edit form was open.
        var rows = await _sheets.GetRowsAsync(Supplier.SheetName);
        if (id.Value > rows.Count)
            return NotFound();

        var supplier = new Supplier
        {
            RowIndex = id.Value,
            SupplierName = SupplierName.Trim(),
            ContactInfo = ContactInfo.Trim(),
        };
        await _sheets.UpdateRowAsync(Supplier.SheetName, id.Value, supplier.ToRow());

        TempData["Success"] = $"Updated supplier \"{supplier.SupplierName}\".";
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
