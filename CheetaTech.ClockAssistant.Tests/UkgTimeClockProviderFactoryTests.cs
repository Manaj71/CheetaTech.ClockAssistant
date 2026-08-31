using System.Net;
using CheetaTech.ClockAssistant.Core.Configuration;
using CheetaTech.ClockAssistant.Providers.UKG;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class UkgTimeClockProviderFactoryTests
{
    [Fact]
    public async Task Create_Uses_Exactly_Configured_ProviderUrl()
    {
        var configuredUrl =
            new Uri(
                "https://tenant.example.test/ta/ConfiguredTenant.clock");

        var handler =
            new CapturingHandler(
                new HttpResponseMessage(
                    HttpStatusCode.OK)
                {
                    Content =
                        new StringContent(
                            RecognizedClockPage())
                });

        using var httpClient =
            new HttpClient(handler);

        var factory =
            new UkgTimeClockProviderFactory(
                httpClient);

        var provider =
            factory.Create(
                Configuration(
                    configuredUrl.ToString()));

        var result =
            await provider
                .TestConnectionAsync();

        Assert.True(result.Success);
        Assert.Single(handler.RequestUris);
        Assert.Equal(
            configuredUrl,
            handler.RequestUris[0]);
    }

    [Fact]
    public void Create_Invalid_ProviderUrl_Fails_Before_Any_Request()
    {
        var handler =
            new CapturingHandler();

        using var httpClient =
            new HttpClient(handler);

        var factory =
            new UkgTimeClockProviderFactory(
                httpClient);

        Assert.Throws<ArgumentException>(
            () => factory.Create(
                Configuration(
                    "not-a-provider-url")));

        Assert.Empty(
            handler.RequestUris);
    }

    [Fact]
    public void Create_Rejects_NonUkg_Configuration()
    {
        var handler =
            new CapturingHandler();

        using var httpClient =
            new HttpClient(handler);

        var factory =
            new UkgTimeClockProviderFactory(
                httpClient);

        var configuration =
            Configuration(
                "https://provider.test/clock") with
            {
                ProviderType = "OTHER"
            };

        Assert.Throws<ArgumentException>(
            () => factory.Create(
                configuration));

        Assert.Empty(
            handler.RequestUris);
    }

    private static ClockAssistantConfiguration Configuration(
        string providerUrl)
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = providerUrl
        };
    }

    private static string RecognizedClockPage()
    {
        return """
            <html>
              <body>
                <form METHOD='POST'
                      name='TheForm'
                      action='/ta/ConfiguredTenant.clock?rnd=TEST'>
                  <input type='hidden' name='$LoginAction'>
                  <input type='hidden' name='$action'>
                  <input type='hidden' name='$actionPrm'>
                  <button onClick='doPunchAction("PUNCH_IN");'>Clock In</button>
                  <button onClick='doPunchAction("PUNCH_OUT");'>Clock Out</button>
                </form>
              </body>
            </html>
            """;
    }

    private sealed class CapturingHandler
        : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public CapturingHandler(
            params HttpResponseMessage[] responses)
        {
            _responses =
                new Queue<HttpResponseMessage>(
                    responses);
        }

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            RequestUris.Add(
                request.RequestUri!);

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "No mocked HTTP response remains.");
            }

            var response =
                _responses.Dequeue();

            response.RequestMessage =
                request;

            return Task.FromResult(
                response);
        }
    }
}