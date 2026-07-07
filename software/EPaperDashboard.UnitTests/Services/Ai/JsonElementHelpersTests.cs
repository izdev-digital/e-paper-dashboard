using System.Text.Json;
using EPaperDashboard.Services.Ai;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Ai;

public class JsonElementHelpersTests
{
    [Fact]
    public void GetStringProp_PropertyIsString_ReturnsValue()
    {
        var el = JsonSerializer.SerializeToElement(new { name = "hello" });

        JsonElementHelpers.GetStringProp(el, "name").Should().Be("hello");
    }

    [Fact]
    public void GetStringProp_PropertyMissing_ReturnsNull()
    {
        var el = JsonSerializer.SerializeToElement(new { other = "x" });

        JsonElementHelpers.GetStringProp(el, "name").Should().BeNull();
    }

    [Fact]
    public void GetStringProp_PropertyIsNotString_ReturnsNull()
    {
        var el = JsonSerializer.SerializeToElement(new { name = 42 });

        JsonElementHelpers.GetStringProp(el, "name").Should().BeNull();
    }

    [Fact]
    public void GetIntProp_PropertyIsNumber_ReturnsValue()
    {
        var el = JsonSerializer.SerializeToElement(new { count = 7 });

        JsonElementHelpers.GetIntProp(el, "count").Should().Be(7);
    }

    [Fact]
    public void GetIntProp_PropertyMissing_ReturnsNull()
    {
        var el = JsonSerializer.SerializeToElement(new { other = 1 });

        JsonElementHelpers.GetIntProp(el, "count").Should().BeNull();
    }

    [Fact]
    public void PatchJsonObject_AddsNewKeyPreservingExistingProperties()
    {
        var el = JsonSerializer.SerializeToElement(new { a = "1", b = 2 });

        var result = JsonElementHelpers.PatchJsonObject(el, "title", "New Title");

        result["a"].Should().Be("1");
        result["b"].Should().Be(2.0);
        result["title"].Should().Be("New Title");
    }

    [Fact]
    public void PatchJsonObject_KeyAlreadyExists_OverwritesWithNewValue()
    {
        var el = JsonSerializer.SerializeToElement(new { title = "Old Title" });

        var result = JsonElementHelpers.PatchJsonObject(el, "title", "New Title");

        result.Should().ContainSingle();
        result["title"].Should().Be("New Title");
    }

    [Fact]
    public void PatchJsonObject_NonObjectInput_ReturnsOnlyThePatchedKey()
    {
        var el = JsonSerializer.SerializeToElement("not an object");

        var result = JsonElementHelpers.PatchJsonObject(el, "title", "value");

        result.Should().ContainSingle();
        result["title"].Should().Be("value");
    }
}
