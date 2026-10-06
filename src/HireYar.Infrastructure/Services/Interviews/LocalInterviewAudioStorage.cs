using System.Text.RegularExpressions;
using HireYar.Application.Interviews;
using Microsoft.Extensions.Options;
namespace HireYar.Infrastructure.Interviews;

public sealed partial class LocalInterviewAudioStorage(IOptions<InterviewStorageOptions> options) : IInterviewAudioStorage
{
    private readonly string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Value.RootPath));
    [GeneratedRegex("^(answers|questions|resumes|videos)/[a-f0-9]{32}/[a-f0-9]{32}\\.audio$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
    private string Resolve(string key)
    {
        if (!KeyPattern().IsMatch(key)) throw new IOException("Invalid storage key.");
        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid storage key.");
        // Reject junctions/symlinks in existing directory components.
        for (var current = new DirectoryInfo(Path.GetDirectoryName(path)!); current is not null; current = current.Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Invalid storage directory.");
            if (string.Equals(current.FullName, root, StringComparison.OrdinalIgnoreCase)) break;
        }
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Invalid storage file.");
        return path;
    }
    public async Task<StoredAudio> SaveAsync(Stream audio, string category, Guid ownerId, long maxBytes, CancellationToken ct)
    {
        var key = $"{category}/{ownerId:N}/{Guid.NewGuid():N}.audio"; var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, FileOptions.Asynchronous);
            var buffer = new byte[8192]; long size = 0; int read;
            while ((read = await audio.ReadAsync(buffer, ct)) > 0)
            {
                size += read; if (size > maxBytes) throw new IOException("Audio limit exceeded.");
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (size == 0) throw new IOException("Empty audio.");
            return new(key, size);
        }
        catch { File.Delete(path); throw; }
    }
    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<Stream>(new FileStream(Resolve(key), FileMode.Open, FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous));
    }
    public Task DeleteAsync(string key, CancellationToken ct) { ct.ThrowIfCancellationRequested(); File.Delete(Resolve(key)); return Task.CompletedTask; }
}
