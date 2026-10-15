using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Collections.Concurrent;
using System.Net;

namespace HireYar.Tests;

public sealed class FakeTicketS3() : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "https://ticket-storage.invalid" })
{
    public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
    public bool PublicAcl { get; set; }
    public string? Policy { get; set; }
    public bool FailPut { get; set; }
    public Func<Task>? OnPut { get; set; }
    public string? LastBucket { get; private set; }
    public S3CannedACL? LastAcl { get; private set; }
    public int Reads { get; private set; }
    public override Task<GetBucketAclResponse> GetBucketAclAsync(GetBucketAclRequest r, CancellationToken ct = default)
    {
        LastBucket = r.BucketName;
        return Task.FromResult(new GetBucketAclResponse { Grants = PublicAcl
            ? [new S3Grant { Grantee = new S3Grantee { URI = "http://acs.amazonaws.com/groups/global/AllUsers" }, Permission = S3Permission.READ }] : [] });
    }
    public override Task<GetBucketPolicyResponse> GetBucketPolicyAsync(GetBucketPolicyRequest r, CancellationToken ct = default)
    {
        if (Policy is null) throw new AmazonS3Exception("synthetic") { ErrorCode = "NoSuchBucketPolicy", StatusCode = HttpStatusCode.NotFound };
        return Task.FromResult(new GetBucketPolicyResponse { HttpStatusCode = HttpStatusCode.OK, Policy = Policy });
    }
    public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest r, CancellationToken ct = default)
    {
        LastBucket = r.BucketName; LastAcl = r.CannedACL;
        using var buffer = new MemoryStream(); await r.InputStream.CopyToAsync(buffer, ct); Objects[r.Key] = buffer.ToArray();
        if (OnPut is { } callback) await callback();
        if (FailPut) throw new AmazonS3Exception("synthetic lost PUT response");
        return new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK };
    }
    public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest r, CancellationToken ct = default)
    {
        Reads++; LastBucket = r.BucketName;
        if (!Objects.TryGetValue(r.Key, out var bytes)) throw new AmazonS3Exception("synthetic") { StatusCode = HttpStatusCode.NotFound };
        return Task.FromResult(new GetObjectResponse { HttpStatusCode = HttpStatusCode.OK, ContentLength = bytes.Length, ResponseStream = new MemoryStream(bytes) });
    }
    public override Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest r, CancellationToken ct = default)
    {
        Assert.False(ct.IsCancellationRequested); LastBucket = r.BucketName; Objects.TryRemove(r.Key, out _);
        return Task.FromResult(new DeleteObjectResponse { HttpStatusCode = HttpStatusCode.NoContent });
    }
}

