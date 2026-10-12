namespace HireYar.Infrastructure.Interviews;

public sealed class InterviewS3Options
{
    public string Endpoint { get; set; } = "https://s3.ir-central1.arvanstorage.ir";
    public string Region { get; set; } = "ir-central1";
    public string Bucket { get; set; } = "hireyar";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}
