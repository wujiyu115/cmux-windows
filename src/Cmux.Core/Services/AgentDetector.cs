namespace Cmux.Core.Services;

public enum AgentType
{
    None, ClaudeCode, Codex, Aider, GithubCopilot, Cursor, Cline, Windsurf
}

public static class AgentDetector
{
    private static readonly (string pattern, AgentType type)[] Patterns =
    [
        ("claude", AgentType.ClaudeCode),
        ("codex", AgentType.Codex),
        ("aider", AgentType.Aider),
        ("copilot", AgentType.GithubCopilot),
        ("cursor", AgentType.Cursor),
        ("cline", AgentType.Cline),
        ("windsurf", AgentType.Windsurf),
    ];

    public static AgentType DetectFromProcessId(int shellPid)
    {
        try
        {
            var names = GetChildProcessNames(shellPid);
            foreach (var name in names)
                foreach (var (pattern, type) in Patterns)
                    if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        return type;
        }
        catch { }
        return AgentType.None;
    }

    public static string GetLabel(AgentType t) => t switch
    {
        AgentType.ClaudeCode => "Claude Code",
        AgentType.Codex => "Codex",
        AgentType.Aider => "Aider",
        AgentType.GithubCopilot => "Copilot",
        AgentType.Cursor => "Cursor",
        AgentType.Cline => "Cline",
        AgentType.Windsurf => "Windsurf",
        _ => "",
    };

    public static string GetIcon(AgentType t) => t switch
    {
        AgentType.ClaudeCode => "\uE99A",
        AgentType.Codex => "\uE943",
        AgentType.Aider => "\uE8D4",
        AgentType.GithubCopilot => "\uE774",
        AgentType.Cursor => "\uE7C8",
        AgentType.Cline => "\uE8D4",
        AgentType.Windsurf => "\uE774",
        _ => "",
    };

    public static string? GetSessionId(AgentType agent, int pid)
    {
        try
        {
            return agent switch
            {
                AgentType.ClaudeCode => ExtractSessionIdFromCommandLine(pid, "--resume"),
                AgentType.Codex => ExtractSessionIdFromCommandLine(pid, "resume"),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractSessionIdFromCommandLine(int pid, string marker)
    {
        var cmdLine = GetCommandLine(pid);
        if (cmdLine == null) return null;

        var idx = cmdLine.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var after = cmdLine[(idx + marker.Length)..].TrimStart();
        var spaceIdx = after.IndexOf(' ');
        return spaceIdx > 0 ? after[..spaceIdx].Trim() : after.Trim();
    }

    private static string? GetCommandLine(int pid)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (var obj in searcher.Get())
                return obj["CommandLine"]?.ToString();
        }
        catch { }
        return null;
    }

    private static List<string> GetChildProcessNames(int parentPid)
    {
        var names = new List<string>();
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT Name FROM Win32_Process WHERE ParentProcessId = {parentPid}");
            foreach (var obj in searcher.Get())
                names.Add(obj["Name"]?.ToString() ?? "");
        }
        catch { }
        return names;
    }
}
