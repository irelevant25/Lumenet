using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;

namespace DmxBox.Server.Api;

// The hub's part for the backups and the updates of Lumenet: both belong to the PC.
internal partial class DeskHub
{
    /// <summary>The backups: their folder, whether a save makes one, how many are kept, the newest. Only on the PC.</summary>
    public BackupView GetBackup()
    {
        RequireLocal("The backups can only be set on the PC.");
        return _backups.View;
    }

    /// <summary>Where the backups go, whether a save makes one and how many are kept (backup.json). Only on the PC.</summary>
    /// <param name="setup">The setup.</param>
    public BackupView SetBackup(BackupSetup setup)
    {
        RequireLocal("The backups can only be set on the PC.");
        return _backups.Set(setup);
    }

    /// <summary>
    /// The version of Lumenet, and the newest release on GitHub: whether it is newer, what it brings, and
    /// whether this Lumenet can update itself (Lumenet.exe can; the desk of run.cmd cannot). Asked only when
    /// the About tab opens. Only on the PC. GitHub may take its time: the answer comes as a stream of one,
    /// which runs beside the page's other calls (see FindArtNetNodes).
    /// </summary>
    /// <param name="cancel">The page stopped asking.</param>
    public async IAsyncEnumerable<UpdateInfo> CheckUpdate([EnumeratorCancellation] CancellationToken cancel = default)
    {
        UpdateInfo info;
        try
        {
            RequireLocal("Lumenet can only be updated on the PC.");
            info = await _update.CheckAsync(cancel);
        }
        catch (DeskException e)
        {
            // UserErrorFilter sees only the start of a stream: the refusal is said here as it says them.
            throw new HubException(e.Message);
        }

        yield return info;
    }

    /// <summary>
    /// Updates Lumenet.exe to the newest release: downloads it, puts it in place of the running one and
    /// starts it; this one ends a moment later, and the pages connect to the new one by themselves. Only on
    /// the PC. The answer is a stream: how far the download is, and at the end the version Lumenet starts
    /// again as.
    /// </summary>
    /// <param name="cancel">The page went away: the download stops, and Lumenet stays as it is.</param>
    public async IAsyncEnumerable<UpdateStep> InstallUpdate([EnumeratorCancellation] CancellationToken cancel = default)
    {
        try
        {
            RequireLocal("Lumenet can only be updated on the PC.");
        }
        catch (DeskException e)
        {
            throw new HubException(e.Message);
        }

        var steps = Channel.CreateUnbounded<UpdateStep>(new UnboundedChannelOptions { SingleReader = true });
        Task<string> install = Task.Run(
            async () =>
            {
                try
                {
                    return await _update.InstallAsync(percent => steps.Writer.TryWrite(new UpdateStep(percent, null)), cancel);
                }
                finally
                {
                    steps.Writer.TryComplete();
                }
            },
            CancellationToken.None);

        await foreach (UpdateStep step in steps.Reader.ReadAllAsync(CancellationToken.None))
            yield return step;

        string version;
        try
        {
            version = await install;
        }
        catch (DeskException e)
        {
            throw new HubException(e.Message);
        }

        yield return new UpdateStep(100, version);
    }
}
