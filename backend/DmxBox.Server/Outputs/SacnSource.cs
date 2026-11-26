namespace DmxBox.Server.Outputs;

/// <summary>
/// The desk as an sACN source: its CID, a UUID made the first time it is needed and kept in a file
/// on this PC (next to the LAN password), so that receivers know it as the same source every time
/// the program runs.
/// </summary>
internal static class SacnSource
{
    /// <summary>The name of the file, in the folder of the LAN password.</summary>
    public const string FileName = "sacn-cid.txt";

    /// <summary>
    /// The CID kept in the file; a new one, written to the file, when there is none or the file
    /// holds no UUID. When the file cannot be written, the new CID holds for this run.
    /// </summary>
    /// <param name="file">The file, %LOCALAPPDATA%\Lumenet\sacn-cid.txt unless the settings say otherwise.</param>
    /// <param name="logger">Gets a line when the file cannot be read or written.</param>
    public static Guid Cid(string file, ILogger logger)
    {
        try
        {
            if (File.Exists(file) && Guid.TryParse(File.ReadAllText(file).Trim(), out Guid kept) && kept != Guid.Empty)
                return kept;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("The sACN CID in {File} cannot be read ({Reason}); the desk makes a new one.", file, e.Message);
        }

        Guid cid = Guid.NewGuid();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            File.WriteAllText(file, cid.ToString("D") + Environment.NewLine);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                "The sACN CID cannot be kept in {File} ({Reason}); receivers may take the desk for a new source the next time.",
                file, e.Message);
        }

        return cid;
    }
}
