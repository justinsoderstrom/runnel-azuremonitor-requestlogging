using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Validates <see cref="BodyLoggerOptions"/> at application startup (via
///     <c>ValidateOnStart()</c>) so configuration mistakes — an invalid regex pattern, an empty
///     tag key — fail app start with a descriptive message instead of failing every request
///     once the middleware first resolves its collaborators.
/// </summary>
internal sealed class BodyLoggerOptionsValidator : IValidateOptions<BodyLoggerOptions>
{
    public ValidateOptionsResult Validate(string? name, BodyLoggerOptions options)
    {
        List<string>? failures = null;
        void Fail(string message) => (failures ??= []).Add(message);

        if (string.IsNullOrWhiteSpace(options.RequestBodyPropertyKey))
        {
            Fail($"{nameof(BodyLoggerOptions.RequestBodyPropertyKey)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.ResponseBodyPropertyKey))
        {
            Fail($"{nameof(BodyLoggerOptions.ResponseBodyPropertyKey)} must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientIpPropertyKey))
        {
            Fail($"{nameof(BodyLoggerOptions.ClientIpPropertyKey)} must not be empty.");
        }

        foreach (var pattern in options.SensitiveDataRegexes)
        {
            if (pattern is null)
            {
                Fail($"{nameof(BodyLoggerOptions.SensitiveDataRegexes)} must not contain null entries.");
                continue;
            }

            try
            {
                _ = new Regex(pattern);
            }
            catch (ArgumentException ex)
            {
                Fail($"{nameof(BodyLoggerOptions.SensitiveDataRegexes)} pattern '{pattern}' " +
                    $"is not a valid regular expression: {ex.Message}");
            }
        }

        return failures is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
