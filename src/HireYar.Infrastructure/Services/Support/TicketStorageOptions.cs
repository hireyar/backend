

namespace HireYar.Infrastructure.Support;

// Independent credentials and bucket; never use the video bucket as a fallback.
public sealed class TicketStorageOptions
{
    public string Endpoint { get; set; } = "https://s3.ir-central1.arvanstorage.ir";
    public string Region { get; set; } = "ir-central1";
    public string Bucket { get; set; } = "hireyar-ticketing";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool IsValid() => Uri.TryCreate(Endpoint, UriKind.Absolute, out var u) && u.Scheme == "https" &&
        u.AbsolutePath == "/" && string.IsNullOrEmpty(u.UserInfo) && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment) &&
        !u.Host.StartsWith(Bucket + ".", StringComparison.OrdinalIgnoreCase) && System.Text.RegularExpressions.Regex.IsMatch(Bucket, "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$") &&
        !string.IsNullOrWhiteSpace(Region) && !string.IsNullOrWhiteSpace(AccessKey) && !string.IsNullOrWhiteSpace(SecretKey);
}

