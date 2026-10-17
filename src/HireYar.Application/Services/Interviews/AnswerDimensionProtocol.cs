using System.Text.Json;

namespace HireYar.Application.Interviews;

public sealed record AnswerDimensionDefinition(string Code, string Title, string Guidance);
public static class AnswerDimensionProtocol
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static IReadOnlyList<AnswerDimensionDefinition> Catalog(string type) => type == "general" ? [
        new("communication", "ارتباط و ساختار بیان", "Assess the clarity and structure of the submitted transcript, not voice, accent or personality."),
        new("motivation", "انگیزه شغلی بیان‌شده", "Only explicitly stated job-related reasons and concrete examples; do not infer internal commitment."),
        new("teamwork", "نمونه‌های همکاری", "Only concrete examples of collaboration and the candidate's stated contribution."),
        new("work_alignment", "همسویی با شیوه کار نقش", "Only explicitly stated working practices matched against explicit job description criteria. Unknown without job criteria; never infer cultural or demographic fit."),
        new("accountability", "مسئولیت‌پذیری در مثال‌های بیان‌شده", "Only stated ownership, actions and outcomes in concrete examples; do not infer a personality trait."),
        new("conflict_management", "مدیریت تعارض در مثال‌ها", "Only concrete job-related conflict examples and described actions."),
        new("documentation", "فرآیند و مستندسازی", "Only concrete described planning, follow-through or documentation practices.")
    ] : [
        new("knowledge_depth", "عمق دانش نشان‌داده‌شده", "Only demonstrated reasoning, mechanisms and tradeoffs relevant to this question."),
        new("communication", "شفافیت و ساختار پاسخ", "Only clarity and structure in the transcript; do not score voice, accent or personality."),
        new("job_alignment", "ارتباط شواهد با شرح نقش", "Only explicit job requirements supported by this answer. Unknown if job description or evidence is absent.")
    ];
    private static AnswerDimension Missing(string code, string state = "InsufficientEvidence", string reason = "dimension_not_returned") => new(code,state,null,null,null,[],reason);
    public static IReadOnlyList<AnswerDimension> Parse(string json, Guid answerId, AnswerAssessmentInput input)
    {
        var catalog = Catalog(input.InterviewType);
        try {
            using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=10});
            if(doc.RootElement.ValueKind!=JsonValueKind.Object || doc.RootElement.EnumerateObject().GroupBy(p=>p.Name).Any(g=>g.Count()>1)) throw new JsonException(); if(!doc.RootElement.TryGetProperty("dimensions",out var dimensions)) return catalog.Select(c=>Missing(c.Code)).ToArray();
            var entries=dimensions.EnumerateArray().Take(30).ToArray();
            return catalog.Select(c=> {
                try {
                    var matches=entries.Where(e=>e.ValueKind==JsonValueKind.Object && e.TryGetProperty("code",out var code) && code.ValueKind==JsonValueKind.String && code.GetString()==c.Code).ToArray();
                    if(matches.Length==0) return Missing(c.Code);
                    if(matches.Length!=1) throw new JsonException();
                    if(c.Code is "job_alignment" or "work_alignment" && string.IsNullOrWhiteSpace(input.PositionDescription)) return Missing(c.Code,reason:"job_description_missing");
                    var parsed=AnswerAssessmentProtocol.Parse(matches[0].GetRawText(),answerId,input.Excerpts);
                    if(parsed.Explanation.Length>500) throw new JsonException();
                    return new AnswerDimension(c.Code,parsed.Score is null?"InsufficientEvidence":"Succeeded",parsed.Score,parsed.Confidence,parsed.Explanation,parsed.Evidence,parsed.Score is null ? "dimension_insufficient_evidence" : null);
                } catch(Exception e) when(e is JsonException or InterviewProviderException or InvalidOperationException) {return Missing(c.Code,"Unavailable","dimension_invalid_output");}
            }).ToArray();
        } catch(Exception e) when(e is JsonException or InvalidOperationException) {return catalog.Select(c=>Missing(c.Code,"Unavailable","dimension_invalid_output")).ToArray();}
    }
    public static IReadOnlyList<AnswerDimension> Read(string json, string type, Guid answerId, string transcript)
    {
        AnswerDimension[] stored;
        try {stored=JsonSerializer.Deserialize<AnswerDimension[]>(json,Json)??[];}
        catch(JsonException) {return Catalog(type).Select(c=>Missing(c.Code,"Unavailable","dimension_storage_invalid")).ToArray();}
        return Catalog(type).Select(c=> {
            var found=stored.Where(d=>d is not null && d.Code==c.Code).ToArray();
            if(found.Length==0) return Missing(c.Code);
            var d=found[0];
            if(found.Length!=1 || d.State is not ("Succeeded" or "InsufficientEvidence" or "Unavailable")) return Missing(c.Code,"Unavailable","dimension_storage_invalid");
            if(d.State!="Succeeded") return d with { Value=null, Confidence=null, Evidence=[], FailureCode=d.FailureCode??"dimension_insufficient_evidence" };
            if(d.Value is null or <0 or >100 || d.Confidence is null or <0 or >1 || string.IsNullOrWhiteSpace(d.Explanation) || d.Explanation.Length>500 || d.Evidence is null || d.Evidence.Count is <1 or >5 ||
                d.Evidence.Any(e=>e is null || e.AnswerId!=answerId || e.Start<0 || e.Length<=0 || e.Start>transcript.Length-e.Length || transcript.Substring(e.Start,e.Length)!=e.Quote)) return Missing(c.Code,"Unavailable","dimension_storage_invalid");
            return d;
        }).ToArray();
    }
}
