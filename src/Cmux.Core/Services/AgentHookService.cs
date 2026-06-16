using System.Text.Json;
using Cmux.Core.Models;

namespace Cmux.Core.Services;

public static class AgentHookService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static AgentHookEvent? ParseEvent(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AgentHookEvent>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static HookAction ClassifyEvent(AgentHookEvent evt)
    {
        return evt.Event.ToLowerInvariant() switch
        {
            "stop" => HookAction.Notify,
            "notification" => HookAction.Notify,
            "session-start" or "session_start" or "sessionstart" => HookAction.SessionStart,
            "permission-request" or "permission_request" or "permissionrequest" => HookAction.Approval,
            "pre-tool-use" or "pre_tool_use" or "pretooluse" => ClassifyToolUse(evt),
            _ => HookAction.Telemetry,
        };
    }

    private static HookAction ClassifyToolUse(AgentHookEvent evt)
    {
        var tool = evt.Tool?.ToLowerInvariant() ?? "";
        var readOnlyTools = new[] { "read", "grep", "glob", "search", "list", "view", "cat" };
        if (readOnlyTools.Any(t => tool.Contains(t)))
            return HookAction.Telemetry;
        return HookAction.Approval;
    }
}
