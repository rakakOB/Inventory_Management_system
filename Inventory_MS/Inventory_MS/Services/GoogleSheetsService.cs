using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace InventoryManagement.Services;

/// <summary>
/// Minimal wrapper around the Google Sheets API v4, used as the app's database.
///
/// Conventions:
///  * All row indexes are 1-based and INCLUDE the header row (row 1 = header).
///  * Sheets are addressed by tab name, e.g. "Electronics_Inventory".
/// </summary>
public sealed class GoogleSheetsService
{
    private readonly SheetsService _sheets;
    private readonly string _spreadsheetId;

    /// <summary>Tab ids are cached per sheet name; needed for row deletion.</summary>
    private readonly ConcurrentDictionary<string, int> _sheetIdCache = new();

    /// <summary>
    /// Serializes all writes. The damage-report flow is a read-then-write
    /// sequence, and this prevents two concurrent requests from interleaving
    /// on the same spreadsheet.
    /// </summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public GoogleSheetsService(string spreadsheetId, string credentialsPath)
    {
        _spreadsheetId = spreadsheetId;

        _sheets = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = LoadCredential(credentialsPath),
            ApplicationName = "Inventory-Management-System",
        });
    }

    /// <summary>
    /// Prefers the explicit service-account key file (App Engine secret volume,
    /// or local dev). Falls back to Application Default Credentials, which is
    /// available automatically on App Engine via the default service account.
    /// </summary>
    private static GoogleCredential LoadCredential(string credentialsPath)
    {
        if (!string.IsNullOrWhiteSpace(credentialsPath) && File.Exists(credentialsPath))
        {
            return GoogleCredential.FromFile(credentialsPath)
                .CreateScoped(SheetsService.Scope.Spreadsheets);
        }

        return GoogleCredential.GetApplicationDefaultAsync()
            .GetAwaiter().GetResult()
            .CreateScoped(SheetsService.Scope.Spreadsheets);
    }

    /// <summary>Reads every row of a tab, including the header row (capped at 1000 rows).</summary>
    public async Task<IList<IList<object>>> GetRowsAsync(string sheetName)
    {
        var request = _sheets.Spreadsheets.Values.Get(_spreadsheetId, $"{sheetName}!A1:Z1000");
        var response = await request.ExecuteAsync().ConfigureAwait(false);
        return response.Values ?? new List<IList<object>>();
    }

    /// <summary>
    /// Appends a row below the existing data and returns its 1-based row index.
    /// </summary>
    public async Task<int> AppendRowAsync(string sheetName, IList<object> row)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var body = new ValueRange { Values = new List<IList<object>> { row } };
            var request = _sheets.Spreadsheets.Values.Append(body, _spreadsheetId, sheetName);
            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            request.InsertDataOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;

            var response = await request.ExecuteAsync().ConfigureAwait(false);

            // e.g. "Electronics_Inventory!A14:K14" -> 14
            return ParseRowIndexFromRange(response.Updates?.UpdatedRange)
                ?? await GetDataRowCountAsync(sheetName).ConfigureAwait(false) + 2;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Overwrites an existing row (1-based row index, includes the header).</summary>
    public async Task UpdateRowAsync(string sheetName, int rowIndex, IList<object> row)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var range = $"{sheetName}!A{rowIndex}:{ColumnLetter(row.Count)}{rowIndex}";
            var body = new ValueRange { Range = range, Values = new List<IList<object>> { row } };
            var request = _sheets.Spreadsheets.Values.Update(body, _spreadsheetId, range);
            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

            await request.ExecuteAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Physically removes a row (shifting the rows below it up). The UniqueCode
    /// column is a real identifier, so remaining rows are NOT renumbered.
    /// </summary>
    public async Task DeleteRowAsync(string sheetName, int rowIndex)
    {
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var deleteRequest = new Request
            {
                DeleteDimension = new DeleteDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId = await GetSheetIdAsync(sheetName).ConfigureAwait(false),
                        Dimension = "ROWS",
                        StartIndex = rowIndex - 1, // 0-based
                        EndIndex = rowIndex,
                    },
                },
            };

            var batch = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request> { deleteRequest },
            };
            await _sheets.Spreadsheets.BatchUpdate(batch, _spreadsheetId).ExecuteAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Reads a single cell and returns its text value.</summary>
    public async Task<string> GetCellValueAsync(string sheetName, int rowIndex, int columnIndex)
    {
        var range = $"{sheetName}!{ColumnLetter(columnIndex)}{rowIndex}";
        var request = _sheets.Spreadsheets.Values.Get(_spreadsheetId, range);
        var response = await request.ExecuteAsync().ConfigureAwait(false);
        return response.Values?[0]?[0]?.ToString() ?? string.Empty;
    }

    // ------------------------------------------------------------------ private

    private async Task<int> GetSheetIdAsync(string sheetName)
    {
        if (_sheetIdCache.TryGetValue(sheetName, out var cached))
            return cached;

        var spreadsheet = await _sheets.Spreadsheets.Get(_spreadsheetId).ExecuteAsync().ConfigureAwait(false);
        var sheet = spreadsheet.Sheets.FirstOrDefault(s => s.Properties?.Title == sheetName)
            ?? throw new InvalidOperationException(
                $"Tab '{sheetName}' was not found in the spreadsheet. " +
                "Check that the tab exists and is spelled exactly as expected.");

        var sheetId = sheet.Properties.SheetId!.Value;
        _sheetIdCache[sheetName] = sheetId;
        return sheetId;
    }

    private async Task<int> GetDataRowCountAsync(string sheetName)
    {
        var rows = await GetRowsAsync(sheetName).ConfigureAwait(false);
        return Math.Max(0, rows.Count - 1);
    }

    private static int? ParseRowIndexFromRange(string? range)
    {
        if (string.IsNullOrEmpty(range))
            return null;

        var cellRange = range.Contains('!') ? range[(range.IndexOf('!') + 1)..] : range;
        var match = Regex.Match(cellRange, @"\d+");
        return match.Success && int.TryParse(match.Value, out var row) ? row : null;
    }

    /// <summary>Converts a 0-based column index to a sheet column letter (0 -> "A", 27 -> "AB").</summary>
    private static string ColumnLetter(int index)
    {
        index++;
        var letter = "";
        while (index > 0)
        {
            var remainder = (index - 1) % 26;
            letter = (char)('A' + remainder) + letter;
            index = (index - 1) / 26;
        }
        return letter;
    }
}
