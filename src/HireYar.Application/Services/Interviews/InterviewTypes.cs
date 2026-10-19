using HireYar.Application.Common;
namespace HireYar.Application.Interviews;

public static class InterviewTypes
{
    public static string Require(string code) => code is "general" or "specialized"
        ? code : throw AppException.Invalid("invalid_interview_type");
}
