using System.Text.Json.Serialization;

namespace Cmux.Core.Models;

public class ProjectConfig
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("cwd")]
    public string? Cwd { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; set; } = new();

    [JsonPropertyName("shell")]
    public string? Shell { get; set; }

    [JsonPropertyName("startDirectory")]
    public string? StartDirectory { get; set; }
}
