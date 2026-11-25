using System.Globalization;

namespace DmxBox.Sim;

/// <summary>What a control of a simulated MIDI controller is.</summary>
internal enum SimControlKind
{
    /// <summary>A pad with a light of many colors (the APC mini).</summary>
    Pad,

    /// <summary>A key, with or without a light of one color.</summary>
    Key,

    /// <summary>A fader, 0-127.</summary>
    Fader,

    /// <summary>A knob, 0-127.</summary>
    Knob,
}

/// <summary>How a controller reads the value of a message that lights a control.</summary>
internal enum LightReading
{
    /// <summary>The APC mini mk2's pads: the velocity is a color of its palette, the channel the brightness (1 dim to 7 full, 8-11 pulsing, 12-16 blinking).</summary>
    ApcPad,

    /// <summary>0 off, 1 on, 2 blinking: the APC's keys, the X-Touch Mini's.</summary>
    OffOnBlink,

    /// <summary>0 off, anything above it on: the nanoKONTROL2's (127).</summary>
    OffAboveZero,

    /// <summary>0 off, 1 blinking, 2 on: the X-Touch Compact's, as other programs light it.</summary>
    OffBlinkOn,
}

/// <summary>
/// How the desk lights a control: with a note or a controller on a channel (0: any, the channel
/// carries the brightness), of a number that may be another than the control's own.
/// </summary>
internal sealed record SimLight(LightReading Reading, bool Note, int Channel, int Number, string Color = "#ff3b30");

/// <summary>
/// A control of a simulated controller: what it sends (a note or a controller, on a channel, of
/// a number), how the desk lights it, and whether the desk sets it with its own controller (a
/// motorized fader, a knob with a ring of lights).
/// </summary>
internal sealed record SimControl(string Id, SimControlKind Kind, string Label, int Channel, bool Note, int Number)
{
    public SimLight? Light { get; init; }

    public bool Driven { get; init; }
}

/// <summary>A part of a controller, as its page shows it: the parts with the same row stand side by side.</summary>
internal sealed record SimSection(string Title, int Row, int Columns, SimControl[] Controls);

/// <summary>A MIDI controller sim.cmd can simulate, with the name it has on a PC.</summary>
/// <param name="Device">Its name for sim.cmd: "apc-mini-mk2".</param>
/// <param name="Name">The name the controller has on a PC: "APC mini mk2".</param>
/// <param name="Title">Its name for people: "Akai APC mini mk2".</param>
/// <param name="Sections">Its parts.</param>
internal sealed record ControllerModel(string Device, string Name, string Title, SimSection[] Sections)
{
    /// <summary>Every control, part after part.</summary>
    public IEnumerable<SimControl> Controls => Sections.SelectMany(section => section.Controls);
}

/// <summary>
/// The controllers of sim.cmd: the Akai APC mini mk2, the Korg nanoKONTROL2 and the Behringer
/// X-Touch Mini and X-Touch Compact (Standard mode, layer A), with the messages their manuals give;
/// the lights of the two Behringers as other programs send them.
/// </summary>
internal static class MidiControllers
{
    /// <summary>Every controller.</summary>
    public static readonly ControllerModel[] All = [ApcMiniMk2(), NanoKontrol2(), XTouchMini(), XTouchCompact()];

    /// <summary>The controller of this name for sim.cmd; null for none.</summary>
    /// <param name="device">"apc-mini-mk2".</param>
    public static ControllerModel? Find(string device)
    {
        return All.FirstOrDefault(model => model.Device == device);
    }

    /// <summary>
    /// The 128 colors of the APC mini mk2's pads, by velocity, as its manual lists them.
    /// </summary>
    public static readonly string[] ApcPalette =
    [
        "#000000", "#1E1E1E", "#7F7F7F", "#FFFFFF", "#FF4C4C", "#FF0000", "#590000", "#190000",
        "#FFBD6C", "#FF5400", "#591D00", "#271B00", "#FFFF4C", "#FFFF00", "#595900", "#191900",
        "#88FF4C", "#54FF00", "#1D5900", "#142B00", "#4CFF4C", "#00FF00", "#005900", "#001900",
        "#4CFF5E", "#00FF19", "#00590D", "#001902", "#4CFF88", "#00FF55", "#00591D", "#001F12",
        "#4CFFB7", "#00FF99", "#005935", "#001912", "#4CC3FF", "#00A9FF", "#004152", "#001019",
        "#4C88FF", "#0055FF", "#001D59", "#000819", "#4C4CFF", "#0000FF", "#000059", "#000019",
        "#874CFF", "#5400FF", "#190064", "#0F0030", "#FF4CFF", "#FF00FF", "#590059", "#190019",
        "#FF4C87", "#FF0054", "#59001D", "#220013", "#FF1500", "#993500", "#795100", "#436400",
        "#033900", "#005735", "#00547F", "#0000FF", "#00454F", "#2500CC", "#7F7F7F", "#202020",
        "#FF0000", "#BDFF2D", "#AFED06", "#64FF09", "#108B00", "#00FF87", "#00A9FF", "#002AFF",
        "#3F00FF", "#7A00FF", "#B21A7D", "#402100", "#FF4A00", "#88E106", "#72FF15", "#00FF00",
        "#3BFF26", "#59FF71", "#38FFCC", "#5B8AFF", "#3151C6", "#877FE9", "#D31DFF", "#FF005D",
        "#FF7F00", "#B9B000", "#90FF00", "#835D07", "#392B00", "#144C10", "#0D5038", "#15152A",
        "#16205A", "#693C1C", "#A8000A", "#DE513D", "#D86A1C", "#FFE126", "#9EE12F", "#67B50F",
        "#1E1E30", "#DCFF6B", "#80FFBD", "#9A99FF", "#8E66FF", "#404040", "#757575", "#E0FFFF",
        "#A00000", "#350000", "#1AD000", "#074200", "#B9B000", "#3F3100", "#B35F00", "#4B1502",
    ];

    private const string Red = "#ff3b30";
    private const string Green = "#34c759";
    private const string Amber = "#ffb020";

    // Session mode, channel 1: pads Note 0-63 (0 at the bottom left), the keys above the faders
    // Note 100-107 (red), the keys at the right Note 112-119 (green), faders CC 48-56.
    private static ControllerModel ApcMiniMk2()
    {
        var pads = new List<SimControl>();
        for (int row = 8; row >= 1; row--)
        {
            for (int column = 1; column <= 8; column++)
            {
                int note = ((row - 1) * 8) + column - 1;
                pads.Add(new SimControl(Id(note: true, 1, note), SimControlKind.Pad, "", 1, true, note)
                {
                    Light = new SimLight(LightReading.ApcPad, Note: true, Channel: 0, note),
                });
            }
        }

        return new ControllerModel(
            "apc-mini-mk2",
            "APC mini mk2",
            "Akai APC mini mk2",
            [
                new SimSection("Pads", 1, 8, [.. pads]),
                new SimSection("Scene keys", 1, 1, [.. Range(112, 8, note => LitKey(1, note, $"Scene {note - 111}", note, 1, LightReading.OffOnBlink, Green))]),
                new SimSection("Keys above the faders", 2, 8, [.. Range(100, 8, note => LitKey(1, note, $"Track {note - 99}", note, 1, LightReading.OffOnBlink, Red))]),
                new SimSection("Faders", 3, 9, [.. Range(48, 9, number => Fader(1, number, number == 56 ? "Master" : $"{number - 47}"))]),
            ]);
    }

    // CC mode, channel 1: sliders CC 0-7, knobs CC 16-23, S M R CC 32-39, 48-55, 64-71, the
    // transport CC 41-46 and 58-62; a key is lit by its own CC, 127 on and 0 off.
    private static ControllerModel NanoKontrol2()
    {
        (int Number, string Label, bool Lit)[] transport =
        [
            (58, "Track ◀", false), (59, "Track ▶", false), (46, "Cycle", true), (60, "Set", false), (61, "Marker ◀", false), (62, "Marker ▶", false),
            (43, "◀◀", true), (44, "▶▶", true), (42, "■", true), (41, "▶", true), (45, "●", true),
        ];
        SimControl[] keys =
        [
            .. transport.Select(key => key.Lit
                ? LitKey(1, key.Number, key.Label, key.Number, 1, LightReading.OffAboveZero, Red, note: false)
                : Key(1, key.Number, key.Label, note: false)),
        ];

        return new ControllerModel(
            "nanokontrol2",
            "nanoKONTROL2",
            "Korg nanoKONTROL2",
            [
                new SimSection("Transport", 1, 6, keys),
                new SimSection(
                    "Strips",
                    1,
                    8,
                    [
                        .. Range(16, 8, number => Knob(1, number, $"{number - 15}")),
                        .. Range(32, 8, number => LitKey(1, number, "S", number, 1, LightReading.OffAboveZero, Red, note: false)),
                        .. Range(48, 8, number => LitKey(1, number, "M", number, 1, LightReading.OffAboveZero, Red, note: false)),
                        .. Range(64, 8, number => LitKey(1, number, "R", number, 1, LightReading.OffAboveZero, Red, note: false)),
                        .. Range(0, 8, number => Fader(1, number, $"{number + 1}")),
                    ]),
            ]);
    }

    // Standard mode, layer A, channel 11: knobs CC 1-8 (set by their own CC), knobs pressed Note 0-7,
    // keys Note 8-23, fader CC 9; a key is lit by Note 0-15 on channel 1.
    private static ControllerModel XTouchMini()
    {
        return new ControllerModel(
            "x-touch-mini",
            "X-TOUCH MINI",
            "Behringer X-Touch Mini",
            [
                new SimSection(
                    "Knobs",
                    1,
                    8,
                    [
                        .. Range(1, 8, number => Knob(11, number, $"{number}", driven: true)),
                        .. Range(0, 8, note => Key(11, note, "Push")),
                    ]),
                new SimSection("Fader", 1, 1, [Fader(11, 9, "A")]),
                new SimSection("Keys", 2, 8, [.. Range(8, 16, note => LitKey(11, note, $"{note - 7}", note - 8, 1, LightReading.OffOnBlink, Amber))]),
            ]);
    }

    // Standard mode, layer A, channel 1: motorized faders CC 1-9, knobs CC 10-25 (set by their own
    // CC), knobs pressed Note 0-15, keys Note 16-54; a key is lit by the note 16 below its own on
    // channel 2.
    private static ControllerModel XTouchCompact()
    {
        string[] transport = ["◀◀", "▶▶", "Loop", "●", "■", "▶"];
        SimControl CompactKey(int note, string label) => LitKey(1, note, label, note - 16, 2, LightReading.OffBlinkOn, Red);
        return new ControllerModel(
            "x-touch-compact",
            "X-TOUCH COMPACT",
            "Behringer X-Touch Compact",
            [
                new SimSection(
                    "Knobs",
                    1,
                    8,
                    [.. Range(10, 8, number => Knob(1, number, $"{number - 9}", driven: true)), .. Range(0, 8, note => Key(1, note, "Push"))]),
                new SimSection(
                    "Knobs at the right",
                    1,
                    4,
                    [.. Range(18, 8, number => Knob(1, number, $"{number - 9}", driven: true)), .. Range(8, 8, note => Key(1, note, "Push"))]),
                new SimSection("Keys", 2, 8, [.. Range(16, 24, note => CompactKey(note, $"{((note - 16) / 8) + 1}.{((note - 16) % 8) + 1}"))]),
                new SimSection("Transport", 2, 3, [.. Range(49, 6, note => CompactKey(note, transport[note - 49]))]),
                new SimSection(
                    "Faders",
                    3,
                    9,
                    [
                        .. Range(1, 9, number => Fader(1, number, number == 9 ? "Main" : $"{number}", driven: true)),
                        .. Range(40, 9, note => CompactKey(note, note == 48 ? "Main" : "Select")),
                    ]),
            ]);
    }

    // The name of a control on its page: "n56" for Note 56 on channel 1, "c48" for CC 48, "c11-1" for CC 1 on channel 11.
    private static string Id(bool note, int channel, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{(note ? 'n' : 'c')}{number}{(channel == 1 ? "" : $"-{channel}")}");

    private static IEnumerable<SimControl> Range(int first, int count, Func<int, SimControl> make) => Enumerable.Range(first, count).Select(make);

    private static SimControl Key(int channel, int number, string label, bool note = true) =>
        new(Id(note, channel, number), SimControlKind.Key, label, channel, note, number);

    private static SimControl LitKey(int channel, int number, string label, int lightNumber, int lightChannel, LightReading reading, string color, bool note = true) =>
        Key(channel, number, label, note) with { Light = new SimLight(reading, note, lightChannel, lightNumber, color) };

    private static SimControl Fader(int channel, int number, string label, bool driven = false) =>
        new(Id(note: false, channel, number), SimControlKind.Fader, label, channel, false, number) { Driven = driven };

    private static SimControl Knob(int channel, int number, string label, bool driven = false) =>
        new(Id(note: false, channel, number), SimControlKind.Knob, label, channel, false, number) { Driven = driven };
}
