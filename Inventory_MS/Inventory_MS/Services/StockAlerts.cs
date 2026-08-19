using InventoryManagement.Models;

namespace InventoryManagement.Services;

/// <summary>
/// Low-stock helpers shared by the four category inventory pages.
///
/// Since v2.2 a category sheet holds one row per stock batch, so several rows
/// can share a UniqueCode. "Running low" is therefore a property of the
/// COMPONENT, not of an individual batch: the remaining quantities of all
/// batches for a code are summed and compared against the Master item's
/// MinStockAlert. Every row belonging to a low component is highlighted.
///
/// Judging each row on its own would light up every nearly-consumed batch even
/// when a fresh batch sits right below it with plenty of stock.
/// </summary>
public static class StockAlerts
{
    /// <summary>Used when a Master item has no usable MinStockAlert, matching the Master default.</summary>
    public const int DefaultMinStockAlert = 5;

    /// <summary>Builds a UniqueCode → MinStockAlert lookup from the Master sheet.</summary>
    public static async Task<Dictionary<string, int>> LoadMinStockAsync(GoogleSheetsService sheets)
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var rows = await sheets.GetRowsAsync(MasterItem.SheetName).ConfigureAwait(false);
            for (int i = 1; i < rows.Count; i++)
            {
                var item = MasterItem.FromRow(rows[i], i + 1);
                if (!string.IsNullOrWhiteSpace(item.UniqueCode))
                    lookup[item.UniqueCode] = item.MinStockAlert;
            }
        }
        catch
        {
            // A failing Master read only costs the highlighting, so the stock
            // list itself still renders.
        }

        return lookup;
    }

    /// <summary>
    /// Returns the UniqueCodes whose total remaining stock across all batches is
    /// below the Master minimum.
    /// </summary>
    public static HashSet<string> LowStockCodes(
        IEnumerable<InventoryItem> items,
        IReadOnlyDictionary<string, int> minStockByCode)
    {
        var low = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in items.GroupBy(i => i.UniqueCode, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
                continue;

            var threshold = minStockByCode.TryGetValue(group.Key, out var min) && min > 0
                ? min
                : DefaultMinStockAlert;

            if (group.Sum(i => i.Remaining) < threshold)
                low.Add(group.Key);
        }

        return low;
    }
}
