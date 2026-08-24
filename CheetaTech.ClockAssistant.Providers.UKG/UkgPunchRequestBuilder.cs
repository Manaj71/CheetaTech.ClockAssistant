namespace CheetaTech.ClockAssistant.Providers.UKG;

public enum UkgPunchAction
{
    ClockIn,
    ClockOut
}

public sealed record UkgPreparedPunchRequest(
    Uri PostUrl,
    UkgPunchAction PunchAction,
    IReadOnlyDictionary<string, string> FormFields);

public static class UkgPunchRequestBuilder
{
    public static UkgPreparedPunchRequest Prepare(
        UkgClockPageInfo pageInfo,
        UkgCredentials credentials,
        UkgPunchAction punchAction)
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
            ["$LoginAction"] = "DoPunch",
            ["$action"] = punchAction switch
            {
                UkgPunchAction.ClockIn => "PUNCH_IN",
                UkgPunchAction.ClockOut => "PUNCH_OUT",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(punchAction),
                    punchAction,
                    "Unsupported UKG punch action.")
            },
            ["$actionPrm"] = string.Empty
        };

        return new UkgPreparedPunchRequest(
            PostUrl: pageInfo.PostUrl,
            PunchAction: punchAction,
            FormFields: fields);
    }
}
