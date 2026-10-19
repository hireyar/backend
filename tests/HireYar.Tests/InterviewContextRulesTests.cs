using System.IO.Compression;
using System.Text;
using System.Text.Json;
using HireYar.Application.Interviews;
using HireYar.Domain.Interviews;
using HireYar.Infrastructure.Interviews;

namespace HireYar.Tests;

public sealed class InterviewContextRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
    private static InterviewSession Session()
    {
        var skill = Guid.NewGuid();
        return new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Enumerable.Range(1, 3).Select(i => new InterviewQuestionSnapshot(Guid.NewGuid(), skill, $"Question {i}", $"topic{i}", i, true, i)),
            [new(skill, "Job skill", true, 100)], 3, 1800, Now, Now.AddDays(1));
    }
    [Fact]
    public void Introduction_stage_cannot_be_skipped_and_fixed_messages_survive_advancement()
    {
        var s = Session(); s.SetPanelSnapshot("Job", "JD", "Template", "Welcome", "Goodbye");
        s.SetAiSnapshot(InterviewDecisionPrompt.Master, "v2", "Resume", "fa-IR", true, "Introduce yourself", true, "Company");
        s.Start(s.Questions[0].QuestionId, Now);
        Assert.Equal(InterviewStage.Welcome, s.Stage);
        s.AdvanceStage(InterviewStage.CompanyIntroduction, null, Now);
        Assert.Equal(InterviewStage.Welcome, s.Stage);
        Assert.Throws<InterviewRuleException>(() => s.SubmitAnswer(s.CurrentQuestionId!.Value, "audio", "audio/mpeg", 10, Now));
        s.AdvanceStage(InterviewStage.Welcome, null, Now);
        Assert.Throws<InterviewRuleException>(() => s.AdvanceStage(InterviewStage.CandidateIntroduction, "", Now));
        Assert.Equal(InterviewStage.CandidateIntroduction, s.Stage);
        s.AdvanceStage(InterviewStage.CandidateIntroduction, "Synthetic career history", Now);
        Assert.Equal(InterviewStage.Questions, s.Stage);
        Assert.Equal("Synthetic career history", s.CandidateIntroductionTranscript);
        s.Complete(InterviewCompletionReason.Manual, Now);
        Assert.Equal(InterviewStage.CompanyIntroduction, s.Stage);
        s.AdvanceStage(InterviewStage.CompanyIntroduction, null, Now);
        Assert.Equal(InterviewStage.Goodbye, s.Stage);
        s.AdvanceStage(InterviewStage.Goodbye, null, Now);
        Assert.Equal(InterviewStage.Finished, s.Stage); Assert.Equal("Company", s.CompanyIntroductionMessage);
    }
    [Fact]
    public void General_interview_preserves_order_and_does_not_finish_on_sufficient_evidence()
    {
        var s = Session(); s.SetInterviewType("general"); var policy = new InterviewPolicy();
        Assert.Equal(s.Questions[0], policy.SelectFallback(s, Now));
        s.Start(s.Questions[0].QuestionId, Now);
        Assert.Equal(s.Questions[1], Assert.Single(policy.GetAllowedQuestions(s, Now)));
    }
    [Fact]
    public void Clarification_preserves_question_and_evidence_and_is_bounded()
    {
        var s = Session(); s.SetInteractionLimit(1); s.Start(s.Questions[0].QuestionId, Now);
        var a = s.SubmitAnswer(s.CurrentQuestionId!.Value, "audio", "audio/mpeg", 10, Now);
        s.BeginTranscription(a.Id); s.SetTranscript(a.Id, "Please clarify", "Test", "Test"); s.BeginEvaluation(a.Id);
        var decision = new InterviewDecision { AnswerAssessment = new() { Relevance = 0, Clarity = 0, EvidenceQuality = 0 },
            SkillUpdates = [], CoveredTopics = [], NextQuestionId = null, ShouldComplete = false, Reason = "Clarification", Action = "clarify", CandidateMessage = "Simpler wording" };
        var json = JsonSerializer.Serialize(decision, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var validator = new InterviewDecisionValidator(new());
        var validated = validator.ValidateOrFallback(json, s, [s.Questions[1].QuestionId], Now);
        Assert.False(validated.UsedFallback);
        s.ContinueQuestion(a.Id, validated.CandidateMessage!, "Test", "v2", Now);
        Assert.Equal(0, s.AnsweredQuestionCount); Assert.Empty(s.Evidence); Assert.True(a.IsInteraction);
        var nextTurn = s.SubmitAnswer(s.CurrentQuestionId!.Value, "audio2", "audio/mpeg", 10, Now);
        s.BeginTranscription(nextTurn.Id); s.SetTranscript(nextTurn.Id, "Corrected answer", "Test", "Test"); s.BeginEvaluation(nextTurn.Id);
        Assert.True(validator.ValidateOrFallback(json, s, [s.Questions[1].QuestionId], Now).UsedFallback);
        Assert.Throws<InterviewRuleException>(() => s.Complete(InterviewCompletionReason.Manual, Now));
    }
    [Fact]
    public async Task Resume_extraction_reads_docx_and_requires_explicit_text_for_legacy_or_unreadable_files()
    {
        using var file = new MemoryStream();
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open(), Encoding.UTF8);
            writer.Write("<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:p><w:r><w:t>Synthetic SQL projects</w:t></w:r></w:p></w:document>");
        }
        var extractor = new ResumeTextExtractor();
        var pdf = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var font = pdf.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        pdf.AddPage(300, 300).AddText("Synthetic SQL work history", 12, new UglyToad.PdfPig.Core.PdfPoint(20, 250), font);
        Assert.Equal("Synthetic SQL work history", await extractor.ExtractAsync(pdf.Build(), ".pdf", default));
        Assert.Equal("Synthetic SQL projects", await extractor.ExtractAsync(file.ToArray(), ".docx", default));
        Assert.Null(await extractor.ExtractAsync("%PDF-broken"u8.ToArray(), ".pdf", default));
        Assert.Null(await extractor.ExtractAsync([1, 2, 3], ".doc", default));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => extractor.ExtractAsync(file.ToArray(), ".docx", cancelled.Token));
    }
}
