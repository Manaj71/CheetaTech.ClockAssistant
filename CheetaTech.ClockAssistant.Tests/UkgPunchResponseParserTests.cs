using CheetaTech.ClockAssistant.Providers.UKG;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class UkgPunchResponseParserTests
{
    [Fact]
    public void Parse_ClockIn_Returns_ProviderConfirmed_Only_When_Success_Text_Exists()
    {
        const string html = """
            <html>
              <body>
                <div class='message'>
                  Fri Aug-21-2026, 07:58a Punched In Successfully
                </div>
              </body>
            </html>
            """;

        var result = UkgPunchResponseParser.Parse(
            html,
            UkgPunchAction.ClockIn);

        Assert.True(result.Success);
        Assert.Equal("ProviderConfirmed", result.TechnicalStatus);
        Assert.Contains(
            "Punched In Successfully",
            result.ProviderMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ClockOut_Returns_ProviderConfirmed_Only_When_Success_Text_Exists()
    {
        const string html = """
            <html>
              <body>
                <div class='message'>
                  Fri Aug-21-2026, 03:32p Punched Out Successfully
                </div>
              </body>
            </html>
            """;

        var result = UkgPunchResponseParser.Parse(
            html,
            UkgPunchAction.ClockOut);

        Assert.True(result.Success);
        Assert.Equal("ProviderConfirmed", result.TechnicalStatus);
        Assert.Contains(
            "Punched Out Successfully",
            result.ProviderMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_Http200StyleHtml_Without_Success_Text_Is_Not_Success()
    {
        const string html = """
            <html>
              <body>
                <h1>Clock</h1>
                <p>Welcome back</p>
              </body>
            </html>
            """;

        var result = UkgPunchResponseParser.Parse(
            html,
            UkgPunchAction.ClockIn);

        Assert.False(result.Success);
        Assert.Equal(
            "UnknownProviderResponse",
            result.TechnicalStatus);
        Assert.Null(result.ProviderMessage);
    }

    [Fact]
    public void Parse_Known_Error_Text_Returns_ProviderRejected()
    {
        const string html = """
            <html>
              <body>
                <div class='error'>
                  Unable to complete punch. Invalid credentials.
                </div>
              </body>
            </html>
            """;

        var result = UkgPunchResponseParser.Parse(
            html,
            UkgPunchAction.ClockOut);

        Assert.False(result.Success);
        Assert.Equal("ProviderRejected", result.TechnicalStatus);
        Assert.NotNull(result.ProviderMessage);
        Assert.Contains(
            "Unable",
            result.ProviderMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_Does_Not_Accept_Opposite_Action_Success_Text()
    {
        const string html = """
            <html>
              <body>
                Fri Aug-21-2026, 03:32p Punched Out Successfully
              </body>
            </html>
            """;

        var result = UkgPunchResponseParser.Parse(
            html,
            UkgPunchAction.ClockIn);

        Assert.False(result.Success);
    }

    [Fact]
    public void Parse_Empty_Response_Is_Unknown_Not_Success()
    {
        var result = UkgPunchResponseParser.Parse(
            string.Empty,
            UkgPunchAction.ClockIn);

        Assert.False(result.Success);
        Assert.Equal(
            "UnknownProviderResponse",
            result.TechnicalStatus);
        Assert.Null(result.ProviderMessage);
    }
}
