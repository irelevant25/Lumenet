namespace DmxBox.Server;

/// <summary>The answer to "ImportProject", in the form the UI receives it.</summary>
/// <param name="Project">The open project: the imported one, with a name but no file yet.</param>
/// <param name="Report">Sentences for the user: what the file held, where it went, and what was left out.</param>
internal sealed record ImportAnswer(ProjectInfo Project, IReadOnlyList<string> Report);
