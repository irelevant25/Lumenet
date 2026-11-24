using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// A pretend DMX USB interface of the kind of the Enttec DMX USB Pro, at a serial port of its own.
/// A test can pull it out and plug it in, let another program hold its port, or silence it. It
/// answers the questions for its parameters and its serial number as the real one does.
/// </summary>
/// <remarks>Thread-safe: the link's own thread may talk to it while the test looks.</remarks>
internal sealed class FakeUsbDevice
{
    private readonly Lock _lock = new();
    private readonly List<byte> _toRead = [];
    private readonly List<byte> _labels = [];
    private byte[]? _frame;
    private bool _plugged = true;
    private bool _held;
    private bool _answers = true;
    private int _opens;
    private int _open;

    /// <summary>Makes the interface, plugged in.</summary>
    /// <param name="name">Its port.</param>
    public FakeUsbDevice(string name = "COM7")
    {
        Name = name;
    }

    public string Name { get; }

    /// <summary>The levels of the last frame it got, slot 1 first, without the start code; null before the first.</summary>
    public byte[]? Frame
    {
        get
        {
            lock (_lock)
            {
                return _frame;
            }
        }
    }

    /// <summary>The label of every message it got, in order: 10 (serial number), 3 (parameters), 6 (a frame).</summary>
    public byte[] Labels
    {
        get
        {
            lock (_lock)
            {
                return [.. _labels];
            }
        }
    }

    /// <summary>How many times its port was opened.</summary>
    public int Opens
    {
        get
        {
            lock (_lock)
            {
                return _opens;
            }
        }
    }

    /// <summary>True while a port of it is open.</summary>
    public bool IsOpen
    {
        get
        {
            lock (_lock)
            {
                return _open > 0;
            }
        }
    }

    /// <summary>The serial ports of the PC: its own while it is plugged in.</summary>
    public string[] Names()
    {
        lock (_lock)
        {
            return _plugged ? [Name] : [];
        }
    }

    /// <summary>The port, as the link makes it for its name.</summary>
    public IDmxPort PortAt(string name)
    {
        Assert.Equal(Name, name);
        return new Port(this);
    }

    public void PullOut() => Set(() => _plugged = false);

    public void PlugIn() => Set(() => _plugged = true);

    /// <summary>Another program holds the port, or lets it go.</summary>
    public void Hold(bool held) => Set(() => _held = held);

    /// <summary>The interface answers questions, or keeps silent like a plain FTDI cable.</summary>
    public void Answer(bool answers) => Set(() => _answers = answers);

    public void Forget() => Set(() => _labels.Clear());

    private void Set(Action change)
    {
        lock (_lock)
        {
            change();
        }
    }

    private void Open()
    {
        lock (_lock)
        {
            if (!_plugged)
                throw new IOException($"The port '{Name}' does not exist.");
            if (_held)
                throw new UnauthorizedAccessException($"Access to the port '{Name}' is denied.");

            _opens++;
            _open++;
            _toRead.Clear();
        }
    }

    private void Close()
    {
        lock (_lock)
        {
            _open--;
        }
    }

    private void Write(byte[] data)
    {
        lock (_lock)
        {
            if (!_plugged)
                throw new IOException("A device attached to the system is not functioning.");

            var message = new List<byte>(data);
            while (EnttecPro.TryTake(message, out byte label, out byte[] payload))
            {
                _labels.Add(label);
                if (label == EnttecPro.SendDmxLabel)
                    _frame = payload[1..];
                else if (label == EnttecPro.ParametersLabel && _answers)
                    _toRead.AddRange(EnttecPro.Message(EnttecPro.ParametersLabel, [44, 1, 9, 1, 40]));
                else if (label == EnttecPro.SerialNumberLabel && _answers)
                    _toRead.AddRange(EnttecPro.Message(EnttecPro.SerialNumberLabel, [0x67, 0x45, 0x23, 0x01]));
            }
        }
    }

    private int Read(byte[] buffer)
    {
        lock (_lock)
        {
            if (!_plugged)
                throw new IOException("A device attached to the system is not functioning.");

            int count = Math.Min(buffer.Length, _toRead.Count);
            _toRead.CopyTo(0, buffer, 0, count);
            _toRead.RemoveRange(0, count);
            return count;
        }
    }

    // The port the link opens; a new one for every attempt, as SerialDmxPort is.
    private sealed class Port(FakeUsbDevice device) : IDmxPort
    {
        private bool _opened;

        public void Open()
        {
            device.Open();
            _opened = true;
        }

        public void Write(byte[] data) => device.Write(data);

        public int Read(byte[] buffer) => device.Read(buffer);

        public void Dispose()
        {
            if (_opened)
                device.Close();
            _opened = false;
        }
    }
}
