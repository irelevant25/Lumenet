using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server;

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
/// </remarks>
internal static class ProjectFormat
{
    private const string FormatName = "lumenet-project";

    // The newest version this program reads, and the one it writes for a stack with steps that add or
    // release (see the remarks). Version 5 has the universes and the boxes.
    private const int CurrentVersion = 6;
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
        var file = new ProjectFile
        {
            Format = FormatName,
            Version = addsOrReleases ? CurrentVersion : VersionWithoutAddOrRelease,
            Universes = project.Universes,
            Boxes = project.Boxes.Length == 0
                ? null
                : project.Boxes.Select(box => new ProjectFileBox { Address = box.Address, Universes = [.. box.Universes] }).ToList<ProjectFileBox?>(),
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
            MemoryIndependents = MemoryIndependentsOf(file.MemoryIndependents, version),
            Sequences = SequencesOf(file.Sequences, looks),
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

    /// <summary>
    /// Reads a stage plan on its own, as a project file has it under "stage": what Import hands over
    /// to put the stage of another project in place of this one's.
    /// </summary>
    /// <param name="text">The plan as JSON.</param>
    /// <returns>The plan, its lights numbered from 1 in the order of the file, and its picture; null for none.</returns>
    /// <exception cref="InvalidDataException">This is no stage plan the program can use. The message says why.</exception>
    public static (StagePlan Stage, string? Picture) ReadStage(string text)
    {
        ProjectFileStage? stage;
        try
        {
            stage = JsonSerializer.Deserialize<ProjectFileStage>(text, Options);
        }
        catch (JsonException e)
        {
            throw Bad($"The stage is damaged: {e.Message}");
        }

        (StagePlan? plan, string? picture) = StageOf(stage);
        return (plan ?? throw Bad("There is no stage in it."), picture);
    }

    // The stage plan and its picture, with the same rules as for a plan made on the UI; none for
    // a file without a plan. The lights, the kinds of lights and the groups are numbered from 1
    // in the order of the file, which is how they name each other there.
    private static (StagePlan? Stage, string? Picture) StageOf(ProjectFileStage? stage)
    {
        if (stage is null)
            return (null, null);

        List<ProjectFileLight?> listed = stage.Lights ?? [];
        var lights = new StageLight[listed.Count];
        for (int index = 0; index < listed.Count; index++)
        {
            ProjectFileLight light = listed[index] ?? throw Bad($"Light {index + 1} of the stage plan in the file is empty.");
            if (light.Address is not int address || light.Channels is null)
                throw Bad($"Light {index + 1} of the stage plan in the file needs an address and its channels.");

            lights[index] = new StageLight(
                index + 1, light.Name ?? "", address, light.Channels, light.Color ?? light.EarlierColor, light.X ?? 0, light.Y ?? 0, light.Angle ?? 0)
            {
                Shape = light.Shape ?? StageLight.Round,
                Width = light.Width ?? 1,
                Depth = light.Depth ?? 1,
                Type = light.Type,
                Limit = light.Limit ?? StageLight.NoLimit,
                Note = light.Note ?? "",
            };
        }

        try
        {
            string? picture = StagePicture.Checked(stage.Picture);
            StagePlan plan = new StagePlan(stage.Width ?? StagePlan.DefaultWidth, stage.Depth ?? StagePlan.DefaultDepth, lights)
            {
                PictureId = picture is null ? null : StagePicture.IdOf(picture),
                PictureOpacity = stage.PictureOpacity ?? StagePlan.DefaultPictureOpacity,
                PictureOver = stage.PictureOver ?? StagePlan.PictureOverStage,
                Types = TypesOf(stage.Types),
                Groups = [.. (stage.Groups ?? []).Select((group, index) =>
                    new LightGroup(index + 1, group?.Name ?? "", [.. group?.Lights ?? []]) { Note = group?.Note ?? "" })],
            };
            return (plan.Checked(), picture);
        }
        catch (DeskException e)
        {
            throw Bad($"The stage plan in the file cannot be used. {e.Message}");
        }
    }

    // The kinds of lights from the fixture library, numbered from 1 in the order of the file.
    private static FixtureType[] TypesOf(List<ProjectFileType?>? listed)
    {
        return [.. (listed ?? []).Select((type, index) => type is null
            ? throw new DeskException($"Kind {index + 1} of the lights is empty.")
            : new FixtureType(
                index + 1,
                type.Maker ?? "",
                type.Model ?? "",
                type.Mode ?? "",
                type.Source ?? "",
                [.. (type.Channels ?? []).Select((channel, number) => channel is null
                    ? throw new DeskException($"Kind {index + 1} of the lights: channel {number + 1} is empty.")
                    : new FixtureChannel(channel.Name ?? "", channel.Code ?? "")
                    {
                        Fine = channel.Fine,
                        Ranges = [.. (channel.Ranges ?? []).Select(range =>
                            new FixtureRange(range?.From ?? 0, range?.To ?? 0, range?.Label ?? "") { Color = range?.Color })],
                    })]))];
    }

    private static ProjectFileStage? FileStage(StagePlan? stage, string? picture)
    {
        if (stage is null)
            return null;

        // In the file the lights and the kinds are named by their place in its lists, from 1.
        Dictionary<int, int> typePlaces = stage.Types.Select((type, index) => (type.Id, Place: index + 1)).ToDictionary();
        Dictionary<int, int> lightPlaces = stage.Lights.Select((light, index) => (light.Id, Place: index + 1)).ToDictionary();

        return new ProjectFileStage
        {
            Width = stage.Width,
            Depth = stage.Depth,
            Lights = stage.Lights.Select(light => new ProjectFileLight
            {
                Name = light.Name,
                Note = light.Note == "" ? null : light.Note,
                Address = light.Address,
                Channels = light.Channels,
                Color = light.Color,
                X = light.X,
                Y = light.Y,
                Angle = light.Angle,
                Shape = light.Shape,
                Width = light.Width,
                Depth = light.Depth,
                Type = light.Type is int type && typePlaces.TryGetValue(type, out int place) ? place : null,
                Limit = light.Limit < StageLight.NoLimit ? light.Limit : null,
            }).ToList<ProjectFileLight?>(),
            // the picture and how it shows only for a plan that has one
            Picture = picture,
            PictureOpacity = picture is null ? null : stage.PictureOpacity,
            PictureOver = picture is null ? null : stage.PictureOver,
            Types = stage.Types.Length == 0 ? null : stage.Types.Select(type => new ProjectFileType
            {
                Maker = type.Maker,
                Model = type.Model,
                Mode = type.Mode,
                Source = type.Source,
                Channels = type.Channels.Select(channel => new ProjectFileTypeChannel
                {
                    Name = channel.Name,
                    Code = channel.Code,
                    Fine = channel.Fine,
                    Ranges = channel.Ranges.Length == 0
                        ? null
                        : channel.Ranges.Select(range => new ProjectFileRange
                        {
                            From = range.From,
                            To = range.To,
                            Label = range.Label,
                            Color = range.Color,
                        }).ToList<ProjectFileRange?>(),
                }).ToList<ProjectFileTypeChannel?>(),
            }).ToList<ProjectFileType?>(),
            Groups = stage.Groups.Length == 0 ? null : stage.Groups.Select(group => new ProjectFileGroup
            {
                Name = group.Name,
                Note = group.Note == "" ? null : group.Note,
                Lights = [.. group.Lights.Where(lightPlaces.ContainsKey).Select(id => lightPlaces[id])],
            }).ToList<ProjectFileGroup?>(),
        };
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

    // The universes of the project: 1 for a file before version 5.
    private static int UniversesOf(ProjectFile file)
    {
        int universes = file.Universes ?? 1;
        if (universes < 1 || universes > Desk.MaxUniverses)
            throw Bad($"The file says {universes} universes; a project has 1 to {Desk.MaxUniverses}.");

        return universes;
    }

    // The boxes the project lists; none when the file lists none.
    private static BoxPlan[] BoxesOf(List<ProjectFileBox?>? listed)
    {
        if (listed is null)
            return [];
        if (listed.Count > BoxPlan.MaxBoxes)
            throw Bad($"The file lists {listed.Count} boxes; a project can have {BoxPlan.MaxBoxes}.");

        var boxes = new List<BoxPlan>();
        for (int index = 0; index < listed.Count; index++)
        {
            ProjectFileBox box = listed[index] ?? throw Bad("The list of boxes in the file has an empty entry.");
            try
            {
                BoxPlan plan = new BoxPlan(box.Address ?? "", [.. box.Universes ?? []]).Checked($"Box {index + 1} of the file");
                if (boxes.Any(other => other.Address == plan.Address))
                    throw Bad($"The file lists the box at {plan.Address} twice.");
                boxes.Add(plan);
            }
            catch (DeskException e)
            {
                throw Bad(e.Message);
            }
        }

        return [.. boxes];
    }

    // One entry per memory: its look, or null for a memory the file does not list.
    private static byte[]?[] LooksOf(ProjectFile file, int channels)
    {
        List<ProjectFileMemory?> memories = file.Memories ?? throw Bad("The file has no list of memories.");

        byte[]?[] looks = new byte[]?[Desk.MemoryCount];
        foreach (ProjectFileMemory? memory in memories)
        {
            if (memory is null)
                throw Bad("The list of memories in the file has an empty entry.");

            int number = NumberOf(memory);
            if (looks[number - 1] is not null)
                throw Bad($"The file lists memory {number} twice.");

            looks[number - 1] = LookOf(memory, number, channels);
        }

        return looks;
    }

    private static int NumberOf(ProjectFileMemory memory)
    {
        if (memory.Number is not int number)
            throw Bad("The file has a memory without a number.");
        if (number < 1 || number > Desk.MemoryCount)
            throw Bad($"The file has a memory {number}; memories go from 1 to {Desk.MemoryCount}.");

        return number;
    }

    private static byte[] LookOf(ProjectFileMemory memory, int number, int channels)
    {
        SortedDictionary<int, int> levels = memory.Levels ?? throw Bad($"Memory {number} in the file has no levels.");

        byte[] look = new byte[channels];
        foreach ((int channel, int level) in levels)
        {
            if (channel < 1 || channel > channels)
            {
                throw Bad(
                    $"Memory {number} in the file has a channel {channel}; " +
                    $"the project's channels go from 1 to {channels}.");
            }

            if (level < 0 || level > Desk.MaxLevel)
            {
                throw Bad(
                    $"Memory {number} in the file has level {level} for channel {channel}; " +
                    $"levels go from 0 to {Desk.MaxLevel}.");
            }

            look[channel - 1] = (byte)level;
        }

        return look;
    }

    // One note per fader, empty where the file lists none. "fader" says which faders these
    // are, "channel" or "memory", for the messages.
    private static string[] NotesOf(SortedDictionary<int, string?>? listed, int faderCount, string fader)
    {
        string[] notes = new string[faderCount];
        Array.Fill(notes, "");
        if (listed is null)
            return notes;

        foreach ((int number, string? text) in listed)
        {
            if (number < 1 || number > faderCount)
                throw Bad($"The file has a note for {fader} {number}; these numbers go from 1 to {faderCount}.");

            notes[number - 1] = NoteOf(text, fader, number);
        }

        return notes;
    }

    // The same rules as for a note typed on the UI, so that a file cannot bring in a note
    // that the desk itself would refuse.
    private static string NoteOf(string? text, string fader, int number)
    {
        if (text is null)
            throw Bad($"The note for {fader} {number} in the file is not a text.");

        try
        {
            return Note.Clean(text);
        }
        catch (DeskException e)
        {
            throw Bad($"The note for {fader} {number} in the file cannot be used. {e.Message}");
        }
    }

    // The channel of each channel key of the independents. A file may leave the list out: then
    // the keys are where they are on a new desk. A list that is there has to be whole for its
    // version: 32 keys, 10 in a file of version 3, 16 in one of version 1 or 2. The keys an older
    // file does not have are where they are on a new desk.
    private static int[] IndependentsOf(List<int>? listed, int version, int channels)
    {
        if (listed is null)
            return Desk.DefaultIndependents();

        int count = version switch
        {
            < 3 => FirstIndependentCount,
            3 => ThirdIndependentCount,
            _ => Desk.ChannelIndependentCount,
        };
        if (listed.Count != count)
            throw Bad($"The file lists {listed.Count} independents; there must be {count}.");

        CheckKeys(listed, "Independent", ("channel", "channels"), channels);
        return [.. listed, .. Desk.DefaultIndependents().Skip(count)];
    }

    // The memory of each memory key of the independents, like the channels above. Files from
    // before version 3 have none: then the keys are where they are on a new desk.
    private static int[] MemoryIndependentsOf(List<int>? listed, int version)
    {
        if (listed is null)
            return Desk.DefaultMemoryIndependents();

        int count = version == 3 ? ThirdIndependentCount : Desk.MemoryIndependentCount;
        if (listed.Count != count)
            throw Bad($"The file lists {listed.Count} memory independents; there must be {count}.");

        CheckKeys(listed, "Memory independent", ("memory", "memories"), Desk.MemoryCount);
        return [.. listed, .. Desk.DefaultMemoryIndependents().Skip(count)];
    }

    // "key" names the keys and "fader" what they are bound to, one and many, for the message.
    private static void CheckKeys(List<int> listed, string key, (string One, string Many) fader, int highest)
    {
        for (int index = 0; index < listed.Count; index++)
        {
            if (listed[index] < 1 || listed[index] > highest)
            {
                throw Bad(
                    $"{key} {index + 1} in the file is on {fader.One} {listed[index]}; " +
                    $"{fader.Many} go from 1 to {highest}.");
            }
        }
    }

    // One entry per memory: its sequence, or null for a memory the file lists none for. A file
    // from before there were sequences has no list at all.
    private static Sequence?[] SequencesOf(List<ProjectFileSequence?>? listed, byte[]?[] looks)
    {
        var sequences = new Sequence?[Desk.MemoryCount];
        foreach (ProjectFileSequence? entry in listed ?? [])
        {
            if (entry?.Number is not int number || number < 1 || number > Desk.MemoryCount)
                throw Bad($"The file has a sequence without the number of its memory, from 1 to {Desk.MemoryCount}.");
            if (sequences[number - 1] is not null || looks[number - 1] is not null)
                throw Bad($"The file lists memory {number} twice.");

            List<ProjectFileStep?> steps = entry.Steps ?? throw Bad($"The sequence of memory {number} in the file has no steps list.");
            if (steps.Count > Sequence.MaxSteps)
                throw Bad($"The sequence of memory {number} in the file has {steps.Count} steps; there can be {Sequence.MaxSteps}.");

            string where = $"of the sequence of memory {number}";
            sequences[number - 1] = new Sequence(
                entry.Loop ?? Sequence.DefaultLoop,
                steps.Select((step, index) => StepOf(step, $"Step {index + 1} {where}", inStack: false)).ToArray())
            {
                Random = entry.Random ?? false,
                Speed = SpeedOf(entry.Speed, number),
            };
        }

        return sequences;
    }

    // The same rules as for a speed set on the UI. A file that does not say means 1.
    private static double SpeedOf(double? speed, int memory)
    {
        try
        {
            return Sequence.CheckedSpeed(speed ?? 1);
        }
        catch (DeskException e)
        {
            throw Bad($"The sequence of memory {memory} in the file cannot be used. {e.Message}");
        }
    }

    // The steps of the stack in the order of their numbers.
    private static ProjectStep[] StackOf(List<ProjectFileStep?>? listed)
    {
        var steps = new List<ProjectStep>();
        foreach (ProjectFileStep? entry in listed ?? [])
        {
            string where = $"Step {entry?.Cue ?? "without a number"} of the stack";
            CueNumber cue;
            try
            {
                cue = CueNumber.Parse(entry?.Cue);
            }
            catch (DeskException e)
            {
                throw Bad($"{where} in the file cannot be used. {e.Message}");
            }

            if (steps.Any(step => step.Cue == cue))
                throw Bad($"The file lists step {cue} of the stack twice.");

            // A release names its step by that step's number ("releases"); its own "number" is not
            // used. One whose step the stack does not have releases nothing, as after taking it out.
            Step step = StepOf(entry, where, inStack: true);
            CueNumber? releases = null;
            if (step.IsRelease)
            {
                step = step with { Number = 0 };
                try
                {
                    releases = entry?.Releases is { } released ? CueNumber.Parse(released) : null;
                }
                catch (DeskException e)
                {
                    throw Bad($"{where} in the file cannot be used: the step it releases has no proper number. {e.Message}");
                }
            }

            steps.Add(new ProjectStep(cue, step) { Releases = releases });
        }

        if (steps.Count > Desk.MaxStackSteps)
            throw Bad($"The stack in the file has {steps.Count} steps; there can be {Desk.MaxStackSteps}.");

        return steps.OrderBy(step => step.Cue).ToArray();
    }

    // The same rules as for a step made on the UI, so that a file cannot bring in a step that
    // the desk itself would refuse. "where" names the step for the message. A step of the stack
    // may add or release; one of a sequence does neither.
    private static Step StepOf(ProjectFileStep? entry, string where, bool inStack)
    {
        if (entry is null || entry.Target is null || entry.Number is null || entry.Value is null)
            throw Bad($"{where} in the file is not complete: it needs a target, a number and a value.");

        try
        {
            var step = new Step(entry.Target, entry.Number.Value, entry.Value.Value, entry.Up ?? 0, entry.Wait ?? 0, entry.Down ?? 0)
            {
                Color = entry.Color,
                Add = inStack && entry.Add == true,
            };
            return inStack ? step.Checked() : step.CheckedForSequence();
        }
        catch (DeskException e)
        {
            throw Bad($"{where} in the file cannot be used. {e.Message}");
        }
    }

    // A step of the stack as the file has it: with its number, and whether it adds or what it releases.
    private static ProjectFileStep FileStackStep(ProjectStep step, StepPlaces places)
    {
        ProjectFileStep file = FileStep(step.Step, step.Cue.ToString(), places);
        if (step.Step.Add)
            file.Add = true;
        if (step.Step.IsRelease)
        {
            file.Number = 0;
            file.Releases = step.Releases?.ToString();
        }

        return file;
    }

    // A step as the file has it. "places" gives a fixture or a group its place in the file's
    // lists (from 1); 0 for one the plan does not have any more.
    private static ProjectFileStep FileStep(Step step, string? cue, StepPlaces places)
    {
        int number = step.Target switch
        {
            Step.Fixture => places.Lights.GetValueOrDefault(step.Number),
            Step.Group => places.Groups.GetValueOrDefault(step.Number),
            _ => step.Number,
        };
        return new ProjectFileStep
        {
            Cue = cue,
            Target = step.Target,
            Number = number,
            Value = step.Value,
            Up = step.Up,
            Wait = step.Wait,
            Down = step.Down,
            Color = step.Color,
        };
    }

    // Where the lights and the groups of a plan stand in the file's lists, by their numbers.
    private sealed record StepPlaces(Dictionary<int, int> Lights, Dictionary<int, int> Groups)
    {
        public static StepPlaces Of(StagePlan? plan) => new(
            (plan?.Lights ?? []).Select((light, index) => (light.Id, index + 1)).ToDictionary(),
            (plan?.Groups ?? []).Select((group, index) => (group.Id, index + 1)).ToDictionary());
    }

    // Channel number and level of every channel that is not at 0.
    private static SortedDictionary<int, int> LevelsAboveZero(byte[] look)
    {
        var levels = new SortedDictionary<int, int>();
        for (int index = 0; index < look.Length; index++)
        {
            if (look[index] > 0)
                levels.Add(index + 1, look[index]);
        }

        return levels;
    }

    // Fader number and text of every fader that has a note.
    private static SortedDictionary<int, string?> NotesThatAreNotEmpty(string[] notes)
    {
        var listed = new SortedDictionary<int, string?>();
        for (int index = 0; index < notes.Length; index++)
        {
            if (notes[index] != "")
                listed.Add(index + 1, notes[index]);
        }

        return listed;
    }

    private static InvalidDataException Bad(string message)
    {
        return new InvalidDataException(message);
    }
}
