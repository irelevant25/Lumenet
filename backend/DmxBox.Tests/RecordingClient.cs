using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// Stands in for "all connected UIs" in the tests of the box link, and remembers the two
/// messages the link sends them.
/// </summary>
/// <remarks>Thread-safe: in one test the box link's own thread sends while the test looks.</remarks>
internal sealed class RecordingClient : IDeskClient
{
    private readonly Lock _lock = new();
    private readonly List<BoxStatus> _boxChanges = [];
    private readonly List<bool> _blackoutChanges = [];

    /// <summary>Every "BoxChanged" message so far, in order.</summary>
    public IReadOnlyList<BoxStatus> BoxChanges
    {
        get
        {
            lock (_lock)
            {
                return _boxChanges.ToArray();
            }
        }
    }

    /// <summary>Every "BlackoutChanged" message so far, in order.</summary>
    public IReadOnlyList<bool> BlackoutChanges
    {
        get
        {
            lock (_lock)
            {
                return _blackoutChanges.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public Task BoxChanged(BoxStatus box)
    {
        lock (_lock)
        {
            _boxChanges.Add(box);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task BlackoutChanged(bool on)
    {
        lock (_lock)
        {
            _blackoutChanges.Add(on);
        }

        return Task.CompletedTask;
    }

    // The box link has no business with the messages below: a test that gets one has found a fault.

    /// <inheritdoc />
    public Task ChannelChanged(int channel, int value) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task MemoryLevelChanged(int memory, int value) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task MasterChanged(int value) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task IndependentChanged(int index, int channel) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task MemoryIndependentChanged(int index, int memory) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task ChannelNoteChanged(int channel, string text) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task MemoryNoteChanged(int memory, string text) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task DeskChanged(DeskState desk) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task ProjectChanged(ProjectInfo project) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task StackChanged(StackState stack) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task SequenceSpeedChanged(int memory, double speed) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task PlaybackChanged(Playback playback) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task LanAccessChanged(LanState lan) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task SettingsChanged(System.Text.Json.JsonElement? settings) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task HistoryChanged(HistoryState history) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task RateChanged(bool on, double rate) => throw new NotSupportedException();

    /// <inheritdoc />
    public Task AutosaveChanged(AutosaveOptions options) => throw new NotSupportedException();
}
