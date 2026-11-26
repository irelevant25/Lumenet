namespace DmxBox.Server.Projects;

/// <summary>The project that is open, in the form the UI receives it.</summary>
/// <param name="Name">The project's name: its file name without ".json". "Untitled" for a new project.</param>
/// <param name="Path">The project's file; null until the project has been saved or loaded.</param>
/// <param name="Unsaved">True while a memory was changed after the last save or load.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record ProjectInfo(string Name, string? Path, bool Unsaved);
