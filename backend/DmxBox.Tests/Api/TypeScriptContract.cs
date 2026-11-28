using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;

namespace DmxBox.Tests.Api;

/// <summary>
/// Writes the contract of the hub in TypeScript, for the page: every method a page can call (its
/// parameters and its answer), every message the desk program sends on its own, and every type that
/// goes over the wire, as the JSON of SignalR has it (names in camelCase, null where C# allows it).
/// The page's connection takes its names and types from it, so a change on one side that the other
/// does not follow is an error of the page's build. <see cref="TypeScriptContractTests"/> keeps the
/// file in the repository the same as what this writes.
/// </summary>
/// <remarks>
/// A type comes as the desk program writes it: every property there. What a page sends may leave out
/// what the desk program fills in by itself (a property outside the record's constructor, or one with
/// a default): such a type has a second form for sending, named with "Sent" at its end.
/// </remarks>
internal static class TypeScriptContract
{
    /// <summary>Where the page keeps the contract, from the root of the repository.</summary>
    public const string PagePath = "frontend/src/app/desk/hub-contract.ts";

    private static readonly string[] SoundStates = [SoundOutputView.Playing, SoundOutputView.Missing, SoundOutputView.NoDevice, SoundOutputView.Off];
    private static readonly string[] ListenStates = ["", .. SoundStates, SoundOutputView.Same];
    private static readonly string[] MixerStates = [UiMixer.None, UiMixer.Connecting, UiMixer.Connected];
    private static readonly string[] Targets = [Step.Channel, Step.Memory, Step.Fixture, Step.Group, Step.Release, Step.EffectTarget, Step.SoundTarget];

    // Texts that may only be one of a few: the page knows them as a union. The values come from the
    // constants of the desk program, so a new one there is new here.
    private static readonly Dictionary<(Type Type, string Property), string[]> Unions = new()
    {
        [(typeof(Step), nameof(Step.Target))] = Targets,
        [(typeof(StackStep), nameof(StackStep.Target))] = Targets,
        [(typeof(SoloKey), nameof(SoloKey.Target))] = [Step.Channel, Step.Memory],
        [(typeof(StackPlayback), nameof(StackPlayback.State))] = [Desk.Idle, Desk.Fading, Desk.Waiting, Desk.Holding],
        [(typeof(StageLight), nameof(StageLight.Shape))] = [StageLight.Round, StageLight.Square],
        [(typeof(StagePlan), nameof(StagePlan.PictureOver))] = [StagePlan.PictureOverStage, StagePlan.PictureOverPlan],
        [(typeof(EffectPreview), nameof(EffectPreview.Kind))] = [Effect.MovementKind, Effect.IntensityKind, Effect.ColorKind, Effect.PixelKind],
        [(typeof(Effect), nameof(Effect.Kind))] = [Effect.MovementKind, Effect.IntensityKind, Effect.ColorKind, Effect.PixelKind],
        [(typeof(Effect), nameof(Effect.Direction))] = [Effect.Forward, Effect.Back],
        [(typeof(Effect), nameof(Effect.Order))] = [Effect.ByAddress, Effect.LeftToRight, Effect.FromCenter, Effect.RandomOrder],
        [(typeof(BoxView), nameof(BoxView.Kind))] = [BoxWiring.Automatic, BoxWiring.Listed, BoxWiring.New],
        [(typeof(OutputPlan), nameof(OutputPlan.Kind))] = [OutputPlan.ArtNet, OutputPlan.Sacn, OutputPlan.Usb],
        [(typeof(OutputView), nameof(OutputView.State))] = [OutputView.Connected, OutputView.Missing, OutputView.Sending, OutputView.Conflict],
        [(typeof(SoundOutputView), nameof(SoundOutputView.State))] = SoundStates,
        [(typeof(SoundOutputView), nameof(SoundOutputView.ListenState))] = ListenStates,
        [(typeof(SoundOutputView), nameof(SoundOutputView.MixerState))] = MixerStates,
        [(typeof(ListenView), nameof(ListenView.State))] = ListenStates,
        [(typeof(MixerView), nameof(MixerView.State))] = MixerStates,
        [(typeof(MidiDeviceView), nameof(MidiDeviceView.State))] = [MidiDeviceView.Open, MidiDeviceView.Missing, MidiDeviceView.Failed, MidiDeviceView.Unused],
        [(typeof(MidiMapping), nameof(MidiMapping.Message))] = MidiMapping.Messages,
        [(typeof(MidiMapping), nameof(MidiMapping.Mode))] = [MidiMapping.Absolute, MidiMapping.RelativeTwos, MidiMapping.RelativeOffset, MidiMapping.RelativeSign],
        [(typeof(OscView), nameof(OscView.State))] = [OscView.Off, OscView.Listening, OscView.Failed],
    };

    /// <summary>The contract as the file holds it.</summary>
    public static string Write() => new Writer().Write();

    private sealed class Writer
    {
        private readonly NullabilityInfoContext _nullability = new();
        // the types to write, by their name in TypeScript; sent forms under their own name
        private readonly SortedDictionary<string, (Type Type, bool Sent)> _types = new(StringComparer.Ordinal);
        private readonly Dictionary<Type, bool> _hasSentForm = [];

        public string Write()
        {
            var methods = new StringBuilder();
            var streams = new StringBuilder();
            var events = new StringBuilder();

            MethodInfo[] hubMethods = typeof(DeskHub)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && method.Name is not ("OnConnectedAsync" or "OnDisconnectedAsync"))
                .OrderBy(method => method.Name, StringComparer.Ordinal)
                .ToArray();
            foreach (MethodInfo method in hubMethods)
            {
                var parameters = method.GetParameters()
                    .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
                    .Select(parameter => $"{parameter.Name}: {TypeOf(parameter.ParameterType, _nullability.Create(parameter), null, sent: true)}");
                Type returns = method.ReturnType;
                NullabilityInfo answer = _nullability.Create(method.ReturnParameter);
                bool stream = returns.IsGenericType && returns.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);
                string result;
                if (returns == typeof(void) || returns == typeof(Task))
                    result = "void";
                else if (stream || returns.IsGenericType && returns.GetGenericTypeDefinition() == typeof(Task<>))
                    result = TypeOf(returns.GetGenericArguments()[0], answer.GenericTypeArguments[0], null, sent: false);
                else
                    result = TypeOf(returns, answer, null, sent: false);
                (stream ? streams : methods).Append(Signature(method.Name, parameters, result));
            }

            foreach (MethodInfo message in typeof(IDeskClient).GetMethods().OrderBy(method => method.Name, StringComparer.Ordinal))
            {
                var parameters = message.GetParameters()
                    .Select(parameter => $"{parameter.Name}: {TypeOf(parameter.ParameterType, _nullability.Create(parameter), null, sent: false)}");
                events.Append(Signature(message.Name, parameters, "void"));
            }

            // the types, each finding the types it uses, until no new one comes
            var typeText = new StringBuilder();
            var written = new HashSet<string>(StringComparer.Ordinal);
            while (_types.Keys.FirstOrDefault(name => !written.Contains(name)) is { } next)
            {
                written.Add(next);
                (Type type, bool sent) = _types[next];
                var body = new StringBuilder();
                HashSet<string> optional = sent ? OptionalOnInput(type) : [];
                foreach (PropertyInfo property in Properties(type))
                {
                    Unions.TryGetValue((type, property.Name), out string[]? union);
                    // a property JSON leaves out while it is empty (false, null) may be missing either way
                    string mark = optional.Contains(property.Name) || LeftOutWhenEmpty(property) ? "?" : "";
                    body.Append($"  readonly {Camel(property.Name)}{mark}: {TypeOf(property.PropertyType, _nullability.Create(property), union, sent)};\n");
                }
                typeText.Append($"\nexport interface {next} {{\n{body}}}\n");
            }
            string types = string.Concat(_types.Keys.Select(name => Section(typeText.ToString(), name)));

            foreach ((Type type, string property) in Unions.Keys)
                if (!_types.Values.Any(item => item.Type == type) || type.GetProperty(property) is null)
                    throw new InvalidOperationException($"a union for {type.Name}.{property}, which goes nowhere");

            return string.Join("\n", [
                "// The contract of the desk program's hub (/hub/desk), WRITTEN BY THE DESK PROGRAM'S TESTS: do not",
                "// change it by hand. After a change of the hub or of what goes over it, run",
                "//   dotnet test backend/DmxBox.sln --filter TypeScriptContract -e LUMENET_WRITE_CONTRACT=1",
                "// and the page's build says where the page has to follow. A type named with \"Sent\" at its end",
                "// is the form a page sends: it may leave out what the desk program fills in by itself.",
                "",
                "/** The methods a page calls on the hub, with their parameters and their answers. */",
                "export interface DeskHubMethods {",
                methods.ToString().TrimEnd('\n'),
                "}",
                "",
                "/** The methods that answer as a stream: the page takes their last item. */",
                "export interface DeskHubStreams {",
                streams.ToString().TrimEnd('\n'),
                "}",
                "",
                "/** What the desk program sends to the pages on its own (IDeskClient). */",
                "export interface DeskClientEvents {",
                events.ToString().TrimEnd('\n'),
                "}",
                types,
            ]).Replace("\r\n", "\n");
        }

        // One interface of the written text, by its name: the text is written in the order the types
        // were found, the file holds them by name.
        private static string Section(string text, string name)
        {
            string head = $"\nexport interface {name} {{\n";
            int start = text.IndexOf(head, StringComparison.Ordinal);
            int end = text.IndexOf("\n}\n", start, StringComparison.Ordinal) + 3;
            return text[start..end];
        }

        private string TypeOf(Type type, NullabilityInfo info, string[]? union, bool sent)
        {
            if (Nullable.GetUnderlyingType(type) is { } inner)
                return TypeOf(inner, info, union, sent) + " | null";
            string text = Plain(type, info, union, sent);
            return !type.IsValueType && info.ReadState == NullabilityState.Nullable ? text + " | null" : text;
        }

        private string Plain(Type type, NullabilityInfo info, string[]? union, bool sent)
        {
            if (type == typeof(string))
                return union is null ? "string" : string.Join(" | ", union.Select(value => $"'{value}'"));
            if (type == typeof(bool))
                return "boolean";
            if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float) ||
                type == typeof(short) || type == typeof(byte) || type == typeof(decimal) || type == typeof(uint))
                return "number";
            if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid))
                return "string";
            if (type == typeof(byte[]))
                return "string"; // JSON carries bytes as base64
            // what a page sends may be a list it keeps read-only; what it gets is its own to change
            string list = sent ? "readonly " : "";
            if (type.IsArray)
                return $"{list}{Element(TypeOf(type.GetElementType()!, info.ElementType!, null, sent))}[]";
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                Type[] arguments = type.GetGenericArguments();
                if (definition == typeof(Dictionary<,>) || definition == typeof(IReadOnlyDictionary<,>) ||
                    definition == typeof(IDictionary<,>) || definition == typeof(SortedDictionary<,>))
                    return $"Record<{Plain(arguments[0], info.GenericTypeArguments[0], null, sent)}, " +
                        $"{TypeOf(arguments[1], info.GenericTypeArguments[1], null, sent)}>";
                if (typeof(IEnumerable).IsAssignableFrom(type) && arguments.Length == 1)
                    return $"{list}{Element(TypeOf(arguments[0], info.GenericTypeArguments[0], null, sent))}[]";
            }
            if (type.IsEnum)
                throw new InvalidOperationException($"{type.Name}: an enum goes over the wire as a number; give it a text");
            if ((type.IsClass || type.IsValueType) && type.Namespace?.StartsWith("DmxBox", StringComparison.Ordinal) == true)
            {
                bool sentForm = sent && HasSentForm(type);
                string name = sentForm ? type.Name + "Sent" : type.Name;
                if (_types.TryGetValue(name, out var known) && known.Type != type)
                    throw new InvalidOperationException($"two types named {name}");
                _types[name] = (type, sentForm);
                return name;
            }
            throw new InvalidOperationException($"no TypeScript for {type}");
        }

        // A union or a "| null" inside a list needs brackets: (string | null)[].
        private static string Element(string text) => text.Contains(' ') ? $"({text})" : text;

        // Whether a page may send the type with something left out: a property of its own, or one of a
        // type inside it.
        private bool HasSentForm(Type type)
        {
            if (_hasSentForm.TryGetValue(type, out bool known))
                return known;
            _hasSentForm[type] = false; // a type inside itself
            bool result = OptionalOnInput(type).Count > 0 ||
                Properties(type).Any(property => Inner(property.PropertyType).Any(inner => inner != type && IsOwn(inner) && HasSentForm(inner)));
            _hasSentForm[type] = result;
            return result;
        }

        private static IEnumerable<Type> Inner(Type type)
        {
            if (Nullable.GetUnderlyingType(type) is { } nullable)
                return Inner(nullable);
            if (type.IsArray)
                return Inner(type.GetElementType()!);
            if (type.IsGenericType)
                return type.GetGenericArguments().SelectMany(Inner);
            return [type];
        }

        private static bool IsOwn(Type type) =>
            type != typeof(string) && type.Namespace?.StartsWith("DmxBox", StringComparison.Ordinal) == true && !type.IsEnum;
    }

    // The properties JSON may leave out when it sends the type to the desk program: those it fills in
    // itself. A record's constructor takes the others; a property of its own, or a parameter with a
    // default, may be missing.
    private static HashSet<string> OptionalOnInput(Type type)
    {
        ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(constructor => !(constructor.GetParameters() is [var only] && only.ParameterType == type))
            .ToArray();
        ConstructorInfo? used = constructors.FirstOrDefault(constructor => constructor.GetCustomAttribute<JsonConstructorAttribute>() is not null)
            ?? (constructors.Any(constructor => constructor.GetParameters().Length == 0)
                ? null
                : constructors.Length == 1 ? constructors[0] : throw new InvalidOperationException($"{type.Name}: which constructor does JSON use?"));
        var needed = new HashSet<string>(
            (used?.GetParameters() ?? []).Where(parameter => !parameter.HasDefaultValue).Select(parameter => parameter.Name!),
            StringComparer.OrdinalIgnoreCase);
        return Properties(type)
            .Where(property => !needed.Contains(property.Name) && property.GetCustomAttribute<RequiredMemberAttribute>() is null)
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Signature(string name, IEnumerable<string> parameters, string result) =>
        $"  {name}({string.Join(", ", parameters)}): {result};\n";

    // The public properties JSON writes, in the order of the type: all but those it always leaves out.
    private static IEnumerable<PropertyInfo> Properties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.GetIndexParameters().Length == 0 && property.GetMethod is { IsPublic: true })
            .Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
            .OrderBy(property => property.MetadataToken);

    // True for a property JSON writes only when it is not empty ("add" only when true).
    private static bool LeftOutWhenEmpty(PropertyInfo property) =>
        property.GetCustomAttribute<JsonIgnoreAttribute>() is { Condition: JsonIgnoreCondition.WhenWritingDefault or JsonIgnoreCondition.WhenWritingNull };

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
