using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// A pretend dmxbox for the tests of the server. It remembers what it was told, and a test can
/// unplug it, restart it and press its blackout button.
/// </summary>
/// <remarks>Thread-safe: in some tests the box link's own thread talks to it while the test looks.</remarks>
internal sealed class FakeBox : IBox
{
    private readonly Lock _lock = new();
    private readonly List<string> _calls = [];
    private byte[] _levels = new byte[512];
    private byte _masterLevel = 255;
    private bool _blackoutOn;
    private TimeSpan _uptime = TimeSpan.FromSeconds(10);
    private Exception? _failure;
    private bool _disposed;

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

    /// <summary>The 512 levels the box holds.</summary>
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

    /// <summary>A power cycle: the box forgets everything, and its uptime starts again.</summary>
    public void Restart()
    {
        lock (_lock)
        {
            _levels = new byte[512];
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
                Firmware: "1.0",
                ArtNetLive: false,
                Blackout: _blackoutOn,
                MergeMode: MergeMode.Htp,
                ArtNetHolding: false,
                Master: _masterLevel,
                Uptime: _uptime,
                Presets: [],
                Channels: 512);
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
