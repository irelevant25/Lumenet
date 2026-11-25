namespace DmxBox.Server;

/// <summary>
/// The attributes of the lights of the stage plan (a pan, a tilt, a wheel, a shutter: see
/// <see cref="PlanRoles"/>) and what is done to the light at the end of the output: the limits of
/// the lights, the master and blackout.
/// </summary>
/// <remarks>
/// <para>
/// Latest takes precedence for an attribute: of the faders that hold it, the one that took it
/// last gives its value, as it is stored, not scaled by any fader. A channel fader takes its
/// channel when it is moved (by hand, by Load or by the stack). A memory fader takes every light
/// its memory holds when it leaves zero: a look holds the lights it gives any level, a sequence
/// those its steps use. When that fader goes back to zero, the one that took the light before
/// gives it again; with no memory left, the channel fader.
/// </para>
/// <para>
/// A sequence gives an attribute what its step that is on gives it, or the last step before
/// that one that gives it something. Every move that takes something gets the next number of
/// <see cref="_claims"/>; the highest number is the latest.
/// </para>
/// </remarks>
internal sealed partial class Desk
{
    // What the stage plan says about every channel. Made anew whenever the plan changes.
    private PlanRoles _roles = PlanRoles.None;

    private long _claims;
    private readonly long[] _channelClaims = new long[MaxChannels];
    private readonly long[] _memoryClaims = new long[MemoryCount];

    // What the steps of fixtures and groups give, by what makes it: target, number, value and
    // color. Made when first needed, and anew when the plan changes.
    private readonly Dictionary<(string, int, int, string?), FixtureLooks.Part[]> _lightLooks = [];

    // Call with the lock held. The plan of the project changes, and with it the roles of the channels.
    private void PutStage(StagePlan plan)
    {
        _stage = plan;
        _roles = new PlanRoles(plan);
        _lightLooks.Clear();
        ForgetEffectRigs();
        _outputChanges++;
    }

    // Call with the lock held. What a step of a fixture or a group gives, at its full value.
    private FixtureLooks.Part[] LookOfLights(Step step)
    {
        var key = (step.Target, step.Number, step.Value, step.Color);
        if (!_lightLooks.TryGetValue(key, out FixtureLooks.Part[]? parts))
            _lightLooks[key] = parts = FixtureLooks.Of(_stage, step);

        return parts;
    }

    // Call with the lock held. A channel fader was moved: from now on it gives its channel, if that is an attribute.
    private void ClaimChannel(int channel)
    {
        _channelClaims[channel - 1] = ++_claims;
    }

    // Call with the lock held. A memory fader left zero: it takes the lights its memory holds.
    private void ClaimMemory(int memory)
    {
        _memoryClaims[memory - 1] = ++_claims;
    }

    // Call with the lock held. What goes out of the channels that give light, as the faders gave
    // it in "output": each light held to its limit, scaled by the master, dark in blackout.
    private void FinishLight(byte[] output)
    {
        foreach (LimitRule limit in _roles.Limits)
            limit.Apply(output);

        bool[] attribute = _roles.Attribute;
        for (int channel = 0; channel < output.Length; channel++)
        {
            if (!attribute[channel])
                output[channel] = _blackout ? (byte)0 : Scale(output[channel], _master);
        }
    }

    // Call with the lock held. Puts the attributes of the lights into "output": what the fader
    // that took each last gives it. The memories in "leftOut" (held with SOLO DOWN) take nothing.
    private void PutAttributes(byte[] output, double now, HashSet<Fader>? leftOut)
    {
        foreach (AttributeLight light in _roles.AttributeLights)
        {
            int latest = LatestMemory(light, leftOut, sequencesOnly: false);
            foreach (int channel in light.Attributes)
            {
                byte? held = latest >= 0 && _memoryClaims[latest] > _channelClaims[channel]
                    ? AttributeOf(latest, channel, light, now)
                    : null;
                output[channel] = held ?? _channels[channel];
            }
        }
    }

    // Call with the lock held. For the UIs: what the running sequences give the attributes, the
    // sequence that took each light last; 0 where no sequence holds the light.
    private void PutSequenceAttributes(byte[] levels, double now)
    {
        foreach (AttributeLight light in _roles.AttributeLights)
        {
            int latest = LatestMemory(light, leftOut: null, sequencesOnly: true);
            foreach (int channel in light.Attributes)
                levels[channel] = latest >= 0 ? AttributeOf(latest, channel, light, now) ?? 0 : (byte)0;
        }
    }

    // Call with the lock held. The memory whose fader is up, holds the light and took it last;
    // -1 for none.
    private int LatestMemory(AttributeLight light, HashSet<Fader>? leftOut, bool sequencesOnly)
    {
        int latest = -1;
        for (int index = 0; index < MemoryCount; index++)
        {
            if (_memoryLevels[index] == 0 || (latest >= 0 && _memoryClaims[index] <= _memoryClaims[latest]))
                continue;
            if (leftOut?.Contains(new Fader(true, index + 1)) == true)
                continue;

            bool holds = _memories[index] is { } look
                ? !sequencesOnly && LookHolds(look, light)
                : _sequences[index] is { } sequence && SequenceHolds(sequence, light);
            if (holds)
                latest = index;
        }

        return latest;
    }

    // Call with the lock held. What the memory at "index" gives an attribute channel of a light it
    // holds: its look's level, or what its sequence gives; null if its sequence gives it nothing.
    private byte? AttributeOf(int index, int channel, AttributeLight light, double now)
    {
        if (_memories[index] is { } look)
            return look[channel];
        if (_sequences[index] is not { Steps.Length: > 0 } sequence)
            return null;

        // The step that is on, or else the last one before it that gives the channel something.
        int current = SequenceTimeline.CurrentStep(sequence, SequenceTime(index, now), _sequenceSeeds[index]);
        int count = sequence.Steps.Length;
        for (int back = 0; back < count; back++)
        {
            Step step = sequence.Steps[((current - back) % count + count) % count];
            if (step.IsLights)
            {
                foreach (FixtureLooks.Part part in LookOfLights(step))
                {
                    if (part.Channel == channel && part.Attribute)
                        return part.Value;
                }
            }
            else if (!step.IsMemory)
            {
                if (step.Number - 1 == channel)
                    return (byte)step.Value;
            }
            else if (_memories[step.Number - 1] is { } stepLook && LookHolds(stepLook, light))
            {
                return stepLook[channel];
            }
        }

        return null;
    }

    // A look holds a light when it gives any of its channels a level.
    private static bool LookHolds(byte[] look, AttributeLight light)
    {
        foreach (int channel in light.Channels)
        {
            if (look[channel] > 0)
                return true;
        }

        return false;
    }

    // Call with the lock held. A sequence holds a light when one of its steps is a channel of the
    // light, a memory whose look holds the light, or the light itself (or a group it is in).
    private bool SequenceHolds(Sequence sequence, AttributeLight light)
    {
        foreach (Step step in sequence.Steps)
        {
            bool holds = step.IsLights
                ? LookOfLights(step).Any(part => Array.IndexOf(light.Channels, part.Channel) >= 0)
                : step.IsMemory
                    ? _memories[step.Number - 1] is { } look && LookHolds(look, light)
                    : Array.IndexOf(light.Channels, step.Number - 1) >= 0;
            if (holds)
                return true;
        }

        return false;
    }
}
