using System.Security.Claims;
using InventoryManagement.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuration
// Priority: environment variable -> appsettings.json -> User Secrets (dev only).
// On App Engine the values come from the env_variables / secret volume in
// app.yaml. Locally, use `dotnet user-secrets set SpreadsheetId "..."`.
// ---------------------------------------------------------------------------
string? spreadsheetId = Environment.GetEnvironmentVariable("SpreadsheetId")
    ?? builder.Configuration["SpreadsheetId"];

if (string.IsNullOrWhiteSpace(spreadsheetId))
{
    throw new InvalidOperationException(
        "SpreadsheetId is not configured. Set the 'SpreadsheetId' environment variable, " +
        "or add it to appsettings.json / User Secrets.");
}

string? credentialsPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")
    ?? builder.Configuration["GOOGLE_APPLICATION_CREDENTIALS"];

// ---------------------------------------------------------------------------
// Google OAuth credentials (v2.2)
// These identify the app to Google's consent screen and are NOT the service
// account used for the Sheets API — they are a separate OAuth 2.0 "Web
// application" client created in the same Google Cloud project.
//
// Set them with either naming style:
//   Authentication:Google:ClientId / :ClientSecret   (appsettings, user-secrets)
//   GoogleClientId / GoogleClientSecret              (environment variables)
//
// The OAuth client's "Authorised redirect URIs" must contain
// <base-url>/signin-google for every host the app is served from, e.g.
//   https://localhost:62251/signin-google
//   https://inventory-ms-xxxxxxxx-uc.a.run.app/signin-google
// ---------------------------------------------------------------------------
string? googleClientId = Environment.GetEnvironmentVariable("GoogleClientId")
    ?? builder.Configuration["Authentication:Google:ClientId"];

string? googleClientSecret = Environment.GetEnvironmentVariable("GoogleClientSecret")
    ?? builder.Configuration["Authentication:Google:ClientSecret"];

if (string.IsNullOrWhiteSpace(googleClientId) || string.IsNullOrWhiteSpace(googleClientSecret))
{
    throw new InvalidOperationException(
        "Google sign-in is not configured. Set 'Authentication:Google:ClientId' and " +
        "'Authentication:Google:ClientSecret' (or the GoogleClientId / GoogleClientSecret " +
        "environment variables) to the credentials of an OAuth 2.0 Web application client. " +
        "Without them the app cannot authenticate anyone and refuses to start rather than " +
        "serving the inventory unprotected.");
}

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------
builder.Services.AddRazorPages();
builder.Services.AddSingleton(new GoogleSheetsService(spreadsheetId, credentialsPath ?? ""));
builder.Services.AddSingleton<AccessControlService>();

// ---------------------------------------------------------------------------
// Authentication (v2.2)
// A cookie carries the session; Google is only used to establish it. The email
// returned by Google is checked against the AllowedUsers tab before the cookie
// is issued, so an authenticated-but-unlisted Google account never gets in.
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/AccessDenied";
        options.ReturnUrlParameter = "returnUrl";

        // "Persistent authentication cookie": survives a browser restart and is
        // renewed while the user keeps working.
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        options.Cookie.Name = "ims.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Cloud Run terminates TLS at the load balancer, so the container sees
        // plain HTTP. UseForwardedHeaders (below) restores the original scheme,
        // which lets "secure only when the request was HTTPS" work correctly.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    })
    .AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;

        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.SaveTokens = false;

        // Gate on the AllowedUsers sheet before the cookie is written.
        options.Events.OnTicketReceived = async context =>
        {
            var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
            var access = context.HttpContext.RequestServices.GetRequiredService<AccessControlService>();

            if (await access.IsAllowedAsync(email))
            {
                // Keep the user signed in across browser restarts.
                if (context.Properties is not null)
                    context.Properties.IsPersistent = true;
                return;
            }

            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Authentication");
            logger.LogWarning("Rejected sign-in for {Email}: not listed in the AllowedUsers sheet.", email);

            // Drop any cookie that may already exist, then stop the sign-in and
            // send the user to the Access Denied page instead.
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            context.HandleResponse();
            var target = "/AccessDenied";
            if (!string.IsNullOrWhiteSpace(email))
                target += "?email=" + Uri.EscapeDataString(email);
            context.Response.Redirect(target);
        };

        // A failed or cancelled consent screen should land somewhere friendly
        // rather than throwing a 500.
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect("/Login?error=" + Uri.EscapeDataString(
                context.Failure?.Message ?? "Google sign-in did not complete."));
            return Task.CompletedTask;
        };
    });

// Every page requires a signed-in user unless it opts out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// App Engine terminates TLS at its load balancer and forwards plain HTTP
// internally with X-Forwarded-* headers. Trust them so UseHttpsRedirection
// and scheme-aware redirects behave correctly in production.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// builder.Services.AddSingleton(new GoogleSheetsService(spreadsheetId, credentialsPath ?? ""));

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // HTTPS is enforced by App Engine's load balancer (HSTS is sent by Google's
    // frontend), so neither is configured here.
}

// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
