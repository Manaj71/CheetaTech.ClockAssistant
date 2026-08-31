using CheetaTech.ClockAssistant.Providers.UKG;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class UkgLoginRequestBuilderTests
{
    private static readonly Uri ClockUrl =
        new("https://provider.test/ta/Tenant.clock");

    [Fact]
    public void Prepare_Uses_Login_Action_And_Never_Uses_Punch_Action()
    {
        var pageInfo = ClockPageInfo();
        var credentials = new UkgCredentials(
            "test-user",
            "test-password");

        var request = UkgLoginRequestBuilder.Prepare(
            pageInfo,
            credentials);

        Assert.Equal(pageInfo.PostUrl, request.PostUrl);
        Assert.Equal(
            "test-user",
            request.FormFields["Username"]);
        Assert.Equal(
            "test-password",
            request.FormFields["Password"]);
        Assert.Equal(
            "Login",
            request.FormFields["$LoginAction"]);
        Assert.Equal(
            string.Empty,
            request.FormFields["$action"]);
        Assert.Equal(
            string.Empty,
            request.FormFields["$actionPrm"]);

        Assert.DoesNotContain(
            request.FormFields,
            pair => pair.Value.Contains(
                "DoPunch",
                StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            request.FormFields,
            pair => pair.Value.Contains(
                "PUNCH_IN",
                StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(
            request.FormFields,
            pair => pair.Value.Contains(
                "PUNCH_OUT",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            "DYNAMIC-LOGIN",
            request.FormFields["u"]);
    }

    [Fact]
    public void Prepare_Rejects_Empty_Username()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            UkgLoginRequestBuilder.Prepare(
                ClockPageInfo(),
                new UkgCredentials(
                    string.Empty,
                    "test-password")));

        Assert.Contains(
            "username",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_Rejects_Empty_Password()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            UkgLoginRequestBuilder.Prepare(
                ClockPageInfo(),
                new UkgCredentials(
                    "test-user",
                    string.Empty)));

        Assert.Contains(
            "password",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static UkgClockPageInfo ClockPageInfo()
    {
        const string html = """
            <html>
              <body>
                <form METHOD='POST'
                      name='TheForm'
                      action='/ta/Tenant.clock?rnd=LOGIN'>
                  <input type='hidden' name='s' value='1'>
                  <input type='hidden' name='$LoginAction' value=''>
                  <input type='hidden' name='$action' value=''>
                  <input type='hidden' name='$actionPrm' value=''>
                  <input type='hidden' name='u' value='DYNAMIC-LOGIN'>
                  <input type='hidden' name='XTimeStamp' value='123456789'>
                  <input type='hidden' name='XServerId' value='server.test'>
                  <input type='text' name='Username' value=''>
                  <input type='password' name='Password' value=''>
                  <input type='hidden' name='NoRedirect' value='1'>
                  <button onClick='doPunchAction("PUNCH_IN");'>Clock In</button>
                  <button onClick='doPunchAction("PUNCH_OUT");'>Clock Out</button>
                  <button type='submit'
                          name='Login'
                          onclick='doLogin();'>
                    Sign In
                  </button>
                </form>
              </body>
            </html>
            """;

        return UkgClockPageParser.Parse(
            html,
            ClockUrl);
    }
}
