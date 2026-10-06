using HireYar.Application.Interviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HireYar.Api.Controllers;

[ApiController, Authorize(Roles = "Owner"), Route("api/panel-wizard/question-suggestions")]
public sealed class QuestionSuggestionsController(QuestionSuggestionService service) : ControllerBase
{
    [HttpPost]
    public Task<QuestionSuggestionsDto> Generate(QuestionSuggestionRequest r, CancellationToken ct) => service.GenerateAsync(r, ct);
    [HttpPost("topic")]
    public Task<QuestionSuggestionsDto> RegenerateTopic(TopicSuggestionRequest r, CancellationToken ct) => service.RegenerateTopicAsync(r, ct);
}
