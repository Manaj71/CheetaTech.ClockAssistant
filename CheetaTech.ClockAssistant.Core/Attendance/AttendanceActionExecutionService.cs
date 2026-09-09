using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Core.Providers;
using CheetaTech.ClockAssistant.Core.Security;

namespace CheetaTech.ClockAssistant.Core.Attendance;

public sealed class AttendanceActionExecutionService
    : IAttendanceActionExecutionService
{
    private readonly IClockAssistantConfigurationStore _configurationStore;
    private readonly ICredentialStore _credentialStore;
    private readonly ITimeClockProviderResolver _providerResolver;
    private readonly IAttendanceStateStore _attendanceStateStore;
    private readonly AttendanceStateEvaluator _stateEvaluator;
    private readonly IAttendanceProviderExecutionGate _providerExecutionGate;
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    public AttendanceActionExecutionService(
        IClockAssistantConfigurationStore configurationStore,
        ICredentialStore credentialStore,
        ITimeClockProviderResolver providerResolver,
        IAttendanceStateStore attendanceStateStore,
        AttendanceStateEvaluator stateEvaluator,
        IAttendanceProviderExecutionGate providerExecutionGate)
    {
        _configurationStore =
            configurationStore
            ?? throw new ArgumentNullException(nameof(configurationStore));

        _credentialStore =
            credentialStore
            ?? throw new ArgumentNullException(nameof(credentialStore));

        _providerResolver =
            providerResolver
            ?? throw new ArgumentNullException(nameof(providerResolver));

        _attendanceStateStore =
            attendanceStateStore
            ?? throw new ArgumentNullException(nameof(attendanceStateStore));

        _stateEvaluator =
            stateEvaluator
            ?? throw new ArgumentNullException(nameof(stateEvaluator));

        _providerExecutionGate =
            providerExecutionGate
            ?? throw new ArgumentNullException(nameof(providerExecutionGate));
    }

    public async Task<AttendanceActionExecutionResult> ExecuteAsync(
        AttendanceActionType actionType,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        await _executionGate.WaitAsync(cancellationToken);

        try
        {
            var configuration =
                await _configurationStore.GetAsync(cancellationToken);

            if (configuration is null)
            {
                return Result(
                    actionType,
                    AttendanceActionExecutionStatus.MissingConfiguration);
            }

            var dateEvaluation =
                _stateEvaluator.Evaluate(
                    configuration,
                    utcNow);

            var currentRecord =
                await _attendanceStateStore.LoadAsync(
                    dateEvaluation.AttendanceDate,
                    cancellationToken);

            var evaluation =
                _stateEvaluator.Evaluate(
                    configuration,
                    utcNow,
                    currentRecord);

            if (!IsEligible(evaluation, actionType))
            {
                return Result(
                    actionType,
                    AttendanceActionExecutionStatus.NotEligible,
                    evaluation.AttendanceDate);
            }

            if (!_providerExecutionGate.IsExecutionAllowed(
                    actionType,
                    evaluation.AttendanceDate,
                    utcNow))
            {
                return Result(
                    actionType,
                    AttendanceActionExecutionStatus.ExecutionDisabled,
                    evaluation.AttendanceDate);
            }

            StoredCredentials? credentials;

            try
            {
                credentials =
                    await _credentialStore.GetCredentialsAsync(
                        cancellationToken);
            }
            catch
            {
                return Result(
                    actionType,
                    AttendanceActionExecutionStatus.CredentialReadFailed,
                    evaluation.AttendanceDate);
            }

            if (credentials is null)
            {
                return Result(
                    actionType,
                    AttendanceActionExecutionStatus.MissingCredentials,
                    evaluation.AttendanceDate);
            }

            ITimeClockProvider provider;

            try
            {
                provider =
                    _providerResolver.Resolve(
                        configuration,
                        credentials);
            }
            catch
            {
                return Result(
                    actionType,
                    AttendanceActionExecutionStatus.ProviderResolutionFailed,
                    evaluation.AttendanceDate);
            }

            var record =
                currentRecord is not null &&
                currentRecord.AttendanceDate == evaluation.AttendanceDate
                    ? currentRecord
                    : new DailyAttendanceRecord
                    {
                        AttendanceDate = evaluation.AttendanceDate
                    };

            var inProgressRecord =
                SetActionState(
                    record,
                    actionType,
                    AttendanceActionState.InProgress);

            try
            {
                await _attendanceStateStore.SaveAsync(
                    inProgressRecord,
                    cancellationToken);
            }
            catch
            {
                return Result(
                    actionType,
                    AttendanceActionExecutionStatus.PersistenceFailed,
                    evaluation.AttendanceDate);
            }

            ProviderResult providerResult;

            try
            {
                providerResult =
                    actionType == AttendanceActionType.ClockIn
                        ? await provider.ClockInAsync()
                        : await provider.ClockOutAsync();
            }
            catch
            {
                return await SaveFinalStateAsync(
                    actionType,
                    evaluation.AttendanceDate,
                    inProgressRecord,
                    AttendanceActionState.Unknown,
                    AttendanceActionExecutionStatus.ProviderUnknown,
                    providerRequestSent: true,
                    providerConfirmed: false,
                    cancellationToken);
            }

            var finalState =
                providerResult.Success
                    ? AttendanceActionState.Succeeded
                    : AttendanceActionState.Failed;

            var finalStatus =
                providerResult.Success
                    ? AttendanceActionExecutionStatus.Succeeded
                    : AttendanceActionExecutionStatus.ProviderRejected;

            return await SaveFinalStateAsync(
                actionType,
                evaluation.AttendanceDate,
                inProgressRecord,
                finalState,
                finalStatus,
                providerRequestSent: true,
                providerConfirmed: providerResult.Success,
                cancellationToken);
        }
        finally
        {
            _executionGate.Release();
        }
    }

    private async Task<AttendanceActionExecutionResult> SaveFinalStateAsync(
        AttendanceActionType actionType,
        DateOnly attendanceDate,
        DailyAttendanceRecord inProgressRecord,
        AttendanceActionState finalState,
        AttendanceActionExecutionStatus finalStatus,
        bool providerRequestSent,
        bool providerConfirmed,
        CancellationToken cancellationToken)
    {
        var finalRecord =
            SetActionState(
                inProgressRecord,
                actionType,
                finalState);

        try
        {
            await _attendanceStateStore.SaveAsync(
                finalRecord,
                cancellationToken);
        }
        catch
        {
            return new AttendanceActionExecutionResult(
                actionType,
                AttendanceActionExecutionStatus.PersistenceFailed,
                attendanceDate,
                providerRequestSent,
                providerConfirmed);
        }

        return new AttendanceActionExecutionResult(
            actionType,
            finalStatus,
            attendanceDate,
            providerRequestSent,
            providerConfirmed);
    }

    private static bool IsEligible(
        AttendanceStateEvaluation evaluation,
        AttendanceActionType actionType)
    {
        return actionType switch
        {
            AttendanceActionType.ClockIn =>
                (evaluation.ClockInState == AttendanceActionState.Due) &&
                evaluation.ClockInState == AttendanceActionState.Due,

            AttendanceActionType.ClockOut =>
                (evaluation.ClockOutState == AttendanceActionState.Due) &&
                evaluation.ClockOutState == AttendanceActionState.Due,

            _ => false
        };
    }

    private static DailyAttendanceRecord SetActionState(
        DailyAttendanceRecord record,
        AttendanceActionType actionType,
        AttendanceActionState state)
    {
        return actionType switch
        {
            AttendanceActionType.ClockIn =>
                record with
                {
                    ClockInState = state
                },

            AttendanceActionType.ClockOut =>
                record with
                {
                    ClockOutState = state
                },

            _ => record
        };
    }

    private static AttendanceActionExecutionResult Result(
        AttendanceActionType actionType,
        AttendanceActionExecutionStatus status,
        DateOnly? attendanceDate = null)
    {
        return new AttendanceActionExecutionResult(
            actionType,
            status,
            attendanceDate,
            ProviderRequestSent: false,
            ProviderConfirmed: false);
    }
}