using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InventoryManagement.Pages;

/// <summary>
/// Anonymous by design: a failure that happens before or during authentication
/// must still be able to render, otherwise the error handler would itself
/// redirect to sign-in and hide the real problem.
/// </summary>
[AllowAnonymous]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}
