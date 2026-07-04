using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Default <see cref="ISensitiveDataFilter"/>. JSON bodies are walked recursively: a value is
///     replaced with <c>***MASKED***</c> when its property name contains one of
///     <see cref="BodyLoggerOptions.PropertyNamesWithSensitiveData"/> (case-insensitive) or the
///     value matches one of <see cref="BodyLoggerOptions.SensitiveDataRegexes"/>. Non-JSON bodies
///     are masked wholesale when a regex matches anywhere in the text.
/// </summary>
public class SensitiveDataFilter : ISensitiveDataFilter
{
    /// <summary>The replacement text used for masked values.</summary>
    public const string SensitiveValueMask = "***MASKED***";

    private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(1);

    private readonly HashSet<string> _sensitiveDataPropertyKeys;
    private readonly IReadOnlyList<Regex> _regexesForSensitiveValues;

    /// <summary>
    ///     Creates a filter from <see cref="BodyLoggerOptions.PropertyNamesWithSensitiveData"/>
    ///     and <see cref="BodyLoggerOptions.SensitiveDataRegexes"/>.
    /// </summary>
    public SensitiveDataFilter(IOptions<BodyLoggerOptions> options)
        : this(options.Value.PropertyNamesWithSensitiveData, options.Value.SensitiveDataRegexes)
    {
    }

    /// <summary>
    ///     Creates a filter from explicit property-name keys and value regex patterns.
    /// </summary>
    public SensitiveDataFilter(IEnumerable<string> sensitiveDataPropertyKeys, IEnumerable<string> regexesForSensitiveValues)
    {
        _sensitiveDataPropertyKeys = sensitiveDataPropertyKeys.Select(k => k.ToLowerInvariant()).ToHashSet();
        _regexesForSensitiveValues = regexesForSensitiveValues
            .Select(pattern => new Regex(pattern, RegexOptions.Compiled, RegexMatchTimeout))
            .ToList();
    }

    /// <inheritdoc />
    public string RemoveSensitiveData(string textOrJson)
    {
        try
        {
            var json = JsonNode.Parse(textOrJson);
            if (json is null) return string.Empty;

            if (json is JsonValue jsonValue && ContainsSensitiveData("", jsonValue.ToString()))
            {
                return SensitiveValueMask;
            }

            MaskNode(json);
            return json.ToJsonString();
        }
        catch (JsonException)
        {
            return ContainsSensitiveData("", textOrJson) ? SensitiveValueMask : textOrJson;
        }
    }

    private void MaskNode(JsonNode node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                MaskObject(jsonObject);
                break;
            case JsonArray jsonArray:
                MaskArray(jsonArray);
                break;
        }
    }

    private void MaskObject(JsonObject jsonObject)
    {
        foreach (var property in jsonObject.ToList())
        {
            switch (property.Value)
            {
                case JsonArray array:
                    MaskArray(array);
                    break;
                case JsonObject nested:
                    MaskObject(nested);
                    break;
                case JsonValue value when ContainsSensitiveData(property.Key, value.ToString()):
                    jsonObject[property.Key] = SensitiveValueMask;
                    break;
            }
        }
    }

    private void MaskArray(JsonArray jsonArray)
    {
        foreach (var item in jsonArray)
        {
            if (item is not null) MaskNode(item);
        }
    }

    private bool ContainsSensitiveData(string propertyName, string propertyValue)
    {
        var nameToCompare = propertyName.ToLowerInvariant();
        if (_sensitiveDataPropertyKeys.Contains(nameToCompare)
            || _sensitiveDataPropertyKeys.Any(key => nameToCompare.Contains(key)))
        {
            return true;
        }

        foreach (var regex in _regexesForSensitiveValues)
        {
            try
            {
                if (regex.IsMatch(propertyValue)) return true;
            }
            catch (RegexMatchTimeoutException)
            {
                // Hostile or degenerate input; mask rather than risk leaking it unfiltered.
                return true;
            }
        }

        return false;
    }
}
