namespace Kontext.TerminalHost;

public sealed class MouseReportingState
{
    private const char Escape = '\u001b';
    private const int ModePress = 1000;
    private const int ModeDrag = 1002;
    private const int ModeAnyMotion = 1003;
    private const int ModeSgr = 1006;

    private const int MaxSequenceLength = 40;

    private readonly Lock _gate = new();

    private bool _press;
    private bool _drag;
    private bool _anyMotion;
    private bool _sgr;
    private string _unfinished = string.Empty;

    public MouseTracking Tracking
    {
        get
        {
            lock (_gate)
            {
                return _anyMotion ? MouseTracking.AnyMotion
                    : _drag ? MouseTracking.Drag
                    : _press ? MouseTracking.Press
                    : MouseTracking.Off;
            }
        }
    }

    public bool UsesSgrEncoding
    {
        get
        {
            lock (_gate)
            {
                return _sgr;
            }
        }
    }

    public void Observe(ReadOnlySpan<char> output)
    {
        if (output.IsEmpty)
        {
            return;
        }

        lock (_gate)
        {
            var text = _unfinished.Length == 0 ? output.ToString() : _unfinished + output.ToString();
            _unfinished = string.Empty;

            var position = 0;
            while (position < text.Length)
            {
                var start = text.IndexOf(Escape, position);
                if (start < 0)
                {
                    return;
                }

                var next = ReadSequence(text, start);
                if (next < 0)
                {
                    _unfinished = text[start..];
                    return;
                }

                position = next;
            }
        }
    }

    private int ReadSequence(string text, int start)
    {
        var index = start + 1;
        if (index >= text.Length)
        {
            return -1;
        }

        if (text[index] == 'c')
        {
            ResetAll();
            return index + 1;
        }

        if (text[index] != '[')
        {
            return index;
        }

        index++;
        if (index >= text.Length)
        {
            return -1;
        }

        if (text[index] != '?')
        {
            return index;
        }

        index++;
        var parametersStart = index;
        while (index < text.Length && (char.IsAsciiDigit(text[index]) || text[index] == ';'))
        {
            index++;
        }

        if (index >= text.Length)
        {
            return index - start > MaxSequenceLength ? index : -1;
        }

        if (text[index] is 'h' or 'l')
        {
            Apply(text.AsSpan(parametersStart, index - parametersStart), on: text[index] == 'h');
        }

        return index + 1;
    }

    private void Apply(ReadOnlySpan<char> parameters, bool on)
    {
        foreach (var range in parameters.Split(';'))
        {
            if (!int.TryParse(parameters[range], out var mode))
            {
                continue;
            }

            switch (mode)
            {
                case ModePress:
                    _press = on;
                    break;
                case ModeDrag:
                    _drag = on;
                    break;
                case ModeAnyMotion:
                    _anyMotion = on;
                    break;
                case ModeSgr:
                    _sgr = on;
                    break;
            }
        }
    }

    private void ResetAll()
    {
        _press = false;
        _drag = false;
        _anyMotion = false;
        _sgr = false;
    }
}
