namespace Cmux.Core.Services;

public record ShellInfo(string Name, string Path, string Icon);

public static class ShellDetector
{
    public static List<ShellInfo> DetectShells()
    {
        var shells = new List<ShellInfo>();

        try
        {
            var pwshRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell");
            if (Directory.Exists(pwshRoot))
            {
                var pwshPaths = Directory.GetFiles(pwshRoot, "pwsh.exe", SearchOption.AllDirectories);
                foreach (var path in pwshPaths.OrderByDescending(p => p))
                    shells.Add(new ShellInfo("PowerShell 7", path, GetIconForShell("PowerShell 7", path)));
            }
        } catch { /* ignore */ }

        try
        {
            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var powershell = System.IO.Path.Combine(system32, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(powershell))
                shells.Add(new ShellInfo("Windows PowerShell", powershell, GetIconForShell("Windows PowerShell", powershell)));

            var cmd = System.IO.Path.Combine(system32, "cmd.exe");
            if (File.Exists(cmd))
                shells.Add(new ShellInfo("Command Prompt", cmd, GetIconForShell("Command Prompt", cmd)));

            var wslPath = System.IO.Path.Combine(system32, "wsl.exe");
            if (File.Exists(wslPath))
                shells.Add(new ShellInfo("WSL", wslPath, GetIconForShell("WSL", wslPath)));
        } catch { /* ignore */ }

        try
        {
            var gitBashPaths = new[]
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe"),
            };
            foreach (var path in gitBashPaths)
            {
                if (File.Exists(path))
                {
                    shells.Add(new ShellInfo("Git Bash", path, GetIconForShell("Git Bash", path)));
                    break;
                }
            }
        } catch { /* ignore */ }

        return shells;
    }

    public static string GetIconForShell(string name, string path)
    {
        var lower = (name + "|" + path).ToLowerInvariant();

        if (lower.Contains("powershell") || lower.Contains("pwsh"))
            return "";
        if (lower.Contains("cmd"))
            return "";
        if (lower.Contains("wsl") || lower.Contains("ubuntu") || lower.Contains("debian")
            || lower.Contains("kali") || lower.Contains("suse") || lower.Contains("fedora"))
            return "";
        if (lower.Contains("git") && lower.Contains("bash"))
            return "";
        if (lower.Contains("azure"))
            return "";
        if (lower.Contains("nushell") || lower.Contains("nu.exe"))
            return "";

        return "";
    }
}
