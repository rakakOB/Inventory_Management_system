using InventoryManagement.Models;

namespace InventoryManagement.Services;

/// <summary>
/// Decides which Google accounts may use the app, by reading the AllowedUsers
/// tab through the existing <see cref="GoogleSheetsService"/>.
///
/// The list is cached briefly. Sign-in is the only thing that consults it, so
/// the traffic is low, but a burst of sign-ins (or a user retrying) should not
/// turn into a burst of Sheets API calls against the read quota.
///
/// Fails CLOSED: if the tab is missing or the API call throws, nobody is
/// allowed in. An outage must not turn into open access.
/// </summary>
public sealed class AccessControlService
{
    /// <summary>How long a fetched allow-list is reused before being re-read.</summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(2);

    private readonly GoogleSheetsService _sheets;
    private readonly ILogger<AccessControlService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private HashSet<string> _allowed = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    private bool _loaded;

    public AccessControlService(GoogleSheetsService sheets, ILogger<AccessControlService> logger)
    {
        _sheets = sheets;
        _logger = logger;
    }

    /// <summary>True when the email appears in the AllowedUsers tab (case-insensitive).</summary>
    public async Task<bool> IsAllowedAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var allowed = await GetAllowedAsync().ConfigureAwait(false);
        return allowed.Contains(email.Trim());
    }

    /// <summary>Drops the cache so the next check re-reads the sheet.</summary>
    public void Invalidate() => _loadedAt = DateTimeOffset.MinValue;

    private async Task<HashSet<string>> GetAllowedAsync()
    {
        if (_loaded && DateTimeOffset.UtcNow - _loadedAt < CacheLifetime)
            return _allowed;

        await _refreshLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Another request may have refreshed the list while we waited.
            if (_loaded && DateTimeOffset.UtcNow - _loadedAt < CacheLifetime)
                return _allowed;

            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = await _sheets.GetRowsAsync(AllowedUser.SheetName).ConfigureAwait(false);
            for (int i = 1; i < rows.Count; i++)
            {
                var email = AllowedUser.FromRow(rows[i], i + 1).Email;
                if (!string.IsNullOrWhiteSpace(email))
                    emails.Add(email);
            }

            _allowed = emails;
            _loadedAt = DateTimeOffset.UtcNow;
            _loaded = true;

            if (emails.Count == 0)
            {
                _logger.LogWarning(
                    "The '{Sheet}' tab is empty, so no one can sign in. Add at least one email to it.",
                    AllowedUser.SheetName);
            }

            return _allowed;
        }
        catch (Exception ex)
        {
            // Fail closed, and do not cache the failure.
            _logger.LogError(ex,
                "Could not read the '{Sheet}' tab; denying access. Check that the tab exists and that the " +
                "service account can read the spreadsheet.",
                AllowedUser.SheetName);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
