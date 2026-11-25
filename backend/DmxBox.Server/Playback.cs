namespace DmxBox.Server;

/// <summary>A step of the stack as the UI gets it: a <see cref="Step"/> with its name and its number.</summary>
/// <param name="Id">Names the step for as long as the program runs; its number may change, this does not.</param>
/// <param name="Cue">Its number in the order of the stack, written like "2.1".</param>
/// <param name="Target">What the step stands for: "channel", "memory", "fixture", "group" or "release".</param>
/// <param name="Number">
/// The channel (1-512) or the memory (1-128); a fixture or a group by its number in the stage plan;
/// for a release the <see cref="Id"/> of the step it takes down, 0 for none.
/// </param>
/// <param name="Value">The level the step brings its fader to, 0-255.</param>
/// <param name="Up">Seconds the fader takes to the value.</param>
/// <param name="Wait">Seconds after which the stack goes on by itself; 0 means it stays until Play.</param>
/// <param name="Down">Seconds the fader takes back to zero when the stack leaves the step.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record StackStep(int Id, string Cue, string Target, int Number, int Value, double Up, double Wait, double Down)
{
    /// <summary>For a step of a fixture or a group: the color of its lights, see <see cref="Step.Color"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; init; }

    /// <summary>True for a step that comes up on top of what is on, see <see cref="Step.Add"/>. Sent only when it is true.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Add { get; init; }
}

/// <summary>The stack as the UI gets it.</summary>
/// <param name="Steps">The steps in the order of their numbers.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record StackState(StackStep[] Steps);

/// <summary>Where the stack stands.</summary>
/// <param name="CurrentId">The step that is on, or on its way up; null if no step is on.</param>
/// <param name="NextId">
/// The step Play goes to: the one behind the current step, and behind the last step the first.
/// Null if the stack has no other step.
/// </param>
/// <param name="State">
/// "idle": no step is on. "fading": the current step is on its way up. "waiting": it is up, and
/// the stack goes on by itself when its wait is over. "holding": it is up, and the stack waits
/// for Play.
/// </param>
/// <param name="Paused">True while Pause holds a fade or a wait.</param>
/// <param name="Remaining">Seconds left of the fade or of the wait; 0 otherwise.</param>
/// <param name="Live">
/// True while the stack is live: it runs, and nothing that is stored can be changed. While it
/// is false no step is on and Play, Pause, Next and Previous are refused.
/// </param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record StackPlayback(int? CurrentId, int? NextId, string State, bool Paused, double Remaining, bool Live)
{
    /// <summary>
    /// The steps that are on, in the order they came: the current one, and with it the steps
    /// it came on top of (<see cref="Step.Add"/>). Empty while no step is on, and after a
    /// release that took the last one down.
    /// </summary>
    public int[] On { get; init; } = [];
}

/// <summary>
/// While the stack goes from one look to the next, the faders that take part give their light
/// through its crossfade, channel by channel, instead of the higher one winning (Desk.Stack.cs).
/// </summary>
/// <param name="Channels">The channel faders that take part.</param>
/// <param name="Memories">The memory faders that take part.</param>
/// <param name="Levels">What the crossfade gives every channel of the project right now, 0 where it gives nothing.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record StackCrossfade(int[] Channels, int[] Memories, int[] Levels);

/// <summary>What changes with time, for the UI: several times a second while something runs.</summary>
/// <param name="Levels">
/// 512 levels: what the running sequences give each channel right now, already scaled by the
/// faders of their memories. 0 where they give nothing.
/// </param>
/// <param name="SequenceSteps">For each memory whose sequence is running: the step it is on, counted from 1.</param>
/// <param name="Stack">Where the stack stands.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record Playback(int[] Levels, Dictionary<int, int> SequenceSteps, StackPlayback Stack)
{
    /// <summary>The stack's crossfade while one runs; left out while none does.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public StackCrossfade? Crossfade { get; init; }

    /// <summary>The playbacks of the effects the project has, and of any other whose fader is up.</summary>
    public EffectPlayback[] Effects { get; init; } = [];

    /// <summary>What the running effects give the channels they touch; left out while none runs.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EffectLevels? EffectLevels { get; init; }
}

/// <summary>A fader the stack has moved, to be told to the UIs.</summary>
/// <param name="Memory">True for the fader of a memory, false for a channel fader.</param>
/// <param name="Number">The memory (1-128), the channel (1-512) or the effect (1-64).</param>
/// <param name="Level">The fader's new level, 0-255.</param>
internal readonly record struct FaderMoved(bool Memory, int Number, int Level)
{
    /// <summary>True for the fader of an effect.</summary>
    public bool Effect { get; init; }
}

/// <summary>A step of the stack with its number, as a project holds it.</summary>
/// <param name="Cue">The number of the step.</param>
/// <param name="Step">The step.</param>
internal sealed record ProjectStep(CueNumber Cue, Step Step)
{
    /// <summary>
    /// For a release: the number of the step it takes down, as the names of the steps last only
    /// as long as the program runs; null for none. The <see cref="Step.Number"/> of a release is
    /// not used in a project.
    /// </summary>
    public CueNumber? Releases { get; init; }
}
