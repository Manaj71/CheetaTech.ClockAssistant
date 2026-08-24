using CheetaTech.ClockAssistant.Core.Providers;

namespace CheetaTech.ClockAssistant.Providers.UKG;

public sealed class UkgReadyProvider : ITimeClockProvider
{
    private readonly HttpClient _httpClient;
    private readonly UkgProviderSettings _settings;
    private readonly UkgCredentials? _credentials;

    public UkgReadyProvider(
        HttpClient httpClient,
        UkgProviderSettings settings,
        UkgCredentials? credentials = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _credentials = credentials;
    }

    public async Task<ProviderResult> TestConnectionAsync()
    {
        var timestamp = DateTimeOffset.UtcNow;

        try
        {
            var pageInfo = await GetClockPageAsync().ConfigureAwait(false);

            if (!pageInfo.HasClockInAction || !pageInfo.HasClockOutAction)
            {
                return Failure(
                    action: "TestConnection",
                    timestamp: timestamp,
                    technicalStatus: "ProviderChanged",
                    errorMessage: "Expected UKG Clock In / Clock Out actions were not found.");
            }

            return new ProviderResult
            {
                Success = true,
                Action = "TestConnection",
                ProviderMessage = "UKG clock page structure recognized.",
                Timestamp = timestamp,
                TechnicalStatus = "Ready"
            };
        }
        catch (UkgHttpStatusException ex)
        {
            return Failure(
                action: "TestConnection",
                timestamp: timestamp,
                technicalStatus: $"HTTP_{ex.StatusCode}",
                errorMessage: ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return Failure(
                action: "TestConnection",
                timestamp: timestamp,
                technicalStatus: "NetworkUnavailable",
                errorMessage: ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            return Failure(
                action: "TestConnection",
                timestamp: timestamp,
                technicalStatus: "NetworkUnavailable",
                errorMessage: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(
                action: "TestConnection",
                timestamp: timestamp,
                technicalStatus: "ProviderChanged",
                errorMessage: ex.Message);
        }
    }

    public Task<ProviderResult> ClockInAsync()
    {
        return ExecutePunchAsync(UkgPunchAction.ClockIn);
    }

    public Task<ProviderResult> ClockOutAsync()
    {
        return ExecutePunchAsync(UkgPunchAction.ClockOut);
    }

    private async Task<ProviderResult> ExecutePunchAsync(UkgPunchAction punchAction)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var actionName = punchAction == UkgPunchAction.ClockIn
            ? "ClockIn"
            : "ClockOut";

        if (_credentials is null)
        {
            return Failure(
                action: actionName,
                timestamp: timestamp,
                technicalStatus: "CredentialsUnavailable",
                errorMessage: "UKG credentials are required for punch execution.");
        }

        try
        {
            var pageInfo = await GetClockPageAsync().ConfigureAwait(false);

            var preparedRequest = UkgPunchRequestBuilder.Prepare(
                pageInfo,
                _credentials,
                punchAction);

            using var content = new FormUrlEncodedContent(
                preparedRequest.FormFields);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                preparedRequest.PostUrl)
            {
                Content = content
            };

            using var response = await _httpClient
                .SendAsync(request)
                .ConfigureAwait(false);

            var responseHtml = await response.Content
                .ReadAsStringAsync()
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Failure(
                    action: actionName,
                    timestamp: timestamp,
                    technicalStatus: $"HTTP_{(int)response.StatusCode}",
                    errorMessage: $"UKG punch request returned HTTP {(int)response.StatusCode}.");
            }

            var parsed = UkgPunchResponseParser.Parse(
                responseHtml,
                punchAction);

            return new ProviderResult
            {
                Success = parsed.Success,
                Action = actionName,
                ProviderMessage = parsed.ProviderMessage,
                Timestamp = timestamp,
                TechnicalStatus = parsed.TechnicalStatus,
                ErrorMessage = parsed.Success
                    ? null
                    : parsed.ProviderMessage ?? "UKG did not confirm the punch."
            };
        }
        catch (UkgHttpStatusException ex)
        {
            return Failure(
                action: actionName,
                timestamp: timestamp,
                technicalStatus: $"HTTP_{ex.StatusCode}",
                errorMessage: ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return Failure(
                action: actionName,
                timestamp: timestamp,
                technicalStatus: "NetworkUnavailable",
                errorMessage: ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            return Failure(
                action: actionName,
                timestamp: timestamp,
                technicalStatus: "NetworkUnavailable",
                errorMessage: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(
                action: actionName,
                timestamp: timestamp,
                technicalStatus: "ProviderChanged",
                errorMessage: ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Failure(
                action: actionName,
                timestamp: timestamp,
                technicalStatus: "InvalidConfiguration",
                errorMessage: ex.Message);
        }
    }

    private async Task<UkgClockPageInfo> GetClockPageAsync()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            _settings.ClockUrl);

        using var response = await _httpClient
            .SendAsync(request)
            .ConfigureAwait(false);

        var html = await response.Content
            .ReadAsStringAsync()
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new UkgHttpStatusException(
                (int)response.StatusCode,
                $"UKG clock page returned HTTP {(int)response.StatusCode}.");
        }

        return UkgClockPageParser.Parse(
            html,
            _settings.ClockUrl);
    }

    private static ProviderResult Failure(
        string action,
        DateTimeOffset timestamp,
        string technicalStatus,
        string errorMessage)
    {
        return new ProviderResult
        {
            Success = false,
            Action = action,
            Timestamp = timestamp,
            TechnicalStatus = technicalStatus,
            ErrorMessage = errorMessage
        };
    }

    private sealed class UkgHttpStatusException : Exception
    {
        public UkgHttpStatusException(
            int statusCode,
            string message)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }
}
