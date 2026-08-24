using CheetaTech.ClockAssistant.Providers.UKG;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class UkgClockPageParserTests
{
    [Fact]
    public void Parse_Recognizes_Current_Ukg_Clock_Form_Actions_And_Inputs()
    {
        const string html = """
            <html>
              <body>
                <form autocomplete='off' METHOD='POST'
                      name='TheForm'
                      action='/ta/NTEVentex.clock?rnd=WWW'>
                  <input type='hidden' name='s' value='1'>
                  <input type='hidden' name='$LoginAction'>
                  <input type='hidden' name='$action'>
                  <input type='hidden' name='$actionPrm'>
                  <input type='hidden' name='u' value='DYNAMIC-U'>
                  <input type='hidden' name='XTimeStamp' value='123456789'>
                  <input type='hidden' name='XServerId' value='server.example'>
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

        var pageUri = new Uri("https://secure5.saashr.com/ta/NTEVentex.clock");

        var result = UkgClockPageParser.Parse(html, pageUri);

        Assert.Equal("TheForm", result.FormName);
        Assert.Equal("POST", result.FormMethod);
        Assert.Equal(
            "https://secure5.saashr.com/ta/NTEVentex.clock?rnd=WWW",
            result.PostUrl.ToString());

        Assert.True(result.HasClockInAction);
        Assert.True(result.HasClockOutAction);

        Assert.Equal("1", result.FormInputs["s"]);
        Assert.Equal("DYNAMIC-U", result.FormInputs["u"]);
        Assert.Equal("123456789", result.FormInputs["XTimeStamp"]);
        Assert.Equal("server.example", result.FormInputs["XServerId"]);
        Assert.Equal(string.Empty, result.FormInputs["Username"]);
        Assert.Equal(string.Empty, result.FormInputs["Password"]);
        Assert.Equal("1", result.FormInputs["NoRedirect"]);
    }

    [Fact]
    public void Parse_Throws_When_TheForm_Is_Missing()
    {
        var pageUri = new Uri("https://secure5.saashr.com/ta/NTEVentex.clock");

        var exception = Assert.Throws<InvalidOperationException>(
            () => UkgClockPageParser.Parse(
                "<html><body>No clock form</body></html>",
                pageUri));

        Assert.Contains("TheForm", exception.Message);
    }
}
