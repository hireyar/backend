namespace HireYar.Api.Controllers;

public sealed record CsrfResponse(string Token);
public sealed record GenericEmailResponse(string Code = "request_received");
