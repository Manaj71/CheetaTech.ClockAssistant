namespace CheetaTech.ClockAssistant.Core.Configuration;

public sealed record SetupCandidate(
    ClockAssistantConfiguration? Configuration,
    string Username,
    string Password);
