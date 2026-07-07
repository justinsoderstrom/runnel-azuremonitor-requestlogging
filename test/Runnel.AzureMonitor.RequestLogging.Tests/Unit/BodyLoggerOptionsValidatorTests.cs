using Microsoft.Extensions.Options;
using Shouldly;

namespace Runnel.AzureMonitor.RequestLogging.Tests.Unit;

public class BodyLoggerOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(BodyLoggerOptions options) =>
        new BodyLoggerOptionsValidator().Validate(name: null, options);

    [Fact]
    public void Defaults_AreValid()
    {
        Validate(new BodyLoggerOptions()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void InvalidRegexPattern_Fails_WithThePatternInTheMessage()
    {
        var result = Validate(new BodyLoggerOptions { SensitiveDataRegexes = ["(unclosed"] });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("(unclosed");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyPropertyKeys_Fail(string key)
    {
        var result = Validate(new BodyLoggerOptions
        {
            RequestBodyPropertyKey = key,
            ResponseBodyPropertyKey = key,
            ClientIpPropertyKey = key
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotBeNull();
        result.FailureMessage.ShouldContain(nameof(BodyLoggerOptions.RequestBodyPropertyKey));
        result.FailureMessage.ShouldContain(nameof(BodyLoggerOptions.ResponseBodyPropertyKey));
        result.FailureMessage.ShouldContain(nameof(BodyLoggerOptions.ClientIpPropertyKey));
    }

    [Fact]
    public void NullRegexEntry_Fails()
    {
        var result = Validate(new BodyLoggerOptions { SensitiveDataRegexes = [null!] });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BodyLoggerOptions.SensitiveDataRegexes));
    }

    [Fact]
    public void MultipleProblems_AreAllReported()
    {
        var result = Validate(new BodyLoggerOptions
        {
            RequestBodyPropertyKey = "",
            SensitiveDataRegexes = ["(unclosed", @"\d+"]
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldNotBeNull();
        result.FailureMessage.ShouldContain(nameof(BodyLoggerOptions.RequestBodyPropertyKey));
        result.FailureMessage.ShouldContain("(unclosed");
    }
}
