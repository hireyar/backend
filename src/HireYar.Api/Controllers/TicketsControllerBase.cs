using System.ComponentModel.DataAnnotations;
using HireYar.Application.Common;
using HireYar.Application.Support;
using HireYar.Domain.Support;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HireYar.Api.Controllers;

public sealed class TicketReplyForm
{
    [Required, StringLength(8000, MinimumLength = 1)] public string Body { get; set; } = "";
    [MaxLength(TicketFileRules.MaxFiles)] public List<IFormFile> Files { get; set; } = [];
}
public sealed class TicketCreateForm
{
    [Required, StringLength(200, MinimumLength = 1)] public string Subject { get; set; } = "";
    [Required, StringLength(8000, MinimumLength = 1)] public string Body { get; set; } = "";
    [EnumDataType(typeof(TicketPriority))] public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    [MaxLength(TicketFileRules.MaxFiles)] public List<IFormFile> Files { get; set; } = [];
}

[ApiController, EnableRateLimiting("tickets")]
public abstract class TicketsControllerBase(TicketService tickets) : ControllerBase
{
    protected TicketService Tickets => tickets;
    protected abstract bool Administrator { get; }
    [HttpGet]
    public Task<TicketPage<TicketDto>> List([FromQuery] TicketQuery query, CancellationToken ct) => tickets.ListAsync(query, Administrator, ct);
    [HttpGet("{id:guid}")]
    public Task<TicketDto> Get(Guid id, CancellationToken ct) => tickets.GetAsync(id, Administrator, ct);
    [HttpGet("{id:guid}/messages")]
    public Task<TicketPage<TicketMessageDto>> Messages(Guid id, [FromQuery] TicketPageQuery query, CancellationToken ct) => tickets.MessagesAsync(id, query, Administrator, ct);
    [HttpPost("{id:guid}/messages"), Consumes("multipart/form-data")]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024, ValueLengthLimit = 10000, ValueCountLimit = 10, MultipartHeadersLengthLimit = 4096)]
    public async Task<TicketDto> Reply(Guid id, [FromForm] TicketReplyForm form, CancellationToken ct)
    {
        await tickets.GetAsync(id, Administrator, ct);
        return await tickets.ReplyAsync(id, form.Body, await ReadFilesAsync(form.Files, ct), Administrator, ct);
    }
    [HttpPut("{id:guid}/status")]
    public Task<TicketDto> Status(Guid id, TicketStatusRequest request, CancellationToken ct) => tickets.SetStatusAsync(id, request, Administrator, ct);
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}"), Produces("application/octet-stream")]
    public async Task<IActionResult> Download(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var file = await tickets.DownloadAsync(id, attachmentId, Administrator, ct);
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        return File(file.Content, "application/octet-stream", file.FileName);
    }
    protected static async Task<IReadOnlyList<TicketUpload>> ReadFilesAsync(IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (files.Count > TicketFileRules.MaxFiles) throw AppException.Invalid("ticket_attachment_count");
        var result = new List<TicketUpload>();
        foreach (var file in files)
        {
            if (file.Length is < 1 or > TicketFileRules.MaxFileBytes)
                throw new AppException(413, "ticket_file_size", "Each attachment must contain 1 byte to 5 MiB.");
            await using var input = file.OpenReadStream();
            using var output = new MemoryStream();
            var buffer = new byte[65536];
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (output.Length + read > TicketFileRules.MaxFileBytes)
                    throw new AppException(413, "ticket_file_size", "Attachment is too large.");
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            result.Add(new(file.FileName, file.ContentType, output.ToArray()));
        }
        TicketFileRules.Validate(result);
        return result;
    }
}

