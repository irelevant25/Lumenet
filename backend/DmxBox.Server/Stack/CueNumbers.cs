namespace DmxBox.Server.Stack;

/// <summary>
/// The rules by which the steps of the stack get their numbers. The aim of all of them: a
/// step keeps the number it has unless it has to give way.
/// </summary>
internal static class CueNumbers
{
    /// <summary>The number for a new step at the end: the next whole number.</summary>
    /// <param name="steps">The steps there are, in the order of their numbers.</param>
    /// <exception cref="DeskException">The numbers have reached their end.</exception>
    public static CueNumber ForNew(IReadOnlyList<(int Id, CueNumber Cue)> steps)
    {
        if (steps.Count == 0)
            return new CueNumber(1, 0);
        if (steps[^1].Cue.Major >= CueNumber.MaxMajor)
            throw new DeskException($"There is no whole number after {CueNumber.MaxMajor}. Give a step a lower number first.");

        return new CueNumber(steps[^1].Cue.Major + 1, 0);
    }

    /// <summary>
    /// Puts a step right after another one. It gets the number of that step with the digit
    /// after the dot one higher: moved between 2.0 and 3.0 it becomes 2.1. If the step that
    /// follows has that number already, it gives way by one digit, and so do the steps that
    /// stand directly behind it without a gap.
    /// </summary>
    /// <param name="steps">The steps in the order of their numbers.</param>
    /// <param name="id">The step to move.</param>
    /// <param name="afterId">The step it goes behind; null puts it at the very top, where it becomes 0.1.</param>
    /// <returns>The new number of every step whose number changes. Empty if the step is there already.</returns>
    /// <exception cref="DeskException">One of the steps is not there, or no number is free at that place.</exception>
    public static Dictionary<int, CueNumber> Move(IReadOnlyList<(int Id, CueNumber Cue)> steps, int id, int? afterId)
    {
        int from = IndexOf(steps, id);
        int after = afterId is int given ? IndexOf(steps, given) : -1;
        var changes = new Dictionary<int, CueNumber>();
        if (afterId == id || after == from - 1)
            return changes;   // dropped where it stands

        List<(int Id, CueNumber Cue)> others = steps.Where(step => step.Id != id).ToList();
        int before = afterId is null ? -1 : others.FindIndex(step => step.Id == afterId);
        CueNumber previous = before >= 0 ? others[before].Cue : new CueNumber(0, 0);
        if (before < 0 && others.Count > 0 && others[0].Cue <= previous)
            throw new DeskException($"There is no number before {others[0].Cue}. Give that step another number first.");

        changes[id] = OneDigitOn(previous);
        MakeWay(others, before + 1, changes[id], changes);
        return changes;
    }

    /// <summary>
    /// Gives a step the number its owner typed. If another step has that number, it gives way:
    /// for a whole number like 3.0 all steps that start with 3 become 4.x (and those that start
    /// with 4 become 5.x, as far as needed); for a number like 3.1 the step that has it and
    /// those directly behind it move up by one digit.
    /// </summary>
    /// <param name="steps">The steps in the order of their numbers.</param>
    /// <param name="id">The step that gets the number.</param>
    /// <param name="wanted">The number.</param>
    /// <returns>The new number of every step whose number changes. Empty if the step has the number already.</returns>
    /// <exception cref="DeskException">The step is not there, or the others cannot give way.</exception>
    public static Dictionary<int, CueNumber> Renumber(IReadOnlyList<(int Id, CueNumber Cue)> steps, int id, CueNumber wanted)
    {
        int index = IndexOf(steps, id);
        var changes = new Dictionary<int, CueNumber>();
        if (steps[index].Cue == wanted)
            return changes;

        List<(int Id, CueNumber Cue)> others = steps.Where(step => step.Id != id).ToList();
        int holder = others.FindIndex(step => step.Cue == wanted);
        if (holder >= 0 && wanted.Minor == 0)
            MoveWholeNumbersOn(others, wanted.Major, changes);
        else if (holder >= 0)
            MakeWay(others, holder, wanted, changes);

        changes[id] = wanted;
        return changes;
    }

    // The steps from "start" on that stand in the way of "taken" move up by one digit each,
    // as far as they follow each other without a gap.
    private static void MakeWay(List<(int Id, CueNumber Cue)> others, int start, CueNumber taken, Dictionary<int, CueNumber> changes)
    {
        for (int index = start; index < others.Count && others[index].Cue == taken; index++)
        {
            taken = OneDigitOn(taken);
            changes[others[index].Id] = taken;
        }
    }

    // All steps whose number starts with "major" get the next whole number in front, and so do
    // those that already had that one, until a whole number that nobody uses.
    private static void MoveWholeNumbersOn(List<(int Id, CueNumber Cue)> others, int major, Dictionary<int, CueNumber> changes)
    {
        int free = major;
        while (others.Any(step => step.Cue.Major == free))
            free++;
        if (free > CueNumber.MaxMajor)
            throw new DeskException($"There is no whole number after {CueNumber.MaxMajor} for the steps that would have to move on.");

        foreach ((int stepId, CueNumber cue) in others)
        {
            if (cue.Major >= major && cue.Major < free)
                changes[stepId] = cue with { Major = cue.Major + 1 };
        }
    }

    private static CueNumber OneDigitOn(CueNumber number)
    {
        if (number.Minor >= CueNumber.MaxMinor)
            throw new DeskException($"There is no free number after {number}. Give a step another number first.");

        return number with { Minor = number.Minor + 1 };
    }

    private static int IndexOf(IReadOnlyList<(int Id, CueNumber Cue)> steps, int id)
    {
        for (int index = 0; index < steps.Count; index++)
        {
            if (steps[index].Id == id)
                return index;
        }

        throw new DeskException("That step is not in the stack any more.");
    }
}
