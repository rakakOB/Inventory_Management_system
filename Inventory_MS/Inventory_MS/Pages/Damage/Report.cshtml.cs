using System.ComponentModel.DataAnnotations;
using System.Globalization;
using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Damage;

/// <summary>
/// Logs damaged components and deducts them from stock.
///
/// v2.2 CHANGE — the component dropdown now lists individual BATCH ROWS rather
/// than one entry per UniqueCode, because a category sheet may hold several rows
/// for the same component. The stock deduction is applied to the specific row
/// the user picked, identified by its RowIndex.
/// </summary>
public class ReportModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    /// <summary>Batch rows with stock (Remaining &gt; 0), grouped by category.</summary>
    public Dictionary<string, List<InventoryItem>> ComponentsByCategory { get; private set; } = new();

    [BindProperty]
    [Display(Name = "Category")]
    public string Category { get; set; } = InventoryItem.Electronics;

    /// <summary>
    /// The selected batch, encoded as "rowIndex|uniqueCode". The RowIndex drives
    /// the deduction; the code is re-checked after the fresh read so a row that
    /// shifted in the meantime cannot be deducted by mistake.
    /// </summary>
    [BindProperty]
    [Required(ErrorMessage = "Please select a component batch.")]
    [Display(Name = "Component batch")]
    public string? BatchKey { get; set; }

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

    /// <summary>Builds the value posted by the batch dropdown.</summary>
    public static string BatchKeyFor(InventoryItem item) =>
        $"{item.RowIndex.ToString(CultureInfo.InvariantCulture)}|{item.UniqueCode}";

    /// <summary>Builds the label shown in the batch dropdown.</summary>
    public static string BatchLabelFor(InventoryItem item)
    {
        var date = string.IsNullOrWhiteSpace(item.DateOfPurchase) ? "no date" : item.DateOfPurchase;
        return $"{item.UniqueCode} – {item.ComponentName} (Date: {date}, Remaining: {item.Remaining})";
    }

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

        if (!TryParseBatchKey(BatchKey, out var rowIndex, out var uniqueCode))
            return Fail("The selected batch could not be read. Please select it again.");

        var item = items.FirstOrDefault(i => i.RowIndex == rowIndex);
        if (item is null || item.Remaining <= 0)
            return Fail("The selected batch could not be found — it may have been deleted or used up.");

        if (!string.Equals(item.UniqueCode, uniqueCode, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                "The stock list changed while this form was open, so the selected batch no longer " +
                "refers to the same component. Please review the list and submit again.");
        }

        if (QuantityDamaged > item.Remaining)
            return Fail($"Only {item.Remaining} unit(s) of \"{item.ComponentName}\" are left in this batch.");

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
            // Optional unit cost; falls back to the batch row's cost when blank.
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

        TempData["Success"] =
            $"Logged {QuantityDamaged} damaged unit(s) of \"{item.ComponentName}\" from the batch dated " +
            $"{(string.IsNullOrWhiteSpace(damaged.BatchPurchaseDate) ? "(no date)" : damaged.BatchPurchaseDate)}.";
        return RedirectToPage("/Damage/History");
    }

    private static bool TryParseBatchKey(string? key, out int rowIndex, out string uniqueCode)
    {
        rowIndex = 0;
        uniqueCode = "";

        if (string.IsNullOrWhiteSpace(key))
            return false;

        var separator = key.IndexOf('|');
        if (separator <= 0)
            return false;

        if (!int.TryParse(key[..separator], NumberStyles.Integer, CultureInfo.InvariantCulture, out rowIndex)
            || rowIndex < 2)
        {
            return false;
        }

        uniqueCode = key[(separator + 1)..].Trim();
        return uniqueCode.Length > 0;
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

        return list
            .OrderBy(i => i.UniqueCode, UniqueCodeComparer.Instance)
            .ThenBy(i => i.DateOfPurchase, StringComparer.Ordinal)
            .ToList();
    }
}
