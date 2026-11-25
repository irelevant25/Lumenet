namespace DmxBox.Server;

/// <summary>A ready-made mapping of a controller: its controls on the desk's keys and faders, for any name the device has on a PC.</summary>
/// <param name="Id">Its name for the desk: "apc-mini-mk2".</param>
/// <param name="Name">Its name for people: "Akai APC mini mk2".</param>
/// <param name="Device">The name the controller usually has on a PC.</param>
/// <param name="About">What it maps, in a few sentences.</param>
/// <param name="Mappings">The mappings, for a device named "" (the device it is used for takes its place).</param>
internal sealed record MidiPreset(string Id, string Name, string Device, string About, MidiMapping[] Mappings);

/// <summary>
/// The ready-made mappings of the controllers the desk knows: the Akai APC mini mk2, the Korg
/// nanoKONTROL2, and the Behringer X-Touch Mini and X-Touch Compact (both in their Standard mode,
/// layer A). Their faders work the faders of the pages shown, so the page keys choose what they
/// move. The messages and lights are those of the makers' manuals; the lights of the Behringers are
/// as other programs send them (no manual says).
/// </summary>
internal static class MidiPresets
{
    /// <summary>Every preset.</summary>
    public static readonly MidiPreset[] All = [ApcMiniMk2(), NanoKontrol2(), XTouchMini(), XTouchCompact()];

    /// <summary>The presets as the pages list them.</summary>
    public static readonly MidiPresetView[] Views = [.. All.Select(preset => new MidiPresetView(preset.Id, preset.Name, preset.Device, preset.About))];

    /// <summary>The preset with this name; null for none.</summary>
    /// <param name="id">"apc-mini-mk2".</param>
    public static MidiPreset? Find(string? id)
    {
        return All.FirstOrDefault(preset => preset.Id == id);
    }

    // ---------------------------------------------------------------- Akai APC mini mk2

    // Its Session mode, channel 1. Pads: Note 0-63, 0 at the bottom left, 8 a row up. Their light is
    // a color of the palette (the velocity) at a brightness the channel says: 1 is 10 %, 3 is 50 %,
    // 7 full. The keys above the faders (Note 100-107) and at the right (112-119) have one color:
    // 0 off, 1 on. Faders: CC 48-55, the master CC 56.
    private static MidiPreset ApcMiniMk2()
    {
        const int red = 5, orange = 9, yellow = 13, white = 3, green = 21, violet = 48;
        static MidiLights Pad(int color) => new(new MidiLight(color, 1), new MidiLight(color, 3), new MidiLight(color, 7));
        var button = new MidiLights(new MidiLight(0), new MidiLight(0), new MidiLight(1));
        static int PadAt(int row, int column) => ((row - 1) * 8) + column - 1;   // row 1 at the bottom, column 1 at the left

        var mappings = new List<MidiMapping>();
        string[] top = ["stack.live", "stack.previous", "stack.play", "stack.pause", "stack.next", "master.clear", "master.solo", "master.solo-down"];
        int[] topColors = [orange, yellow, yellow, yellow, yellow, white, white, white];
        string[] right = ["record.save", "record.edit", "record.record", "record.copy", "record.delete", "master.rate", "master.tempo", "master.blackout"];
        for (int column = 1; column <= 8; column++)
        {
            mappings.Add(Key(1, MidiMapping.Note, PadAt(8, column), top[column - 1], Pad(topColors[column - 1])));
            mappings.Add(Key(1, MidiMapping.Note, PadAt(7, column), $"page.channel.{column}", Pad(green)));
            mappings.Add(Key(1, MidiMapping.Note, PadAt(6, column), $"page.memory.{column}", Pad(red)));
            for (int row = 5; row >= 2; row--)
                mappings.Add(Key(1, MidiMapping.Note, PadAt(row, column), $"memory.{((5 - row) * 8) + column}", Pad(red)));
            mappings.Add(Key(1, MidiMapping.Note, PadAt(1, column), $"effect.{column}", Pad(violet)));

            mappings.Add(Fader(1, 47 + column, $"fader.slot.channel.{column}"));
            mappings.Add(Key(1, MidiMapping.Note, 99 + column, $"slot.channel.{column}", button));
            mappings.Add(Key(1, MidiMapping.Note, 111 + column, right[column - 1], button));
        }

        mappings.Add(Fader(1, 56, "fader.master"));
        return new MidiPreset(
            "apc-mini-mk2",
            "Akai APC mini mk2",
            "APC mini mk2",
            "Faders: the channel faders of the page shown and the master; the keys above them: their channel keys. "
            + "Pads from the top: the stack, Clear and the Solos; channel pages; memory pages; memories 1 to 32; "
            + "effects 1 to 8. At the right: the Record keys, Rate, Tempo and Blackout.",
            [.. mappings]);
    }

    // ---------------------------------------------------------------- Korg nanoKONTROL2

    // Its CC mode, channel 1. Sliders CC 0-7, knobs CC 16-23, the keys S, M and R CC 32-39, 48-55
    // and 64-71; the transport CC 41-46, Track ◀ ▶ CC 58 and 59, Marker Set, ◀ and ▶ CC 60-62. The keys
    // send 127 pressed and 0 let go; they are lit with the same, once the LED mode is External.
    private static MidiPreset NanoKontrol2()
    {
        var mappings = new List<MidiMapping>();
        for (int strip = 1; strip <= 8; strip++)
        {
            mappings.Add(Fader(1, strip - 1, $"fader.slot.channel.{strip}"));
            mappings.Add(Fader(1, 15 + strip, $"fader.slot.memory.{strip}"));
            mappings.Add(Key(1, MidiMapping.Controller, 31 + strip, $"slot.channel.{strip}"));
            mappings.Add(Key(1, MidiMapping.Controller, 47 + strip, $"slot.memory.{strip}"));
            mappings.Add(Key(1, MidiMapping.Controller, 63 + strip, $"slot.effect.{strip}"));
        }

        mappings.Add(Key(1, MidiMapping.Controller, 41, "stack.play"));
        mappings.Add(Key(1, MidiMapping.Controller, 42, "stack.pause"));
        mappings.Add(Key(1, MidiMapping.Controller, 43, "stack.previous"));
        mappings.Add(Key(1, MidiMapping.Controller, 44, "stack.next"));
        mappings.Add(Key(1, MidiMapping.Controller, 45, "stack.live"));
        mappings.Add(Key(1, MidiMapping.Controller, 46, "master.blackout"));
        // Track and Marker have no light
        mappings.Add(Key(1, MidiMapping.Controller, 58, "page.channel.previous", light: false));
        mappings.Add(Key(1, MidiMapping.Controller, 59, "page.channel.next", light: false));
        mappings.Add(Key(1, MidiMapping.Controller, 60, "master.clear", light: false));
        mappings.Add(Key(1, MidiMapping.Controller, 61, "page.memory.previous", light: false));
        mappings.Add(Key(1, MidiMapping.Controller, 62, "page.memory.next", light: false));
        return new MidiPreset(
            "nanokontrol2",
            "Korg nanoKONTROL2",
            "nanoKONTROL2",
            "Sliders and knobs: the channel and memory faders of the page shown; S, M and R: its channel, memory and "
            + "effect keys. The transport works the stack, Cycle is Blackout, Set is Clear; Track and Marker go through "
            + "the pages. Its lights need the LED mode External (Korg Kontrol Editor).",
            [.. mappings]);
    }

    // ---------------------------------------------------------------- Behringer X-Touch Mini

    // Its Standard mode, layer A, channel 11: knobs CC 1-8 (absolute; the knob's own CC sets it and
    // its ring), knobs pressed Note 0-7, the upper keys Note 8-15, the lower keys Note 16-23, the
    // fader CC 9. A key is lit by Note 0-15 on channel 1 (8 below its own): 0 off, 1 on, 2 blinking.
    private static MidiPreset XTouchMini()
    {
        static MidiLights Button(int note) => new(new MidiLight(0, 1), new MidiLight(0, 1), new MidiLight(1, 1), new MidiLight(2, 1), note - 8);

        var mappings = new List<MidiMapping>();
        string[] lower = ["stack.live", "stack.play", "stack.pause", "stack.next", "page.channel.previous", "page.channel.next", "page.memory.previous", "page.memory.next"];
        for (int knob = 1; knob <= 8; knob++)
        {
            mappings.Add(Fader(11, knob, $"fader.slot.channel.{knob}", feedback: true));
            mappings.Add(Key(11, MidiMapping.Note, knob - 1, $"slot.channel.{knob}", light: false));
            mappings.Add(Key(11, MidiMapping.Note, 7 + knob, $"slot.memory.{knob}", Button(7 + knob)));
            mappings.Add(Key(11, MidiMapping.Note, 15 + knob, lower[knob - 1], Button(15 + knob)));
        }

        mappings.Add(Fader(11, 9, "fader.master"));
        return new MidiPreset(
            "x-touch-mini",
            "Behringer X-Touch Mini",
            "X-TOUCH MINI",
            "Standard mode, layer A. Knobs: the channel faders of the page shown (pressed, its channel keys); upper "
            + "keys: its memory keys; lower keys: the stack and the pages back and on; the fader: the master.",
            [.. mappings]);
    }

    // ---------------------------------------------------------------- Behringer X-Touch Compact

    // Its Standard mode, layer A, channel 1: the motorized faders CC 1-8 and the main fader CC 9;
    // the upper knobs CC 10-17 and those at the right CC 18-25 (their own CC sets them and their
    // rings), knobs pressed Note 0-15; three rows of keys Note 16-23, 24-31 and 32-39, the keys under
    // the faders Note 40-47 and 48, the transport Note 49-54. A key is lit by the note 16 below its
    // own on channel 2, with 2.
    private static MidiPreset XTouchCompact()
    {
        static MidiLights Button(int note) => new(new MidiLight(0, 2), new MidiLight(0, 2), new MidiLight(2, 2), null, note - 16);

        var mappings = new List<MidiMapping>();
        string[] third = ["record.save", "record.edit", "record.record", "record.copy", "record.delete", "master.clear", "master.solo", "master.solo-down"];
        for (int strip = 1; strip <= 8; strip++)
        {
            mappings.Add(Fader(1, strip, $"fader.slot.channel.{strip}", feedback: true));
            mappings.Add(Fader(1, 9 + strip, $"fader.slot.memory.{strip}", feedback: true));
            mappings.Add(Fader(1, 17 + strip, $"fader.slot.effect.{strip}", feedback: true));
            mappings.Add(Key(1, MidiMapping.Note, strip - 1, $"slot.memory.{strip}", light: false));
            mappings.Add(Key(1, MidiMapping.Note, 7 + strip, $"slot.effect.{strip}", light: false));
            mappings.Add(Key(1, MidiMapping.Note, 15 + strip, $"page.channel.{strip}", Button(15 + strip)));
            mappings.Add(Key(1, MidiMapping.Note, 23 + strip, $"page.memory.{strip}", Button(23 + strip)));
            mappings.Add(Key(1, MidiMapping.Note, 31 + strip, third[strip - 1], Button(31 + strip)));
            mappings.Add(Key(1, MidiMapping.Note, 39 + strip, $"slot.channel.{strip}", Button(39 + strip)));
        }

        mappings.Add(Fader(1, 9, "fader.master", feedback: true));
        mappings.Add(Key(1, MidiMapping.Note, 48, "master.blackout", Button(48)));
        string[] transport = ["stack.previous", "stack.next", "master.tempo", "stack.live", "stack.pause", "stack.play"];
        for (int index = 0; index < transport.Length; index++)
            mappings.Add(Key(1, MidiMapping.Note, 49 + index, transport[index], Button(49 + index)));

        return new MidiPreset(
            "x-touch-compact",
            "Behringer X-Touch Compact",
            "X-TOUCH COMPACT",
            "Standard mode, layer A. Motorized faders: the channel faders of the page shown and the master; knobs: its "
            + "memory and effect faders; keys: its channel keys, the pages, the Record keys, Clear, the Solos, the stack "
            + "and Blackout.",
            [.. mappings]);
    }

    // ---------------------------------------------------------------- the parts

    // A key, lit by the desk unless it has no light.
    private static MidiMapping Key(int channel, string message, int number, string target, MidiLights? lights = null, bool light = true)
    {
        return new MidiMapping
        {
            Channel = channel,
            Message = message,
            Number = number,
            Target = target,
            Pickup = false,
            Feedback = light,
            Lights = lights,
        };
    }

    // A fader or knob (CC, 0-127): it takes over at the desk's value, unless the desk moves it (a motor, a knob with a ring).
    private static MidiMapping Fader(int channel, int number, string target, bool feedback = false)
    {
        return new MidiMapping
        {
            Channel = channel,
            Message = MidiMapping.Controller,
            Number = number,
            Target = target,
            Mode = MidiMapping.Absolute,
            Pickup = !feedback,
            Feedback = feedback,
        };
    }
}
