using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cmux.Core.Services;

public class AppEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("data")]
    public object? Data { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false });
}

public sealed class EventBus
{
    private readonly Dictionary<Guid, Action<AppEvent>> _subscribers = new();
    private readonly object _lock = new();

    public Guid Subscribe(Action<AppEvent> handler)
    {
        var id = Guid.NewGuid();
        lock (_lock) _subscribers[id] = handler;
        return id;
    }

    public void Unsubscribe(Guid id)
    {
        lock (_lock) _subscribers.Remove(id);
    }

    public void Publish(string type, object? data)
    {
        var evt = new AppEvent { Type = type, Data = data };
        Action<AppEvent>[] handlers;
        lock (_lock) handlers = _subscribers.Values.ToArray();
        foreach (var handler in handlers)
        {
            try { handler(evt); }
            catch { }
        }
    }
}
