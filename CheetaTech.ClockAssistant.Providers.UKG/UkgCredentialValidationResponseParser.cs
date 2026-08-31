using System.Net;
using System.Text.RegularExpressions;

namespace CheetaTech.ClockAssistant.Providers.UKG;

public sealed record UkgCredentialValidationResponse(
    bool Success,
    string TechnicalStatus,
    string? ProviderMessage);

public static partial class UkgCredentialValidationResponseParser
{
    public static UkgCredentialValidationResponse Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var plainText = NormalizeToPlainText(html);

        foreach (var marker in new[]
        {
            "invalid username",
            "invalid password",
            "incorrect password",
            "login failed",
            "sign in failed",
            "unable to sign in",
            "authentication failed"
        })
        {
            if (plainText.Contains(
                marker,
                StringComparison.OrdinalIgnoreCase))
            {
                return new UkgCredentialValidationResponse(
                    Success: false,
                    TechnicalStatus: "CredentialsRejected",
                    ProviderMessage: ExtractAroundMarker(
                        plainText,
                        marker));
            }
        }

        var loginFormStillPresent =
            UsernameInputRegex().IsMatch(html) &&
            PasswordInputRegex().IsMatch(html) &&
            DoLoginRegex().IsMatch(html);

        var authenticatedSignal =
            plainText.Contains(
                "Welcome back",
                StringComparison.OrdinalIgnoreCase);

        if (authenticatedSignal && !loginFormStillPresent)
        {
            return new UkgCredentialValidationResponse(
                Success: true,
                TechnicalStatus: "CredentialsAccepted",
                ProviderMessage: "Provider accepted the credentials.");
        }

        return new UkgCredentialValidationResponse(
            Success: false,
            TechnicalStatus: "UnknownProviderResponse",
            ProviderMessage: null);
    }

    private static string NormalizeToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutScript = ScriptRegex().Replace(html, " ");
        var withoutStyle = StyleRegex().Replace(withoutScript, " ");
        var withoutTags = HtmlTagRegex().Replace(withoutStyle, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);

        return WhitespaceRegex().Replace(decoded, " ").Trim();
    }

    private static string ExtractAroundMarker(
        string plainText,
        string marker)
    {
        var index = plainText.IndexOf(
            marker,
            StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return marker;
        }

        var start = Math.Max(0, index - 80);
        var length = Math.Min(
            240,
            plainText.Length - start);

        return plainText
            .Substring(start, length)
            .Trim();
    }

    [GeneratedRegex(
        @"<script\b[^>]*>.*?</script>",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex(
        @"<style\b[^>]*>.*?</style>",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline |
        RegexOptions.CultureInvariant)]
    private static partial Regex StyleRegex();

    [GeneratedRegex(
        @"<[^>]+>",
        RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(
        @"\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(
        @"<input\b[^>]*\bname\s*=\s*['""]Username['""][^>]*>",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex UsernameInputRegex();

    [GeneratedRegex(
        @"<input\b[^>]*\bname\s*=\s*['""]Password['""][^>]*>",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex PasswordInputRegex();

    [GeneratedRegex(
        @"doLogin\s*\(\s*\)",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant)]
    private static partial Regex DoLoginRegex();
}
