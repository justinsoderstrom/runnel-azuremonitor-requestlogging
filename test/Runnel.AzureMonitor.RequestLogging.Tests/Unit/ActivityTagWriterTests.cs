using System.Diagnostics;
using Shouldly;

namespace Runnel.AzureMonitor.RequestLogging.Tests.Unit;

public class ActivityTagWriterTests
{
    [Fact]
    public void Write_SetsTagOnActivity()
    {
        using var activity = new Activity("test").Start();

        new ActivityTagWriter().Write(activity, "RequestBody", "{}");

        activity.GetTagItem("RequestBody").ShouldBe("{}");
    }

    [Fact]
    public void Write_UsesDupeSuffixedKey_WhenKeyAlreadyExists()
    {
        using var activity = new Activity("test").Start();
        var writer = new ActivityTagWriter();

        writer.Write(activity, "RequestBody", "first");
        writer.Write(activity, "RequestBody", "second");

        activity.GetTagItem("RequestBody").ShouldBe("first");
        activity.TagObjects.ShouldContain(tag =>
            tag.Key.StartsWith("RequestBody-dupe-") && Equals(tag.Value, "second"));
    }

    [Fact]
    public void Write_DoesNothing_WhenActivityIsNull()
    {
        Should.NotThrow(() => new ActivityTagWriter().Write(null, "RequestBody", "value"));
    }

    [Fact]
    public void Write_DoesNothing_WhenValueIsNull()
    {
        using var activity = new Activity("test").Start();

        new ActivityTagWriter().Write(activity, "RequestBody", null);

        activity.GetTagItem("RequestBody").ShouldBeNull();
    }
}
