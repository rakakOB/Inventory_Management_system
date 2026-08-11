using System.ComponentModel.DataAnnotations;
using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Damage;

public class ReportModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<PcbComponent> PcbComponents { get; private set; } = new();
    public List<Tool> Tools { get; private set; } = new();
    public List<PanelComponent> PanelComponents { get; private set; } = new();

    [BindProperty]
    public string? InventoryType { get; set; }

    /// <summary>Row index of the selected component within its inventory sheet.</summary>
    [BindProperty]
    public int RowIndex { get; set; }

    [BindProperty]
    [Range(1, 99999, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; }

    [BindProperty]
    [Display(Name = "Reason for Damage")]
    [Required(ErrorMessage = "Reason for damage is required.")]
    public string Reason { get; set; } = "";

    [BindProperty]
    [Display(Name = "Date of Damage")]
    public string DamageDate { get; set; } = "";

    [BindProperty]
    [Display(Name = "Remarks")]
    public string Remarks { get; set; } = "";

    public ReportModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync(string? type, int? rowId)
    {
        await LoadInventoriesAsync();
        InventoryType = type is "pcb" or "tools" or "panel" ? type : "pcb";
        RowIndex = rowId ?? 0;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadInventoriesAsync();
        if (!ModelState.IsValid)
            return Page();

        string sheetName;
        string componentName;
        string category;
        string invoiceNo;
        decimal costPerUnit;
        List<object>? originalRow = null;

        // Resolve the selected row against the matching inventory sheet and
        // check that stock is sufficient BEFORE any write happens.
        switch (InventoryType)
        {
            case "pcb":
            {
                var item = PcbComponents.FirstOrDefault(c => c.RowIndex == RowIndex);
                if (item is null)
                    return Fail("The selected PCB component could not be found — it may have been deleted.");
                if (Quantity > item.Remaining)
                    return Fail($"Only {item.Remaining} unit(s) of \"{item.ComponentName}\" are in stock.");

                sheetName = PcbComponent.SheetName;
                componentName = item.ComponentName;
                category = item.Category;
                invoiceNo = item.InvoiceNo;
                costPerUnit = item.CostPerUnit;

                originalRow = item.ToRow();
                item.Remaining -= Quantity;
                await _sheets.UpdateRowAsync(sheetName, RowIndex, item.ToRow());
                break;
            }
            case "tools":
            {
                var item = Tools.FirstOrDefault(t => t.RowIndex == RowIndex);
                if (item is null)
                    return Fail("The selected tool could not be found — it may have been deleted.");
                if (Quantity > item.Available)
                    return Fail($"Only {item.Available} unit(s) of \"{item.ToolName}\" are available.");

                sheetName = Tool.SheetName;
                componentName = item.ToolName;
                category = item.Category;
                invoiceNo = item.InvoiceNo;
                costPerUnit = item.CostPerUnit;

                originalRow = item.ToRow();
                item.Available -= Quantity;
                await _sheets.UpdateRowAsync(sheetName, RowIndex, item.ToRow());
                break;
            }
            case "panel":
            {
                var item = PanelComponents.FirstOrDefault(p => p.RowIndex == RowIndex);
                if (item is null)
                    return Fail("The selected panel component could not be found — it may have been deleted.");
                if (Quantity > item.Remaining)
                    return Fail($"Only {item.Remaining} unit(s) of \"{item.ComponentName}\" are in stock.");

                sheetName = PanelComponent.SheetName;
                componentName = item.ComponentName;
                category = item.Category;
                invoiceNo = item.InvoiceNo;
                costPerUnit = item.CostPerUnit;

                originalRow = item.ToRow();
                item.Remaining -= Quantity;
                await _sheets.UpdateRowAsync(sheetName, RowIndex, item.ToRow());
                break;
            }
            default:
                return Fail("Please choose an inventory type.");
        }

        // Log the damage record. If logging fails, restore the stock decrement
        // so the two sheets stay consistent.
        var rows = await _sheets.GetRowsAsync(DamageRecord.SheetName);
        var record = new DamageRecord
        {
            SlNo = (Math.Max(0, rows.Count - 1) + 1).ToString(),
            Date = string.IsNullOrWhiteSpace(DamageDate)
                ? DateTime.Today.ToString("yyyy-MM-dd")
                : DamageDate.Trim(),
            ComponentName = componentName,
            Category = category,
            QuantityDamaged = Quantity,
            Reason = Reason.Trim(),
            InvoiceNo = invoiceNo,
            CostPerUnit = costPerUnit,
            Remarks = Remarks.Trim(),
        };

        try
        {
            await _sheets.AppendRowAsync(DamageRecord.SheetName, record.ToRow());
        }
        catch
        {
            if (originalRow is not null)
                await _sheets.UpdateRowAsync(sheetName, RowIndex, originalRow);
            throw;
        }

        TempData["Success"] = $"Logged {Quantity} damaged unit(s) of \"{componentName}\" and updated stock.";
        return RedirectToPage("/Damage/History");
    }

    private IActionResult Fail(string message)
    {
        ModelState.AddModelError(string.Empty, message);
        return Page();
    }

    private async Task LoadInventoriesAsync()
    {
        var pcbTask = _sheets.GetRowsAsync(PcbComponent.SheetName);
        var toolsTask = _sheets.GetRowsAsync(Tool.SheetName);
        var panelTask = _sheets.GetRowsAsync(PanelComponent.SheetName);
        await Task.WhenAll(pcbTask, toolsTask, panelTask);

        PcbComponents = new List<PcbComponent>();
        var pcbRows = pcbTask.Result;
        for (int i = 1; i < pcbRows.Count; i++)
            PcbComponents.Add(PcbComponent.FromRow(pcbRows[i], i + 1));

        Tools = new List<Tool>();
        var toolRows = toolsTask.Result;
        for (int i = 1; i < toolRows.Count; i++)
            Tools.Add(Tool.FromRow(toolRows[i], i + 1));

        PanelComponents = new List<PanelComponent>();
        var panelRows = panelTask.Result;
        for (int i = 1; i < panelRows.Count; i++)
            PanelComponents.Add(PanelComponent.FromRow(panelRows[i], i + 1));
    }
}
