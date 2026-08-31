namespace CheetaTech.ClockAssistant.Core.Configuration;

public interface IConfigurationCompletenessEvaluator
{
    ConfigurationCompletenessResult Evaluate(
        ClockAssistantConfiguration? configuration);
}
