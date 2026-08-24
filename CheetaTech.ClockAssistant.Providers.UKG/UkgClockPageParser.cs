using System.Net;
using System.Text.RegularExpressions;

namespace CheetaTech.ClockAssistant.Providers.UKG;

public sealed record UkgClockPageInfo(
    string FormName,
    string FormMethod,
    Uri PostUrl,
    bool HasClockInAction,
    bool HasClockOutAction,
    IReadOnlyDictionary<string, string> FormInputs);

public static partial class UkgClockPageParser
{
    public static UkgClockPageInfo Parse(string html, Uri pageUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentNullException.ThrowIfNull(pageUri);

        var formMatch = TheFormRegex().Match(html);

        if (!formMatch.Success)
        {
            throw new InvalidOperationException("UKG clock form 'TheForm' was not found.");
        }

        var formTag = formMatch.Value;

        var methodMatch = MethodRegex().Match(formTag);
        var actionMatch = ActionRegex().Match(formTag);

        if (!methodMatch.Success)
        {
            throw new InvalidOperationException("UKG clock form method was not found.");
        }

        if (!actionMatch.Success)
        {
            throw new InvalidOperationException("UKG clock form action was not found.");
        }

        var method = WebUtility.HtmlDecode(methodMatch.Groups["value"].Value).Trim();
        var action = WebUtility.HtmlDecode(actionMatch.Groups["value"].Value).Trim();

        if (!method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unexpected UKG clock form method '{method}'. Expected POST.");
        }

        if (!Uri.TryCreate(pageUri, action, out var postUrl))
        {
            throw new InvalidOperationException("UKG clock form action could not be resolved.");
        }

        var formInputs = ParseNamedInputs(html);

        return new UkgClockPageInfo(
            FormName: "TheForm",
            FormMethod: method.ToUpperInvariant(),
            PostUrl: postUrl,
            HasClockInAction: ClockInRegex().IsMatch(html),
            HasClockOutAction: ClockOutRegex().IsMatch(html),
            FormInputs: formInputs);
    }

    private static IReadOnlyDictionary<string, string> ParseNamedInputs(string html)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (Match inputMatch in InputTagRegex().Matches(html))
        {
            var tag = inputMatch.Value;
            var nameMatch = NameRegex().Match(tag);

            if (!nameMatch.Success)
            {
                continue;
            }

            var name = WebUtility.HtmlDecode(nameMatch.Groups["value"].Value).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var valueMatch = ValueRegex().Match(tag);

            var value = valueMatch.Success
                ? WebUtility.HtmlDecode(valueMatch.Groups["value"].Value)
                : string.Empty;

            values[name] = value;
        }

        return values;
    }

    [GeneratedRegex(
        @"<form\b(?=[^>]*\bname\s*=\s*['""]?TheForm['""]?)[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TheFormRegex();

    [GeneratedRegex(
        @"\bmethod\s*=\s*['""]?(?<value>[^'"">\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MethodRegex();

    [GeneratedRegex(
        @"\baction\s*=\s*['""](?<value>[^'""]+)['""]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ActionRegex();

    [GeneratedRegex(
        @"<input\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex(
        @"\bname\s*=\s*(?:['""](?<value>[^'""]*)['""]|(?<value>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    [GeneratedRegex(
        @"\bvalue\s*=\s*(?:['""](?<value>[^'""]*)['""]|(?<value>[^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    [GeneratedRegex(
        @"doPunchAction\s*\(\s*['""]PUNCH_IN['""]\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClockInRegex();

    [GeneratedRegex(
        @"doPunchAction\s*\(\s*['""]PUNCH_OUT['""]\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ClockOutRegex();
}
