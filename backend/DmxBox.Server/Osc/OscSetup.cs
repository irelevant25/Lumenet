using System.Text.Json;
using System.Text.Json.Serialization;

namespace DmxBox.Server.Osc;

/// <summary>Whether the desk receives OSC, and on which UDP port. It belongs to the PC, as MIDI does (osc.json).</summary>
/// <param name="On">True to receive.</param>
/// <param name="Port">The UDP port, 1 to 65535.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record OscSetup(bool On, int Port)
{
    /// <summary>The port at first: the one most programs that send OSC to a lighting desk use.</summary>
    public const int DefaultPort = 8000;

    /// <summary>Off, on the usual port: a PC that never set OSC.</summary>
    public static readonly OscSetup Off = new(false, DefaultPort);

    /// <summary>The setup, if it is possible.</summary>
    /// <exception cref="DeskException">The port is no UDP port.</exception>
    internal OscSetup Checked()
    {
        return Port is >= 1 and <= 65535 ? this : throw new DeskException("The port of OSC is a number from 1 to 65535.");
    }
}

/// <summary>OSC as the MIDI view shows it.</summary>
/// <param name="On">True while the desk is to receive OSC.</param>
/// <param name="Port">The UDP port it receives on.</param>
/// <param name="State">"off", "listening", or "failed" (the port cannot be had: <paramref name="Problem"/>).</param>
/// <param name="Problem">Why it cannot receive, for "failed".</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record OscView(bool On, int Port, string State, string? Problem)
{
    /// <summary>OSC is off.</summary>
    public const string Off = "off";

    /// <summary>The desk receives OSC on the port.</summary>
    public const string Listening = "listening";

    /// <summary>The port cannot be had: <see cref="Problem"/> says why.</summary>
    public const string Failed = "failed";

    /// <summary>Off, before anything was read.</summary>
    public static readonly OscView None = new(false, OscSetup.DefaultPort, Off, null);
}

/// <summary>
/// osc.json: the OSC setup of the PC, beside midi.json and the LAN password. A file that cannot be
/// read is put aside as osc.json.bad, and OSC stays off.
/// </summary>
internal sealed class OscSetupFile
{
    /// <summary>The name of the file, beside the LAN password.</summary>
    public const string FileName = "osc.json";

    private const string Format = "lumenet-osc";
    private const int Version = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger _log;

    /// <summary>The file at this path.</summary>
    /// <param name="path">Usually osc.json beside the LAN password.</param>
    /// <param name="log">Where a file that cannot be read is told about.</param>
    public OscSetupFile(string path, ILogger log)
    {
        _path = path;
        _log = log;
    }

    /// <summary>The path of the file.</summary>
    public string Path => _path;

    /// <summary>The setup in the file; off when there is no file or it cannot be read.</summary>
    public OscSetup Read()
    {
        if (!File.Exists(_path))
            return OscSetup.Off;

        try
        {
            FileShape? shape = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(_path), Options);
            if (shape is null || shape.Format != Format)
                throw new DeskException("It is no OSC setup of Lumenet.");

            return new OscSetup(shape.On, shape.Port ?? OscSetup.DefaultPort).Checked();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or DeskException)
        {
            _log.LogWarning("The OSC setup {File} cannot be read ({Reason}): OSC stays off, and the file is kept as {Bad}.", _path, e.Message, _path + ".bad");
            try
            {
                File.Move(_path, _path + ".bad", overwrite: true);
            }
            catch (Exception moving) when (moving is IOException or UnauthorizedAccessException)
            {
                // It stays where it is; the next start tells of it again.
            }

            return OscSetup.Off;
        }
    }

    /// <summary>Writes the setup, written beside the file first and then put in its place.</summary>
    /// <param name="setup">The setup, checked.</param>
    public void Write(OscSetup setup)
    {
        string? folder = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        string written = _path + ".new";
        File.WriteAllText(written, JsonSerializer.Serialize(new FileShape { Format = Format, Version = Version, On = setup.On, Port = setup.Port }, Options));
        File.Move(written, _path, overwrite: true);
    }

    // The file as JSON.
    private sealed class FileShape
    {
        [JsonPropertyOrder(-2)]
        public string? Format { get; set; }

        [JsonPropertyOrder(-1)]
        public int Version { get; set; }

        public bool On { get; set; }

        public int? Port { get; set; }
    }
}
