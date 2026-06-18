using System.Diagnostics;
using Cmux.Core.Config;

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
                long maxBytes = SettingsService.Current.DevLogMaxSizeMB * 1024L * 1024;
                var fi = new FileInfo(_logPath);
                if (maxBytes > 0 && fi.Exists && fi.Length > maxBytes)
                    TruncateLog(fi);

                using var fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var sw = new StreamWriter(fs);
                sw.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}] {message}\n");
            }
        }
        catch { }
    }

    private static void TruncateLog(FileInfo fi)
    {
        try
        {
            var bytes = File.ReadAllBytes(fi.FullName);
            int keepFrom = bytes.Length / 2;
            // Advance to the next newline so we don't start mid-line
            while (keepFrom < bytes.Length && bytes[keepFrom] != (byte)'\n')
                keepFrom++;
            if (keepFrom < bytes.Length)
                keepFrom++;
            File.WriteAllBytes(fi.FullName, bytes[keepFrom..]);
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
