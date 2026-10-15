namespace HireYar.Application.Support;

public interface ITicketAttachmentStorage
{
    Task SaveAsync(string key, byte[] content, CancellationToken ct);
    Task<Stream> OpenAsync(string key, int expectedLength, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

