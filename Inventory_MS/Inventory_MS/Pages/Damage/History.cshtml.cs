using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages.Damage;

public class HistoryModel : PageModel
{
    private readonly GoogleSheetsService _sheets;

    public List<DamageRecord> Records { get; private set; } = new();
    public int TotalUnitsDamaged { get; private set; }
    public decimal TotalValueLoss { get; private set; }

    public HistoryModel(GoogleSheetsService sheets) => _sheets = sheets;

    public async Task OnGetAsync()
    {
        var rows = await _sheets.GetRowsAsync(DamageRecord.SheetName);

        Records = new List<DamageRecord>();
        // Newest records first.
        for (int i = rows.Count - 1; i >= 1; i--)
        {
            var record = DamageRecord.FromRow(rows[i], i + 1);
            Records.Add(record);
            TotalUnitsDamaged += record.QuantityDamaged;
            TotalValueLoss += record.QuantityDamaged * record.CostPerUnit;
        }
    }
}
