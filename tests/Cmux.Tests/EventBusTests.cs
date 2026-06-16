using Cmux.Core.Services;
using FluentAssertions;
using Xunit;

namespace Cmux.Tests;

public class EventBusTests
{
    [Fact]
    public void Publish_NotifiesSubscribers()
    {
        var bus = new EventBus();
        var received = new List<AppEvent>();
        bus.Subscribe(e => received.Add(e));
        bus.Publish("workspace.created", new { name = "test" });
        received.Should().HaveCount(1);
        received[0].Type.Should().Be("workspace.created");
    }

    [Fact]
    public void Unsubscribe_StopsNotifications()
    {
        var bus = new EventBus();
        var received = new List<AppEvent>();
        var id = bus.Subscribe(e => received.Add(e));
        bus.Publish("test", null);
        bus.Unsubscribe(id);
        bus.Publish("test", null);
        received.Should().HaveCount(1);
    }

    [Fact]
    public void Publish_IncludesTimestamp()
    {
        var bus = new EventBus();
        AppEvent? captured = null;
        bus.Subscribe(e => captured = e);
        bus.Publish("test", null);
        captured.Should().NotBeNull();
        captured!.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Publish_MultipleSubscribers_AllNotified()
    {
        var bus = new EventBus();
        var received1 = new List<AppEvent>();
        var received2 = new List<AppEvent>();
        bus.Subscribe(e => received1.Add(e));
        bus.Subscribe(e => received2.Add(e));
        bus.Publish("test", new { value = 42 });
        received1.Should().HaveCount(1);
        received2.Should().HaveCount(1);
    }

    [Fact]
    public void Publish_FaultySubscriber_DoesNotBlockOthers()
    {
        var bus = new EventBus();
        var received = new List<AppEvent>();
        bus.Subscribe(_ => throw new InvalidOperationException("boom"));
        bus.Subscribe(e => received.Add(e));
        bus.Publish("test", null);
        received.Should().HaveCount(1);
    }

    [Fact]
    public void ToJson_ProducesValidJson()
    {
        var evt = new AppEvent { Type = "test.event", Data = new { key = "value" } };
        var json = evt.ToJson();
        json.Should().Contain("\"type\":\"test.event\"");
        json.Should().Contain("\"timestamp\":");
        json.Should().Contain("\"data\":");
    }
}
