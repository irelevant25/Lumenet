namespace DmxBox.Tests.Boxes;

/// <summary>
/// A pretend dmxbox for the tests of the server. It remembers what it was told, and a test can
/// unplug it, restart it, press its blackout button, and play a console on its DMX IN.
/// </summary>
/// <remarks>Thread-safe: in some tests the box link's own thread talks to it while the test looks.</remarks>
internal sealed class FakeBox : IBox
{
    private readonly Lock _lock = new();
    private readonly List<string> _calls = [];
    private readonly int _channels;
    private byte[] _levels;
    private byte _masterLevel = 255;
    private bool _blackoutOn;
    private TimeSpan _uptime = TimeSpan.FromSeconds(10);
    private Exception? _failure;
    private bool _disposed;
    private readonly int? _dmxInBox;
    private byte[]? _console;
    private uint _frame;

    /// <summary>A box that answers, with nothing set yet.</summary>
    /// <param name="outputs">Its outputs, each a universe of 512 channels.</param>
    /// <param name="dmxInBox">For a box with DMX IN (firmware 3.2 and its number); null for one without.</param>
    public FakeBox(int outputs = 1, int? dmxInBox = null)
    {
        _channels = outputs * 512;
        _levels = new byte[_channels];
        _dmxInBox = dmxInBox;
    }

    /// <summary>
    /// Every request the box got, in order: "ping", "levels", "master 255", "blackout on",
    /// "blackout off". Requests it did not answer are listed too.
    /// </summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_lock)
            {
                return _calls.ToArray();
            }
        }
    }

    /// <summary>The levels the box holds: 512 for each output, output 1 first.</summary>
    public byte[] Levels
    {
        get
        {
            lock (_lock)
            {
                return (byte[])_levels.Clone();
            }
        }
    }

    /// <summary>The box's own master. A test may set it, as another program could.</summary>
    public byte MasterLevel
    {
        get
        {
            lock (_lock)
            {
                return _masterLevel;
            }
        }

        set
        {
            lock (_lock)
            {
                _masterLevel = value;
            }
        }
    }

    /// <summary>True while the box is in blackout.</summary>
    public bool BlackoutOn
    {
        get
        {
            lock (_lock)
            {
                return _blackoutOn;
            }
        }
    }

    /// <summary>True once the box link has closed the box.</summary>
    public bool Disposed
    {
        get
        {
            lock (_lock)
            {
                return _disposed;
            }
        }
    }

    /// <summary>Empties <see cref="Calls"/>, so that a test sees only what comes after.</summary>
    public void ForgetCalls()
    {
        lock (_lock)
        {
            _calls.Clear();
        }
    }

    /// <summary>From now on no request is answered: each one ends like a timeout of the real box.</summary>
    public void Unplug()
    {
        FailWith(new DmxBoxTimeoutException("no answer from the pretend box"));
    }

    /// <summary>From now on every request ends in this exception.</summary>
    public void FailWith(Exception failure)
    {
        lock (_lock)
        {
            _failure = failure;
        }
    }

    /// <summary>The box answers again, with the state it had.</summary>
    public void PlugIn()
    {
        lock (_lock)
        {
            _failure = null;
        }
    }

    /// <summary>Presses the blackout button on the box: blackout changes over.</summary>
    public void PressBlackoutButton()
    {
        lock (_lock)
        {
            _blackoutOn = !_blackoutOn;
        }
    }

    /// <summary>A console sends a frame to the box's DMX IN.</summary>
    /// <param name="levels">Its levels, channel 1 first.</param>
    public void Console(params byte[] levels)
    {
        lock (_lock)
        {
            _console = (byte[])levels.Clone();
            _frame++;
        }
    }

    /// <summary>A power cycle: the box forgets everything, and its uptime starts again.</summary>
    public void Restart()
    {
        lock (_lock)
        {
            _levels = new byte[_channels];
            _masterLevel = 255;
            _blackoutOn = false;
            _uptime = TimeSpan.FromMilliseconds(100);
        }
    }

    /// <inheritdoc />
    public PingInfo Ping()
    {
        lock (_lock)
        {
            Record("ping");
            return new PingInfo(
                Firmware: _dmxInBox is null ? "1.0" : "3.2",
                ArtNetLive: false,
                Blackout: _blackoutOn,
                MergeMode: MergeMode.Htp,
                ArtNetHolding: false,
                Master: _masterLevel,
                Uptime: _uptime,
                Presets: [],
                Channels: _channels,
                Abilities: _dmxInBox is null ? BoxAbilities.None : BoxAbilities.DmxIn,
                BoxNumber: _dmxInBox,
                Model: _dmxInBox is null ? null : 4);
        }
    }

    /// <inheritdoc />
    public void SetLevels(byte[] levels)
    {
        lock (_lock)
        {
            Record("levels");
            _levels = (byte[])levels.Clone();
        }
    }

    /// <inheritdoc />
    public void Master(byte level)
    {
        lock (_lock)
        {
            Record($"master {level}");
            _masterLevel = level;
        }
    }

    /// <inheritdoc />
    public void Blackout(bool on)
    {
        lock (_lock)
        {
            Record(on ? "blackout on" : "blackout off");
            _blackoutOn = on;
        }
    }

    /// <inheritdoc />
    public DmxInput GetInput(uint since)
    {
        lock (_lock)
        {
            Record("input");
            if (_dmxInBox is null)
                throw new DmxBoxException(9);   // NOT_SUPPORTED
            return new DmxInput(_frame > 0, _frame, _frame > 0 && _frame != since ? (byte[])_console!.Clone() : null);
        }
    }

    /// <summary>Notes that the box link has closed the box.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }
    }

    // Notes the request, and ends it there if the box is unplugged.
    private void Record(string call)
    {
        _calls.Add(call);
        if (_failure is not null)
            throw _failure;
    }
}
