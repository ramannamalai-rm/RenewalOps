using FluentAssertions;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RenewalOps.UnitTests;

public class ExpiryDateParsingTests
{
    private static readonly Regex ExpiryRegex = new(
        @"(?:expir(?:y|es|ation)(?:\s+(?:date|on))?|valid\s+(?:till|until|thru|through)|exp\.?\s*date|best\s+before)\s*[:\-]?\s*(\d{1,2}[\/.\-]\d{1,2}[\/.\-]\d{2,4}|\d{4}[\/.\-]\d{1,2}[\/.\-]\d{1,2}|\w+\s+\d{1,2},?\s+\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] DateFormats = new[]
    {
        "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "MM-dd-yyyy", "dd-MM-yyyy",
        "MM.dd.yyyy", "dd.MM.yyyy", "MMMM d, yyyy", "MMMM dd, yyyy",
        "MMM d, yyyy", "MMM dd, yyyy", "MM/dd/yy", "dd/MM/yy"
    };

    private DateTime? TryParseExpiry(string text)
    {
        var match = ExpiryRegex.Match(text);
        if (!match.Success) return null;
        var dateStr = match.Groups[1].Value.Trim();
        if (DateTime.TryParseExact(dateStr, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;
        return null;
    }

    [Theory]
    [InlineData("Expiry Date: 12/31/2025", 2025, 12, 31)]
    [InlineData("Valid Until: 2026-06-15", 2026, 6, 15)]
    [InlineData("EXPIRES ON: 01/15/2027", 2027, 1, 15)]
    [InlineData("Exp. Date: March 5, 2026", 2026, 3, 5)]
    [InlineData("valid till 06/30/2025", 2025, 6, 30)]
    public void Should_Extract_Expiry_Date(string input, int year, int month, int day)
    {
        var result = TryParseExpiry(input);
        result.Should().NotBeNull();
        result!.Value.Year.Should().Be(year);
        result.Value.Month.Should().Be(month);
        result.Value.Day.Should().Be(day);
    }

    [Theory]
    [InlineData("This is a regular document with no dates")]
    [InlineData("Created on 2025-01-01")]
    public void Should_Return_Null_When_No_Expiry(string input)
    {
        var result = TryParseExpiry(input);
        result.Should().BeNull();
    }
}
