namespace CheetaTech.ClockAssistant.Providers.UKG;

public sealed record UkgPreparedLoginRequest(
    Uri PostUrl,
    IReadOnlyDictionary<string, string> FormFields);

public static class UkgLoginRequestBuilder
{
    public static UkgPreparedLoginRequest Prepare(
        UkgClockPageInfo pageInfo,
        UkgCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(pageInfo);
        ArgumentNullException.ThrowIfNull(credentials);

        if (string.IsNullOrWhiteSpace(credentials.Username))
        {
            throw new ArgumentException(
                "UKG username is required.",
                nameof(credentials));
        }

        if (string.IsNullOrWhiteSpace(credentials.Password))
        {
            throw new ArgumentException(
                "UKG password is required.",
                nameof(credentials));
        }

        var fields = new Dictionary<string, string>(
            pageInfo.FormInputs,
            StringComparer.Ordinal)
        {
            ["Username"] = credentials.Username,
            ["Password"] = credentials.Password,
            ["$LoginAction"] = "Login",
            ["$action"] = string.Empty,
            ["$actionPrm"] = string.Empty
        };

        return new UkgPreparedLoginRequest(
            pageInfo.PostUrl,
            fields);
    }
}
