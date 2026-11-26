namespace DmxBox.Tests.Midi;

/// <summary>The MIDI messages as bytes, a stream of them, the names of the keys and faders, and the scales of the faders.</summary>
public sealed class MidiMessageTests
{
    [Fact]
    public void AMessageGoesToItsBytesAndBack()
    {
        MidiMessage[] messages =
        [
            new(MidiKind.NoteOn, 1, 56, 127),
            new(MidiKind.NoteOff, 16, 0, 64),
            new(MidiKind.ControlChange, 11, 48, 100),
            new(MidiKind.PitchBend, 9, 0, 16383),
            new(MidiKind.ProgramChange, 2, 5, 0),
        ];
        foreach (MidiMessage message in messages)
            Assert.Equal(message, MidiMessage.FromBytes(message.ToBytes()));

        Assert.Equal(new byte[] { 0x90, 56, 127 }, messages[0].ToBytes());
        Assert.Equal(new byte[] { 0xE8, 0x7F, 0x7F }, messages[3].ToBytes());
        Assert.Equal(new byte[] { 0xC1, 5 }, messages[4].ToBytes());
    }

    [Fact]
    public void NoteOnWithVelocityZeroIsANoteOff()
    {
        Assert.Equal(new MidiMessage(MidiKind.NoteOff, 1, 60, 0), MidiMessage.FromBytes([0x90, 60, 0]));
        Assert.False(new MidiMessage(MidiKind.NoteOff, 1, 60, 0).IsPress);
        Assert.True(new MidiMessage(MidiKind.ControlChange, 1, 32, 127).IsPress);
        Assert.False(new MidiMessage(MidiKind.ControlChange, 1, 32, 0).IsPress);
        Assert.Null(MidiMessage.FromBytes([0xF8]));        // clock
        Assert.Null(MidiMessage.FromBytes([0xA0, 60, 10])); // aftertouch: not taken
    }

    [Fact]
    public void AStreamOfBytesGivesItsMessagesWithRunningStatusAndPassesOverTheRest()
    {
        var reader = new MidiStreamReader();
        var got = new List<MidiMessage>();
        reader.Take([0xB0, 7, 10, 8, 20], got.Add);              // running status: a second CC without its status byte
        reader.Take([0xF0, 0x47, 0x7F, 0xF7], got.Add);           // system exclusive: passed over
        reader.Take([0x90, 60], got.Add);                         // half a message ...
        reader.Take([0xF8, 100, 61, 0], got.Add);                 // ... a clock between, then the rest, and one more by running status
        reader.Take([0xC0, 3, 4], got.Add);                       // a program change has one data byte

        Assert.Equal(
            [
                new MidiMessage(MidiKind.ControlChange, 1, 7, 10),
                new MidiMessage(MidiKind.ControlChange, 1, 8, 20),
                new MidiMessage(MidiKind.NoteOn, 1, 60, 100),
                new MidiMessage(MidiKind.NoteOff, 1, 61, 0),
                new MidiMessage(MidiKind.ProgramChange, 1, 3, 0),
                new MidiMessage(MidiKind.ProgramChange, 1, 4, 0),
            ],
            got);
    }

    [Fact]
    public void AnEndOfSystemExclusiveEndsRunningStatusToo()
    {
        var reader = new MidiStreamReader();
        var got = new List<MidiMessage>();
        reader.Take([0x90, 60, 100, 0xF0, 1, 2, 0xF7, 61, 100], got.Add);
        Assert.Equal([new MidiMessage(MidiKind.NoteOn, 1, 60, 100)], got);
    }

    [Fact]
    public void TheKeysAreNamedAsThePageNamesThemAndTheFadersHere()
    {
        Assert.Equal(new ControlTarget("memory.5", null, 0, false), ControlTarget.Parse("memory.5"));
        Assert.True(ControlTarget.Parse("master.solo-down")?.IsKey);
        Assert.True(ControlTarget.Parse("lan")?.IsKey);
        Assert.Equal(new ControlTarget("fader.channel.12", FaderKind.Channel, 12, false), ControlTarget.Parse("fader.channel.12"));
        Assert.Equal(new ControlTarget("fader.master", FaderKind.Master, 0, false), ControlTarget.Parse("fader.master"));
        Assert.Equal(new ControlTarget("fader.slot.memory.3", FaderKind.Memory, 3, true), ControlTarget.Parse("fader.slot.memory.3"));
        Assert.Equal(FaderKind.EffectSpread, ControlTarget.Parse("fader.effect-spread.64")?.Fader);
        Assert.Equal(new ControlTarget("fader.sound.64", FaderKind.Sound, 64, false), ControlTarget.Parse("fader.sound.64"));
        Assert.Equal(new ControlTarget("fader.sound-volume", FaderKind.SoundVolume, 0, false), ControlTarget.Parse("fader.sound-volume"));
        Assert.True(ControlTarget.Parse("sound.mute")?.IsKey);

        foreach (string wrong in new[] { "", "Memory.5", "fader.channel", "fader.master.1", "fader.slot.master", "fader.channel.0", "fader.channel.4097", "fader.colour.1", "memory 5", "fader.slot.sequence.1", "fader.sound", "fader.sound-volume.1", "fader.slot.sound.1" })
            Assert.Null(ControlTarget.Parse(wrong));
    }

    [Fact]
    public void AFaderStandsWhereThePagesFaderStandsForTheSameValue()
    {
        // levels as they are
        Assert.Equal(200, FaderScale.PositionOf(FaderKind.Channel, 200));
        Assert.Equal(200.0, FaderScale.ValueAt(FaderKind.Memory, 200));

        // the rate: 30 to 300 BPM in whole beats
        Assert.Equal(30.0, FaderScale.ValueAt(FaderKind.Rate, 0));
        Assert.Equal(300.0, FaderScale.ValueAt(FaderKind.Rate, 255));
        Assert.Equal(120.0, FaderScale.ValueAt(FaderKind.Rate, FaderScale.PositionOf(FaderKind.Rate, 120)));

        // a sequence: 1x at both middle places, 0.1x and 10x at the ends, on steps of 0.05
        Assert.Equal(0.1, FaderScale.ValueAt(FaderKind.Sequence, 0));
        Assert.Equal(1.0, FaderScale.ValueAt(FaderKind.Sequence, 127));
        Assert.Equal(1.0, FaderScale.ValueAt(FaderKind.Sequence, 128));
        Assert.Equal(10.0, FaderScale.ValueAt(FaderKind.Sequence, 255));
        Assert.Equal(127, FaderScale.PositionOf(FaderKind.Sequence, 1));
        Assert.Equal(255, FaderScale.PositionOf(FaderKind.Sequence, 10));
        Assert.Equal(2.0, FaderScale.ValueAt(FaderKind.Sequence, FaderScale.PositionOf(FaderKind.Sequence, 2)));
        Assert.Equal(0.0, FaderScale.ValueAt(FaderKind.Sequence, 77) * 20 % 1, 6);

        // an effect: its level in per cent, its speed in octaves, its size and spread in per cent
        Assert.Equal(100.0, FaderScale.ValueAt(FaderKind.Effect, 255));
        Assert.Equal(0.5, FaderScale.ValueAt(FaderKind.EffectSpeed, 0));
        Assert.Equal(600.0, FaderScale.ValueAt(FaderKind.EffectSpeed, 255));
        int at60 = FaderScale.PositionOf(FaderKind.EffectSpeed, 60);
        int at120 = FaderScale.PositionOf(FaderKind.EffectSpeed, 120);
        int at240 = FaderScale.PositionOf(FaderKind.EffectSpeed, 240);
        Assert.InRange(Math.Abs((at240 - at120) - (at120 - at60)), 0, 1);   // twice as fast: the same way further
        Assert.Equal(50.0, FaderScale.ValueAt(FaderKind.EffectSize, 128));
        Assert.Equal(128, FaderScale.PositionOf(FaderKind.EffectSpread, 50));

        // a sound: its level in per cent; the volume of the sound -100 dB to 0 dB, the gain as the place squared
        Assert.Equal(100.0, FaderScale.ValueAt(FaderKind.Sound, 255));
        Assert.Equal(0.0, FaderScale.ValueAt(FaderKind.SoundVolume, 255));
        Assert.Equal(-12.0, FaderScale.ValueAt(FaderKind.SoundVolume, 128));
        Assert.Equal(-100.0, FaderScale.ValueAt(FaderKind.SoundVolume, 0));
        Assert.InRange(FaderScale.ValueAt(FaderKind.SoundVolume, FaderScale.PositionOf(FaderKind.SoundVolume, -20)), -20.2, -19.8);
        Assert.Equal(0, FaderScale.PositionOf(FaderKind.SoundVolume, -100));
    }
}
