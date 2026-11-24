using System.IO.Ports;

namespace DmxBox.Server;

/// <summary>
/// The serial port of a DMX USB interface, as far as <see cref="UsbLink"/> needs it. Behind an
/// interface so that the tests can put a pretend interface in its place. Every method throws when
/// the interface is gone.
/// </summary>
internal interface IDmxPort : IDisposable
{
    /// <summary>Opens the port.</summary>
    /// <exception cref="UnauthorizedAccessException">Another program has the port open.</exception>
    /// <exception cref="IOException">The port is not there, or does not open.</exception>
    void Open();

    /// <summary>Writes bytes to the interface.</summary>
    /// <param name="data">The bytes, a whole message.</param>
    void Write(byte[] data);

    /// <summary>Reads what the interface has sent, without waiting for more.</summary>
    /// <param name="buffer">Takes the bytes.</param>
    /// <returns>How many came; 0 when there were none.</returns>
    int Read(byte[] buffer);
}

/// <summary>A real serial port, through System.IO.Ports, which works the same on Windows, Linux and macOS.</summary>
/// <remarks>Not thread-safe: the link uses it from its one thread.</remarks>
internal sealed class SerialDmxPort : IDmxPort
{
    // The interface has a USB chip that is no serial line (an FT245), or a processor that takes
    // USB directly: the baud rate is a dummy value to it. Every program that drives one opens it
    // with some rate or other.
    private const int BaudRate = 115200;

    private readonly SerialPort _port;

    /// <summary>Prepares the port. Nothing happens until it is opened.</summary>
    /// <param name="name">"COM3", "/dev/ttyUSB0".</param>
    public SerialDmxPort(string name)
    {
        _port = new SerialPort(name, BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            WriteTimeout = 500,
            ReadTimeout = 1,
        };
    }

    /// <summary>The serial ports of this PC, as the system names them: "COM3", "/dev/ttyUSB0".</summary>
    /// <remarks>
    /// On macOS every device is there twice; the "cu." one is for a program that writes to it, the
    /// "tty." one waits for a modem, and is left out.
    /// </remarks>
    public static string[] Names()
    {
        string[] names;
        try
        {
            names = SerialPort.GetPortNames();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return [];
        }

        if (OperatingSystem.IsMacOS())
            names = [.. names.Where(name => !name.StartsWith("/dev/tty.", StringComparison.Ordinal))];

        return [.. names.Distinct(StringComparer.Ordinal).Order(Comparer<string>.Create(CompareNames))];
    }

    /// <inheritdoc />
    public void Open()
    {
        _port.Open();
        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();
    }

    /// <inheritdoc />
    public void Write(byte[] data)
    {
        _port.Write(data, 0, data.Length);
    }

    /// <inheritdoc />
    public int Read(byte[] buffer)
    {
        int waiting = _port.BytesToRead;
        return waiting == 0 ? 0 : _port.Read(buffer, 0, Math.Min(waiting, buffer.Length));
    }

    /// <summary>Closes the port.</summary>
    public void Dispose()
    {
        _port.Dispose();
    }

    // COM3 before COM10: the number at the end counts as a number (the shorter one is smaller).
    private static int CompareNames(string a, string b)
    {
        string numberA = new([.. a.Reverse().TakeWhile(char.IsAsciiDigit).Reverse()]);
        string numberB = new([.. b.Reverse().TakeWhile(char.IsAsciiDigit).Reverse()]);
        int stem = string.CompareOrdinal(a[..^numberA.Length], b[..^numberB.Length]);
        if (stem != 0)
            return stem;

        int length = numberA.Length.CompareTo(numberB.Length);
        return length != 0 ? length : string.CompareOrdinal(numberA, numberB);
    }
}
