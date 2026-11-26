using System.ComponentModel;
using System.Diagnostics;

namespace Lumenet;

/// <summary>
/// How Lumenet.exe updates itself (see AppUpdate): the running exe is renamed to "Lumenet.exe.old"
/// (Windows lets the file of a running program be renamed, not deleted), the new one takes its name and
/// is started with the same command line and "--Update:WaitFor=" this process; this one ends a moment
/// later. The new one waits for it to end before it takes the port and the icon
/// (<see cref="WaitForTheOneBefore"/>), and deletes the old file once it runs (<see cref="CleanUp"/>):
/// one that does not start leaves it, to be put back by hand.
/// </summary>
internal sealed class SelfReplace : ISelfReplace
{
    private const string WaitForArgument = "--Update:WaitFor=";
    private const string OpenPageArgument = "--Tray:OpenPage=";

    // How long a new Lumenet waits for the one before it to end.
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(30);

    // How long the one before still runs once the new one is started: its answer goes out meanwhile.
    private static readonly TimeSpan ExitDelay = TimeSpan.FromSeconds(1);

    private readonly string[] _args;
    private readonly string _exe;
    private Action? _exit;

    /// <param name="args">The command line Lumenet was started with: the new one gets it too.</param>
    public SelfReplace(string[] args)
    {
        _args = args;

        // Taken now: once renamed, the running file has another name.
        _exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Lumenet.exe");
    }

    /// <inheritdoc/>
    public string ExePath => _exe;

    /// <inheritdoc/>
    public string? CannotReplace
    {
        get
        {
            if (_exit is null)
                return "Lumenet is still starting.";

            string folder = Path.GetDirectoryName(_exe) ?? "";
            try
            {
                // a file that is gone when it is closed: whether the folder may be written
                using (File.Create(Path.Combine(folder, $"{Path.GetFileName(_exe)}.{Environment.ProcessId}.probe"), 1, FileOptions.DeleteOnClose))
                {
                }

                return null;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return $"Lumenet may not write in its folder ({folder}): download the new Lumenet.exe from the releases on GitHub and put it there in place of this one.";
            }
        }
    }

    /// <summary>
    /// Started by an update: waits until the Lumenet before has ended, so that the port, the icon and the
    /// signal of the first Lumenet are free. Does nothing for a start of the usual kind.
    /// </summary>
    /// <param name="args">The command line.</param>
    public static void WaitForTheOneBefore(string[] args)
    {
        string? given = args.LastOrDefault(argument => argument.StartsWith(WaitForArgument, StringComparison.OrdinalIgnoreCase));
        if (given is null || !int.TryParse(given.AsSpan(WaitForArgument.Length), out int id))
            return;

        try
        {
            using Process before = Process.GetProcessById(id);
            before.WaitForExit(WaitLimit);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // it has ended already
        }
    }

    /// <summary>How Lumenet ends: the tray's Exit, once it is there.</summary>
    /// <param name="exit">Ends Lumenet from any thread.</param>
    public void UseExit(Action exit)
    {
        _exit = exit;
    }

    /// <summary>
    /// Lumenet runs: the file of the Lumenet before an update, and a download that did not end, go. A file
    /// that is still held is tried again a few times, then left for the next start.
    /// </summary>
    public void CleanUp()
    {
        _ = Task.Run(async () =>
        {
            foreach (string leftOver in new[] { _exe + ".old", _exe + ".new" })
            {
                for (int attempt = 0; attempt < 10 && File.Exists(leftOver); attempt++)
                {
                    try
                    {
                        File.Delete(leftOver);
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                    {
                        await Task.Delay(500);
                    }
                }
            }
        });
    }

    /// <inheritdoc/>
    public void ReplaceAndRestart(string newExe)
    {
        Action exit = _exit ?? throw new DeskException("Lumenet is still starting.");
        string old = _exe + ".old";
        if (File.Exists(old))
            File.Delete(old);

        File.Move(_exe, old);
        try
        {
            File.Move(newExe, _exe);
        }
        catch
        {
            File.Move(old, _exe);
            throw;
        }

        try
        {
            var start = new ProcessStartInfo(_exe)
            {
                UseShellExecute = false,
                WorkingDirectory = Environment.CurrentDirectory,
            };
            foreach (string argument in _args)
            {
                if (!argument.StartsWith(WaitForArgument, StringComparison.OrdinalIgnoreCase)
                    && !argument.StartsWith(OpenPageArgument, StringComparison.OrdinalIgnoreCase))
                {
                    start.ArgumentList.Add(argument);
                }
            }

            // the pages are open already: they connect to the new one by themselves
            start.ArgumentList.Add(WaitForArgument + Environment.ProcessId);
            start.ArgumentList.Add(OpenPageArgument + "false");
            using Process? started = Process.Start(start);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException)
        {
            // this one back in its place: Lumenet stays as it was
            File.Delete(_exe);
            File.Move(old, _exe);
            throw new DeskException($"The new Lumenet could not be started: {e.Message}");
        }

        _ = Task.Delay(ExitDelay).ContinueWith(_ => exit(), TaskScheduler.Default);
    }
}
