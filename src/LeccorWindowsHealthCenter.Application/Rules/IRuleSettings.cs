using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace LeccorWindowsHealthCenter.Application.Rules;

/// <summary>Configuração de regras: habilitação e override de thresholds.</summary>
public interface IRuleSettings
{
    bool IsEnabled(string ruleId);

    double? GetParameter(string ruleId, string key);
}

/// <summary>
/// Lê a seção "Rules" do appsettings.json, ex.:
/// <code>"Rules": { "cpu.sustained-high": { "Enabled": true, "Threshold": 90 } }</code>
/// </summary>
public sealed class ConfigurationRuleSettings : IRuleSettings
{
    public const string SectionName = "Rules";

    private readonly IConfiguration _section;

    public ConfigurationRuleSettings(IConfiguration configuration)
    {
        _section = configuration.GetSection(SectionName);
    }

    public bool IsEnabled(string ruleId)
    {
        var raw = _section.GetSection(ruleId)["Enabled"];
        return raw is null || !bool.TryParse(raw, out var enabled) || enabled;
    }

    public double? GetParameter(string ruleId, string key)
    {
        var raw = _section.GetSection(ruleId)[key];
        return raw is not null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
