using System.Text.Json;

namespace DmxBox.Server;

/// <summary>
/// The settings of the UI on the PC that belong to a project: its layout and its keyboard
/// shortcuts. The desk keeps them for the UI and writes them into the project file, but what is
/// in them is the UI's business; the server only sees to it that they are a JSON object of a
/// sensible size.
/// </summary>
internal static class ProjectSettings
{
    /// <summary>
    /// The most characters the settings may have, written without spaces. A shortcut for every
    /// key of the desk takes about a third of it.
    /// </summary>
    public const int MaxLength = 64 * 1024;

    // Written as typed, as the project files are: "Ctrl+KeyC", not "Ctrl+KeyC".
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Settings from a UI or a file, checked, and written without spaces.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The settings as the desk keeps them.</returns>
    /// <exception cref="DeskException">They are no JSON object, or too long. The message says so.</exception>
    public static string Checked(JsonElement settings)
    {
        if (settings.ValueKind != JsonValueKind.Object)
            throw new DeskException("The settings must be a JSON object.");

        string written = JsonSerializer.Serialize(settings, Options);
        if (written.Length > MaxLength)
            throw new DeskException($"The settings are too long: at most {MaxLength} characters.");

        return written;
    }

    /// <summary>The settings as the desk keeps them, as a JSON element to hand on.</summary>
    /// <param name="settings">The settings as <see cref="Checked"/> made them; null for none.</param>
    public static JsonElement? Element(string? settings)
    {
        if (settings is null)
            return null;

        using JsonDocument document = JsonDocument.Parse(settings);
        return document.RootElement.Clone();
    }
}
