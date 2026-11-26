using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Server.Projects;

// The effects in a project file, and their keys of the independents.
internal static partial class ProjectFormat
{
    // An effect as the file has it: its lights and group by their places in the file's lists, and only
    // what differs from how an effect is unless it says otherwise.
    private static ProjectFileEffect FileEffect(Effect effect, StepPlaces places)
    {
        return new ProjectFileEffect
        {
            Number = effect.Number,
            Name = effect.Name,
            Kind = effect.Kind,
            Shape = effect.Shape,
            Group = effect.Group is int group ? places.Groups.GetValueOrDefault(group) : null,
            Lights = effect.Group is null ? [.. effect.Lights.Where(places.Lights.ContainsKey).Select(light => places.Lights[light])] : null,
            Speed = effect.Speed,
            Size = effect.Size,
            Spread = effect.Spread,
            Direction = effect.Direction == Effect.Forward ? null : effect.Direction,
            Order = effect.Order == Effect.ByAddress ? null : effect.Order,
            Blocks = effect.Blocks == 1 ? null : effect.Blocks,
            Wings = effect.Wings == 1 ? null : effect.Wings,
            FadeIn = effect.FadeIn == 0 ? null : effect.FadeIn,
            FadeOut = effect.FadeOut == 0 ? null : effect.FadeOut,
            Duty = effect.Duty == 50 ? null : effect.Duty,
            Colors = effect.Colors.Length == 0 ? null : [.. effect.Colors],
            Center = effect.CenterPan is double pan && effect.CenterTilt is double tilt ? [pan, tilt] : null,
            Angle = effect.Angle == 0 ? null : effect.Angle,
            Point = effect.PointX is double x && effect.PointY is double y ? [x, y] : null,
            Seed = effect.Seed == 0 ? null : effect.Seed,
        };
    }

    // One entry per effect number: the effect, or null for one the file does not list. A file from
    // before there were effects has none.
    private static Effect?[] EffectsOf(List<ProjectFileEffect?>? listed)
    {
        var effects = new Effect?[Effect.MaxEffects];
        foreach (ProjectFileEffect? entry in listed ?? [])
        {
            if (entry?.Number is not int number || number < 1 || number > Effect.MaxEffects)
                throw Bad($"The file has an effect without a number from 1 to {Effect.MaxEffects}.");
            if (effects[number - 1] is not null)
                throw Bad($"The file lists effect {number} twice.");
            if (entry.Center is { Count: not 2 } || entry.Point is { Count: not 2 })
                throw Bad($"Effect {number} in the file has a center or a point that is not a pair of numbers.");

            try
            {
                effects[number - 1] = new Effect(number, entry.Name ?? "", entry.Kind ?? "", entry.Shape ?? "")
                {
                    Group = entry.Group is > 0 ? entry.Group : null,
                    Lights = [.. entry.Lights ?? []],
                    Speed = entry.Speed ?? 30,
                    Size = entry.Size ?? 100,
                    Spread = entry.Spread ?? 100,
                    Direction = entry.Direction ?? Effect.Forward,
                    Order = entry.Order ?? Effect.ByAddress,
                    Blocks = entry.Blocks ?? 1,
                    Wings = entry.Wings ?? 1,
                    FadeIn = entry.FadeIn ?? 0,
                    FadeOut = entry.FadeOut ?? 0,
                    Duty = entry.Duty ?? 50,
                    Colors = [.. (entry.Colors ?? []).Select(color => color ?? "")],
                    CenterPan = entry.Center?[0],
                    CenterTilt = entry.Center?[1],
                    Angle = entry.Angle ?? 0,
                    PointX = entry.Point?[0],
                    PointY = entry.Point?[1],
                    Seed = entry.Seed ?? 0,
                }.Checked();
            }
            catch (DeskException e)
            {
                throw Bad($"Effect {number} in the file cannot be used. {e.Message}");
            }
        }

        return effects;
    }

    // The effect of each effect key of the independents; a file without them has them where they are on a new desk.
    private static int[] EffectIndependentsOf(List<int>? listed)
    {
        if (listed is null)
            return Desk.DefaultEffectIndependents();
        if (listed.Count != Desk.EffectIndependentCount)
            throw Bad($"The file lists {listed.Count} effect independents; there must be {Desk.EffectIndependentCount}.");

        CheckKeys(listed, "Effect independent", ("effect", "effects"), Effect.MaxEffects);
        return [.. listed];
    }
}
