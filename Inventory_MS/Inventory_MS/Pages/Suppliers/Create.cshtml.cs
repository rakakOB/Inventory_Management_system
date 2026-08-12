using System.ComponentModel.DataAnnotations;
using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Suppliers;

public class CreateModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    [BindProperty]
    [Display(Name = "Supplier Name")]
    [Required(ErrorMessage = "Supplier name is required.")]
    public string SupplierName { get; set; } = "";

    [BindProperty]
    [Display(Name = "Contact Info")]
    public string ContactInfo { get; set; } = "";

    public CreateModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var supplier = new Supplier
        {
            SupplierName = SupplierName.Trim(),
            ContactInfo = ContactInfo.Trim(),
        };
        await _sheets.AppendRowAsync(Supplier.SheetName, supplier.ToRow());

        TempData["Success"] = $"Added supplier \"{supplier.SupplierName}\".";
        return RedirectToPage("./Index");
    }
}
