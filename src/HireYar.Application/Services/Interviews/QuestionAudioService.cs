using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HireYar.Application.Common;
using HireYar.Domain.Interviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace HireYar.Application.Interviews;

public sealed class QuestionAudioService(IApplicationDbContext db, IInterviewAudioStorage storage, ITextToSpeechService speech,
    TimeProvider time, ILogger<QuestionAudioService> logger)
{
    public static string Hash(string text, SpeechProfile profile) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        System.Text.Json.JsonSerializer.Serialize(new[] { text.Trim().Normalize(NormalizationForm.FormC), profile.Model, profile.Voice, profile.Speed.ToString("G29", CultureInfo.InvariantCulture) }))));
    public async Task<QuestionAudio?> ResolveAsync(Guid organizationId, string text, bool generate, CancellationToken ct)
    {
        var hash = Hash(text, speech.Profile);
        var cached = await db.QuestionAudios.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.AudioHash == hash, ct);
        if (cached is not null)
        {
            try { await using var existing = await storage.OpenReadAsync(cached.AudioStorageKey, ct); return cached; }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        if (!generate) return null;
        try
        {
            var audio = await speech.GenerateAsync(text, ct);
            using var stream = new MemoryStream(audio.Audio);
            var saved = await storage.SaveAsync(stream, "questions", organizationId, 10 * 1024 * 1024, ct);
            var row = cached is null ? new QuestionAudio { OrganizationId = organizationId, AudioHash = hash } :
                await db.QuestionAudios.SingleAsync(x => x.OrganizationId == organizationId && x.AudioHash == hash, ct);
            if (cached is null) db.QuestionAudios.Add(row);
            row.AudioStorageKey = saved.Key; row.AudioModel = speech.Profile.Model; row.AudioVoice = speech.Profile.Voice;
            row.AudioSpeed = speech.Profile.Speed; row.CreatedAtUtc = time.GetUtcNow();
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) when (cached is null)
            {
                db.DetachEntity(row);
                var winner = await db.QuestionAudios.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.AudioHash == hash, ct);
                if (winner is null) throw;
                await storage.DeleteAsync(saved.Key, ct); return winner;
            }
            return row;
        }
        catch (Exception e) when (e is InterviewProviderException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Question audio unavailable operation=tts failureCode={FailureCode}", e is InterviewProviderException p ? p.Code : "storage_unavailable");
            return null;
        }
    }
}
