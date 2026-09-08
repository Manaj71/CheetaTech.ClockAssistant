namespace CheetaTech.ClockAssistant.Core.Attendance;

public enum AttendanceActionExecutionStatus
{
    Succeeded = 0,
    NotEligible = 1,
    MissingConfiguration = 2,
    MissingCredentials = 3,
    ProviderResolutionFailed = 4,
    ProviderRejected = 5,
    ProviderUnknown = 6,
    PersistenceFailed = 7,
    ExecutionDisabled = 8
}