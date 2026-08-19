using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages;

/// <summary>
/// Shown when Google authenticated the visitor but their email is not listed in
/// the AllowedUsers tab. Any session cookie is cleared on arrival so the visitor
/// is genuinely signed out, not merely blocked.
/// </summary>
[AllowAnonymous]
public class AccessDeniedModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    public async Task OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
