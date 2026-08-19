using System.IO.Compression;
using System.Text;
using InventoryManagement.Models;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages;

/// <summary>
/// Exports the sheets as CSV, read live at the moment of the request — nothing
/// is cached or precomputed, so a download always reflects the current state of
/// the spreadsheet.
/// </summary>
public class ReportsModel : PageModel
{
    /// <summary>
    /// The exportable sheets. This doubles as an allow-list: the handler resolves
    /// a caller-supplied key through this dictionary and rejects anything else,
    /// so the endpoint cannot be used to read an arbitrary tab.
    /// </summary>
    public static readonly IReadOnlyList<ReportDefinition> Reports = new List<ReportDefinition>
    {
        new("master", "Master List", MasterItem.SheetName,
            "Every component in the catalogue, with category, unit and minimum stock alert."),
        new("electronics", "Electronics Inventory", InventoryItem.SheetNameFor(InventoryItem.Electronics),
            "All electronics batch rows with quantities, costs and suppliers."),
        new("electrical", "Electrical Inventory", InventoryItem.SheetNameFor(InventoryItem.Electrical),
            "All electrical batch rows with quantities, costs and suppliers."),
        new("tools", "Tools Inventory", InventoryItem.SheetNameFor(InventoryItem.Tools),
            "All tools batch rows with quantities, costs and suppliers."),
        new("modules", "Modules Inventory", InventoryItem.SheetNameFor(InventoryItem.Modules),
            "All modules batch rows with quantities, costs and suppliers."),
        new("suppliers", "Suppliers", Supplier.SheetName,
            "Supplier names and contact details."),
        new("usage", "Usage History", UsedItem.SheetName,
            "Every logged consumption, with batch date, used date and quantity."),
        new("damage", "Damage History", DamagedItem.SheetName,
            "Every logged damage, with batch date, damage date, quantity and unit cost."),
    };

    private readonly GoogleSheetsService _sheets;

    public ReportsModel(GoogleSheetsService sheets) => _sheets = sheets;

    public void OnGet()
    {
    }

    /// <summary>Downloads one sheet as CSV.</summary>
    public async Task<IActionResult> OnGetCsvAsync(string? key)
    {
        var report = Reports.FirstOrDefault(r =>
            string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase));

        if (report is null)
            return NotFound();

        byte[] csv;
        try
        {
            csv = await BuildCsvAsync(report.SheetName);
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty,
                $"Could not read the '{report.SheetName}' tab. Check that it exists in the spreadsheet.");
            return Page();
        }

        return File(csv, "text/csv", FileNameFor(report.SheetName, "csv"));
    }

    /// <summary>Downloads every sheet as CSV files inside a single ZIP.</summary>
    public async Task<IActionResult> OnGetZipAsync()
    {
        using var buffer = new MemoryStream();

        // The archive must be disposed before the buffer is read, otherwise the
        // central directory has not been flushed and the ZIP is unreadable.
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var report in Reports)
            {
                byte[] csv;
                try
                {
                    csv = await BuildCsvAsync(report.SheetName);
                }
                catch
                {
                    // One missing tab should not cost the whole archive.
                    continue;
                }

                var entry = archive.CreateEntry(FileNameFor(report.SheetName, "csv"),
                    CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(csv);
            }
        }

        return File(buffer.ToArray(), "application/zip",
            $"InventoryReports_{DateTime.Now:yyyy-MM-dd}.zip");
    }

    private async Task<byte[]> BuildCsvAsync(string sheetName)
    {
        var rows = await _sheets.GetRowsAsync(sheetName);

        // Trailing empty cells are omitted by the Sheets API, so rows arrive
        // ragged. Pad every line to the widest row (usually the header) to keep
        // the columns aligned in the output.
        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Count);

        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            for (int col = 0; col < width; col++)
            {
                if (col > 0)
                    builder.Append(',');

                builder.Append(Escape(col < row.Count ? row[col]?.ToString() : null));
            }
            builder.Append("\r\n");
        }

        // UTF-8 with a BOM: without it Excel misreads the ₹ sign and any other
        // non-ASCII text in remarks or supplier names.
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    /// <summary>Quotes a CSV field only when it needs it, doubling embedded quotes.</summary>
    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var needsQuotes = value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            || value.StartsWith(' ')
            || value.EndsWith(' ');

        return needsQuotes ? '"' + value.Replace("\"", "\"\"") + '"' : value;
    }

    private static string FileNameFor(string sheetName, string extension) =>
        $"{sheetName}_{DateTime.Now:yyyy-MM-dd}.{extension}";

    /// <summary>One downloadable report.</summary>
    public sealed record ReportDefinition(string Key, string Title, string SheetName, string Description);
}
