using System.Diagnostics;

namespace Cmux.Core.Services;

public static class DevLogService
{
    private static readonly object _lock = new();
    private static readonly string _logPath;

    static DevLogService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cmux");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "dev.log");
    }

    public static bool IsEnabled { get; set; }

    public static string GetLogPath() => _logPath;

    public static void Log(string category, string message)
    {
        if (!IsEnabled) return;
        try
        {
            lock (_lock)
            {
                using var fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs);
                sw.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}\n");
            }
        }
        catch { }
    }

    public static Stopwatch StartTiming() => Stopwatch.StartNew();

    public static void LogTiming(string category, string message, Stopwatch sw)
    {
        if (!IsEnabled) return;
        Log(category, $"{message} ({sw.ElapsedMilliseconds}ms)");
    }

    public static void Clear()
    {
        try
        {
            lock (_lock)
            {
                File.WriteAllText(_logPath, string.Empty);
            }
        }
        catch { }
    }
}
