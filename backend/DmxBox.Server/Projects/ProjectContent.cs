namespace DmxBox.Server.Projects;

/// <summary>
/// What a project file holds: the looks and the sequences of the memories, the stack, the
/// notes under the faders, and the channels and memories the independent keys are bound to.
/// </summary>
/// <param name="Looks">
/// One entry per memory, memory 1 first: a level for each channel of the project's universes
/// (512 each), or null while the memory holds no look.
/// </param>
/// <param name="ChannelNotes">A note under each channel fader of the project's universes; an empty text where there is none.</param>
/// <param name="MemoryNotes">The 128 notes under the memory faders; an empty text where there is none.</param>
/// <param name="Independents">The channel number of each channel key of the independents; key 1 first.</param>
internal sealed record ProjectContent(byte[]?[] Looks, string[] ChannelNotes, string[] MemoryNotes, int[] Independents)
{
    /// <summary>
    /// How many universes the project has, 1 to <see cref="Desk.MaxUniverses"/>; its channels are
    /// 1 to 512 for each of them. The looks and the channel notes are that long.
    /// </summary>
    public int Universes { get; init; } = 1;

    /// <summary>
    /// The boxes the project wants and the universe each of their outputs plays, see <see cref="BoxPlan"/>;
    /// empty for none, and then every box that answers plays universes by itself, in the order of the boxes.
    /// </summary>
    public BoxPlan[] Boxes { get; init; } = [];

    /// <summary>
    /// The project's outputs besides its boxes: Art-Net nodes, sACN and DMX USB interfaces, see
    /// <see cref="OutputPlan"/>; empty for none.
    /// </summary>
    public OutputPlan[] Outputs { get; init; } = [];

    /// <summary>The memory number, 1-128, of each of the 10 memory keys of the independents; key 1 first.</summary>
    public int[] MemoryIndependents { get; init; } = Desk.DefaultMemoryIndependents();

    /// <summary>
    /// One entry per memory, memory 1 first: the sequence the memory holds, or null. A memory
    /// holds a look or a sequence, never both.
    /// </summary>
    public Sequence?[] Sequences { get; init; } = new Sequence?[Desk.MemoryCount];

    /// <summary>One entry per effect number, effect 1 first: the effect, or null where there is none.</summary>
    public Effect?[] Effects { get; init; } = new Effect?[Effect.MaxEffects];

    /// <summary>The effect number, 1-64, of each of the 32 effect keys of the independents; key 1 first.</summary>
    public int[] EffectIndependents { get; init; } = Desk.DefaultEffectIndependents();

    /// <summary>One entry per sound number, sound 1 first: the sound, or null where there is none.</summary>
    public Sound?[] Sounds { get; init; } = new Sound?[Sound.MaxSounds];

    /// <summary>The sound number, 1-64, of each of the 32 sound keys of the independents; key 1 first.</summary>
    public int[] SoundIndependents { get; init; } = Desk.DefaultSoundIndependents();

    /// <summary>The steps of the stack, in the order of their numbers.</summary>
    public ProjectStep[] Stack { get; init; } = [];

    /// <summary>
    /// The notes of the project, instructions for the show, as the UI wrote them, see
    /// <see cref="ProjectNotes"/>; an empty text for none.
    /// </summary>
    public string Notes { get; init; } = "";

    /// <summary>
    /// The stage plan: the lights of the rig and where they hang; null for a project that does not
    /// say, such as one from a file of an older version or an imported show. Then the stage is
    /// empty.
    /// </summary>
    public StagePlan? Stage { get; init; }

    /// <summary>The picture under the stage plan, as a data URL; null without one. It goes with <see cref="Stage"/>.</summary>
    public string? StagePicture { get; init; }
}
