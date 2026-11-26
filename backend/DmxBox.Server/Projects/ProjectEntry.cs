namespace DmxBox.Server.Projects;

/// <summary>One project file in the projects folder, in the form the UI receives it.</summary>
/// <param name="Name">The project's name: the file name without ".json".</param>
/// <param name="Path">The file, with its folder.</param>
/// <param name="Modified">
/// When the file was last written, in universal time. It reaches the UI as ISO 8601 text,
/// for example "2026-11-18T15:04:05.1234567Z".
/// </param>
internal sealed record ProjectEntry(string Name, string Path, DateTime Modified);
