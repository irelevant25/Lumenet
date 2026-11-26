using System.Text;

namespace DmxBox.Tests.Api;

/// <summary>
/// The page's copy of the hub's contract (<see cref="TypeScriptContract.PagePath"/>) is what the desk
/// program's hub is now: a change of the hub that the file does not have fails here, in CI too. With
/// LUMENET_WRITE_CONTRACT=1 the test writes the file instead.
/// </summary>
public sealed class TypeScriptContractTests
{
    [Fact]
    public void ThePageHasTheContractOfTheHub()
    {
        string written = TypeScriptContract.Write();
        string path = Path.Combine(RepositoryRoot(), TypeScriptContract.PagePath);
        if (Environment.GetEnvironmentVariable("LUMENET_WRITE_CONTRACT") == "1")
        {
            File.WriteAllText(path, written, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        string kept = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";
        Assert.True(
            kept == written,
            $"{TypeScriptContract.PagePath} is not the contract of the hub. Write it anew with " +
            "\"dotnet test backend/DmxBox.sln --filter TypeScriptContract -e LUMENET_WRITE_CONTRACT=1\", " +
            "then build the page: it says where the page has to follow.");
    }

    // The folder that holds backend/ and frontend/, above the tests' own folder.
    private static string RepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (Directory.Exists(Path.Combine(folder.FullName, "backend")) && Directory.Exists(Path.Combine(folder.FullName, "frontend")))
                return folder.FullName;
        throw new InvalidOperationException("no repository above " + AppContext.BaseDirectory);
    }
}
