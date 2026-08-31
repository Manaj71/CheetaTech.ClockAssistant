using System.Net;
using CheetaTech.ClockAssistant.Providers.UKG;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class UkgReadyProviderTests
{
    private static readonly Uri ClockUrl =
        new("https://provider.test/ta/Tenant.clock");

    [Fact]
    public async Task TestConnectionAsync_Returns_Ready_For_Recognized_Clock_Page()
    {
        using var httpClient = new HttpClient(
            new SequenceHttpMessageHandler(
                Response(HttpStatusCode.OK, ClockPage("CONNECTION"))));

        var provider = CreateProvider(
            httpClient,
            credentials: null);

        var result = await provider.TestConnectionAsync();

        Assert.True(result.Success);
        Assert.Equal("TestConnection", result.Action);
        Assert.Equal("Ready", result.TechnicalStatus);
    }

    [Fact]
    public async Task TestConnectionAsync_Returns_ProviderChanged_When_Actions_Are_Missing()
    {
        const string html = """
            <html>
              <body>
                <form METHOD='POST'
                      name='TheForm'
                      action='/ta/Tenant.clock?rnd=TEST'>
                </form>
              </body>
            </html>
            """;

        using var httpClient = new HttpClient(
            new SequenceHttpMessageHandler(
                Response(HttpStatusCode.OK, html)));

        var provider = CreateProvider(
            httpClient,
            credentials: null);

        var result = await provider.TestConnectionAsync();

        Assert.False(result.Success);
        Assert.Equal("ProviderChanged", result.TechnicalStatus);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_Uses_Login_Action_And_Returns_Accepted()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(
                HttpStatusCode.OK,
                ClockPage("VALIDATE")),
            Response(
                HttpStatusCode.OK,
                "<html><body>Welcome back MANSOOR</body></html>"));

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(
            httpClient,
            credentials: null);

        var result = await provider.ValidateCredentialsAsync(
            "fake-user",
            "fake-password");

        Assert.True(result.Success);
        Assert.Equal(
            "ValidateCredentials",
            result.Action);
        Assert.Equal(
            "CredentialsAccepted",
            result.TechnicalStatus);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(
            HttpMethod.Get,
            handler.Requests[0].Method);
        Assert.Equal(
            HttpMethod.Post,
            handler.Requests[1].Method);

        var postedBody = handler.Requests[1].Body!;

        Assert.Contains(
            "Username=fake-user",
            postedBody);
        Assert.Contains(
            "Password=fake-password",
            postedBody);
        Assert.Contains(
            "%24LoginAction=Login",
            postedBody);
        Assert.Contains(
            "%24action=",
            postedBody);
        Assert.Contains(
            "%24actionPrm=",
            postedBody);

        Assert.DoesNotContain(
            "DoPunch",
            postedBody,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "PUNCH_IN",
            postedBody,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "PUNCH_OUT",
            postedBody,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_Returns_Rejected_For_Invalid_Password()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(
                HttpStatusCode.OK,
                ClockPage("BADLOGIN")),
            Response(
                HttpStatusCode.OK,
                """
                <html>
                  <body>
                    <div>Invalid password. Please try again.</div>
                    <form name='TheForm'>
                      <input name='Username' />
                      <input name='Password' type='password' />
                      <button onclick='doLogin()'>Sign In</button>
                    </form>
                  </body>
                </html>
                """));

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(
            httpClient,
            credentials: null);

        var result = await provider.ValidateCredentialsAsync(
            "fake-user",
            "wrong-password");

        Assert.False(result.Success);
        Assert.Equal(
            "CredentialsRejected",
            result.TechnicalStatus);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_Rejects_Empty_Username_Without_Request()
    {
        var handler = new SequenceHttpMessageHandler();

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(
            httpClient,
            credentials: null);

        var result = await provider.ValidateCredentialsAsync(
            string.Empty,
            "fake-password");

        Assert.False(result.Success);
        Assert.Equal(
            "InvalidConfiguration",
            result.TechnicalStatus);
        Assert.Empty(handler.Requests);
    }
    [Fact]
    public async Task ClockInAsync_Performs_Get_Then_Post_And_Returns_ProviderConfirmed()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.OK, ClockPage("CLOCKIN")),
            Response(
                HttpStatusCode.OK,
                "<html><body>Fri Aug-21-2026, 07:58a Punched In Successfully</body></html>"));

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(
            httpClient,
            new UkgCredentials("fake-user", "fake-password"));

        var result = await provider.ClockInAsync();

        Assert.True(result.Success);
        Assert.Equal("ClockIn", result.Action);
        Assert.Equal("ProviderConfirmed", result.TechnicalStatus);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);

        var postedBody = handler.Requests[1].Body!;

        Assert.Contains("Username=fake-user", postedBody);
        Assert.Contains("Password=fake-password", postedBody);
        Assert.Contains("%24LoginAction=DoPunch", postedBody);
        Assert.Contains("%24action=PUNCH_IN", postedBody);
        Assert.Contains("u=DYNAMIC-CLOCKIN", postedBody);
        Assert.Contains("XTimeStamp=123456789", postedBody);
    }

    [Fact]
    public async Task ClockOutAsync_Performs_Get_Then_Post_And_Returns_ProviderConfirmed()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.OK, ClockPage("CLOCKOUT")),
            Response(
                HttpStatusCode.OK,
                "<html><body>Fri Aug-21-2026, 03:32p Punched Out Successfully</body></html>"));

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(
            httpClient,
            new UkgCredentials("fake-user", "fake-password"));

        var result = await provider.ClockOutAsync();

        Assert.True(result.Success);
        Assert.Equal("ClockOut", result.Action);
        Assert.Equal("ProviderConfirmed", result.TechnicalStatus);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);

        var postedBody = handler.Requests[1].Body!;

        Assert.Contains("%24action=PUNCH_OUT", postedBody);
        Assert.Contains("u=DYNAMIC-CLOCKOUT", postedBody);
    }

    [Fact]
    public async Task Http200_Post_Without_Provider_Success_Text_Is_Not_Success()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.OK, ClockPage("UNKNOWN")),
            Response(
                HttpStatusCode.OK,
                "<html><body><h1>Clock</h1><p>Welcome back</p></body></html>"));

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(
            httpClient,
            new UkgCredentials("fake-user", "fake-password"));

        var result = await provider.ClockInAsync();

        Assert.False(result.Success);
        Assert.Equal(
            "UnknownProviderResponse",
            result.TechnicalStatus);
    }

    [Fact]
    public async Task ClockInAsync_Without_Credentials_Does_Not_Send_Any_Request()
    {
        var handler = new SequenceHttpMessageHandler();

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(
            httpClient,
            credentials: null);

        var result = await provider.ClockInAsync();

        Assert.False(result.Success);
        Assert.Equal(
            "CredentialsUnavailable",
            result.TechnicalStatus);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task NonSuccess_Post_Status_Is_Not_Attendance_Success()
    {
        var handler = new SequenceHttpMessageHandler(
            Response(HttpStatusCode.OK, ClockPage("HTTPFAIL")),
            Response(
                HttpStatusCode.InternalServerError,
                "<html><body>Server error</body></html>"));

        using var httpClient = new HttpClient(handler);
        var provider = CreateProvider(
            httpClient,
            new UkgCredentials("fake-user", "fake-password"));

        var result = await provider.ClockOutAsync();

        Assert.False(result.Success);
        Assert.Equal("HTTP_500", result.TechnicalStatus);
    }

    private static UkgReadyProvider CreateProvider(
        HttpClient httpClient,
        UkgCredentials? credentials)
    {
        return new UkgReadyProvider(
            httpClient,
            new UkgProviderSettings
            {
                ClockUrl = ClockUrl
            },
            credentials);
    }

    private static HttpResponseMessage Response(
        HttpStatusCode statusCode,
        string content)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
    }

    private static string ClockPage(string dynamicValue)
    {
        return $$"""
            <html>
              <body>
                <form METHOD='POST'
                      name='TheForm'
                      action='/ta/Tenant.clock?rnd=WWW'>
                  <input type='hidden' name='s' value='1'>
                  <input type='hidden' name='$LoginAction'>
                  <input type='hidden' name='$action'>
                  <input type='hidden' name='$actionPrm'>
                  <input type='hidden' name='u' value='DYNAMIC-{{dynamicValue}}'>
                  <input type='hidden' name='XTimeStamp' value='123456789'>
                  <input type='hidden' name='XServerId' value='server.test'>
                  <input type='text' name='Username' value=''>
                  <input type='password' name='Password' value=''>
                  <input type='text' name='Badge' value=''>
                  <input type='hidden' name='NoRedirect' value='1'>
                  <button onClick='doPunchAction("PUNCH_IN");'>Clock In</button>
                  <button onClick='doPunchAction("PUNCH_OUT");'>Clock Out</button>
                </form>
              </body>
            </html>
            """;
    }

    private sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHttpMessageHandler(
            params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string? body = null;

            if (request.Content is not null)
            {
                body = await request.Content
                    .ReadAsStringAsync(cancellationToken);
            }

            Requests.Add(
                new CapturedRequest(
                    request.Method,
                    request.RequestUri!,
                    body));

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException(
                    "No mocked HTTP response remains for this request.");
            }

            var response = _responses.Dequeue();
            response.RequestMessage = request;

            return response;
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri RequestUri,
        string? Body);
}

