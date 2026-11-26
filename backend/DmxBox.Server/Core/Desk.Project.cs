namespace DmxBox.Server.Core;

// The project on the desk: a new one, one loaded, one brought back by undo, and its universes.
internal partial class Desk
{
    /// <summary>
    /// Makes the desk clean for a new project: every channel fader and memory fader down to 0,
    /// every memory empty, an empty stack, no notes under the faders and none of the project, the
    /// independent keys back on channels 1 to 32 and memories 1 to 32. The master and blackout
    /// stay: they belong to the evening, not to the project. The stage plan belongs to the
    /// project: a new one has an empty stage, without lights and picture.
    /// </summary>
    /// <returns><see cref="ProjectChanges"/> right after the change.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public long StartNewProject()
    {
        lock (_lock)
        {
            RequireUnlocked();
            ZeroFaders();
            Array.Clear(_memories);
            Array.Clear(_sequences);
            _stack.Clear();
            StopStack();
            Array.Fill(_channelNotes, "");
            Array.Fill(_memoryNotes, "");
            DefaultIndependents().CopyTo(_independents, 0);
            DefaultMemoryIndependents().CopyTo(_memoryIndependents, 0);
            PutEffects([], DefaultEffectIndependents());
            PutSounds([], DefaultSoundIndependents());
            PutStage(StagePlan.Empty);
            _stagePicture = null;
            _notes = "";
            CountLookChange();
            return _projectChanges;
        }
    }

    /// <summary>
    /// Replaces everything a project file holds at once, which is what loading a project does:
    /// the looks and sequences of all memories, the stack, all notes, the bindings of the
    /// independent keys and the stage plan (a project without one has an empty stage). Every
    /// memory fader goes down to 0 and the stack stands before its first step, so that nothing
    /// of the new project appears on stage unasked. The channel faders stay.
    /// </summary>
    /// <param name="project">What the file held, with an entry for every memory, fader and key.</param>
    /// <returns><see cref="ProjectChanges"/> right after the change.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public long ReplaceProject(ProjectContent project)
    {
        RequireWhole(project);

        lock (_lock)
        {
            RequireUnlocked();
            PutProject(project);
            Array.Clear(_memoryLevels);
            StopEffects();
            StopSounds();
            CountLookChange();
            return _projectChanges;
        }
    }

    /// <summary>
    /// Puts back what the project held at an earlier moment, for undo and redo: the looks and
    /// sequences, the stack, the notes, the bindings of the independent keys and the stage plan.
    /// Unlike loading, the faders stay where they are.
    /// </summary>
    /// <param name="project">The project as <see cref="GetProject"/> gave it then.</param>
    /// <returns><see cref="ProjectChanges"/> right after the change.</returns>
    /// <exception cref="DeskException">The stack is live.</exception>
    public long RestoreProject(ProjectContent project)
    {
        RequireWhole(project);

        lock (_lock)
        {
            RequireUnlocked();
            PutProject(project);
            CountLookChange();
            return _projectChanges;
        }
    }

    /// <summary>
    /// Copies everything a project file holds, together with the change count it belongs to.
    /// </summary>
    public (ProjectContent Content, long Changes) GetProject()
    {
        lock (_lock)
        {
            // The looks as long as the project's channels go.
            byte[]?[] looks = new byte[]?[MemoryCount];
            for (int index = 0; index < MemoryCount; index++)
                looks[index] = _memories[index] is { } look ? look[..Channels] : null;

            var content = new ProjectContent(looks, _channelNotes[..Channels], Copy(_memoryNotes), Copy(_independents))
            {
                Universes = _universes,
                Boxes = _boxes,
                Outputs = _outputs,
                MemoryIndependents = Copy(_memoryIndependents),
                Sequences = Copy(_sequences),
                Effects = Copy(_effects),
                EffectIndependents = Copy(_effectIndependents),
                Sounds = Copy(_sounds),
                SoundIndependents = Copy(_soundIndependents),
                Stack = _stack.Select(entry => new ProjectStep(entry.Cue, entry.Step) { Releases = ReleasedCue(entry.Step) }).ToArray(),
                Stage = _stage,
                StagePicture = _stagePicture,
                Notes = _notes,
            };
            return (content, _projectChanges);
        }
    }

    // Call with the lock held. The looks and sequences, the stack, the notes, the bindings of the
    // independent keys and the stage plan with its picture of a project take the place of the
    // desk's; a project without a plan (a file from before plans, an imported show) has an empty
    // stage. The project's own arrays are copied where the desk changes its arrays in place, so
    // that what the project came from (a file, or an earlier moment kept for undo) stays as it
    // was. The plan is never changed in place.
    private void PutProject(ProjectContent project)
    {
        _universes = project.Universes;
        _boxes = project.Boxes;
        _outputs = project.Outputs;
        PutStage(project.Stage ?? StagePlan.Empty);
        _stagePicture = project.Stage is null ? null : project.StagePicture;
        _notes = project.Notes;

        for (int index = 0; index < MemoryCount; index++)
        {
            _memories[index] = project.Looks[index] is { } look ? Widened(look) : null;
            _sequences[index] = project.Sequences[index];
        }

        ReplaceStack(project.Stack);
        Array.Fill(_channelNotes, "");
        project.ChannelNotes.CopyTo(_channelNotes, 0);
        project.MemoryNotes.CopyTo(_memoryNotes, 0);
        project.Independents.CopyTo(_independents, 0);
        project.MemoryIndependents.CopyTo(_memoryIndependents, 0);
        PutEffects(project.Effects, project.EffectIndependents);
        PutSounds(project.Sounds, project.SoundIndependents);
        ClearAbove(Channels);
    }

    // Call with the lock held. The faders, claims and notes of channels that the project does not
    // have go back to nothing, so that they do not come back when universes are added again.
    private void ClearAbove(int channels)
    {
        Array.Clear(_channels, channels, MaxChannels - channels);
        Array.Clear(_channelClaims, channels, MaxChannels - channels);
        Array.Fill(_channelNotes, "", channels, MaxChannels - channels);
        foreach (byte[]? look in _memories)
        {
            if (look is not null)
                Array.Clear(look, channels, MaxChannels - channels);
        }
    }

    /// <summary>
    /// Changes how many universes the project has. More universes add channels, at 0 and without
    /// notes. Fewer take the channels of the universes that go away, with the levels the memories
    /// hold there and the notes of those channels (Undo brings them back); refused while a step,
    /// a light of the stage plan or a channel key of the independents is on such a channel.
    /// </summary>
    /// <param name="universes">1 to <see cref="MaxUniverses"/>.</param>
    /// <returns><see cref="ProjectChanges"/> right after the change.</returns>
    /// <exception cref="DeskException">Something is on a channel that would go, or the stack is live.</exception>
    public long SetUniverses(int universes)
    {
        RequireRange("number of universes", universes, 1, MaxUniverses);

        lock (_lock)
        {
            RequireUnlocked();
            if (universes == _universes)
                return _projectChanges;

            int channels = universes * UniverseSize;
            if (universes < _universes)
            {
                string? use = UseAbove(
                    channels, _sequences, _stack.Select(entry => (entry.Cue, entry.Step)), _stage, _independents);
                if (use is not null)
                {
                    string going = universes + 1 == _universes
                        ? $"Universe {_universes} is"
                        : $"Universes {universes + 1} to {_universes} are";
                    throw new DeskException($"{going} still in use: {use}. Take it out of there first.");
                }

                ClearAbove(channels);
            }

            _universes = universes;
            CountLookChange();
            return _projectChanges;
        }
    }

    // The first thing of a project that is on a channel above the given number, said for the
    // user: "step 2 of the stack is on channel 7 (U2)"; null if nothing is. Looks and notes do
    // not count: they simply end there.
    internal static string? UseAbove(
        int channels, Sequence?[] sequences, IEnumerable<(CueNumber Cue, Step Step)> stack, StagePlan stage, int[] independents)
    {
        for (int memory = 0; memory < sequences.Length; memory++)
        {
            Step[] steps = sequences[memory]?.Steps ?? [];
            for (int index = 0; index < steps.Length; index++)
            {
                if (steps[index].Target == Step.Channel && steps[index].Number > channels)
                    return $"step {index + 1} of the sequence of memory {memory + 1} is on channel {ChannelLabel(steps[index].Number)}";
            }
        }

        foreach ((CueNumber cue, Step step) in stack)
        {
            if (step.Target == Step.Channel && step.Number > channels)
                return $"cue {cue} of the stack is on channel {ChannelLabel(step.Number)}";
        }

        foreach (StageLight light in stage.Lights)
        {
            if (light.Address + light.Channels.Length - 1 > channels)
            {
                string which = light.Name.Length > 0 ? $"the light \"{light.Name}\"" : "a light";
                return $"{which} of the stage is at address {ChannelLabel(light.Address)}";
            }
        }

        for (int key = 0; key < independents.Length; key++)
        {
            if (independents[key] > channels)
                return $"channel key {key + 1} of the independents is on channel {ChannelLabel(independents[key])}";
        }

        return null;
    }

    // "1 universe", "2 universes".
    internal static string UniversesSaid(int universes) => universes == 1 ? "1 universe" : $"{universes} universes";

    // Call with the lock held: a channel step must be on a channel of the project's universes.
    private void RequireStepFits(Step step)
    {
        if (step.Target == Step.Channel && step.Number > Channels)
        {
            throw new DeskException(
                $"Channel {ChannelLabel(step.Number)} is not in the project, which has {UniversesSaid(_universes)}. " +
                "Add universes in Settings first.");
        }
    }
}
