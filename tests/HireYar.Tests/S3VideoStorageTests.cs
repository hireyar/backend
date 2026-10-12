using Amazon.S3;
using HireYar.Application.Common;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Options;

namespace HireYar.Tests;

public sealed class S3VideoStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hireyar-s3-unit-" + Guid.NewGuid().ToString("N"));
    private readonly FakeVideoS3 client = new();
    private S3InterviewVideoStorage Storage() => new(client, new LocalInterviewAudioStorage(Options.Create(new InterviewStorageOptions { RootPath = root })), Options.Create(new InterviewStorageOptions()));
    public void Dispose() { client.Dispose(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Fact]
    public async Task Multipart_is_bounded_private_and_seek_reads_only_requested_ranges()
    {
        var data = new byte[S3InterviewVideoStorage.PartBytes + 27]; Random.Shared.NextBytes(data);
        using var input = new MemoryStream(data);
        var storage = Storage(); var saved = await storage.SaveAsync(input, "videos", Guid.NewGuid(), data.Length, default);
        Assert.StartsWith("s3/videos/", saved.Key); Assert.Equal(data.Length, saved.SizeBytes);
        Assert.Equal(S3CannedACL.Private, client.WrittenAcl); Assert.Equal(S3InterviewVideoStorage.PartBytes, client.LargestPart);
        Assert.Equal(data, client.Objects[saved.Key]); Assert.False(Directory.Exists(root));
        await using var stream = await storage.OpenReadAsync(saved.Key, default);
        Assert.True(stream.CanSeek); Assert.Equal(data.Length, stream.Length);
        stream.Seek(-10, SeekOrigin.End); var last = new byte[10]; Assert.Equal(10, await stream.ReadAsync(last));
        Assert.Equal(data[^10..], last); Assert.Equal((data.Length - 10L, data.Length - 1L), Assert.Single(client.Ranges));
        stream.Seek(3, SeekOrigin.Begin); var first = new byte[7]; Assert.Equal(7, await stream.ReadAsync(first)); Assert.Equal(data[3..10], first);
        Assert.Equal(2, client.Ranges.Count); Assert.Equal(3, client.Ranges[1].Start);
        await storage.DeleteAsync(saved.Key, default); Assert.Empty(client.Objects);
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Public_acl_or_policy_is_rejected_before_any_content_is_uploaded(bool acl, bool policy)
    {
        client.PublicAcl = acl;
        if (policy) client.Policy = "{\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"AWS\":[\"*\"]},\"Action\":\"s3:GetObject\",\"Resource\":\"*\"}]}";
        using var input = new MemoryStream(new byte[10]);
        Assert.Equal("video_storage_not_private", (await Assert.ThrowsAsync<AppException>(() => Storage().SaveAsync(input, "videos", Guid.NewGuid(), 10, default))).Code);
        Assert.Equal(0, client.Initiations); Assert.Empty(client.Objects);
    }
    [Theory]
    [InlineData(0, 10)]
    [InlineData(11, 10)]
    public async Task Empty_and_oversized_streams_abort_upload(int length, int max)
    {
        using var input = new MemoryStream(new byte[length]);
        await Assert.ThrowsAsync<IOException>(() => Storage().SaveAsync(input, "videos", Guid.NewGuid(), max, default));
        Assert.Equal(1, client.Aborts); Assert.Empty(client.Objects);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_part_or_lost_completion_cleans_up_without_local_fallback_or_provider_message(bool completion)
    {
        client.FailPart = !completion; client.FailCompletion = completion;
        using var input = new MemoryStream(new byte[20]);
        var error = await Assert.ThrowsAsync<AppException>(() => Storage().SaveAsync(input, "videos", Guid.NewGuid(), 20, default));
        Assert.Equal(503, error.Status); Assert.DoesNotContain("synthetic", error.Message);
        Assert.Null(error.InnerException); Assert.Equal(1, client.Aborts); Assert.Empty(client.Objects); Assert.False(Directory.Exists(root));
    }
    [Fact]
    public async Task Cancellation_aborts_with_independent_token()
    {
        using var cancel = new CancellationTokenSource(); client.OnPart = cancel.Cancel;
        using var input = new MemoryStream(new byte[20]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Storage().SaveAsync(input, "videos", Guid.NewGuid(), 20, cancel.Token));
        Assert.Equal(1, client.Aborts); Assert.Empty(client.Objects);
    }
    [Fact]
    public async Task Old_local_media_remains_readable_and_cloud_keys_reject_traversal()
    {
        var storage = Storage(); using var input = new MemoryStream("legacy-audio"u8.ToArray());
        var saved = await storage.SaveAsync(input, "answers", Guid.NewGuid(), 100, default);
        Assert.StartsWith("answers/", saved.Key); Assert.Equal(0, client.Initiations);
        await using (var read = await storage.OpenReadAsync(saved.Key, default)) Assert.Equal(input.Length, read.Length);
        await Assert.ThrowsAsync<IOException>(() => storage.OpenReadAsync("s3/videos/../../private", default));
        await Assert.ThrowsAsync<IOException>(() => storage.DeleteAsync("s3/videos/../../private", default));
        await storage.DeleteAsync(saved.Key, default);
        var missing = $"s3/videos/{Guid.NewGuid():N}/{Guid.NewGuid():N}.audio";
        Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(() => storage.OpenReadAsync(missing, default))).Status);
    }
    [Theory]
    [InlineData("<Error><Code>NoSuchBucketPolicy</Code></Error>", true)]
    [InlineData("<Error><Code>AccessDenied</Code></Error>", false)]
    [InlineData("<!DOCTYPE Error [<!ENTITY secret SYSTEM 'file:///invalid'>]><Error><Code>&secret;</Code></Error>", false)]
    [InlineData("not XML", false)]
    public async Task Sdk_404_policy_response_accepts_only_confirmed_missing_policy(string body, bool missing)
    {
        client.Policy = body; client.PolicyStatus = System.Net.HttpStatusCode.NotFound;
        using var input = new MemoryStream(new byte[16]);
        if (missing) { var saved = await Storage().SaveAsync(input, "videos", Guid.NewGuid(), 16, default); Assert.Contains(saved.Key, client.Objects.Keys); }
        else {
            Assert.Equal("video_storage_unavailable", (await Assert.ThrowsAsync<AppException>(() => Storage().SaveAsync(input, "videos", Guid.NewGuid(), 16, default))).Code);
            Assert.Equal(0, client.Initiations);
        }
    }
    [Theory]
    [InlineData("{\"Statement\":{\"Effect\":\"Allow\",\"NotPrincipal\":{\"AWS\":\"private-user\"}}}", "video_storage_not_private")]
    [InlineData("{\"Statement\":[{\"Effect\":\"Allow\"}]}", "video_storage_not_private")]
    [InlineData("{}", "video_storage_unavailable")]
    public async Task Unverifiable_or_exclusion_policies_fail_closed(string policy, string code)
    {
        client.Policy = policy; using var input = new MemoryStream(new byte[16]);
        Assert.Equal(code, (await Assert.ThrowsAsync<AppException>(() => Storage().SaveAsync(input, "videos", Guid.NewGuid(), 16, default))).Code);
        Assert.Equal(0, client.Initiations);
    }
}
