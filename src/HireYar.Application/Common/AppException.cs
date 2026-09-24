using HireYar.Domain.Organizations;
using HireYar.Domain.JobPositions;
using HireYar.Domain.InterviewTemplates;
using Microsoft.EntityFrameworkCore;
namespace HireYar.Application.Common;

public sealed class AppException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public static AppException NotFound() => new(404, "not_found", "Record not found.");
    public static AppException Invalid(string code = "invalid_request") => new(400, code, "The request is invalid.");
    public static AppException Unauthorized() => new(401, "invalid_credentials", "Authentication failed.");
    public static AppException Conflict() => new(409, "conflict", "The record changed or already exists.");
}
