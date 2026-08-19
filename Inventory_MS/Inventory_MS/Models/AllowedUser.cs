namespace InventoryManagement.Models;

/// <summary>
/// One row of the AllowedUsers sheet — the access control list for the app.
///
/// The tab has a single column, "Email", with the header in row 1. Only the
/// Google accounts listed here may sign in; everyone else is bounced to the
/// Access Denied page.
///
/// NOTE FOR DEPLOYMENT: the service account the app runs as (on Cloud Run this
/// is the Compute Engine default service account,
/// &lt;project-number&gt;-compute@developer.gserviceaccount.com) must be able to read
/// this tab. Sharing the whole spreadsheet with that account as Editor — which
/// is already required for the other eight tabs — covers it; no extra grant is
/// needed. If the tab is missing, nobody can sign in, and the sign-in attempt
/// fails closed rather than open.
/// </summary>
public sealed class AllowedUser
{
    public const string SheetName = "AllowedUsers";
    public const int ColumnCount = 1;

    // 0-based column index, matching the sheet's header row.
    private const int ColEmail = 0;

    /// <summary>1-based row index in the spreadsheet (row 1 = header).</summary>
    public int RowIndex { get; set; }

    public string Email { get; set; } = "";

    public static AllowedUser FromRow(IList<object> row, int rowIndex) => new()
    {
        RowIndex = rowIndex,
        Email = SheetCell.Cell(row, ColEmail),
    };

    public List<object> ToRow() => [Email];
}
