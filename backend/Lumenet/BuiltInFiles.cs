using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Lumenet;

/// <summary>
/// Files that this program carries as resources, under names that start with a prefix
/// ("lumenet-page/"), handed out as if they were in a folder: the server serves the page this
/// way (see Lumenet.csproj).
/// </summary>
internal sealed class BuiltInFiles : IFileProvider
{
    private readonly Assembly _assembly;

    // The path of each file ("index.html", "media/x.woff2") with the name of its resource. Exact:
    // the page's file names differ in capitals alone ("chunk-b8_hGRDb.js").
    private readonly Dictionary<string, string> _resources = new(StringComparer.Ordinal);

    // When the files changed: when the program was made. The browser asks again after an update.
    private readonly DateTimeOffset _madeAt;

    public BuiltInFiles(Assembly assembly, string prefix)
    {
        _assembly = assembly;
        foreach (string name in assembly.GetManifestResourceNames())
        {
            // The build writes the folders of a path with "\" on Windows.
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                _resources[name[prefix.Length..].Replace('\\', '/')] = name;
        }

        string? program = Environment.ProcessPath;
        _madeAt = program is null ? DateTimeOffset.UtcNow : File.GetLastWriteTimeUtc(program);
    }

    /// <summary>How many files there are.</summary>
    public int Count => _resources.Count;

    public IFileInfo GetFileInfo(string subpath)
    {
        string path = subpath.TrimStart('/');
        return _resources.TryGetValue(path, out string? resource)
            ? new BuiltInFile(_assembly, resource, Path.GetFileName(path), _madeAt)
            : new NotFoundFileInfo(path);
    }

    // The server only asks for single files.
    public IDirectoryContents GetDirectoryContents(string subpath)
    {
        return NotFoundDirectoryContents.Singleton;
    }

    // They never change while the program runs.
    public IChangeToken Watch(string filter)
    {
        return NullChangeToken.Singleton;
    }

    private sealed class BuiltInFile(Assembly assembly, string resource, string name, DateTimeOffset madeAt) : IFileInfo
    {
        public bool Exists => true;

        public long Length
        {
            get
            {
                using Stream stream = CreateReadStream();
                return stream.Length;
            }
        }

        public string? PhysicalPath => null;

        public string Name => name;

        public DateTimeOffset LastModified => madeAt;

        public bool IsDirectory => false;

        public Stream CreateReadStream()
        {
            return assembly.GetManifestResourceStream(resource)
                ?? throw new FileNotFoundException($"The resource {resource} is missing.");
        }
    }
}
