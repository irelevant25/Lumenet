using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server.Projects;

/// <summary>
/// Reads and writes project files: JSON that lists the memories that hold a look or a
/// sequence, the stack, the notes under the faders, and the channels of the independent keys.
/// </summary>
/// <remarks>
/// <code>
/// {
///   "format": "lumenet-project",
///   "version": 4,
///   "notes": "&lt;div&gt;Cue 1 &lt;b&gt;on the house lights out&lt;/b&gt;&lt;/div&gt;",
///   "memories": [
///     { "number": 1, "levels": { "5": 255, "6": 128 } }
///   ],
///   "channelNotes": { "5": "Front left" },
///   "memoryNotes": { "1": "Blue wash" },
///   "independents": [ 1, 2, 3, ... 32 ],
///   "memoryIndependents": [ 1, 2, 3, ... 32 ],
///   "sequences": [
///     { "number": 2, "loop": true, "random": false, "speed": 1, "steps": [
///       { "target": "channel", "number": 5, "value": 255, "up": 1, "wait": 0.5, "down": 1 } ] }
///   ],
///   "stack": [
///     { "cue": "1.0", "target": "memory", "number": 1, "value": 255, "up": 2, "wait": 0, "down": 2 }
///   ],
///   "stage": { "width": 16, "depth": 10, "lights": [
///     { "name": "LED bar", "address": 30, "channels": "RGBRGBRGBRGB", "x": 8, "y": 1, "angle": 0,
///       "shape": "square", "width": 4, "depth": 1 },
///     { "name": "Back blue 1", "address": 6, "channels": "D", "color": "#2060ff", "x": 2, "y": 1.5, "angle": 0,
///       "shape": "round", "width": 1, "depth": 1 } ],
///     "picture": "data:image/jpeg;base64,...", "pictureOpacity": 0.6, "pictureOver": "stage" }
/// }
/// </code>
/// "notes" are the notes of the project, instructions for the show, see <see cref="ProjectNotes"/>.
/// They are there only for a project that has some, and a reader that does not know them passes
/// over them, so they needed no new version. Files that earlier versions of the program wrote can
/// have "settings", the layout and the keyboard shortcuts of the UI on the PC; those belong to each
/// device now, and the reader passes over them.
/// "stage" is the stage plan, see <see cref="StagePlan"/>; it came without a new version too. A
/// file without it has an empty stage. A light without "shape", "width" and "depth" is
/// round and one square big; a plan without "picture" has none. A plan can also list "types",
/// the kinds of lights from the fixture library with the names of their channels, and "groups"
/// of lights; a light names its kind ("type") and a group its lights by their place in the
/// file's lists, counted from 1, and a light with a "limit" gives at most that many per cent.
/// A light or a group can have a "note", the one under its key on the Fixtures page.
/// All of these are written only when they are there, so files without them stay as they were.
/// A step of a sequence or of the stack can also stand for a "fixture" or a "group", named by its
/// place in those lists (0 for one that is gone), with a "color".
/// "outputs" are the outputs besides the boxes, see <see cref="OutputPlan"/>: Art-Net nodes, sACN
/// and DMX USB interfaces, each with a "kind", a "target" and its "ports" (a "universe" of the
/// project and an "address": the Port-Address of Art-Net, the universe of sACN), and a "network"
/// and a "priority" where they are not automatic and 100. They are written only for a project
/// that has some, and an older program passes over them, so they needed no new version either.
/// The notes and the independents may be missing: then there are no notes, and the keys are
/// on channels 1 to 32 and memories 1 to 32. That keeps the files from before these existed
/// readable. Version 2 added the sequences and the stack; a file of version 1 has neither, and
/// is still read. A sequence that does not say whether it loops is played in a loop, one that
/// does not say "random" in turn, and one without a speed at speed 1. Version 4 has 32 keys for
/// channels and 32 for memories among the independents; version 3 had 10 of each, versions 1
/// and 2 had 16 keys for channels and none for memories. The keys an older file does not have
/// are where they are on a new desk: key 11 on channel 11, and so on.
/// Version 6 has steps of the stack that add ("add": true) and releases ("target": "release",
/// "number": 0, "releases": "2.0", the number of the step it takes down). An older program would
/// play a step that adds as one that does not, and cannot read a release; so a file with either
/// says version 6, which an older program refuses with a clear message, and a file without them
/// still says version 5, which it reads.
/// Version 7 has "effects" (see <see cref="Effect"/>; their lights and groups named by their
/// places in the plan's lists, like the steps), the "effectIndependents" and steps of the stack
/// that play an effect ("target": "effect", "number": 3). An older program would drop the effects
/// unsaid when it saves, so a file with effects, such steps or effect keys bound otherwise than on a
/// new desk says version 7, and one without them stays version 5 or 6.
/// Version 8 has sequences whose steps add ("add": true), release ("target": "release", "number":
/// the step it takes down, counted from 1 in the sequence, 0 for none) or play an effect. Versions 6
/// and 7 have these in the stack only: a program that reads them would play such a sequence wrong
/// (a step that adds as one that does not) or refuse it with a message about the stack. So a file
/// with such a sequence says version 8, and one without stays as it was.
/// Version 9 has "sounds" (see <see cref="Sound"/>): music and sound effects, each with its "file"
/// (in the folder beside the project's file: "Show sounds" beside "Show.json") and its "hash"; and
/// steps of the stack and of sequences that play a sound ("target": "sound", "number": 3), which can
/// play on through the steps after them ("continue": true); and steps that are skipped ("disabled":
/// true), of the stack and of sequences. An older program would drop the sounds unsaid when it saves,
/// cannot read the steps of sounds and would play a skipped step, so a project with any of them says
/// version 9.
/// Version 10 has cues of the stack that come at a moment of a sound ("at": { "sound": 3, "seconds":
/// 31.25 }, see <see cref="Step.At"/>). An older program would let them wait for GO and drop the
/// moments when it saves, so a project with one says version 10, and one without stays as it was.
/// </remarks>
internal static partial class ProjectFormat
{
    private const string FormatName = "lumenet-project";

    // The newest version this program reads, and the one it writes for a project whose cues come at
    // moments of sounds (see the remarks). Version 9 has sounds; version 8 sequences that add, release
    // or play effects; version 7 effects; version 6 a stack with steps that add or release; version 5
    // the universes and the boxes.
    private const int CurrentVersion = 10;
    private const int VersionWithSounds = 9;
    private const int VersionWithSequencesLikeTheStack = 8;
    private const int VersionWithEffects = 7;
    private const int VersionWithAddOrRelease = 6;
    private const int VersionWithoutAddOrRelease = 5;

    // The channel keys of the independents in a file of version 1 or 2, and the keys of each
    // kind in a file of version 3.
    private const int FirstIndependentCount = 16;
    private const int ThirdIndependentCount = 10;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // Notes go into the file as they were typed, so that it stays readable. Without this
        // every letter beyond ASCII would be written as a six-character code instead.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Writes a project file: UTF-8 without a byte order mark, indented.</summary>
    /// <param name="stream">Receives the file.</param>
    /// <param name="project">The looks, the notes and the independents to write.</param>
    public static void Write(Stream stream, ProjectContent project)
    {
        StepPlaces places = StepPlaces.Of(project.Stage);
        bool addsOrReleases = project.Stack.Any(step => step.Step.Add || step.Step.IsRelease);
        bool effects = project.Effects.Any(effect => effect is not null)
            || project.Stack.Any(step => step.Step.IsEffect)
            || !project.EffectIndependents.SequenceEqual(Desk.DefaultEffectIndependents());
        bool sequencesLikeTheStack = project.Sequences.Any(sequence =>
            sequence is not null && sequence.Steps.Any(step => step.Add || !step.GivesLight));
        bool sounds = project.Sounds.Any(sound => sound is not null);
        bool soundKeys = !project.SoundIndependents.SequenceEqual(Desk.DefaultSoundIndependents());
        bool soundSteps = project.Stack.Any(step => step.Step.IsSound || step.Step.Disabled)
            || project.Sequences.Any(sequence => sequence is not null && sequence.Steps.Any(step => step.IsSound || step.Disabled));
        bool moments = project.Stack.Any(step => step.Step.At is not null);
        int version = moments ? CurrentVersion
            : sounds || soundSteps || soundKeys ? VersionWithSounds
            : sequencesLikeTheStack ? VersionWithSequencesLikeTheStack
            : effects ? VersionWithEffects
            : addsOrReleases ? VersionWithAddOrRelease
            : VersionWithoutAddOrRelease;
        var file = new ProjectFile
        {
            Format = FormatName,
            Version = version,
            Effects = effects ? project.Effects.OfType<Effect>().Select(effect => FileEffect(effect, places)).ToList<ProjectFileEffect?>() : null,
            EffectIndependents = effects ? [.. project.EffectIndependents] : null,
            Sounds = sounds ? project.Sounds.OfType<Sound>().Select(FileSound).ToList<ProjectFileSound?>() : null,
            SoundIndependents = soundKeys ? [.. project.SoundIndependents] : null,
            Universes = project.Universes,
            Boxes = project.Boxes.Length == 0
                ? null
                : project.Boxes.Select(box => new ProjectFileBox { Address = box.Address, Universes = [.. box.Universes] }).ToList<ProjectFileBox?>(),
            Outputs = project.Outputs.Length == 0 ? null : project.Outputs.Select(FileOutput).ToList<ProjectFileOutput?>(),
            Notes = project.Notes.Length == 0 ? null : project.Notes,
            Memories = [],
            ChannelNotes = NotesThatAreNotEmpty(project.ChannelNotes),
            MemoryNotes = NotesThatAreNotEmpty(project.MemoryNotes),
            Independents = project.Independents.ToList(),
            MemoryIndependents = project.MemoryIndependents.ToList(),
            Sequences = [],
            Stack = project.Stack.Select(step => FileStackStep(step, places)).ToList<ProjectFileStep?>(),
            Stage = FileStage(project.Stage, project.StagePicture),
        };

        for (int index = 0; index < project.Looks.Length; index++)
        {
            if (project.Looks[index] is { } look)
                file.Memories.Add(new ProjectFileMemory { Number = index + 1, Levels = LevelsAboveZero(look) });
        }

        for (int index = 0; index < project.Sequences.Length; index++)
        {
            if (project.Sequences[index] is { } sequence)
            {
                file.Sequences.Add(new ProjectFileSequence
                {
                    Number = index + 1,
                    Loop = sequence.Loop,
                    Random = sequence.Random,
                    Speed = sequence.Speed,
                    Steps = sequence.Steps.Select(step => FileStep(step, cue: null, places)).ToList<ProjectFileStep?>(),
                });
            }
        }

        JsonSerializer.Serialize(stream, file, Options);
    }

    /// <summary>A project file as text, as <see cref="Write"/> writes it: for Export, which hands it to the UI.</summary>
    /// <param name="project">The project.</param>
    public static string WriteText(ProjectContent project)
    {
        using var stream = new MemoryStream();
        Write(stream, project);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Reads a project file.</summary>
    /// <param name="stream">The file.</param>
    /// <returns>The looks, the notes and the independents, with an entry for every memory, fader and key.</returns>
    /// <exception cref="InvalidDataException">
    /// This is not a project file the program can use. The message says why.
    /// </exception>
    public static ProjectContent Read(Stream stream)
    {
        ProjectFile file = Parse(stream);
        int version = RequireFormat(file);
        int universes = UniversesOf(file);
        int channels = universes * Desk.UniverseSize;

        byte[]?[] looks = LooksOf(file, channels);
        (StagePlan? stage, string? picture) = StageOf(file.Stage);
        var content = new ProjectContent(
            looks,
            NotesOf(file.ChannelNotes, channels, "channel"),
            NotesOf(file.MemoryNotes, Desk.MemoryCount, "memory"),
            IndependentsOf(file.Independents, version, channels))
        {
            Universes = universes,
            Boxes = BoxesOf(file.Boxes),
            Outputs = OutputsOf(file.Outputs),
            MemoryIndependents = MemoryIndependentsOf(file.MemoryIndependents, version),
            Sequences = SequencesOf(file.Sequences, looks),
            Effects = EffectsOf(file.Effects),
            EffectIndependents = EffectIndependentsOf(file.EffectIndependents),
            Sounds = SoundsOf(file.Sounds),
            SoundIndependents = SoundIndependentsOf(file.SoundIndependents),
            Stack = StackOf(file.Stack),
            Stage = stage,
            StagePicture = picture,
            Notes = ProjectNotesOf(file.Notes),
        };

        // A step or a light beyond the project's universes: a file edited by hand.
        if (Desk.UseAbove(channels, content.Sequences, content.Stack.Select(step => (step.Cue, step.Step)), stage ?? StagePlan.Empty, content.Independents)
            is { } use)
        {
            throw Bad($"The project has {Desk.UniversesSaid(universes)}, but {use}.");
        }

        return content;
    }

    /// <summary>Reads a project file from its text, as Import hands it over.</summary>
    /// <param name="text">The project file.</param>
    /// <exception cref="InvalidDataException">This is not a project file the program can use. The message says why.</exception>
    public static ProjectContent ReadText(string text)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        return Read(stream);
    }

    // The same rules as for notes written on the UI.
    private static string ProjectNotesOf(string? notes)
    {
        try
        {
            return ProjectNotes.Checked(notes);
        }
        catch (DeskException e)
        {
            throw Bad($"The notes of the project in the file cannot be used. {e.Message}");
        }
    }

    private static ProjectFile Parse(Stream stream)
    {
        try
        {
            return JsonSerializer.Deserialize<ProjectFile>(stream, Options) ?? throw Bad("The file holds no project.");
        }
        catch (JsonException e)
        {
            // Not JSON at all, or for example a text where a number belongs. The JSON reader's
            // message names the place in the file.
            throw Bad($"The file is damaged: {e.Message}");
        }
    }

    // The version of the format the file has, once it is clear that this program can read it.
    private static int RequireFormat(ProjectFile file)
    {
        if (file.Format != FormatName)
            throw Bad("The file is not a Lumenet project.");
        if (file.Version is not int version || version < 1)
            throw Bad("The file does not say which version of the project format it has.");
        if (version > CurrentVersion)
        {
            throw Bad(
                $"The file has version {version} of the project format, and this program reads up to " +
                $"version {CurrentVersion}. A newer Lumenet wrote the file.");
        }

        return version;
    }

    private static InvalidDataException Bad(string message)
    {
        return new InvalidDataException(message);
    }
}
