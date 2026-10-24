using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;

namespace MiniInfos;

/// <summary>
/// A count in a TextBlock that rolls up to its new value, like an odometer, instead of jumping.
/// A count that goes down (a window that reset) is shown at once.
/// </summary>
public sealed class RollingCount(TextBlock text)
{
    private static readonly TimeSpan RollFor = TimeSpan.FromMilliseconds(1200);

    private Func<long, string> _format = value => value.ToString();
    private long _from;
    private long _to;
    private long _shown;
    private long _startedAt;
    private bool _hasValue;
    private bool _isRolling;

    /// <param name="format">How the count reads; applied at once, mid-roll included.</param>
    public void Show(long value, Func<long, string> format)
    {
        _format = format;

        if (!_hasValue || value < _to)
        {
            _hasValue = true;
            _from = _to = _shown = value;
            StopRolling();
        }
        else if (value != _to)
        {
            _from = _shown;
            _to = value;
            _startedAt = Stopwatch.GetTimestamp();
            StartRolling();
        }

        Render();
    }

    private void StartRolling()
    {
        if (_isRolling) return;
        _isRolling = true;
        CompositionTarget.Rendering += OnFrame;
    }

    private void StopRolling()
    {
        if (!_isRolling) return;
        _isRolling = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var progress = Math.Min(1, Stopwatch.GetElapsedTime(_startedAt) / RollFor);
        var eased = 1 - Math.Pow(1 - progress, 3);
        _shown = _from + (long)Math.Round((_to - _from) * eased);
        Render();

        if (progress >= 1) StopRolling();
    }

    private void Render() => text.Text = _format(_shown);
}
