using System.Security.Cryptography;
using System.Text;
namespace HireYar.Infrastructure.Auth;

public sealed class SecurityOptions
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public int AccessMinutes { get; set; } = 15;
    public int RefreshDays { get; set; } = 7;
    public string FrontendUrl { get; set; } = "";
    public string[] AllowedOrigins { get; set; } = [];
    public string DataProtectionPath { get; set; } = "";
}
public static class TokenSecrets
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
