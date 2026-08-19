using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages;

/// <summary>
/// Sign-in page. Reached automatically whenever an unauthenticated request hits
/// any other page (see CookieAuthenticationOptions.LoginPath in Program.cs).
/// </summary>
[AllowAnonymous]
public class LoginModel : PageModel
{
    /// <summary>Where to send the user once Google has vouched for them.</summary>
    public string ReturnUrl { get; private set; } = "/";

    /// <summary>Message shown when a previous attempt failed or was cancelled.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        // Already signed in? Nothing to do here.
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(SafeReturnUrl(returnUrl));

        ReturnUrl = SafeReturnUrl(returnUrl);
        return Page();
    }

    public IActionResult OnPost(string? returnUrl = null)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = SafeReturnUrl(returnUrl),
            IsPersistent = true,
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>Only ever redirect within this app — never to a caller-supplied host.</summary>
    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
}
