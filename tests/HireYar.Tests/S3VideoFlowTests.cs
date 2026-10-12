using System.Net;
using HireYar.Application.Interviews;

namespace HireYar.Tests;

public sealed partial class InterviewFlowTests
{
    [PostgresFact]
    public async Task S3_recording_obeys_owner_scope_and_http_range_without_exposing_bucket_url()
    {
        var isolated = new ApiFixture { UseS3VideoStorage = true }; await isolated.InitializeAsync();
        try
        {
            var flow = new InterviewFlowTests(isolated);
            await flow.Session_recording_timeline_uses_existing_answers_and_replays_without_replacing_private_media();
            Assert.Single(isolated.VideoS3.Objects); Assert.Equal(0, isolated.VideoS3.Aborts);
            Assert.NotEmpty(isolated.VideoS3.Ranges);
        }
        finally { await ((IAsyncLifetime)isolated).DisposeAsync(); }
    }
}
