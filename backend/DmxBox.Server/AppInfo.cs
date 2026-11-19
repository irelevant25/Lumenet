namespace DmxBox.Server;

/// <summary>The program's name and version, for the UI and for /api/health.</summary>
internal static class AppInfo
{
    /// <summary>The name of the whole application.</summary>
    public const string Name = "Lumenet";

    /// <summary>
    /// The version, for example "0.1.0". It comes from the assembly, which gets it from
    /// &lt;Version&gt; in the project file: that is the one place to change it.
    /// </summary>
    public static string Version { get; } = typeof(AppInfo).Assembly.GetName().Version!.ToString(3);
}
