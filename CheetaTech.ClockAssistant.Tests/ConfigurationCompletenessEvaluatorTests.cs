using CheetaTech.ClockAssistant.Core.Configuration;

namespace CheetaTech.ClockAssistant.Tests;

public sealed class ConfigurationCompletenessEvaluatorTests
{
    private readonly ConfigurationCompletenessEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_Null_Configuration_Requires_Setup()
    {
        var result = _evaluator.Evaluate(null);

        Assert.False(result.IsComplete);
        Assert.Contains(
            "Configuration",
            result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_Complete_Basic_Configuration_Is_Complete()
    {
        var result = _evaluator.Evaluate(
            ValidConfiguration());

        Assert.True(result.IsComplete);
        Assert.Empty(result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_Missing_ProviderType_Is_Incomplete()
    {
        var configuration = ValidConfiguration() with
        {
            ProviderType = string.Empty
        };

        var result = _evaluator.Evaluate(configuration);

        Assert.False(result.IsComplete);
        Assert.Contains(
            nameof(configuration.ProviderType),
            result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_Invalid_ProviderUrl_Is_Incomplete()
    {
        var configuration = ValidConfiguration() with
        {
            ProviderUrl = "not-a-provider-url"
        };

        var result = _evaluator.Evaluate(configuration);

        Assert.False(result.IsComplete);
        Assert.Contains(
            nameof(configuration.ProviderUrl),
            result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_No_WorkDays_Is_Incomplete()
    {
        var configuration = ValidConfiguration() with
        {
            WorkDays = Array.Empty<DayOfWeek>()
        };

        var result = _evaluator.Evaluate(configuration);

        Assert.False(result.IsComplete);
        Assert.Contains(
            nameof(configuration.WorkDays),
            result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_Missing_ClockInTime_Is_Incomplete()
    {
        var configuration = ValidConfiguration() with
        {
            ClockInTime = null
        };

        var result = _evaluator.Evaluate(configuration);

        Assert.False(result.IsComplete);
        Assert.Contains(
            nameof(configuration.ClockInTime),
            result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_Missing_ClockOutTime_Is_Incomplete()
    {
        var configuration = ValidConfiguration() with
        {
            ClockOutTime = null
        };

        var result = _evaluator.Evaluate(configuration);

        Assert.False(result.IsComplete);
        Assert.Contains(
            nameof(configuration.ClockOutTime),
            result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_Midnight_Times_Are_Valid_When_Explicitly_Configured()
    {
        var configuration = ValidConfiguration() with
        {
            ClockInTime = TimeOnly.MinValue,
            ClockOutTime = TimeOnly.MinValue
        };

        var result = _evaluator.Evaluate(configuration);

        Assert.True(result.IsComplete);
        Assert.Empty(result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_Missing_TimeZone_Is_Incomplete()
    {
        var configuration = ValidConfiguration() with
        {
            TimeZoneId = string.Empty
        };

        var result = _evaluator.Evaluate(configuration);

        Assert.False(result.IsComplete);
        Assert.Contains(
            nameof(configuration.TimeZoneId),
            result.MissingOrInvalidFields);
    }

    [Fact]
    public void Evaluate_Negative_NotificationLeadTime_Is_Incomplete()
    {
        var configuration = ValidConfiguration() with
        {
            NotificationLeadTime = TimeSpan.FromMinutes(-1)
        };

        var result = _evaluator.Evaluate(configuration);

        Assert.False(result.IsComplete);
        Assert.Contains(
            nameof(configuration.NotificationLeadTime),
            result.MissingOrInvalidFields);
    }

    [Fact]
    public void Configuration_Defaults_To_Basic_Confirmation_Mode()
    {
        var configuration = new ClockAssistantConfiguration();

        Assert.Equal(
            ClockExecutionMode.BasicConfirmation,
            configuration.ExecutionMode);
    }

    [Fact]
    public void Advanced_Mode_Is_Represented_Separately_From_Basic()
    {
        var configuration = ValidConfiguration() with
        {
            ExecutionMode = ClockExecutionMode.AdvancedAutomatic
        };

        Assert.Equal(
            ClockExecutionMode.AdvancedAutomatic,
            configuration.ExecutionMode);
    }

    private static ClockAssistantConfiguration ValidConfiguration()
    {
        return new ClockAssistantConfiguration
        {
            ProviderType = "UKG",
            ProviderUrl = "https://provider.test/ta/Tenant.clock",
            WorkDays =
            [
                DayOfWeek.Monday,
                DayOfWeek.Tuesday,
                DayOfWeek.Wednesday,
                DayOfWeek.Thursday,
                DayOfWeek.Friday
            ],
            ClockInTime = new TimeOnly(7, 53),
            ClockOutTime = new TimeOnly(15, 0),
            TimeZoneId = "America/Toronto",
            NotificationLeadTime = TimeSpan.FromMinutes(15),
            ExecutionMode = ClockExecutionMode.BasicConfirmation
        };
    }
}

