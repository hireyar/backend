using HireYar.Application.Common;
using HireYar.Domain.Interviews;

namespace HireYar.Application.Interviews;

public sealed partial class CandidateInterviewService
{
    public async Task<InterviewAudioCheckDto> CheckAudioAsync(Guid id, Stream stream, string fileName,
        string contentType, long length, int? durationSeconds, CancellationToken ct)
    {
        var session = await Load(id, ct);
        if (session.Status != InterviewStatus.Created)
            throw new AppException(409, "audio_check_unavailable", "تست میکروفون فقط پیش از شروع مصاحبه در دسترس است.");
        var limits = new InterviewOptions { MaxUploadBytes = Math.Min(options.MaxUploadBytes, 2 * 1024 * 1024), MaxAnswerSeconds = 30 };
        await AudioUploadValidator.ValidateAsync(stream, fileName, contentType, length, durationSeconds, limits, ct);
        if (stream.Length != length) throw AppException.Invalid("audio_incomplete");
        var mime = contentType.Split(';')[0].Trim().ToLowerInvariant();
        SpeechTranscriptionResult result;
        try
        {
            // Do not forward a user-controlled file name to the provider.
            result = await stt.TranscribeAsync(stream, "microphone-check" + AudioUploadValidator.Extension(mime), mime, ct);
        }
        catch (InterviewProviderException)
        {
            throw new AppException(503, "audio_check_transcription_unavailable", "تبدیل صدای آزمایشی به متن انجام نشد. دوباره تلاش کنید.");
        }
        // Access or interview state may have changed while the provider was running.
        await db.ReloadInterviewAsync(session, ct);
        CheckAccess(session);
        if (session.Status != InterviewStatus.Created)
            throw new AppException(409, "audio_check_unavailable", "تست میکروفون فقط پیش از شروع مصاحبه در دسترس است.");
        if (result.Text.Length > 32000) throw new AppException(503, "audio_check_transcription_unavailable", "خروجی تبدیل صدا معتبر نیست.");
        var text = result.Text.Trim();
        return new(true, length, text.Length > 0, text, false, text.Length > 0
            ? "فایل صوتی دریافت و گفتار به متن تبدیل شد. متن را با جمله‌ای که گفتید تطبیق دهید؛ شفافیت صوت به‌صورت مستقل سنجیده نشده است."
            : "فایل صوتی دریافت شد اما گفتاری تشخیص داده نشد. میکروفون را بررسی و دوباره امتحان کنید.");
    }
}
