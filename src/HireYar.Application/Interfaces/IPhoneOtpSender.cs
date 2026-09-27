namespace HireYar.Application.Auth;

public interface IPhoneOtpSender
{
    Task SendAsync(string mobile, string code, CancellationToken ct);
}
