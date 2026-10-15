using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using HireYar.Application.Common;
using HireYar.Application.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace HireYar.Infrastructure.Support;

public sealed partial class TicketS3Storage([FromKeyedServices("ticketing")] IAmazonS3 client,
    IOptions<TicketStorageOptions> options) : ITicketAttachmentStorage
{
    private readonly string bucket = options.Value.Bucket;
    [GeneratedRegex("^tickets/[a-f0-9]{32}/[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
    private static void ValidateKey(string key) { if (!KeyPattern().IsMatch(key)) throw AppException.Invalid("ticket_storage_key"); }
    private static AppException Unavailable() => new(503, "ticket_storage_unavailable", "Ticket attachment storage is temporarily unavailable.");

    public async Task SaveAsync(string key, byte[] content, CancellationToken ct)
    {
        ValidateKey(key);
        if (content.Length is < 1 or > TicketFileRules.MaxFileBytes) throw AppException.Invalid("ticket_file_size");
        try
        {
            await RequirePrivateBucketAsync(ct);
            using var stream = new MemoryStream(content, writable: false);
            await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, InputStream = stream,
                ContentType = "application/octet-stream", CannedACL = S3CannedACL.Private, AutoCloseStream = false }, ct);
        }
        catch (Exception ex) when (ex is AmazonS3Exception or AmazonClientException or HttpRequestException or IOException) { throw Unavailable(); }
    }
    public async Task<Stream> OpenAsync(string key, int expectedLength, CancellationToken ct)
    {
        ValidateKey(key);
        if (expectedLength is < 1 or > TicketFileRules.MaxFileBytes) throw Unavailable();
        MemoryStream? output = null;
        try
        {
            await RequirePrivateBucketAsync(ct);
            using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = key }, ct);
            if (response.ContentLength != expectedLength) throw Unavailable();
            output = new MemoryStream(expectedLength);
            var buffer = new byte[65536];
            int read;
            while ((read = await response.ResponseStream.ReadAsync(buffer, ct)) > 0)
            {
                if (output.Length + read > expectedLength) throw Unavailable();
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            if (output.Length != expectedLength) throw Unavailable();
            output.Position = 0;
            return output;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { output?.Dispose(); throw AppException.NotFound(); }
        catch (Exception ex) when (ex is AmazonS3Exception or AmazonClientException or HttpRequestException or IOException) { output?.Dispose(); throw Unavailable(); }
        catch { output?.Dispose(); throw; }
    }
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        ValidateKey(key);
        try { await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key }, ct); }
        catch (Exception ex) when (ex is AmazonS3Exception or AmazonClientException or HttpRequestException) { throw Unavailable(); }
    }
    // Private object ACLs cannot override public bucket policies. Refuse writes and reads in that case.
    public async Task RequirePrivateBucketAsync(CancellationToken ct)
    {
        try
        {
            var acl = await client.GetBucketAclAsync(new GetBucketAclRequest { BucketName = bucket }, ct);
            if (acl.Grants is null) throw Unavailable();
            if (acl.Grants.Any(g => g.Grantee?.URI is "http://acs.amazonaws.com/groups/global/AllUsers" or "http://acs.amazonaws.com/groups/global/AuthenticatedUsers"))
                throw new AppException(503, "ticket_storage_not_private", "Ticket bucket must be private.");
            var policy = await client.GetBucketPolicyAsync(new GetBucketPolicyRequest { BucketName = bucket }, ct);
            if (policy.HttpStatusCode == HttpStatusCode.NotFound)
            {
                using var reader = XmlReader.Create(new StringReader(policy.Policy ?? ""), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
                var error = XDocument.Load(reader).Root;
                if (error?.Name.LocalName == "Error" && error.Elements().Any(e => e.Name.LocalName == "Code" && e.Value == "NoSuchBucketPolicy")) return;
                throw Unavailable();
            }
            if (policy.HttpStatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(policy.Policy)) throw Unavailable();
            using var document = JsonDocument.Parse(policy.Policy);
            var statements = document.RootElement.GetProperty("Statement");
            var rows = statements.ValueKind == JsonValueKind.Array ? statements.EnumerateArray().ToArray() : [statements];
            bool Wildcard(JsonElement e) => e.ValueKind switch {
                JsonValueKind.String => e.GetString() == "*", JsonValueKind.Array => e.EnumerateArray().Any(Wildcard),
                JsonValueKind.Object => e.EnumerateObject().Any(p => Wildcard(p.Value)), _ => false };
            if (rows.Any(row => row.GetProperty("Effect").GetString() == "Allow" &&
                (!row.TryGetProperty("Principal", out var principal) || row.TryGetProperty("NotPrincipal", out _) || Wildcard(principal))))
                throw new AppException(503, "ticket_storage_not_private", "Ticket bucket must be private.");
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucketPolicy") { }
        catch (Exception ex) when (ex is AmazonS3Exception or AmazonClientException or HttpRequestException or JsonException or XmlException or InvalidOperationException or KeyNotFoundException) { throw Unavailable(); }
    }
}

