using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace HireYar.Infrastructure.Interviews;

// Old keys continue to resolve locally. Only new videos are written to S3.
public sealed partial class S3InterviewVideoStorage(IAmazonS3 client, LocalInterviewAudioStorage local,
    IOptions<InterviewStorageOptions> options) : IInterviewAudioStorage
{
    public const int PartBytes = 8 * 1024 * 1024;
    private readonly string bucket = options.Value.S3.Bucket;
    [GeneratedRegex("^s3/videos/[a-f0-9]{32}/[a-f0-9]{32}\\.audio$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
    private static void Validate(string key) { if (!KeyPattern().IsMatch(key)) throw new IOException("Invalid storage key."); }
    internal static AppException Unavailable() => new(503, "video_storage_unavailable", "Video storage is temporarily unavailable.");

    public async Task<StoredAudio> SaveAsync(Stream audio, string category, Guid ownerId, long maxBytes, CancellationToken ct)
    {
        if (category != "videos") return await local.SaveAsync(audio, category, ownerId, maxBytes, ct);
        if (ownerId == Guid.Empty || maxBytes <= 0) throw new IOException("Invalid video storage request.");
        ct.ThrowIfCancellationRequested();
        var key = $"s3/videos/{ownerId:N}/{Guid.NewGuid():N}.audio";
        string? uploadId = null;
        try
        {
            await RequirePrivateBucketAsync(ct);
            var initiated = await client.InitiateMultipartUploadAsync(new() { BucketName = bucket, Key = key,
                CannedACL = S3CannedACL.Private, ContentType = "application/octet-stream" }, ct);
            uploadId = initiated.UploadId;
            var parts = new List<PartETag>();
            var buffer = new byte[PartBytes]; long size = 0;
            while (true)
            {
                int count = 0, read;
                while (count < buffer.Length && (read = await audio.ReadAsync(buffer.AsMemory(count), ct)) > 0)
                {
                    count += read;
                    if (size + count > maxBytes) throw new IOException("Video limit exceeded.");
                }
                if (count == 0) break;
                if (parts.Count == 10000) throw new IOException("Video part limit exceeded.");
                using var part = new MemoryStream(buffer, 0, count, writable: false);
                var uploaded = await client.UploadPartAsync(new() { BucketName = bucket, Key = key, UploadId = uploadId,
                    PartNumber = parts.Count + 1, InputStream = part, PartSize = count }, ct);
                if (string.IsNullOrEmpty(uploaded.ETag)) throw new IOException("Invalid video upload response.");
                parts.Add(new(parts.Count + 1, uploaded.ETag)); size += count;
            }
            if (size == 0) throw new IOException("Empty video.");
            await client.CompleteMultipartUploadAsync(new() { BucketName = bucket, Key = key, UploadId = uploadId, PartETags = parts }, ct);
            return new(key, size);
        }
        catch (Exception ex) when (ex is AmazonS3Exception or AmazonClientException or HttpRequestException or IOException or OperationCanceledException)
        {
            // Cleanup uses its own bounded token, including after caller cancellation.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (uploadId is not null)
            {
                try { await client.AbortMultipartUploadAsync(new() { BucketName = bucket, Key = key, UploadId = uploadId }, cleanup.Token); }
                catch (Exception) { /* Lifecycle must remove abandoned parts if storage is unreachable. */ }
                // Completion may have succeeded even if its response was lost. The key is unique to this request.
                try { await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key }, cleanup.Token); }
                catch (Exception) { /* No customer content or provider error is logged. */ }
            }
            if (ex is OperationCanceledException && ct.IsCancellationRequested) throw;
            if (ex is IOException) throw;
            throw Unavailable();
        }
    }
    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        if (!key.StartsWith("s3/", StringComparison.Ordinal)) return await local.OpenReadAsync(key, ct);
        Validate(key);
        try
        {
            await RequirePrivateBucketAsync(ct);
            var metadata = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = bucket, Key = key }, ct);
            if (metadata.ContentLength <= 0 || string.IsNullOrWhiteSpace(metadata.ETag)) throw Unavailable();
            return new S3VideoReadStream(client, bucket, key, metadata.ContentLength, metadata.ETag, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { throw AppException.NotFound(); }
        catch (Exception ex) when (ex is AmazonClientException or HttpRequestException) { throw Unavailable(); }
    }
    // A private object ACL cannot override a public bucket policy. Fail closed before storing content.
    private async Task RequirePrivateBucketAsync(CancellationToken ct)
    {
        var acl = await client.GetBucketAclAsync(new GetBucketAclRequest { BucketName = bucket }, ct);
        if (acl.Grants is null) throw Unavailable();
        if (acl.Grants.Any(g => g.Grantee?.URI is
            "http://acs.amazonaws.com/groups/global/AllUsers" or "http://acs.amazonaws.com/groups/global/AuthenticatedUsers"))
            throw new AppException(503, "video_storage_not_private", "Video storage must be private.");
        try
        {
            var policy = await client.GetBucketPolicyAsync(new GetBucketPolicyRequest { BucketName = bucket }, ct);
            // GetBucketPolicy in SDK v4 can return a 404 response instead of throwing.
            if (policy.HttpStatusCode == HttpStatusCode.NotFound)
            {
                using var reader = XmlReader.Create(new StringReader(policy.Policy ?? ""), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var error = XDocument.Load(reader).Root;
                if (error?.Name.LocalName == "Error" && error.Elements().Any(e => e.Name.LocalName == "Code" && e.Value == "NoSuchBucketPolicy")) return;
                throw Unavailable();
            }
            if (policy.HttpStatusCode != HttpStatusCode.OK) throw Unavailable();
            if (string.IsNullOrWhiteSpace(policy.Policy)) throw Unavailable();
            using var document = JsonDocument.Parse(policy.Policy);
            var statements = document.RootElement.GetProperty("Statement");
            var rows = statements.ValueKind == JsonValueKind.Array ? statements.EnumerateArray().ToArray() : [statements];
            bool Wildcard(JsonElement e) => e.ValueKind switch {
                JsonValueKind.String => e.GetString() == "*",
                JsonValueKind.Array => e.EnumerateArray().Any(Wildcard),
                JsonValueKind.Object => e.EnumerateObject().Any(p => Wildcard(p.Value)), _ => false
            };
            if (rows.Any(row => row.GetProperty("Effect").GetString() == "Allow" &&
                (!row.TryGetProperty("Principal", out var principal) || row.TryGetProperty("NotPrincipal", out _) || Wildcard(principal))))
                throw new AppException(503, "video_storage_not_private", "Video storage must be private.");
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucketPolicy") { }
        catch (Exception ex) when (ex is JsonException or XmlException or InvalidOperationException or KeyNotFoundException) { throw Unavailable(); }
    }
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        if (!key.StartsWith("s3/", StringComparison.Ordinal)) { await local.DeleteAsync(key, ct); return; }
        Validate(key);
        try { await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key }, ct); }
        catch (Exception ex) when (ex is AmazonClientException or HttpRequestException) { throw Unavailable(); }
    }
}

// ASP.NET FileStreamResult can seek for HTTP Range without loading the recording into memory/disk.
internal sealed class S3VideoReadStream(IAmazonS3 client, string bucket, string key, long length, string etag,
    CancellationToken requestToken) : Stream
{
    private long position, remaining;
    private GetObjectResponse? response;
    private bool disposed;
    public override bool CanRead => !disposed;
    public override bool CanSeek => !disposed;
    public override bool CanWrite => false;
    public override long Length { get { ObjectDisposedException.ThrowIf(disposed, this); return length; } }
    public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
    public override long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var next = checked(origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => position + offset,
            SeekOrigin.End => length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) });
        if (next < 0) throw new IOException("Invalid stream position.");
        if (next != position) { response?.Dispose(); response = null; remaining = 0; position = next; }
        return position;
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestToken, ct);
        linked.Token.ThrowIfCancellationRequested();
        if (buffer.Length == 0 || position >= length) return 0;
        try
        {
            if (response is null)
            {
                remaining = Math.Min(length - position, S3InterviewVideoStorage.PartBytes);
                response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key,
                    ByteRange = new ByteRange(position, position + remaining - 1), EtagToMatch = etag }, linked.Token);
                if (response.HttpStatusCode != HttpStatusCode.PartialContent || response.ContentLength != remaining)
                    throw new IOException("Invalid video range response.");
            }
            var read = await response.ResponseStream.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining)], linked.Token);
            if (read == 0) throw new IOException("Incomplete video range response.");
            position += read; remaining -= read;
            if (remaining == 0) { response.Dispose(); response = null; }
            return read;
        }
        catch (Exception ex) when (ex is AmazonClientException or HttpRequestException or IOException)
        { response?.Dispose(); response = null; throw S3InterviewVideoStorage.Unavailable(); }
    }
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset, count), ct).AsTask();
    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    protected override void Dispose(bool disposing) { if (disposing) response?.Dispose(); disposed = true; base.Dispose(disposing); }
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
