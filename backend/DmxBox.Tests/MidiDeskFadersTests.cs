using DmxBox.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmxBox.Tests;

/// <summary>The desk's faders as MIDI moves them: on the desk, as a page moves them, and every page hears of it.</summary>
public sealed class MidiDeskFadersTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-midi-desk-");
    private readonly Desk _desk = new(new ManualTime());
    private readonly NotedPages _notes;
    private readonly MidiDeskFaders _faders;

    public MidiDeskFadersTests()
    {
        (IDeskClient pages, _notes) = NotedPages.Make();
        _faders = new MidiDeskFaders(_desk, new ProjectStore(_desk, _folder.FullName), new UndoHistory(_desk), pages, NullLogger.Instance);
    }

    public void Dispose()
    {
        _folder.Delete(recursive: true);
    }

    [Fact]
    public void TheFadersMoveAsAPagesDoAndEveryPageHearsOfIt()
    {
        Assert.Equal(new FaderMove(200, null), _faders.Move(new DeskFader(FaderKind.Channel, 5), 200));
        _faders.Move(new DeskFader(FaderKind.Memory, 3), 128);
        _faders.Move(new DeskFader(FaderKind.Master), 100);
        _faders.Move(new DeskFader(FaderKind.Rate), FaderScale.PositionOf(FaderKind.Rate, 120));

        DeskState state = _desk.GetState();
        Assert.Equal((200, 128, 100, 120.0), (state.Channels[4], state.MemoryLevels[2], state.Master, state.Bpm));
        Assert.Equal(["ChannelChanged 5 200", "MemoryLevelChanged 3 128", "MasterChanged 100", "RateChanged false 120"], _notes.Notes);
        Assert.Equal(200, _faders.PositionOf(new DeskFader(FaderKind.Channel, 5)));
        Assert.Equal(FaderScale.PositionOf(FaderKind.Rate, 120), _faders.PositionOf(new DeskFader(FaderKind.Rate)));
        Assert.Null(_faders.PositionOf(new DeskFader(FaderKind.Channel, 513)));   // one universe: 512 channels
    }

    [Fact]
    public void ASequencesSpeedIsPartOfTheProjectAndWaitsWhileTheStackIsLive()
    {
        Assert.Null(_faders.PositionOf(new DeskFader(FaderKind.Sequence, 2)));     // no sequence there: no fader
        _desk.SetSequence(2, loop: true, [new Step("channel", 5, 200, Up: 0, Wait: 1, Down: 0)]);
        _faders.Refresh();   // MIDI reads the desk once for each message: this is the next one
        Assert.Equal(127, _faders.PositionOf(new DeskFader(FaderKind.Sequence, 2)));   // 1x

        Assert.Equal(new FaderMove(255, null), _faders.Move(new DeskFader(FaderKind.Sequence, 2), 255));
        Assert.Equal(10.0, _desk.GetSequences()[2].Speed);
        Assert.Contains("SequenceSpeedChanged 2 10", _notes.Notes);
        Assert.Contains(_notes.Notes, note => note.StartsWith("ProjectChanged", StringComparison.Ordinal));

        _desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        _desk.SetStackLive(true);
        FaderMove refused = _faders.Move(new DeskFader(FaderKind.Sequence, 2), 0);
        Assert.Null(refused.Position);
        Assert.Contains("live", refused.Refused, StringComparison.Ordinal);
        Assert.Equal(10.0, _desk.GetSequences()[2].Speed);

        // A channel fader is played while the stack is live, as on the page.
        Assert.Equal(new FaderMove(50, null), _faders.Move(new DeskFader(FaderKind.Channel, 9), 50));
    }

    [Fact]
    public void AnEffectsFaderAndItsLiveValuesMoveItsPlayback()
    {
        Assert.Null(_faders.PositionOf(new DeskFader(FaderKind.Effect, 1)));      // no effect there yet
        _desk.SetEffect(new Effect(1, "Pulse", Effect.IntensityKind, "sine") { Speed = 60 });
        _faders.Refresh();

        _faders.Move(new DeskFader(FaderKind.Effect, 1), 255);
        _faders.Move(new DeskFader(FaderKind.EffectSpeed, 1), FaderScale.PositionOf(FaderKind.EffectSpeed, 120));
        _faders.Move(new DeskFader(FaderKind.EffectSize, 1), 128);
        EffectPlayback playback = _desk.GetEffectPlayback(1);
        Assert.Equal((true, 255, 50), (playback.Running, playback.Level, playback.Size));
        Assert.InRange(playback.Speed, 118, 122);
        Assert.Equal(255, _faders.PositionOf(new DeskFader(FaderKind.Effect, 1)));
        Assert.Contains(_notes.Notes, note => note.StartsWith("EffectPlaybackChanged effect 1 level 255", StringComparison.Ordinal));
    }
}
