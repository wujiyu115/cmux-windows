using System.Windows.Input;
using System.Windows.Threading;

namespace Cmux.Input;

public sealed class ChordKeyHandler
{
    private readonly Dictionary<(ModifierKeys, Key), Dictionary<(ModifierKeys, Key), Action>> _chords = new();
    private (ModifierKeys, Key)? _pendingFirst;
    private DispatcherTimer? _timer;
    private readonly int _timeoutMs;

    public bool IsWaitingForSecondKey => _pendingFirst.HasValue;
    public event Action<string>? ChordHintChanged;

    public ChordKeyHandler(int timeoutMs = 500)
    {
        _timeoutMs = timeoutMs;
    }

    public void Register(ModifierKeys mod1, Key key1, ModifierKeys mod2, Key key2, Action action)
    {
        var first = (mod1, key1);
        if (!_chords.TryGetValue(first, out var seconds))
        {
            seconds = new Dictionary<(ModifierKeys, Key), Action>();
            _chords[first] = seconds;
        }
        seconds[(mod2, key2)] = action;
    }

    public bool HandleKeyDown(ModifierKeys modifiers, Key key)
    {
        var combo = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt), key);

        if (_pendingFirst.HasValue)
        {
            CancelTimer();
            if (_chords.TryGetValue(_pendingFirst.Value, out var seconds) &&
                seconds.TryGetValue(combo, out var action))
            {
                _pendingFirst = null;
                ChordHintChanged?.Invoke("");
                action();
                return true;
            }
            _pendingFirst = null;
            ChordHintChanged?.Invoke("");
            return false;
        }

        if (_chords.ContainsKey(combo))
        {
            _pendingFirst = combo;
            ChordHintChanged?.Invoke(FormatCombo(combo) + ", ...");
            StartTimer();
            return true;
        }

        return false;
    }

    private void StartTimer()
    {
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_timeoutMs) };
        _timer.Tick += (_, _) =>
        {
            _pendingFirst = null;
            _timer.Stop();
            ChordHintChanged?.Invoke("");
        };
        _timer.Start();
    }

    private void CancelTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private static string FormatCombo((ModifierKeys mod, Key key) combo)
    {
        var parts = new List<string>();
        if (combo.mod.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (combo.mod.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (combo.mod.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        parts.Add(combo.key.ToString());
        return string.Join("+", parts);
    }
}
