using EPaperDashboard.Services.Rendering;
using FluentAssertions;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class DesignerPreviewImageStoreTests
{
    [Fact]
    public void TryGet_RequiresMatchingUserAndDashboard()
    {
        var store = new DesignerPreviewImageStore(TimeProvider.System);
        var token = store.Add("user-1", "dashboard-1", [1, 2, 3]);

        store.TryGet(token, "user-2", "dashboard-1", out _).Should().BeFalse();
        store.TryGet(token, "user-1", "dashboard-2", out _).Should().BeFalse();
        store.TryGet(token, "user-1", "dashboard-1", out var image).Should().BeTrue();
        image.Should().Equal(1, 2, 3);
    }
}
