using System.Text.Json.Serialization;

namespace Cmux.Core.Models;

public class AgentHookEvent
{
    [JsonPropertyName("agent")]
    public string Agent { get; set; } = "";

    [JsonPropertyName("event")]
    public string Event { get; set; } = "";

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("workspaceId")]
    public string? WorkspaceId { get; set; }

    [JsonPropertyName("surfaceId")]
    public string? SurfaceId { get; set; }

    [JsonPropertyName("paneId")]
    public string? PaneId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("tool")]
    public string? Tool { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }
}

public enum HookAction
{
    Notify,
    Approval,
    SessionStart,
    Telemetry,
}
