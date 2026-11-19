namespace DmxBox.Server;

/// <summary>The projects that can be loaded, in the form the UI receives it.</summary>
/// <param name="Folder">The projects folder.</param>
/// <param name="Projects">The project files in the folder, the newest first.</param>
internal sealed record ProjectList(string Folder, IReadOnlyList<ProjectEntry> Projects);
