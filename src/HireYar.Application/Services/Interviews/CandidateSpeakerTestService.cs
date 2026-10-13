using HireYar.Application.Common;
using HireYar.Domain.Interviews;
namespace HireYar.Application.Interviews;

public sealed partial class CandidateInterviewService
{
    private const string SpeakerTestText = "سلام وقت بخیر";
    public async Task<Stream> OpenSpeakerTestAudioAsync(Guid id, CancellationToken ct)
    {
        var session = await Load(id, ct);
        if (session.Status != InterviewStatus.Created)
            throw new AppException(409, "speaker_test_unavailable", "تست اسپیکر فقط پیش از شروع مصاحبه در دسترس است.");
        var sound = await audio.ResolveAsync(session.OrganizationId, SpeakerTestText, true, ct);
        await db.ReloadInterviewAsync(session, ct);
        CheckAccess(session);
        if (session.Status != InterviewStatus.Created)
            throw new AppException(409, "speaker_test_unavailable", "تست اسپیکر فقط پیش از شروع مصاحبه در دسترس است.");
        if (sound is null)
            throw new AppException(503, "speaker_test_audio_unavailable", "صدای آزمایشی موقتاً در دسترس نیست. دوباره تلاش کنید.");
        return await storage.OpenReadAsync(sound.AudioStorageKey, ct);
    }
}
