namespace CheetaTech.ClockAssistant.Providers.UKG;

/// <summary>
/// Creates a fresh HTTP client/session for one UKG credential-validation attempt.
/// The returned client preserves cookies across the validation GET and Login POST
/// and exposes redirect responses instead of following them automatically.
/// </summary>
public interface IUkgCredentialValidationHttpClientFactory
{
    HttpClient Create();
}
