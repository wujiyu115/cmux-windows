using Cmux.Core.Models;
using Cmux.Core.Services;
using FluentAssertions;
using Xunit;

namespace Cmux.Tests;

public class AgentHookTests
{
    [Fact]
    public void ParseEvent_ValidJson_ReturnsEvent()
    {
        var json = """
        {
            "agent": "claude-code",
            "event": "stop",
            "sessionId": "abc123",
            "workspaceId": "ws1",
            "surfaceId": "sf1"
        }
        """;
        var evt = AgentHookService.ParseEvent(json);
        evt.Should().NotBeNull();
        evt!.Agent.Should().Be("claude-code");
        evt.Event.Should().Be("stop");
        evt.SessionId.Should().Be("abc123");
    }

    [Fact]
    public void ParseEvent_InvalidJson_ReturnsNull()
    {
        var evt = AgentHookService.ParseEvent("not json");
        evt.Should().BeNull();
    }

    [Fact]
    public void ParseEvent_MinimalPayload_ReturnsEvent()
    {
        var json = """{"agent": "codex", "event": "notification"}""";
        var evt = AgentHookService.ParseEvent(json);
        evt.Should().NotBeNull();
        evt!.Agent.Should().Be("codex");
    }

    [Fact]
    public void ClassifyEvent_StopEvent_IsNotification()
    {
        var evt = new AgentHookEvent { Agent = "claude-code", Event = "stop" };
        AgentHookService.ClassifyEvent(evt).Should().Be(HookAction.Notify);
    }

    [Fact]
    public void ClassifyEvent_PermissionRequest_IsApproval()
    {
        var evt = new AgentHookEvent { Agent = "claude-code", Event = "permission-request" };
        AgentHookService.ClassifyEvent(evt).Should().Be(HookAction.Approval);
    }

    [Fact]
    public void ClassifyEvent_SessionStart_IsSessionStart()
    {
        var evt = new AgentHookEvent { Agent = "claude-code", Event = "session-start" };
        AgentHookService.ClassifyEvent(evt).Should().Be(HookAction.SessionStart);
    }

    [Fact]
    public void ClassifyEvent_ReadToolUse_IsTelemetry()
    {
        var evt = new AgentHookEvent { Agent = "claude-code", Event = "pre-tool-use", Tool = "Read" };
        AgentHookService.ClassifyEvent(evt).Should().Be(HookAction.Telemetry);
    }

    [Fact]
    public void ClassifyEvent_WriteToolUse_IsApproval()
    {
        var evt = new AgentHookEvent { Agent = "claude-code", Event = "pre-tool-use", Tool = "Write" };
        AgentHookService.ClassifyEvent(evt).Should().Be(HookAction.Approval);
    }

    [Fact]
    public void ClassifyEvent_UnknownEvent_IsTelemetry()
    {
        var evt = new AgentHookEvent { Agent = "codex", Event = "unknown-thing" };
        AgentHookService.ClassifyEvent(evt).Should().Be(HookAction.Telemetry);
    }
}
