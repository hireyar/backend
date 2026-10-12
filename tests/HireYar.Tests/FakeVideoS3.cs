using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;

namespace HireYar.Tests;

public sealed class FakeVideoS3 : AmazonS3Client
{
    public FakeVideoS3() : base(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "https://storage.invalid", ForcePathStyle = true }) { }
    public Dictionary<string, byte[]> Objects { get; } = [];
    private readonly Dictionary<string, List<byte[]>> uploads = [];
    public bool PublicAcl { get; set; }
    public string? Policy { get; set; }
    public HttpStatusCode PolicyStatus { get; set; } = HttpStatusCode.OK;
    public bool FailPart { get; set; }
    public Action? OnPart { get; set; }
    public bool FailCompletion { get; set; }
    public int Aborts { get; private set; }
    public int Initiations { get; private set; }
    public int LargestPart { get; private set; }
    public S3CannedACL? WrittenAcl { get; private set; }
    public List<(long Start, long End)> Ranges { get; } = [];
    public override Task<GetBucketAclResponse> GetBucketAclAsync(GetBucketAclRequest r, CancellationToken ct = default) => Task.FromResult(new GetBucketAclResponse {
        Grants = PublicAcl ? [new S3Grant { Grantee = new S3Grantee { URI = "http://acs.amazonaws.com/groups/global/AllUsers" }, Permission = S3Permission.READ }] : []
    });
    public override Task<GetBucketPolicyResponse> GetBucketPolicyAsync(GetBucketPolicyRequest r, CancellationToken ct = default)
    {
        if (Policy is null) throw new AmazonS3Exception("synthetic missing policy") { ErrorCode = "NoSuchBucketPolicy", StatusCode = HttpStatusCode.NotFound };
        return Task.FromResult(new GetBucketPolicyResponse { Policy = Policy, HttpStatusCode = PolicyStatus });
    }
    public override Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest r, CancellationToken ct = default)
    {
        Initiations++; WrittenAcl = r.CannedACL;
        var id = Guid.NewGuid().ToString(); uploads[id] = [];
        return Task.FromResult(new InitiateMultipartUploadResponse { UploadId = id });
    }
    public override async Task<UploadPartResponse> UploadPartAsync(UploadPartRequest r, CancellationToken ct = default)
    {
        OnPart?.Invoke(); ct.ThrowIfCancellationRequested();
        if (FailPart) throw new AmazonS3Exception("synthetic sensitive provider body");
        using var data = new MemoryStream(); await r.InputStream.CopyToAsync(data, ct);
        Assert.Equal(data.Length, r.PartSize); LargestPart = Math.Max(LargestPart, (int)data.Length);
        uploads[r.UploadId].Add(data.ToArray());
        return new UploadPartResponse { ETag = "part-" + r.PartNumber, PartNumber = r.PartNumber };
    }
    public override Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest r, CancellationToken ct = default)
    {
        Assert.Equal(uploads[r.UploadId].Count, r.PartETags.Count);
        Objects[r.Key] = uploads[r.UploadId].SelectMany(b => b).ToArray(); uploads.Remove(r.UploadId);
        if (FailCompletion) throw new AmazonS3Exception("synthetic lost completion response");
        return Task.FromResult(new CompleteMultipartUploadResponse());
    }
    public override Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest r, CancellationToken ct = default)
    { Assert.False(ct.IsCancellationRequested); Aborts++; uploads.Remove(r.UploadId); return Task.FromResult(new AbortMultipartUploadResponse()); }
    public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest r, CancellationToken ct = default)
    { Objects.Remove(r.Key); return Task.FromResult(new DeleteObjectResponse()); }
    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest r, CancellationToken ct = default)
    {
        if (!Objects.TryGetValue(r.Key, out var bytes)) throw new AmazonS3Exception("synthetic missing object") { StatusCode = HttpStatusCode.NotFound };
        return Task.FromResult(new GetObjectMetadataResponse { ContentLength = bytes.Length, ETag = "synthetic-etag" });
    }
    public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest r, CancellationToken ct = default)
    {
        Assert.Equal("synthetic-etag", r.EtagToMatch);
        var bytes = Objects[r.Key]; var start = r.ByteRange.Start; var end = r.ByteRange.End;
        Ranges.Add((start, end));
        return Task.FromResult(new GetObjectResponse { HttpStatusCode = HttpStatusCode.PartialContent,
            ContentLength = end - start + 1, ResponseStream = new MemoryStream(bytes, (int)start, (int)(end - start + 1), false) });
    }
}
