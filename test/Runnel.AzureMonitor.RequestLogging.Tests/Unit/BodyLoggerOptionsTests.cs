using Shouldly;

namespace Runnel.AzureMonitor.RequestLogging.Tests.Unit;

/// <summary>
///     The defaults are the migration contract with the legacy
///     Azureblue.ApplicationInsights.RequestLogging package — they must not drift.
/// </summary>
public class BodyLoggerOptionsTests
{
    [Fact]
    public void Defaults_MatchLegacyPackage()
    {
        var options = new BodyLoggerOptions();

        options.HttpCodes.ShouldBe([.. StatusCodeRanges.Status4xx, .. StatusCodeRanges.Status5xx]);
        options.HttpVerbs.ShouldBe(["POST", "PUT", "PATCH"]);
        options.ExcludedContentTypes.ShouldBeEmpty();
        options.RequestBodyPropertyKey.ShouldBe("RequestBody");
        options.ResponseBodyPropertyKey.ShouldBe("ResponseBody");
        options.ClientIpPropertyKey.ShouldBe("ClientIp");
        options.MaxBytes.ShouldBe(1000);
        options.Appendix.ShouldBe("***TRUNCATED***");
        options.DisableIpMasking.ShouldBeFalse();
        options.EnableBodyLoggingOnExceptions.ShouldBeFalse();
        options.PropertyNamesWithSensitiveData.ShouldBe(
            ["password", "secret", "passwd", "api_key", "access_token", "accessToken", "auth", "credentials", "mysql_pwd"]);
        options.SensitiveDataRegexes.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(404, true)]
    [InlineData(500, true)]
    [InlineData(200, false)]
    [InlineData(302, false)]
    public void DefaultHttpCodes_CoverClientAndServerErrorsOnly(int statusCode, bool expected)
    {
        new BodyLoggerOptions().HttpCodes.Contains(statusCode).ShouldBe(expected);
    }

    [Fact]
    public void IsExcludedContentType_MatchesByPrefixIgnoringCase()
    {
        var options = new BodyLoggerOptions { ExcludedContentTypes = ["multipart/form-data"] };

        options.IsExcludedContentType("Multipart/Form-Data; boundary=xyz").ShouldBeTrue();
        options.IsExcludedContentType("application/json").ShouldBeFalse();
        options.IsExcludedContentType(null).ShouldBeFalse();
    }
}
