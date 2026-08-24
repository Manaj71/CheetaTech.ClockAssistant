using System.Net;
using System.Text.RegularExpressions;

namespace CheetaTech.ClockAssistant.Providers.UKG;

public sealed record UkgPunchResponse(
    bool Success,
    string TechnicalStatus,
    string? ProviderMessage);

public static partial class UkgPunchResponseParser
{
    public static UkgPunchResponse Parse(
        string html,
        UkgPunchAction action)
    {
        ArgumentNullException.ThrowIfNull(html);

        var plainText = NormalizeToPlainText(html);

        var expectedSuccessText = action switch
        {
            UkgPunchAction.ClockIn => "Punched In Successfully",
            UkgPunchAction.ClockOut => "Punched Out Successfully",
            _ => throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                "Unsupported UKG punch action.")
        };

        var successIndex = plainText.IndexOf(
            expectedSuccessText,
            StringComparison.OrdinalIgnoreCase);

        if (successIndex >= 0)
        {
            return new UkgPunchResponse(
                Success: true,
                TechnicalStatus: "ProviderConfirmed",
                ProviderMessage: ExtractMessageContaining(
                    plainText,
                    expectedSuccessText));
        }

        var message = ExtractLikelyProviderMessage(plainText);

        return new UkgPunchResponse(
            Success: false,
            TechnicalStatus: string.IsNullOrWhiteSpace(message)
                ? "UnknownProviderResponse"
                : "ProviderRejected",
            ProviderMessage: message);
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

    private static string ExtractMessageContaining(
        string plainText,
        string expectedText)
    {
        var sentences = SentenceSplitRegex().Split(plainText);

        foreach (var sentence in sentences)
        {
            var trimmed = sentence.Trim();

            if (trimmed.Contains(
                expectedText,
                StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }

        return expectedText;
    }

    private static string? ExtractLikelyProviderMessage(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return null;
        }

        foreach (var marker in new[]
        {
            "failed",
            "error",
            "invalid",
            "unable",
            "cannot",
            "not allowed",
            "already"
        })
        {
            var index = plainText.IndexOf(
                marker,
                StringComparison.OrdinalIgnoreCase);

            if (index >= 0)
            {
                var start = Math.Max(0, index - 80);
                var length = Math.Min(240, plainText.Length - start);

                return plainText.Substring(start, length).Trim();
            }
        }

        return null;
    }

    [GeneratedRegex(
        @"<script\b[^>]*>.*?</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ScriptRegex();

    [GeneratedRegex(
        @"<style\b[^>]*>.*?</style>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
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
        @"(?<=[.!?])\s+",
        RegexOptions.CultureInvariant)]
    private static partial Regex SentenceSplitRegex();
}
