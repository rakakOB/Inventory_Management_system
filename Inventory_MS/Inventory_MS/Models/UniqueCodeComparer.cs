using System.Globalization;

namespace InventoryManagement.Models;

/// <summary>
/// Orders UniqueCode values ("E-001", "EL-014", "T-7", "M-1000") the way a
/// human expects: alphabetically by category prefix, then numerically by the
/// suffix.
///
/// A plain string sort is nearly correct because codes are zero-padded to three
/// digits, but it breaks as soon as a code reaches four digits ("E-1000" would
/// sort before "E-002") or if a code was typed into the sheet without padding.
/// Comparing the numeric part as a number sidesteps both problems.
/// </summary>
public sealed class UniqueCodeComparer : IComparer<string>
{
    public static readonly UniqueCodeComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        var (prefixX, numberX) = Split(x);
        var (prefixY, numberY) = Split(y);

        var byPrefix = string.Compare(prefixX, prefixY, StringComparison.OrdinalIgnoreCase);
        if (byPrefix != 0)
            return byPrefix;

        // Codes that carry no number (or an unparseable one) sort before numbered
        // ones so malformed rows stay visible at the top of their group.
        if (numberX != numberY)
            return numberX.CompareTo(numberY);

        // Same prefix and same number: fall back to the raw text so the order is
        // stable and never reports two different codes as equal.
        return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Splits "EL-014" into ("EL-", 14). Returns (code, -1) when there is no numeric tail.</summary>
    private static (string Prefix, int Number) Split(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (string.Empty, -1);

        code = code.Trim();

        // Walk back over the trailing digits.
        var start = code.Length;
        while (start > 0 && char.IsDigit(code[start - 1]))
            start--;

        if (start == code.Length)
            return (code, -1);

        var prefix = code[..start];
        return int.TryParse(code[start..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? (prefix, number)
            : (code, -1);
    }
}
