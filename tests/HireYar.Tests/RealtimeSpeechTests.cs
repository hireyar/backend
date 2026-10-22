using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using HireYar.Application.Common;
using HireYar.Application.Interviews;
using HireYar.Infrastructure.Interviews;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace HireYar.Tests;

public sealed class FakeRealtimeSpeechProvider(FakeInterviewProvider batch) : ISpeechToTextService, IRealtimeSpeechToTextService
{
    public TimeSpan FinalizationTimeout => TimeSpan.FromSeconds(5);
    public Task<SpeechTranscriptionResult> TranscribeAsync(Stream audio,string file,string type,CancellationToken ct) => batch.TranscribeAsync(audio,file,type,ct);
    public async Task<SpeechTranscriptionResult> TranscribeRealtimeAsync(Stream audio, Func<SpeechTranscriptUpdate,CancellationToken,Task> update,CancellationToken ct)
    {
        var buffer = new byte[16384]; var first = true;
        while (await audio.ReadAsync(buffer, ct) > 0) {
            if (first) { await update(new("", "متن موقت"),ct); first=false; }
        }
        await update(new("متن قطعی آزمایشی", ""), ct);
        return new("متن قطعی آزمایشی","Soniox","synthetic-realtime");
    }
}
public sealed class RealtimeSpeechReceiptTests
{
    private static RealtimeSpeechContext Context() => new(Guid.NewGuid(),Guid.NewGuid(),"answer", "turn",Guid.NewGuid(),0);
    [Theory]
    [InlineData("organization")][InlineData("interview")][InlineData("turn")][InlineData("purpose")][InlineData("question")][InlineData("version")]
    public async Task Receipt_cannot_be_reused_in_another_context(string field)
    {
        var clock=new TestClock(); var service=new RealtimeSpeechReceipt(new EphemeralDataProtectionProvider(),clock);var context=Context();byte[] data=[1,2,3];
        var receipt=service.Issue(context,new("متن", "Soniox", "test"),Convert.ToHexString(SHA256.HashData(data)),data.Length);
        var other=field switch {"organization"=>context with{OrganizationId=Guid.NewGuid()},"interview"=>context with{InterviewId=Guid.NewGuid()},
            "turn"=>context with{ClientTurnId="different"},"purpose"=>context with{Purpose="audioCheck"},"question"=>context with{QuestionId=Guid.NewGuid()},_=>context with{TurnVersion=1}};
        var error=await Assert.ThrowsAsync<AppException>(()=>service.VerifyAsync(receipt,other,new MemoryStream(data),default));Assert.Equal("realtime_receipt_invalid",error.Code);
    }
    [Fact]
    public async Task Receipt_checks_expiry_tampering_audio_hash_and_preserves_farsi_and_stream_position()
    {
        var clock=new TestClock(); var service=new RealtimeSpeechReceipt(new EphemeralDataProtectionProvider(),clock);var context=Context();byte[] data=[1,2,3];
        var result=new SpeechTranscriptionResult(new string('س',32000),"Soniox","test");
        var receipt=service.Issue(context,result,Convert.ToHexString(SHA256.HashData(data)),data.Length); Assert.True(receipt.Length<180000);
        using var audio=new MemoryStream(data); Assert.Equal(result,await service.VerifyAsync(receipt,context,audio,default));Assert.Equal(0,audio.Position);
        Assert.Equal("realtime_audio_mismatch",(await Assert.ThrowsAsync<AppException>(()=>service.VerifyAsync(receipt,context,new MemoryStream([1,2,4]),default))).Code);
        await Assert.ThrowsAsync<AppException>(()=>service.VerifyAsync("tampered"+receipt,context,audio,default));
        clock.Offset=TimeSpan.FromMinutes(16);await Assert.ThrowsAsync<AppException>(()=>service.VerifyAsync(receipt,context,audio,default));
    }
}
public sealed partial class InterviewFlowTests
{
    private static readonly byte[] LiveAudio=Convert.FromHexString("1A45DFA3000000000000000000000000");
    private static async Task WsSend(WebSocket ws,object value)=>await ws.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(value)),WebSocketMessageType.Text,true,default);
    private static async Task<JsonElement> WsRead(WebSocket ws)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));using var data=new MemoryStream();var buffer=new byte[16384];WebSocketReceiveResult part;
        do {part=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token); Assert.Equal(WebSocketMessageType.Text,part.MessageType);data.Write(buffer,0,part.Count);}while(!part.EndOfMessage);
        return JsonDocument.Parse(data.ToArray()).RootElement.Clone();
    }
    [PostgresFact]
    public async Task Realtime_stream_returns_interim_before_finish_and_receipt_skips_batch_stt_for_audio_check()
    {
        var (_,invite)=await Invite(); using var live=f.WithWebHostBuilder(b=>b.ConfigureTestServices(s=>{s.RemoveAll<ISpeechToTextService>();s.AddSingleton<ISpeechToTextService>(new FakeRealtimeSpeechProvider(f.InterviewAi));}));
        using var client=live.CreateClient();client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",invite.Token);
        var caps=await client.GetFromJsonAsync<SpeechCapabilitiesDto>($"/api/candidate/interviews/{invite.InterviewId}/speech/capabilities");Assert.Equal("realtime",caps!.Mode);
        var websocket=live.Server.CreateWebSocketClient();websocket.SubProtocols.Add("hireyar-stt-v1");websocket.SubProtocols.Add("hireyar-interview."+invite.Token);
        websocket.ConfigureRequest=r=>r.Headers.Origin="http://localhost:4200";
        using var ws=await websocket.ConnectAsync(new Uri($"ws://localhost/api/candidate/interviews/{invite.InterviewId}/speech/stream"),default);
        var turn=Guid.NewGuid().ToString("N");await WsSend(ws,new{type="start",purpose="audioCheck",clientTurnId=turn});Assert.Equal("ready",(await WsRead(ws)).GetProperty("type").GetString());
        await ws.SendAsync(new ArraySegment<byte>(LiveAudio),WebSocketMessageType.Binary,true,default);
        var interim=await WsRead(ws);Assert.Equal("متن موقت",interim.GetProperty("interimText").GetString());
        await WsSend(ws,new{type="finish"});string? receipt=null;
        for(var i=0;i<4;i++){var update=await WsRead(ws);if(update.GetProperty("type").GetString()=="completed"){receipt=update.GetProperty("receipt").GetString();break;}}
        Assert.NotNull(receipt);var before=f.InterviewAi.SttCalls;
        using var form=new MultipartFormDataContent();var content=new ByteArrayContent(LiveAudio);content.Headers.ContentType=new("audio/webm");form.Add(content,"audio","test.webm");
        form.Add(new StringContent(turn),"clientTurnId");form.Add(new StringContent(receipt),"realtimeReceipt");
        var response=await client.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/audio-check",form);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var check=await response.Content.ReadFromJsonAsync<InterviewAudioCheckDto>();Assert.Equal("متن قطعی آزمایشی",check!.Transcript);Assert.Equal(before,f.InterviewAi.SttCalls);
        await f.Db(async db=>{var session=await db.InterviewSessions.SingleAsync(x=>x.Id==invite.InterviewId);Assert.Equal(HireYar.Domain.Interviews.InterviewStatus.Created,session.Status);Assert.Empty(await db.InterviewAnswers.Where(x=>x.InterviewSessionId==invite.InterviewId).ToListAsync());});
    }
    [PostgresFact]
    public async Task Batch_provider_advertises_batch_and_rejects_realtime_without_breaking_existing_upload()
    {
        var (_,invite)=await Invite();using var client=Candidate(invite);
        var caps=await client.GetFromJsonAsync<SpeechCapabilitiesDto>($"/api/candidate/interviews/{invite.InterviewId}/speech/capabilities");Assert.Equal("batch",caps!.Mode);Assert.Null(caps.StreamUrl);
        Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync($"/api/candidate/interviews/{invite.InterviewId}/speech/stream")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await CheckAudio(client,invite.InterviewId)).StatusCode);
    }
    [PostgresFact]
    public async Task Realtime_handshake_rejects_wrong_origin_invalid_credential_and_second_connection()
    {
        var (_,invite)=await Invite();using var live=f.WithWebHostBuilder(b=>b.ConfigureTestServices(s=>{s.RemoveAll<ISpeechToTextService>();s.AddSingleton<ISpeechToTextService>(new FakeRealtimeSpeechProvider(f.InterviewAi));}));
        _=live.CreateClient();var uri=new Uri($"ws://localhost/api/candidate/interviews/{invite.InterviewId}/speech/stream");
        Microsoft.AspNetCore.TestHost.WebSocketClient Client(string origin,string credential){var client=live.Server.CreateWebSocketClient();client.SubProtocols.Add("hireyar-stt-v1");client.SubProtocols.Add("hireyar-interview."+credential);client.ConfigureRequest=r=>r.Headers.Origin=origin;return client;}
        var originError=await Record.ExceptionAsync(async()=>{using var invalid=await Client("https://evil.example.test",invite.Token).ConnectAsync(uri,default);});
        Assert.NotNull(originError);Assert.Contains("403",originError.Message);
        var authError=await Record.ExceptionAsync(async()=>{using var invalid=await Client("http://localhost:4200","invalid").ConnectAsync(uri,default);});
        Assert.NotNull(authError);Assert.Contains("401",authError.Message);
        using var first=await Client("http://localhost:4200",invite.Token).ConnectAsync(uri,default);
        await WsSend(first,new{type="start",purpose="audioCheck",clientTurnId="concurrency"});Assert.Equal("ready",(await WsRead(first)).GetProperty("type").GetString());
        var secondError=await Record.ExceptionAsync(async()=>{using var duplicate=await Client("http://localhost:4200",invite.Token).ConnectAsync(uri,default);});
        Assert.NotNull(secondError);Assert.Contains("409",secondError.Message);
    }

    private string MintReceipt(RealtimeSpeechContext context, byte[] data)
    {
        using var scope=f.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRealtimeSpeechReceipt>().Issue(context,new("متن معتبر سرور", "Soniox", "synthetic-realtime"),Convert.ToHexString(SHA256.HashData(data)),data.Length);
    }
    [PostgresFact]
    public async Task Realtime_answer_receipt_preserves_transcript_with_batch_provider_after_switch_and_replay_never_retranscribes()
    {
        var (_,invite)=await Invite();using var client=Candidate(invite);var state=await Start(client,invite.InterviewId);var question=state.Question!.Id;
        Guid org=default;await f.Db(async db=>org=await db.InterviewSessions.Where(x=>x.Id==invite.InterviewId).Select(x=>x.OrganizationId).SingleAsync());
        var turn=Guid.NewGuid().ToString("N");var receipt=MintReceipt(new(org,invite.InterviewId,"answer",turn,question,0),LiveAudio);var before=f.InterviewAi.SttCalls;
        MultipartFormDataContent Form(){var form=new MultipartFormDataContent();var audio=new ByteArrayContent(LiveAudio);audio.Headers.ContentType=new("audio/webm");form.Add(audio,"audio","answer.webm");form.Add(new StringContent(turn),"clientTurnId");form.Add(new StringContent(question.ToString()),"questionId");form.Add(new StringContent(receipt),"realtimeReceipt");return form;}
        using(var form=Form()){var response=await client.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers",form);Assert.Equal(HttpStatusCode.OK,response.StatusCode);}
        Assert.Equal(before,f.InterviewAi.SttCalls);Assert.Equal("متن معتبر سرور",f.InterviewAi.LastContext!.CandidateTranscript);
        using(var replay=Form()){Assert.Equal(HttpStatusCode.OK,(await client.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers",replay)).StatusCode);}
        Assert.Equal(before,f.InterviewAi.SttCalls);
        await f.Db(async db=>{var answers=await db.InterviewAnswers.Where(x=>x.InterviewSessionId==invite.InterviewId).ToArrayAsync();var answer=Assert.Single(answers);Assert.Equal("متن معتبر سرور",answer.Transcript);Assert.Equal("Soniox",answer.TranscriptProvider);});
    }
    [PostgresFact]
    public async Task Realtime_modified_audio_is_rejected_before_answer_is_saved()
    {
        var (_,invite)=await Invite();using var client=Candidate(invite);var state=await Start(client,invite.InterviewId);var question=state.Question!.Id;
        Guid org=default;await f.Db(async db=>org=await db.InterviewSessions.Where(x=>x.Id==invite.InterviewId).Select(x=>x.OrganizationId).SingleAsync());
        var turn=Guid.NewGuid().ToString("N");var receipt=MintReceipt(new(org,invite.InterviewId,"answer",turn,question,0),LiveAudio);
        using var form=new MultipartFormDataContent();var changed=LiveAudio.ToArray();changed[^1]=1;var audio=new ByteArrayContent(changed);audio.Headers.ContentType=new("audio/webm");form.Add(audio,"audio","answer.webm");
        form.Add(new StringContent(turn),"clientTurnId");form.Add(new StringContent(question.ToString()),"questionId");form.Add(new StringContent(receipt),"realtimeReceipt");
        var response=await client.PostAsync($"/api/candidate/interviews/{invite.InterviewId}/answers",form);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        await f.Db(async db=>Assert.Empty(await db.InterviewAnswers.Where(x=>x.InterviewSessionId==invite.InterviewId).ToListAsync()));
    }
    [PostgresFact]
    public async Task Realtime_revocation_before_finish_does_not_issue_a_receipt()
    {
        var (_,invite)=await Invite();using var live=f.WithWebHostBuilder(b=>b.ConfigureTestServices(s=>{s.RemoveAll<ISpeechToTextService>();s.AddSingleton<ISpeechToTextService>(new FakeRealtimeSpeechProvider(f.InterviewAi));}));
        _=live.CreateClient();var websocket=live.Server.CreateWebSocketClient();websocket.SubProtocols.Add("hireyar-stt-v1");websocket.SubProtocols.Add("hireyar-interview."+invite.Token);websocket.ConfigureRequest=r=>r.Headers.Origin="http://localhost:4200";
        using var ws=await websocket.ConnectAsync(new Uri($"ws://localhost/api/candidate/interviews/{invite.InterviewId}/speech/stream"),default);
        await WsSend(ws,new{type="start",purpose="audioCheck",clientTurnId="revoked"});await WsRead(ws);await ws.SendAsync(new ArraySegment<byte>(LiveAudio),WebSocketMessageType.Binary,true,default);await WsRead(ws);
        await f.Db(async db=>{var session=await db.InterviewSessions.SingleAsync(x=>x.Id==invite.InterviewId);session.RevokeAccess(f.Clock.GetUtcNow());await db.SaveChangesAsync();});
        await WsSend(ws,new{type="finish"});JsonElement result=default;
        for(var i=0;i<4;i++){result=await WsRead(ws);if(result.GetProperty("type").GetString()=="error")break;}
        Assert.Equal("error",result.GetProperty("type").GetString());Assert.False(result.TryGetProperty("receipt",out _));
    }

}

public sealed partial class PanelWizardTests
{
    [PostgresFact]
    public async Task Realtime_introduction_receipt_advances_stage_once_without_another_stt_or_llm()
    {
        f.InterviewAi.Reset();var (client,route,invite,_)=await VoiceInvite();using var candidate=client;
        Guid org=default;await f.Db(async db=>org=await db.InterviewSessions.Where(x=>x.Id==invite.InterviewId).Select(x=>x.OrganizationId).SingleAsync());
        var turn=Guid.NewGuid().ToString("N");var bytes=Convert.FromHexString("1A45DFA3000000000000000000000000");string receipt;
        using(var scope=f.Services.CreateScope()) receipt=scope.ServiceProvider.GetRequiredService<IRealtimeSpeechReceipt>().Issue(new(org,invite.InterviewId,"introduction",turn,null,0),new("معرفی زنده", "Soniox", "synthetic"),Convert.ToHexString(SHA256.HashData(bytes)),bytes.Length);
        MultipartFormDataContent Form(){var form=new MultipartFormDataContent();var audio=new ByteArrayContent(bytes);audio.Headers.ContentType=new("audio/webm");form.Add(audio,"audio","intro.webm");form.Add(new StringContent(turn),"clientTurnId");form.Add(new StringContent(receipt),"realtimeReceipt");return form;}
        var stt=f.InterviewAi.SttCalls;var llm=f.InterviewAi.LlmCalls;
        using(var form=Form()){var state=await ReadState(await candidate.PostAsync(route+"/introduction",form));Assert.Equal("Questions",state.Stage);Assert.Equal("معرفی زنده",state.Introduction!.Transcript);Assert.Equal("Processed",state.Introduction.Status);}
        using(var again=Form()){Assert.Equal(HttpStatusCode.OK,(await candidate.PostAsync(route+"/introduction",again)).StatusCode);}
        Assert.Equal(stt,f.InterviewAi.SttCalls);Assert.Equal(llm,f.InterviewAi.LlmCalls);
        await f.Db(async db=>Assert.Single(await db.InterviewIntroductions.Where(x=>x.InterviewSessionId==invite.InterviewId).ToListAsync()));
    }
}
