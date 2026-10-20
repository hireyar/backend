using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace HireYar.Infrastructure.Auth;
public sealed class SalesAdminOptions
{
    public bool Enabled {get;set;}
    public string RandomKey {get;set;}="";
    public int AccessMinutes {get;set;}=15;
    public int AuthRequestsPerMinute {get;set;}=10;
    public bool IsValid()=>AccessMinutes is >=1 and <=30 && AuthRequestsPerMinute is >=1 and <=1000 && (!Enabled || Regex.IsMatch(RandomKey,"^[A-Za-z0-9_-]{32,128}$"));
    public bool MatchesEntry(string supplied)=>Enabled && supplied.Length<=128 && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(RandomKey)),SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
    public string EntryFingerprint=>TokenSecrets.Hash(RandomKey);
}
