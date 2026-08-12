using System.ComponentModel.DataAnnotations;
using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Damage;

public class ReportModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    /// <summary>Components with stock (Remaining &gt; 0), grouped by category.</summary>
    public Dictionary<string, List<InventoryItem>> ComponentsByCategory { get; private set; } = new();

    [BindProperty]
    [Display(Name = "Category")]
    public string Category { get; set; } = InventoryItem.Electronics;

    /// <summary>UniqueCode of the selected component within its category inventory.</summary>
    [BindProperty]
    [Required(ErrorMessage = "Please select a component.")]
    [Display(Name = "Component")]
    public string? UniqueCode { get; set; }

    [BindProperty]
    [Range(1, 999999, ErrorMessage = "Quantity damaged must be at least 1.")]
    [Display(Name = "Quantity Damaged")]
    public int QuantityDamaged { get; set; }

    [BindProperty]
    [Display(Name = "Damage Date")]
    public string DamageDate { get; set; } = "";

    [BindProperty]
    [Display(Name = "Invoice No.")]
    public string InvoiceNo { get; set; } = "";

    [BindProperty]
    [Range(0, 99999999, ErrorMessage = "Cost per unit cannot be negative.")]
    [Display(Name = "Cost per Unit (₹)")]
    public decimal CostPerUnit { get; set; }

    [BindProperty]
    [Display(Name = "Remarks")]
    public string Remarks { get; set; } = "";

    public ReportModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        await LoadComponentsAsync();
        if (string.IsNullOrWhiteSpace(DamageDate))
            DamageDate = DateTime.Today.ToString("yyyy-MM-dd");
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadComponentsAsync();
        if (string.IsNullOrWhiteSpace(DamageDate))
            DamageDate = DateTime.Today.ToString("yyyy-MM-dd");

        if (!ModelState.IsValid)
            return Page();

        var sheetName = InventoryItem.SheetNameFor(Category);
        if (sheetName.Length == 0)
            return Fail("Please choose a valid category.");

        if (!ComponentsByCategory.TryGetValue(Category, out var items))
            return Fail("No stock found for the selected category.");

        var item = items.FirstOrDefault(i =>
            string.Equals(i.UniqueCode, UniqueCode, StringComparison.OrdinalIgnoreCase) && i.Remaining > 0);
        if (item is null)
            return Fail("The selected component could not be found — it may have been deleted.");

        if (QuantityDamaged > item.Remaining)
            return Fail($"Only {item.Remaining} unit(s) of \"{item.ComponentName}\" are in stock.");

        // Deduct stock first, then log the damage; restore the row if the log fails.
        var originalRow = item.ToRow();
        item.Remaining -= QuantityDamaged;
        await _sheets.UpdateRowAsync(sheetName, item.RowIndex, item.ToRow());

        var damaged = new DamagedItem
        {
            UniqueCode = item.UniqueCode,
            ComponentName = item.ComponentName,
            Category = Category,
            BatchPurchaseDate = item.DateOfPurchase,
            DamageDate = DamageDate.Trim(),
            QuantityDamaged = QuantityDamaged,
            InvoiceNo = string.IsNullOrWhiteSpace(InvoiceNo) ? item.InvoiceNo : InvoiceNo.Trim(),
            // Optional unit cost; falls back to the inventory row's cost when blank.
            CostPerUnit = CostPerUnit > 0 ? CostPerUnit : item.CostPerUnit,
            Remarks = Remarks.Trim(),
        };

        try
        {
            await _sheets.AppendRowAsync(DamagedItem.SheetName, damaged.ToRow());
        }
        catch
        {
            await _sheets.UpdateRowAsync(sheetName, item.RowIndex, originalRow);
            throw;
        }

        TempData["Success"] = $"Logged {QuantityDamaged} damaged unit(s) of \"{item.ComponentName}\" and updated stock.";
        return RedirectToPage("/Damage/History");
    }

    private IActionResult Fail(string message)
    {
        ModelState.AddModelError(string.Empty, message);
        return Page();
    }

    private async Task LoadComponentsAsync()
    {
        ComponentsByCategory = new Dictionary<string, List<InventoryItem>>();

        var electronicsTask = LoadCategoryAsync(InventoryItem.Electronics);
        var electricalTask = LoadCategoryAsync(InventoryItem.Electrical);
        var toolsTask = LoadCategoryAsync(InventoryItem.Tools);
        var modulesTask = LoadCategoryAsync(InventoryItem.Modules);
        await Task.WhenAll(electronicsTask, electricalTask, toolsTask, modulesTask);

        ComponentsByCategory[InventoryItem.Electronics] = electronicsTask.Result;
        ComponentsByCategory[InventoryItem.Electrical] = electricalTask.Result;
        ComponentsByCategory[InventoryItem.Tools] = toolsTask.Result;
        ComponentsByCategory[InventoryItem.Modules] = modulesTask.Result;
    }

    private async Task<List<InventoryItem>> LoadCategoryAsync(string category)
    {
        var list = new List<InventoryItem>();
        try
        {
            var rows = await _sheets.GetRowsAsync(InventoryItem.SheetNameFor(category));
            for (int i = 1; i < rows.Count; i++)
            {
                var item = InventoryItem.FromRow(rows[i], i + 1);
                if (item.Remaining > 0)
                    list.Add(item);
            }
        }
        catch
        {
            // A missing/failing category sheet degrades to an empty list.
        }
        return list;
    }
}
