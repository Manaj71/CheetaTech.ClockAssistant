using CheetaTech.ClockAssistant.Providers.UKG;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class UkgPunchRequestBuilderTests
{
    [Fact]
    public void Prepare_ClockIn_Preserves_Dynamic_Fields_And_Applies_Overrides()
    {
        var pageInfo = CreatePageInfo();
        var credentials = new UkgCredentials(
            Username: "test-user",
            Password: "test-password");

        var request = UkgPunchRequestBuilder.Prepare(
            pageInfo,
            credentials,
            UkgPunchAction.ClockIn);

        Assert.Equal(pageInfo.PostUrl, request.PostUrl);
        Assert.Equal(UkgPunchAction.ClockIn, request.PunchAction);

        Assert.Equal("DYNAMIC-U", request.FormFields["u"]);
        Assert.Equal("123456789", request.FormFields["XTimeStamp"]);
        Assert.Equal("dynamic-server", request.FormFields["XServerId"]);

        Assert.Equal("test-user", request.FormFields["Username"]);
        Assert.Equal("test-password", request.FormFields["Password"]);

        Assert.Equal("DoPunch", request.FormFields["$LoginAction"]);
        Assert.Equal("PUNCH_IN", request.FormFields["$action"]);
        Assert.Equal(string.Empty, request.FormFields["$actionPrm"]);
    }

    [Fact]
    public void Prepare_ClockOut_Maps_To_PunchOut()
    {
        var request = UkgPunchRequestBuilder.Prepare(
            CreatePageInfo(),
            new UkgCredentials("test-user", "test-password"),
            UkgPunchAction.ClockOut);

        Assert.Equal("PUNCH_OUT", request.FormFields["$action"]);
    }

    [Fact]
    public void Prepare_Does_Not_Mutate_Original_Page_Inputs()
    {
        var pageInfo = CreatePageInfo();

        _ = UkgPunchRequestBuilder.Prepare(
            pageInfo,
            new UkgCredentials("test-user", "test-password"),
            UkgPunchAction.ClockIn);

        Assert.Equal(string.Empty, pageInfo.FormInputs["Username"]);
        Assert.Equal(string.Empty, pageInfo.FormInputs["Password"]);
        Assert.Equal(string.Empty, pageInfo.FormInputs["$LoginAction"]);
        Assert.Equal(string.Empty, pageInfo.FormInputs["$action"]);
    }

    [Fact]
    public void Prepare_Rejects_Empty_Username()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => UkgPunchRequestBuilder.Prepare(
                CreatePageInfo(),
                new UkgCredentials(string.Empty, "test-password"),
                UkgPunchAction.ClockIn));

        Assert.Contains("username", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prepare_Rejects_Empty_Password()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => UkgPunchRequestBuilder.Prepare(
                CreatePageInfo(),
                new UkgCredentials("test-user", string.Empty),
                UkgPunchAction.ClockIn));

        Assert.Contains("password", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static UkgClockPageInfo CreatePageInfo()
    {
        return new UkgClockPageInfo(
            FormName: "TheForm",
            FormMethod: "POST",
            PostUrl: new Uri("https://secure5.saashr.com/ta/NTEVentex.clock?rnd=TEST"),
            HasClockInAction: true,
            HasClockOutAction: true,
            FormInputs: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["s"] = "1",
                ["$LoginAction"] = string.Empty,
                ["$action"] = string.Empty,
                ["$actionPrm"] = string.Empty,
                ["u"] = "DYNAMIC-U",
                ["XTimeStamp"] = "123456789",
                ["XServerId"] = "dynamic-server",
                ["Username"] = string.Empty,
                ["Password"] = string.Empty,
                ["Badge"] = string.Empty,
                ["NoRedirect"] = "1"
            });
    }
}
