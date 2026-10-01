using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace HireYar.Tests;

public sealed class InterviewAudioTests
{
    [Theory]
    [InlineData(".webm", "audio/webm", "1A45DFA3000000000000000000000000")]
    [InlineData(".wav", "audio/wav", "52494646000000005741564500000000")]
    [InlineData(".mp3", "audio/mpeg", "49443300000000000000000000000000")]
    [InlineData(".m4a", "audio/mp4", "00000018667479704D34412000000000")]
    [InlineData(".ogg", "audio/ogg", "4F676753000000000000000000000000")]
    public async Task Practical_browser_audio_signatures_are_accepted(string extension, string mime, string hex)
    {
        using var stream = new MemoryStream(Convert.FromHexString(hex));
        await AudioUploadValidator.ValidateAsync(stream, "answer" + extension, mime, stream.Length, 10, new(), default);
        Assert.Equal(0, stream.Position);
        using var forged = new MemoryStream(new byte[16]);
        Assert.Equal("audio_invalid", (await Assert.ThrowsAsync<AppException>(() => AudioUploadValidator.ValidateAsync(forged, "answer" + extension, mime, forged.Length, 10, new(), default))).Code);
    }
    [Fact]
    public async Task Empty_oversize_and_duration_uploads_are_rejected()
    {
        using var stream = new MemoryStream(new byte[16]);
        Assert.Equal("audio_invalid", (await Assert.ThrowsAsync<AppException>(() => AudioUploadValidator.ValidateAsync(stream, "a.mp3", "audio/mpeg", 0, null, new(), default))).Code);
        Assert.Equal("audio_too_large", (await Assert.ThrowsAsync<AppException>(() => AudioUploadValidator.ValidateAsync(stream, "a.mp3", "audio/mpeg", 100, null, new() { MaxUploadBytes = 16 }, default))).Code);
        Assert.Equal("answer_duration_invalid", (await Assert.ThrowsAsync<AppException>(() => AudioUploadValidator.ValidateAsync(stream, "a.mp3", "audio/mpeg", 16, 1000, new(), default))).Code);
    }
    [Theory]
    [InlineData("a.exe", "audio/webm")]
    [InlineData("a.webm", "audio/mpeg")]
    [InlineData("a.mp3", "audio/mpeg")]
    public async Task Extension_mime_and_signature_must_agree(string name, string mime)
    {
        using var audio = new MemoryStream(new byte[] { 0x1a, 0x45, 0xdf, 0xa3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
        Assert.Equal("audio_invalid", (await Assert.ThrowsAsync<AppException>(() =>
            AudioUploadValidator.ValidateAsync(audio, name, mime, audio.Length, 1, new(), default))).Code);
    }
    [Fact]
    public async Task Local_storage_round_trip_rejects_traversal_and_oversized_stream()
    {
        var root = Path.Combine(Path.GetTempPath(), "hireyar-audio-unit-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalInterviewAudioStorage(Options.Create(new InterviewStorageOptions { RootPath = root }));
        try
        {
            using var input = new MemoryStream("synthetic-audio"u8.ToArray());
            var saved = await storage.SaveAsync(input, "answers", Guid.NewGuid(), 100, default);
            await using (var read = await storage.OpenReadAsync(saved.Key, default)) { Assert.Equal(input.Length, read.Length); }
            await Assert.ThrowsAsync<IOException>(() => storage.OpenReadAsync("../private.txt", default));
            using var large = new MemoryStream(new byte[101]);
            await Assert.ThrowsAsync<IOException>(() => storage.SaveAsync(large, "answers", Guid.NewGuid(), 100, default));
            await storage.DeleteAsync(saved.Key, default);
            Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public void Enabled_provider_requires_key_and_valid_timeouts()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI:Provider"] = "NetArz" }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddInterviews(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<NetArzOptions>>().Value);
    }
}
