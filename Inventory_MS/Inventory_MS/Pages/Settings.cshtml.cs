using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages;

/// <summary>
/// Account and appearance settings. Deliberately thin: the only persisted
/// preference so far is the colour theme, and that lives in the browser rather
/// than in the spreadsheet.
/// </summary>
public class SettingsModel : PageModel
{
    /// <summary>Email of the signed-in Google account.</summary>
    public string Email { get; private set; } = "";

    /// <summary>Display name from the Google profile, when it was supplied.</summary>
    public string DisplayName { get; private set; } = "";

    public void OnGet()
    {
        Email = User.FindFirstValue(ClaimTypes.Email) ?? "";
        DisplayName = User.FindFirstValue(ClaimTypes.Name)
            ?? User.Identity?.Name
            ?? "";
    }
}
