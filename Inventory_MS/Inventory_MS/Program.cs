using InventoryManagement.Services;
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
// Services
// ---------------------------------------------------------------------------
builder.Services.AddRazorPages();
builder.Services.AddSingleton(new GoogleSheetsService(spreadsheetId, credentialsPath ?? ""));

// App Engine terminates TLS at its load balancer and forwards plain HTTP
// internally with X-Forwarded-* headers. Trust them so UseHttpsRedirection
// and scheme-aware redirects behave correctly in production.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // HTTPS is enforced by App Engine's load balancer (HSTS is sent by Google's
    // frontend), so neither is configured here.
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
