using System.Text.Json.Serialization;

namespace Cmux.Core.Models;

public class WorkspaceGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "Group";

    [JsonPropertyName("isCollapsed")]
    public bool IsCollapsed { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}
