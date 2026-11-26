namespace DmxBox.Server.Projects;

/// <summary>The projects that can be loaded, in the form the UI receives it.</summary>
/// <param name="Folder">The projects folder.</param>
/// <param name="Projects">The project files in the folder, the newest first.</param>
/// <param name="Examples">The names of the example projects that come with the program, in the order of their names.</param>
internal sealed record ProjectList(string Folder, IReadOnlyList<ProjectEntry> Projects, IReadOnlyList<string> Examples);
