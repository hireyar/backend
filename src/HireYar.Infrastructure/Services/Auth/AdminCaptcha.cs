using System.Security.Cryptography;
using HireYar.Application.Auth;
using HireYar.Application.Common;
namespace HireYar.Infrastructure.Auth;

// Deliberately local to this process; expiry, restart and use all require a new challenge.
public sealed class AdminCaptcha(TimeProvider time) : IAdminCaptcha
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, (string Hash, DateTimeOffset Expires)> challenges = new();

    public AdminCaptchaDto Create()
    {
        var now = time.GetUtcNow(); var id = Guid.NewGuid(); var expires = now.AddMinutes(2);
        var answer = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        lock (gate)
        {
            foreach (var stale in challenges.Where(x => x.Value.Expires <= now).Select(x => x.Key).ToArray()) challenges.Remove(stale);
            if (challenges.Count >= 1024) throw new AppException(429, "rate_limited", "Please request another challenge later.");
            challenges.Add(id, (TokenSecrets.Hash(answer), expires));
        }
        // Raster output contains pixels only, with no text/vector answer in its markup.
        return new(id, "data:image/png;base64," + Convert.ToBase64String(AdminCaptchaImage.Render(answer)), expires);
    }
    public bool Consume(Guid id, string answer)
    {
        lock (gate)
        {
            if (!challenges.Remove(id, out var challenge) || challenge.Expires <= time.GetUtcNow()) return false;
            return answer.Length == 6 && answer.All(char.IsAsciiDigit) && CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(challenge.Hash), Convert.FromHexString(TokenSecrets.Hash(answer)));
        }
    }
}
