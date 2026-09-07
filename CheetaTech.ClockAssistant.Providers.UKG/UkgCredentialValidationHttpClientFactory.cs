using System.Net;

namespace CheetaTech.ClockAssistant.Providers.UKG;

/// <summary>
/// Creates an isolated UKG credential-validation HTTP session.
///
/// Redirect following is disabled intentionally so ValidateCredentialsAsync can
/// inspect the evidence-backed authentication transition:
/// POST *.clock -> 302 Location: same-provider *.home.
///
/// Each validation attempt receives a fresh CookieContainer. The same returned
/// HttpClient is used for both the clock-page GET and Login POST, preserving the
/// UKG session while keeping this behavior isolated from punch execution.
/// </summary>
public sealed class UkgCredentialValidationHttpClientFactory
    : IUkgCredentialValidationHttpClientFactory
{
    public HttpClient Create()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
            CookieContainer = new CookieContainer()
        };

        return new HttpClient(
            handler,
            disposeHandler: true);
    }
}
