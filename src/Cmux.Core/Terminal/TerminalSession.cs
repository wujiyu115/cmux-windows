using System.IO.Pipes;
using System.Text;
using System.Diagnostics;
using Cmux.Core.IPC;
using Cmux.Core.Services;
using Microsoft.Win32.SafeHandles;

namespace Cmux.Core.Terminal;

/// <summary>
/// A complete terminal session: ConPTY process + VT parser + buffer + OSC handling.
/// This is the main class that WPF controls interact with.
/// </summary>
public sealed class TerminalSession : IDisposable
{
    private PseudoConsole? _console;
    private TerminalProcess? _process;
    private readonly VtParser _parser;
    private readonly OscHandler _oscHandler;
    private FileStream? _readStream;
    private FileStream? _writeStream;
    private Thread? _readThread;
    private volatile bool _disposed;
    private volatile bool _daemonWriteLogged;
    private volatile bool _localWriteNullLogged;
    private object _lock => Buffer.SyncRoot;
    private readonly object _writeLock = new();
    private Timer? _cwdPollTimer;

    public TerminalBuffer Buffer { get; }
    public string PaneId { get; }
    public string? Title { get; private set; }
    public string? WorkingDirectory { get; set; }
    public bool IsRunning => _process != null && !_process.HasExited;
    public int? ProcessId => _process?.ProcessId;

    // Daemon-mode delegates: when set, Write/Resize route through these instead of local ConPTY
    public Func<byte[], Task>? DaemonWrite { get; set; }
    public Func<int, int, Task>? DaemonResize { get; set; }

    // Events
    public event Action? OutputReceived;
    public event Action? ProcessExited;
    public event Action<string>? TitleChanged;
    public event Action<string>? WorkingDirectoryChanged;
    public event Action<string, string?, string>? NotificationReceived;
    public event Action<char, string?>? ShellPromptMarker;
    public event Action? Redraw;
    public event Action? BellReceived;
    public event Action<byte[]>? RawOutputReceived;

    public TerminalSession(string paneId, int cols = 120, int rows = 30)
    {
        PaneId = paneId;
        Buffer = new TerminalBuffer(cols, rows);
        _parser = new VtParser();
        _oscHandler = new OscHandler();
        WireParser();
    }

    private void WireParser()
    {
        _parser.OnPrint = c => Buffer.WriteChar(c);

        _parser.OnExecute = b =>
        {
            switch (b)
            {
                case 0x07: // BEL
                    DevLogService.Log("Terminal", $"BEL pane={PaneId}");
                    BellReceived?.Invoke();
                    break;
                case 0x08: // BS (Backspace)
                    Buffer.Backspace();
                    break;
                case 0x09: // HT (Tab)
                    Buffer.Tab();
                    break;
                case 0x0A: // LF (Line Feed)
                case 0x0B: // VT (Vertical Tab)
                case 0x0C: // FF (Form Feed)
                    Buffer.LineFeed();
                    break;
                case 0x0D: // CR (Carriage Return)
                    Buffer.CarriageReturn();
                    break;
            }
        };

        _parser.OnCsiDispatch = (parameters, final, qualifier) =>
        {
            HandleCsi(parameters, final, qualifier);
        };

        _parser.OnEscDispatch = b =>
        {
            switch ((char)b)
            {
                case '7': // DECSC — Save Cursor
                    Buffer.SaveCursor();
                    break;
                case '8': // DECRC — Restore Cursor
                    Buffer.RestoreCursor();
                    break;
                case 'M': // RI — Reverse Index
                    Buffer.ReverseLineFeed();
                    break;
                case 'D': // IND — Index (line feed)
                    Buffer.LineFeed();
                    break;
                case 'E': // NEL — Next Line
                    Buffer.NewLine();
                    break;
                case 'c': // RIS — Full Reset
                    Buffer.Clear();
                    Buffer.CurrentAttribute = TerminalAttribute.Default;
                    Buffer.ResetScrollRegion();
                    Buffer.MoveCursorTo(0, 0);
                    break;
            }
        };

        _parser.OnOscDispatch = osc =>
        {
            DevLogService.Log("Terminal", $"OSC pane={PaneId} data={osc[..Math.Min(osc.Length, 80)]}");
            _oscHandler.Handle(osc);
        };

        _oscHandler.TitleChanged += title =>
        {
            Title = title;
            TitleChanged?.Invoke(title);
        };

        _oscHandler.WorkingDirectoryChanged += dir =>
        {
            WorkingDirectory = dir;
            WorkingDirectoryChanged?.Invoke(dir);
        };

        _oscHandler.NotificationReceived += (title, subtitle, body) =>
        {
            NotificationReceived?.Invoke(title, subtitle, body);
        };

        _oscHandler.ShellPromptMarker += (marker, payload) =>
        {
            ShellPromptMarker?.Invoke(marker, payload);
        };
    }

    /// <summary>
    /// Starts the terminal process.
    /// </summary>
    public void Start(string? command = null, string? workingDirectory = null, Dictionary<string, string>? environmentVariables = null)
    {
        var totalSw = DevLogService.StartTiming();
        DevLogService.Log("Terminal", $"Start command=\"{command}\" cwd=\"{workingDirectory}\" pane={PaneId}");

        var fallbackDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(fallbackDirectory))
            fallbackDirectory = Environment.CurrentDirectory;

        var effectiveWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? fallbackDirectory
            : workingDirectory;

        // CreateProcess doesn't support UNC or Linux paths as working directory.
        // For WSL, pass the path via --cd; for others, fall back to user profile.
        var processCommand = command;
        var processCwd = effectiveWorkingDirectory;
        bool isWsl = !string.IsNullOrEmpty(processCommand) &&
                     processCommand.Contains("wsl", StringComparison.OrdinalIgnoreCase);

        if (processCwd.StartsWith('/'))
        {
            DevLogService.Log("Terminal", $"Linux path detected, falling back CWD. original=\"{processCwd}\"");
            processCwd = fallbackDirectory;

            if (isWsl)
                processCommand = $"{processCommand} --cd \"{effectiveWorkingDirectory}\"";
        }
        else if (processCwd.StartsWith(@"\\"))
        {
            DevLogService.Log("Terminal", $"UNC path detected, falling back CWD. original=\"{processCwd}\"");
            processCwd = fallbackDirectory;

            if (isWsl)
            {
                var wslPath = ConvertUncToWslPath(effectiveWorkingDirectory);
                if (wslPath != null)
                    processCommand = $"{processCommand} --cd \"{wslPath}\"";
            }
        }

        WorkingDirectory = effectiveWorkingDirectory;

        lock (_lock)
        {
            var sw = DevLogService.StartTiming();
            _console = PseudoConsole.Create((short)Buffer.Cols, (short)Buffer.Rows);
            DevLogService.LogTiming("Terminal", "PseudoConsole.Create", sw);

            sw = DevLogService.StartTiming();
            _process = new TerminalProcess(_console, processCommand, processCwd, environmentVariables);
            DevLogService.LogTiming("Terminal", $"TerminalProcess created pid={_process.ProcessId}", sw);

            _readStream = new FileStream(_console.ReadPipe, FileAccess.Read);
            _writeStream = new FileStream(_console.WritePipe, FileAccess.Write);

            _process.Exited += () =>
            {
                ProcessExited?.Invoke();
            };

            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = $"Terminal-Read-{PaneId}",
            };
            _readThread.Start();
        }

        if (!string.IsNullOrWhiteSpace(WorkingDirectory))
            WorkingDirectoryChanged?.Invoke(WorkingDirectory);

        _cwdPollTimer = new Timer(_ => PollWorkingDirectory(), null,
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

        DevLogService.LogTiming("Terminal", "Start total", totalSw);
    }

    private void PollWorkingDirectory()
    {
        try
        {
            if (_process == null || _process.HasExited)
            {
                _cwdPollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            var dir = ProcessCwdReader.GetCurrentDirectory(_process.ProcessHandle);
            if (string.IsNullOrEmpty(dir))
                return;

            var current = WorkingDirectory;
            var normalizedCurrent = current != null ? ProcessCwdReader.NormalizePath(current) : null;

            if (!string.Equals(dir, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
            {
                WorkingDirectory = dir;
                WorkingDirectoryChanged?.Invoke(dir);
            }
        }
        catch
        {
            // Process may have exited between the check and the read
        }
    }

    private void ReadLoop()
    {
        var buffer = new byte[4096];
        try
        {
            while (!_disposed && _readStream != null)
            {
                int bytesRead = _readStream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                var chunk = buffer.AsSpan(0, bytesRead).ToArray();

                if (DevLogService.IsEnabled)
                    DevLogService.Log("Terminal.Raw", $"pane={PaneId} bytes={EscapeForLog(chunk)}");

                lock (_lock)
                {
                    try
                    {
                        _parser.Feed(chunk);
                    }
                    catch (Exception ex)
                    {
                        // Never let malformed/edge VT sequences crash the app.
                        Debug.WriteLine($"[TerminalSession:{PaneId}] VT parse error: {ex}");
                    }
                }

                // Event invocations run user code (handlers). A throwing handler
                // must never escape this thread — in .NET an unhandled exception
                // on a Thread proc terminates the whole process, which would kill
                // the daemon and every surviving session. Catch and log instead.
                try
                {
                    RawOutputReceived?.Invoke(chunk);
                    OutputReceived?.Invoke();
                    Redraw?.Invoke();
                }
                catch (Exception ex)
                {
                    DaemonClient.LogDaemon($"[TerminalSession:{PaneId}] Event handler error (suppressed): {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        catch (IOException) when (_disposed)
        {
            // Expected on shutdown
        }
        catch (ObjectDisposedException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            // Last-resort: never let the read thread take down the process.
            DaemonClient.LogDaemon($"[TerminalSession:{PaneId}] ReadLoop fatal (suppressed): {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes a raw byte chunk into a readable form for dev logging:
    /// printable ASCII as-is, control bytes as C0/C1 mnemonics.
    /// </summary>
    private static string EscapeForLog(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            switch (b)
            {
                case 0x07: sb.Append("\\a"); break;       // BEL
                case 0x08: sb.Append("\\b"); break;       // BS
                case 0x09: sb.Append("\\t"); break;       // HT
                case 0x0A: sb.Append("\\n"); break;       // LF
                case 0x0D: sb.Append("\\r"); break;       // CR
                case 0x1B: sb.Append("\\e"); break;       // ESC
                case 0x7F: sb.Append("\\x7f"); break;     // DEL
                default:
                    if (b < 0x20) sb.Append($"\\x{b:X2}");
                    else if (b >= 0x7F) sb.Append($"\\x{b:X2}");
                    else sb.Append((char)b);
                    break;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes raw input (keyboard data) to the terminal process.
    /// </summary>
    public void Write(string text)
    {
        if (_disposed) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        Write(bytes);
    }

    /// <summary>
    /// Writes raw bytes to the terminal process.
    /// </summary>
    public void Write(byte[] data)
    {
        if (_disposed) return;

        // Daemon mode: forward to daemon instead of local ConPTY
        if (DaemonWrite != null)
        {
            if (!_daemonWriteLogged)
            {
                _daemonWriteLogged = true;
                DaemonClient.LogDaemon($"[TerminalSession:{PaneId}] First DaemonWrite ({data.Length} bytes)");
            }
            _ = DaemonWrite(data);
            return;
        }

        if (_writeStream == null)
        {
            if (!_localWriteNullLogged)
            {
                _localWriteNullLogged = true;
                DaemonClient.LogDaemon($"[TerminalSession:{PaneId}] Write called but _writeStream is null (no ConPTY started)");
            }
            return;
        }
        try
        {
            lock (_writeLock)
            {
                _writeStream.Write(data, 0, data.Length);
                _writeStream.Flush();
            }
        }
        catch (IOException) when (_disposed)
        {
            // Expected during shutdown
        }
    }

    /// <summary>
    /// Writes a response to the terminal on a thread pool thread to avoid
    /// deadlocking when the read thread needs to send a reply (e.g. DA, DSR)
    /// while the child process's stdin pipe buffer is full.
    /// </summary>
    private void WriteResponse(string text)
    {
        if (_disposed) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            if (_disposed || _writeStream == null) return;
            try
            {
                lock (_writeLock)
                {
                    _writeStream.Write(bytes, 0, bytes.Length);
                    _writeStream.Flush();
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        });
    }

    /// <summary>
    /// Resizes the terminal.
    /// </summary>
    public void Resize(int cols, int rows)
    {
        if (_disposed) return;

        cols = Math.Max(1, cols);
        rows = Math.Max(1, rows);

        lock (_lock)
        {
            Buffer.Resize(cols, rows);

            // Daemon mode: forward resize to daemon
            if (DaemonResize != null)
                _ = DaemonResize(cols, rows);
            else
                _console?.Resize((short)cols, (short)rows);
        }

        Redraw?.Invoke();
    }

    /// <summary>
    /// Feeds raw VT output bytes through the parser into the buffer.
    /// Used by daemon-backed sessions to receive output from the daemon.
    /// </summary>
    public void FeedOutput(byte[] data)
    {
        if (_disposed) return;

        lock (_lock)
        {
            try
            {
                _parser.Feed(data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TerminalSession:{PaneId}] VT parse error (feed): {ex}");
            }
        }

        OutputReceived?.Invoke();
        Redraw?.Invoke();
    }

    public TerminalBufferSnapshot CreateBufferSnapshot(int maxScrollbackLines = 3000)
    {
        lock (_lock)
        {
            return Buffer.CreateSnapshot(maxScrollbackLines);
        }
    }

    public void RestoreBufferSnapshot(TerminalBufferSnapshot snapshot)
    {
        if (_disposed) return;

        lock (_lock)
        {
            Buffer.RestoreSnapshot(snapshot);
        }

        Redraw?.Invoke();
    }

    private void HandleCsi(List<int> parameters, char final, string qualifier)
    {
        int Param(int index, int defaultValue = 0) =>
            index < parameters.Count && parameters[index] != 0 ? parameters[index] : defaultValue;

        bool isPrivate = qualifier.Contains('?');

        switch (final)
        {
            // Cursor movement
            case 'A': // CUU — Cursor Up
                Buffer.MoveCursorUp(Param(0, 1));
                break;
            case 'B': // CUD — Cursor Down
                Buffer.MoveCursorDown(Param(0, 1));
                break;
            case 'C': // CUF — Cursor Forward
                Buffer.MoveCursorForward(Param(0, 1));
                break;
            case 'D': // CUB — Cursor Backward
                Buffer.MoveCursorBackward(Param(0, 1));
                break;
            case 'E': // CNL — Cursor Next Line
                Buffer.CarriageReturn();
                Buffer.MoveCursorDown(Param(0, 1));
                break;
            case 'F': // CPL — Cursor Previous Line
                Buffer.CarriageReturn();
                Buffer.MoveCursorUp(Param(0, 1));
                break;
            case 'G': // CHA — Cursor Horizontal Absolute
                Buffer.MoveCursorTo(Buffer.CursorRow, Param(0, 1) - 1);
                break;
            case 'H': // CUP — Cursor Position
            case 'f': // HVP — Horizontal Vertical Position
                Buffer.MoveCursorTo(Param(0, 1) - 1, Param(1, 1) - 1);
                break;
            case 'd': // VPA — Vertical Position Absolute
                Buffer.MoveCursorTo(Param(0, 1) - 1, Buffer.CursorCol);
                break;

            // Erase
            case 'J': // ED — Erase in Display
                Buffer.EraseInDisplay(Param(0));
                break;
            case 'K': // EL — Erase in Line
                Buffer.EraseInLine(Param(0));
                break;
            case 'X': // ECH — Erase Characters
                Buffer.EraseChars(Param(0, 1));
                break;

            // Insert/Delete
            case 'L': // IL — Insert Lines
                Buffer.InsertLines(Param(0, 1));
                break;
            case 'M': // DL — Delete Lines
                Buffer.DeleteLines(Param(0, 1));
                break;
            case '@': // ICH — Insert Characters
                Buffer.InsertChars(Param(0, 1));
                break;
            case 'P': // DCH — Delete Characters
                Buffer.DeleteChars(Param(0, 1));
                break;

            // Scroll
            case 'S': // SU — Scroll Up
                Buffer.ScrollUp(Param(0, 1));
                break;
            case 'T': // SD — Scroll Down
                Buffer.ScrollDown(Param(0, 1));
                break;

            // Scroll region
            case 'r': // DECSTBM — Set Top and Bottom Margins
                if (parameters.Count == 0)
                {
                    Buffer.ResetScrollRegion();
                }
                else
                {
                    Buffer.SetScrollRegion(Param(0, 1) - 1, Param(1, Buffer.Rows) - 1);
                }
                Buffer.MoveCursorTo(0, 0);
                break;

            // SGR — Select Graphic Rendition
            case 'm':
                HandleSgr(parameters);
                break;

            // Mode set/reset
            case 'h': // SM / DECSET
                HandleMode(parameters, true, isPrivate);
                break;
            case 'l': // RM / DECRST
                HandleMode(parameters, false, isPrivate);
                break;

            // Cursor save/restore
            case 's': // SCOSC — Save Cursor Position
                if (!isPrivate)
                    Buffer.SaveCursor();
                break;
            case 'u': // SCORC — Restore Cursor Position
                if (!isPrivate)
                    Buffer.RestoreCursor();
                break;

            // Device status
            case 'n': // DSR — Device Status Report
                if (Param(0) == 6)
                {
                    // CPR — Cursor Position Report
                    WriteResponse($"\x1b[{Buffer.CursorRow + 1};{Buffer.CursorCol + 1}R");
                }
                break;

            case 'c': // DA — Device Attributes
                if (!isPrivate)
                    WriteResponse("\x1b[?1;0c");
                break;
        }
    }

    private void HandleSgr(List<int> parameters)
    {
        if (parameters.Count == 0)
        {
            Buffer.CurrentAttribute = TerminalAttribute.Default;
            return;
        }

        var attr = Buffer.CurrentAttribute;
        int i = 0;
        while (i < parameters.Count)
        {
            int code = parameters[i];
            switch (code)
            {
                case 0: attr = TerminalAttribute.Default; break;
                case 1: attr.Flags |= CellFlags.Bold; break;
                case 2: attr.Flags |= CellFlags.Dim; break;
                case 3: attr.Flags |= CellFlags.Italic; break;
                case 4: attr.Flags |= CellFlags.Underline; break;
                case 5: attr.Flags |= CellFlags.Blink; break;
                case 7: attr.Flags |= CellFlags.Inverse; break;
                case 8: attr.Flags |= CellFlags.Hidden; break;
                case 9: attr.Flags |= CellFlags.Strikethrough; break;
                case 21: attr.Flags &= ~CellFlags.Bold; break;
                case 22: attr.Flags &= ~(CellFlags.Bold | CellFlags.Dim); break;
                case 23: attr.Flags &= ~CellFlags.Italic; break;
                case 24: attr.Flags &= ~CellFlags.Underline; break;
                case 25: attr.Flags &= ~CellFlags.Blink; break;
                case 27: attr.Flags &= ~CellFlags.Inverse; break;
                case 28: attr.Flags &= ~CellFlags.Hidden; break;
                case 29: attr.Flags &= ~CellFlags.Strikethrough; break;

                // Foreground colors
                case >= 30 and <= 37:
                    attr.Foreground = TerminalColor.FromIndex(code - 30);
                    break;
                case 38: // Extended foreground
                    i = ParseExtendedColor(parameters, i, out var fg);
                    attr.Foreground = fg;
                    continue;
                case 39: attr.Foreground = TerminalColor.Default; break;

                // Background colors
                case >= 40 and <= 47:
                    attr.Background = TerminalColor.FromIndex(code - 40);
                    break;
                case 48: // Extended background
                    i = ParseExtendedColor(parameters, i, out var bg);
                    attr.Background = bg;
                    continue;
                case 49: attr.Background = TerminalColor.Default; break;

                // Bright foreground
                case >= 90 and <= 97:
                    attr.Foreground = TerminalColor.FromIndex(code - 90 + 8);
                    break;

                // Bright background
                case >= 100 and <= 107:
                    attr.Background = TerminalColor.FromIndex(code - 100 + 8);
                    break;
            }
            i++;
        }
        Buffer.CurrentAttribute = attr;
    }

    private static int ParseExtendedColor(List<int> parameters, int index, out TerminalColor color)
    {
        color = TerminalColor.Default;
        if (index + 1 >= parameters.Count)
            return index + 1;

        int type = parameters[index + 1];
        switch (type)
        {
            case 5: // 256-color: 38;5;N
                if (index + 2 < parameters.Count)
                {
                    color = TerminalColor.FromIndex(parameters[index + 2]);
                    return index + 3;
                }
                return index + 2;

            case 2: // Truecolor: 38;2;R;G;B
                if (index + 4 < parameters.Count)
                {
                    color = TerminalColor.FromRgb(
                        (byte)Math.Clamp(parameters[index + 2], 0, 255),
                        (byte)Math.Clamp(parameters[index + 3], 0, 255),
                        (byte)Math.Clamp(parameters[index + 4], 0, 255));
                    return index + 5;
                }
                return index + 2;

            default:
                return index + 1;
        }
    }

    private void HandleMode(List<int> parameters, bool set, bool isPrivate)
    {
        foreach (int param in parameters)
        {
            if (isPrivate)
            {
                switch (param)
                {
                    case 1: // DECCKM -- Cursor Keys Mode
                        Buffer.ApplicationCursorKeys = set;
                        break;
                    case 6: // DECOM — Origin Mode
                        Buffer.OriginMode = set;
                        break;
                    case 7: // DECAWM — Auto-wrap Mode
                        Buffer.AutoWrapMode = set;
                        break;
                    case 25: // DECTCEM — Text Cursor Enable Mode
                        Buffer.CursorVisible = set;
                        break;
                    case 1049: // Alternate screen buffer
                        if (set)
                        {
                            Buffer.SwitchToAlternateScreen();
                        }
                        else
                        {
                            Buffer.SwitchToMainScreen();
                        }
                        break;
                    case 47: // Alternate screen (older)
                    case 1047: // Alternate screen (xterm)
                        if (set)
                            Buffer.SwitchToAlternateScreen();
                        else
                            Buffer.SwitchToMainScreen();
                        break;
                    case 2004: // Bracketed paste mode
                        Buffer.BracketedPasteMode = set;
                        break;
                    case 1000: // Normal mouse tracking
                        Buffer.MouseTrackingNormal = set;
                        break;
                    case 1002: // Button-event mouse tracking
                        Buffer.MouseTrackingButton = set;
                        break;
                    case 1003: // Any-event mouse tracking
                        Buffer.MouseTrackingAny = set;
                        break;
                    case 1006: // SGR extended mouse reporting
                        Buffer.MouseSgrExtended = set;
                        break;
                }
            }
            else
            {
                switch (param)
                {
                    case 4: // IRM — Insert/Replace Mode
                        Buffer.InsertMode = set;
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Converts a WSL UNC path (\\wsl$\distro\path or \\wsl.localhost\distro\path
    /// or \\hostname\home\...) to a Linux path that wsl.exe --cd understands.
    /// Returns null if the path can't be converted.
    /// </summary>
    private static string? ConvertUncToWslPath(string uncPath)
    {
        var normalized = uncPath.Replace('\\', '/').TrimStart('/');

        // \\wsl$\Ubuntu\home\user → /home/user
        // \\wsl.localhost\Ubuntu\home\user → /home/user
        if (normalized.StartsWith("wsl$/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("wsl.localhost/", StringComparison.OrdinalIgnoreCase))
        {
            var afterPrefix = normalized.IndexOf('/') + 1; // skip "wsl$/" or "wsl.localhost/"
            var distroEnd = normalized.IndexOf('/', afterPrefix);
            if (distroEnd >= 0)
                return normalized[distroEnd..]; // "/home/user/..."
            return "~";
        }

        // Generic UNC like \\hostname\home\user\path — assume /home/user/path
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
            return "/" + string.Join("/", parts.Skip(1));

        return "~";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cwdPollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _cwdPollTimer?.Dispose();

        _readStream?.Dispose();
        _writeStream?.Dispose();
        _process?.Dispose();
        _console?.Dispose();
    }
}
