using CheetaTech.ClockAssistant.Providers.UKG;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class UkgCredentialValidationResponseParserTests
{
    [Fact]
    public void Parse_Returns_Accepted_For_Authenticated_Response()
    {
        const string html = """
            <html>
              <body>
                <div>Welcome back MANSOOR</div>
                <div>Employee Home</div>
              </body>
            </html>
            """;

        var result =
            UkgCredentialValidationResponseParser.Parse(html);

        Assert.True(result.Success);
        Assert.Equal(
            "CredentialsAccepted",
            result.TechnicalStatus);
    }

    [Fact]
    public void Parse_Returns_Rejected_For_Invalid_Password()
    {
        const string html = """
            <html>
              <body>
                <div>Invalid password. Please try again.</div>
                <form name="TheForm">
                  <input name="Username" />
                  <input name="Password" type="password" />
                  <button onclick="doLogin()">Sign In</button>
                </form>
              </body>
            </html>
            """;

        var result =
            UkgCredentialValidationResponseParser.Parse(html);

        Assert.False(result.Success);
        Assert.Equal(
            "CredentialsRejected",
            result.TechnicalStatus);
    }

    [Fact]
    public void Parse_Does_Not_Accept_Welcome_Back_When_Login_Form_Remains()
    {
        const string html = """
            <html>
              <body>
                <div>Welcome back</div>
                <form name="TheForm">
                  <input name="Username" />
                  <input name="Password" type="password" />
                  <button onclick="doLogin()">Sign In</button>
                </form>
              </body>
            </html>
            """;

        var result =
            UkgCredentialValidationResponseParser.Parse(html);

        Assert.False(result.Success);
        Assert.Equal(
            "UnknownProviderResponse",
            result.TechnicalStatus);
    }

    [Fact]
    public void Parse_Returns_Unknown_For_Unrecognized_Response()
    {
        const string html = """
            <html>
              <body>
                <div>Provider response</div>
              </body>
            </html>
            """;

        var result =
            UkgCredentialValidationResponseParser.Parse(html);

        Assert.False(result.Success);
        Assert.Equal(
            "UnknownProviderResponse",
            result.TechnicalStatus);
    }
}
