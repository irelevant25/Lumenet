using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server.Projects;

// The sounds in a project file, and their keys of the independents.
internal static partial class ProjectFormat
{
    // A sound as the file has it: only what differs from how a sound is unless it says otherwise.
    private static ProjectFileSound FileSound(Sound sound)
    {
        return new ProjectFileSound
        {
            Number = sound.Number,
            Name = sound.Name,
            File = sound.File,
            Playlist = sound.Playlist,
            Hash = sound.IsTrack ? null : sound.Hash,
            Seconds = sound.Seconds > 0 ? sound.Seconds : null,
            Start = sound.Start > 0 ? sound.Start : null,
            End = sound.End,
            Loop = sound.Loop ? true : null,
            Decibels = sound.Decibels != 0 ? sound.Decibels : null,
            FadeIn = sound.FadeIn > 0 ? sound.FadeIn : null,
            FadeOut = sound.FadeOut > 0 ? sound.FadeOut : null,
            Pan = sound.Pan != 0 ? sound.Pan : null,
            Speed = sound.Speed != 1 ? sound.Speed : null,
            Pitch = sound.Pitch != 0 ? sound.Pitch : null,
            Echo = sound.Echo > 0 ? sound.Echo : null,
            EchoTime = sound.Echo > 0 ? sound.EchoTime : null,
            EchoRepeats = sound.Echo > 0 ? sound.EchoRepeats : null,
            Reverb = sound.Reverb > 0 ? sound.Reverb : null,
            Room = sound.Reverb > 0 ? sound.Room : null,
        };
    }

    // One entry per sound number: the sound, or null for one the file does not list. A file from
    // before there were sounds has none.
    private static Sound?[] SoundsOf(List<ProjectFileSound?>? listed)
    {
        var sounds = new Sound?[Sound.MaxSounds];
        var defaults = new Sound(1, "", "x.wav", "");
        foreach (ProjectFileSound? entry in listed ?? [])
        {
            if (entry?.Number is not int number || number < 1 || number > Sound.MaxSounds)
                throw Bad($"The file has a sound without a number from 1 to {Sound.MaxSounds}.");
            if (sounds[number - 1] is not null)
                throw Bad($"The file lists sound {number} twice.");

            try
            {
                // "category" (music or a sound effect) of a file of before means nothing now
                sounds[number - 1] = new Sound(number, entry.Name ?? "", entry.File ?? "", entry.Hash ?? "")
                {
                    Playlist = entry.Playlist,
                    Seconds = entry.Seconds ?? 0,
                    Start = entry.Start ?? 0,
                    End = entry.End,
                    Loop = entry.Loop ?? false,
                    Decibels = entry.Decibels ?? (entry.Volume is int percent
                        ? Math.Round(Math.Min(Sound.MaxDecibels, Sound.DecibelsOf(percent / 100.0)), 1)
                        : defaults.Decibels),
                    FadeIn = entry.FadeIn ?? 0,
                    FadeOut = entry.FadeOut ?? 0,
                    Pan = entry.Pan ?? 0,
                    Speed = entry.Speed ?? defaults.Speed,
                    Pitch = entry.Pitch ?? 0,
                    Echo = entry.Echo ?? 0,
                    EchoTime = entry.EchoTime ?? defaults.EchoTime,
                    EchoRepeats = entry.EchoRepeats ?? defaults.EchoRepeats,
                    Reverb = entry.Reverb ?? 0,
                    Room = entry.Room ?? defaults.Room,
                }.Checked();
            }
            catch (DeskException e)
            {
                throw Bad($"Sound {number} in the file cannot be used. {e.Message}");
            }
        }

        return sounds;
    }

    // The sound of each sound key of the independents; a file without them has them where they are on a new desk.
    private static int[] SoundIndependentsOf(List<int>? listed)
    {
        if (listed is null)
            return Desk.DefaultSoundIndependents();
        if (listed.Count != Desk.SoundIndependentCount)
            throw Bad($"The file lists {listed.Count} sound independents; there must be {Desk.SoundIndependentCount}.");

        CheckKeys(listed, "Sound independent", ("sound", "sounds"), Sound.MaxSounds);
        return [.. listed];
    }
}
