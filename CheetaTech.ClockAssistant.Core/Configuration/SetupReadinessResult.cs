namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed record SetupReadinessResult(
    SetupReadinessState State,
    bool ConfigurationAvailable,
    bool ConfigurationComplete,
    bool CredentialsAvailable,
    IReadOnlyCollection<string> ConfigurationIssues)
{
    public bool SetupRequired =>
        State == SetupReadinessState.SetupRequired;

    public bool Ready =>
        State == SetupReadinessState.Ready;
}
