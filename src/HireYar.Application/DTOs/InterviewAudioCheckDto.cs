namespace HireYar.Application.Interviews;

public sealed record InterviewAudioCheckDto(bool AudioReceived, long ReceivedBytes, bool SpeechRecognized,
    string Transcript, bool ClarityVerified, string Message);
