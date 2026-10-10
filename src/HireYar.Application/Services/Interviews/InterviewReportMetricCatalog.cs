namespace HireYar.Application.Interviews;

public static class InterviewReportMetricCatalog
{
    public static IReadOnlyList<string> Codes(string type) => type == "general"
        ? ["communication", "motivation", "teamwork", "working_practices", "responsibility", "conflict_management", "documentation"]
        : ["knowledge_depth", "communication", "job_alignment"];

    public const string Instructions = """
        Include metrics, exactly one object per code for the interview type. Each object has code, score,
        confidence, explanation and evidence:[{answerId,quote}]. Scores are integers 0..5, confidence 0..1.
        Each scored metric requires at least one exact final-answer transcript citation supporting this particular metric.
        If the question/dialogue did not assess it, score and confidence must both be null, evidence empty,
        and explanation must describe missing evidence. Never infer a score from a topic name or a resume claim.
        Specialized codes: knowledge_depth (understanding, mechanisms and concrete examples at the asked level),
        communication (structure and clarity of the transcript, never accent, voice or appearance),
        job_alignment (demonstrated job skills against explicit job description requirements; no JD means null).
        General codes: communication (structure and clarity), motivation (explicit job-related reasons and goals),
        teamwork (concrete collaboration behaviors), working_practices (explicit job-related working practices against
        stated role expectations, never personality, cultural background or demographic similarity),
        responsibility (accountability and follow-through examples), conflict_management (concrete resolution steps),
        documentation (job-related organization and record-keeping examples).
        General metrics ignore resume and introduction. No protected-trait, honesty or personality inference.
        Explanation is nonempty, max 1000 characters; evidence at most 5, quotes max 500 characters.
        Write explanations in the supplied language. Unknown evidence remains unknown, never zero by default.
        """;
}
