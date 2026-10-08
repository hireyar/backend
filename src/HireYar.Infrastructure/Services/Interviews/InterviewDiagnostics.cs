using System.Text;
using HireYar.Application.Common;
using HireYar.Application.Interviews;

namespace HireYar.Infrastructure.Interviews;

// Temporary, process-local ring buffers. No disk, database, ILogger or external telemetry.
public sealed class InterviewDiagnostics(InterviewDiagnosticOptions options, TimeProvider time) : IInterviewDiagnostics, IDisposable
{
    private sealed class Buffer
    {
        public readonly Queue<(long Sequence, InterviewDiagnosticDto Event, int Bytes)> Events = new();
        public DateTimeOffset Updated;
        public long DroppedThrough;
        public bool HasDropped;
    }
    private readonly object gate = new();
    private readonly Dictionary<(Guid, Guid), Buffer> buffers = new();
    private readonly Dictionary<(Guid, Guid), int> subscribers = new();
    private readonly string instance = Guid.NewGuid().ToString("N");
    private Timer? expiration;
    private long sequence;
    private long bytes;
    public bool Enabled => options.Enabled;
    public bool CapturePrompts => Enabled && options.CapturePrompts;
    private void Prune()
    {
        foreach (var buffer in buffers.Values)
            while (buffer.Events.TryPeek(out var entry) && time.GetUtcNow() - entry.Event.AtUtc > TimeSpan.FromMinutes(10)) Drop(buffer);
        foreach (var pair in buffers.Where(x => !subscribers.ContainsKey(x.Key) && time.GetUtcNow() - x.Value.Updated > TimeSpan.FromMinutes(10)).ToArray())
            Remove(pair.Key);
    }
    private void Remove((Guid, Guid) key)
    {
        if (!buffers.Remove(key, out var buffer)) return;
        bytes -= buffer.Events.Sum(x => (long)x.Bytes);
    }
    public void Publish(Guid organizationId, Guid interviewId, InterviewDiagnosticDto diagnostic)
    {
        if (!Enabled) return;
        lock (gate)
        {
            expiration ??= new Timer(_ => { lock (gate) Prune(); }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            Prune();
            var key = (organizationId, interviewId);
            if (!buffers.TryGetValue(key, out var buffer))
            {
                if (buffers.Count >= 128) Remove(buffers.Where(x => !subscribers.ContainsKey(x.Key)).MinBy(x => x.Value.Updated).Key);
                buffers[key] = buffer = new() { DroppedThrough = sequence };
            }
            var prompt = CapturePrompts ? diagnostic.PromptJson : null;
            var truncated = prompt?.Length > 524288;
            if (truncated) prompt = prompt![..524288];
            var number = ++sequence;
            var entry = diagnostic with { Id = $"{instance}:{number}", AtUtc = time.GetUtcNow(), PromptJson = prompt, PromptTruncated = truncated };
            var size = 2048 + Encoding.UTF8.GetByteCount(prompt ?? "");
            buffer.Events.Enqueue((number, entry, size)); bytes += size; buffer.Updated = time.GetUtcNow();
            while (buffer.Events.Count > 256) Drop(buffer);
            while (bytes > 64 * 1024 * 1024)
            {
                var oldest = buffers.Where(x => x.Value.Events.Count > 0).MinBy(x => x.Value.Events.Peek().Sequence).Value;
                Drop(oldest);
            }
        }
    }
    private void Drop(Buffer buffer)
    {
        var removed = buffer.Events.Dequeue(); bytes -= removed.Bytes; buffer.DroppedThrough = removed.Sequence; buffer.HasDropped = true;
    }
    public InterviewDiagnosticBatch Read(Guid organizationId, Guid interviewId, string? cursor, bool includePrompts)
    {
        lock (gate)
        {
            Prune();
            long after = 0;
            var parts = cursor?.Split(':');
            var reset = cursor is not null && (parts?.Length != 2 || parts[0] != instance || !long.TryParse(parts[1], out after) || after < 0 || after > sequence);
            if (reset) after = 0;
            if (!Enabled || !buffers.TryGetValue((organizationId, interviewId), out var buffer))
                return new([], reset || cursor is not null, $"{instance}:{sequence}");
            if (after < buffer.DroppedThrough && (cursor is not null || buffer.HasDropped)) reset = true;
            var entries = buffer.Events.Where(x => x.Sequence > after).Select(x => includePrompts ? x.Event : x.Event with { PromptJson = null, PromptTruncated = false }).ToArray();
            return new(entries, reset, entries.LastOrDefault()?.Id ?? $"{instance}:{Math.Max(after, sequence)}");
        }
    }
    public IDisposable Subscribe(Guid organizationId, Guid interviewId)
    {
        lock (gate)
        {
            Prune();
            var key = (organizationId, interviewId);
            if (!Enabled) throw AppException.NotFound();
            var count = subscribers.GetValueOrDefault(key);
            if (count >= 4 || subscribers.Values.Sum() >= 64) throw new AppException(429, "diagnostics_busy", "Too many diagnostic streams.");
            if (!buffers.TryGetValue(key, out var buffer))
            {
                if (buffers.Count >= 128) throw new AppException(429, "diagnostics_busy", "Diagnostic buffers are busy.");
                buffers[key] = buffer = new() { Updated = time.GetUtcNow(), DroppedThrough = sequence };
            }
            subscribers[key] = count + 1;
            return new Subscription(() => { lock (gate) { if (subscribers[key] == 1) subscribers.Remove(key); else subscribers[key]--; } });
        }
    }
    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? release = dispose;
        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }
    public void Dispose() { expiration?.Dispose(); lock (gate) buffers.Clear(); }
}
