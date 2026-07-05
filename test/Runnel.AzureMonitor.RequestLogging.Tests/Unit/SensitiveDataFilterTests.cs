using Microsoft.Extensions.Options;
using Shouldly;

namespace Runnel.AzureMonitor.RequestLogging.Tests.Unit;

public class SensitiveDataFilterTests
{
    private static SensitiveDataFilter CreateDefaultFilter() =>
        new(Options.Create(new BodyLoggerOptions()));

    [Fact]
    public void MasksValue_WhenPropertyNameIsSensitive()
    {
        var result = CreateDefaultFilter().RemoveSensitiveData("""{"user":"bob","password":"hunter2"}""");

        result.ShouldBe("""{"user":"bob","password":"***MASKED***"}""");
    }

    [Fact]
    public void MasksValue_WhenPropertyNameContainsSensitiveKey_CaseInsensitive()
    {
        var result = CreateDefaultFilter().RemoveSensitiveData("""{"UserPassword":"hunter2"}""");

        result.ShouldBe("""{"UserPassword":"***MASKED***"}""");
    }

    [Fact]
    public void MasksValue_InNestedObjectsAndArrays()
    {
        var input = """{"items":[{"secret":"s1"},{"nested":{"api_key":"k1","safe":"ok"}}]}""";

        var result = CreateDefaultFilter().RemoveSensitiveData(input);

        result.ShouldBe("""{"items":[{"secret":"***MASKED***"},{"nested":{"api_key":"***MASKED***","safe":"ok"}}]}""");
    }

    [Fact]
    public void MasksValue_WhenValueMatchesCreditCardRegex()
    {
        var result = CreateDefaultFilter().RemoveSensitiveData("""{"card":"4111111111111111"}""");

        result.ShouldBe("""{"card":"***MASKED***"}""");
    }

    [Fact]
    public void MasksScalarArrayItems_WhenTheyMatchRegex()
    {
        var result = CreateDefaultFilter().RemoveSensitiveData("""{"cards":["4111111111111111","safe"]}""");

        result.ShouldBe("""{"cards":["***MASKED***","safe"]}""");
    }

    [Fact]
    public void MasksScalarItems_InNestedArraysAndRootArrays()
    {
        var result = CreateDefaultFilter().RemoveSensitiveData("""[["4111111111111111"],"safe"]""");

        result.ShouldBe("""[["***MASKED***"],"safe"]""");
    }

    [Fact]
    public void MasksEntireObject_WhenPropertyNameIsSensitive()
    {
        var result = CreateDefaultFilter().RemoveSensitiveData("""{"password":{"value":"hunter2"},"user":"bob"}""");

        result.ShouldBe("""{"password":"***MASKED***","user":"bob"}""");
    }

    [Fact]
    public void MasksEntireArray_WhenPropertyNameIsSensitive()
    {
        var result = CreateDefaultFilter().RemoveSensitiveData("""{"credentials":["hunter2","hunter3"]}""");

        result.ShouldBe("""{"credentials":"***MASKED***"}""");
    }

    [Fact]
    public void MasksEntireBody_WhenNonJsonTextMatchesRegex()
    {
        var result = CreateDefaultFilter().RemoveSensitiveData("card number is 4111111111111111 thanks");

        result.ShouldBe("***MASKED***");
    }

    [Fact]
    public void ReturnsNonJsonTextUnchanged_WhenNothingMatches()
    {
        const string input = "just some plain text";

        CreateDefaultFilter().RemoveSensitiveData(input).ShouldBe(input);
    }

    [Fact]
    public void ReturnsUnmatchedJsonUnchanged()
    {
        const string input = """{"name":"widget","count":3}""";

        CreateDefaultFilter().RemoveSensitiveData(input).ShouldBe(input);
    }

    [Fact]
    public void MasksScalarJsonValue_WhenItMatchesRegex()
    {
        CreateDefaultFilter().RemoveSensitiveData("4111111111111111").ShouldBe("***MASKED***");
    }

    [Fact]
    public void CustomKeysAndRegexes_AreApplied()
    {
        var filter = new SensitiveDataFilter(["ssn"], [@"\b\d{3}-\d{2}-\d{4}\b"]);

        filter.RemoveSensitiveData("""{"ssn":"anything","note":"123-45-6789"}""")
            .ShouldBe("""{"ssn":"***MASKED***","note":"***MASKED***"}""");
    }
}
