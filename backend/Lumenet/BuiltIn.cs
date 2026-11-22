using System.Drawing;
using System.Reflection;
using DmxBox.Server;

namespace Lumenet;

/// <summary>
/// What Lumenet.exe carries inside itself (the page, the examples, the icon; see Lumenet.csproj),
/// and how the desk program gets it.
/// </summary>
internal static class BuiltIn
{
    private const string PagePrefix = "lumenet-page/";
    private const string ExamplesPrefix = "lumenet-examples/";
    private const string IconName = "lumenet.ico";

    // The name of Lumenet's own lines in the log.
    public const string LogName = "Lumenet";

    private static readonly Assembly Self = typeof(BuiltIn).Assembly;

    /// <summary>Changes the desk program before it is put together (see Program.Build).</summary>
    public static void Adjust(WebApplicationBuilder builder, LogBook log)
    {
        // Its log goes to the log window: there is no console to write to.
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(log);

        // As in the desk program's settings file, which the exe does without: requests and the
        // like only when something goes wrong. The lines of the start (the folder, "Press Ctrl+C")
        // say nothing here; Lumenet writes the address of the page itself. A setting given on the
        // command line or in an appsettings.json beside the exe wins.
        QuieterUnlessSet(builder, "Microsoft.AspNetCore");
        QuieterUnlessSet(builder, "Microsoft.Hosting.Lifetime");

        // The page, unless Ui:Folder names a folder with another one.
        builder.Environment.WebRootFileProvider = new BuiltInFiles(Self, PagePrefix);

        if (string.IsNullOrWhiteSpace(builder.Configuration["Projects:Examples"]))
        {
            string? examples = UnpackExamples(log);
            if (examples is not null)
                builder.Configuration["Projects:Examples"] = examples;
        }
    }

    /// <summary>Lumenet's icon, in the size closest to this one.</summary>
    public static Icon Icon(Size size)
    {
        using Stream stream = Self.GetManifestResourceStream(IconName)
            ?? throw new FileNotFoundException($"The resource {IconName} is missing.");
        return new Icon(stream, size);
    }

    private static void QuieterUnlessSet(WebApplicationBuilder builder, string category)
    {
        if (builder.Configuration[$"Logging:LogLevel:{category}"] is null)
            builder.Logging.AddFilter(category, LogLevel.Warning);
    }

    // The desk lists the examples in a folder, so they are written to one of their own at every
    // start, as this version of Lumenet has them: Lumenet\examples in the local application data.
    private static string? UnpackExamples(LogBook log)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name, "examples");
        try
        {
            Directory.CreateDirectory(folder);
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string resource in Self.GetManifestResourceNames())
            {
                if (!resource.StartsWith(ExamplesPrefix, StringComparison.Ordinal))
                    continue;

                string file = resource[ExamplesPrefix.Length..];
                using (Stream from = Self.GetManifestResourceStream(resource)!)
                using (FileStream to = File.Create(Path.Combine(folder, file)))
                    from.CopyTo(to);

                written.Add(file);
            }

            // Examples of an earlier version that this one does not have.
            foreach (string old in Directory.GetFiles(folder, "*.json"))
            {
                if (!written.Contains(Path.GetFileName(old)))
                    File.Delete(old);
            }

            return folder;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.Add(LogLevel.Warning, LogName, $"The examples could not be written to {folder}, so Load lists none. {e.Message}");
            return null;
        }
    }
}
