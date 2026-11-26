using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server.Projects;

// The sequences and the stack in a project file: their steps and the speed of a sequence.
internal static partial class ProjectFormat
{
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

            // A release names a step of its sequence by its number. One that names itself or a step
            // the sequence does not have releases nothing, as in the stack.
            string where = $"of the sequence of memory {number}";
            Step[] read = [.. steps.Select((step, index) => StepOf(step, $"Step {index + 1} {where}"))];
            for (int index = 0; index < read.Length; index++)
            {
                if (read[index].At is not null)
                    throw Bad($"Step {index + 1} {where} in the file comes at a moment of a sound: only a cue of the stack can.");
                if (read[index].IsRelease && (read[index].Number > read.Length || read[index].Number == index + 1))
                    read[index] = read[index] with { Number = 0 };
            }

            sequences[number - 1] = new Sequence(entry.Loop ?? Sequence.DefaultLoop, read)
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
            string where = $"Cue {entry?.Cue ?? "without a number"} of the stack";
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
                throw Bad($"The file lists cue {cue} of the stack twice.");

            // A release names its step by that step's number ("releases"); its own "number" is not
            // used. One whose step the stack does not have releases nothing, as after taking it out.
            Step step = StepOf(entry, where);
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
                    throw Bad($"{where} in the file cannot be used: the cue it releases has no proper number. {e.Message}");
                }
            }

            steps.Add(new ProjectStep(cue, step) { Releases = releases });
        }

        if (steps.Count > Desk.MaxStackSteps)
            throw Bad($"The stack in the file has {steps.Count} cues; there can be {Desk.MaxStackSteps}.");

        return steps.OrderBy(step => step.Cue).ToArray();
    }

    // The same rules as for a step made on the UI, so that a file cannot bring in a step that
    // the desk itself would refuse. "where" names the step for the message.
    private static Step StepOf(ProjectFileStep? entry, string where)
    {
        if (entry is null || entry.Target is null || entry.Number is null || entry.Value is null)
            throw Bad($"{where} in the file is not complete: it needs a target, a number and a value.");

        try
        {
            var step = new Step(entry.Target, entry.Number.Value, entry.Value.Value, entry.Up ?? 0, entry.Wait ?? 0, entry.Down ?? 0)
            {
                Color = entry.Color,
                Add = entry.Add == true,
                Continue = entry.Continue == true,
                Disabled = entry.Disabled == true,
                At = entry.At is { } at ? new SoundTime(at.Sound ?? 0, at.Seconds ?? double.NaN) : null,
            };
            return step.Checked();
        }
        catch (DeskException e)
        {
            throw Bad($"{where} in the file cannot be used. {e.Message}");
        }
    }

    // A step of the stack as the file has it: with its number, and what it releases.
    private static ProjectFileStep FileStackStep(ProjectStep step, StepPlaces places)
    {
        ProjectFileStep file = FileStep(step.Step, step.Cue.ToString(), places);
        if (step.Step.IsRelease)
        {
            file.Number = 0;
            file.Releases = step.Releases?.ToString();
        }

        return file;
    }

    // A step as the file has it, and whether it adds. "places" gives a fixture or a group its place
    // in the file's lists (from 1); 0 for one the plan does not have any more. A release of a
    // sequence names its step by its number in the sequence, which needs no translation.
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
            Add = step.Add ? true : null,
            Continue = step.Continue ? true : null,
            Disabled = step.Disabled ? true : null,
            At = step.At is { } at ? new ProjectFileMoment { Sound = at.Sound, Seconds = at.Seconds } : null,
        };
    }

    // Where the lights and the groups of a plan stand in the file's lists, by their numbers.
    private sealed record StepPlaces(Dictionary<int, int> Lights, Dictionary<int, int> Groups)
    {
        public static StepPlaces Of(StagePlan? plan) => new(
            (plan?.Lights ?? []).Select((light, index) => (light.Id, index + 1)).ToDictionary(),
            (plan?.Groups ?? []).Select((group, index) => (group.Id, index + 1)).ToDictionary());
    }
}
