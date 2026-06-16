using System.Text.Json.Serialization;

namespace Cmux.Core.Models;

public class SidebarStatusEntry
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}
