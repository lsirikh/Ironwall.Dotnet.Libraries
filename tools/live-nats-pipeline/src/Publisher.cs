using System.Collections.Concurrent;
using NATS.Client.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiveNatsPipeline;

public sealed record Sent(DateTime At, string Subject, string Data);
public sealed record Tapped(DateTime At, string Subject, string Data);

/// <summary>
/// The probe's OWN NATS client (separate from the pipeline's): publishes test envelopes to sensorway.unit999.*
/// and taps everything on sensorway.unit999.&gt; (so the pipeline's own ACTION_REPORT publishes are observable).
/// </summary>
public sealed class Publisher : IAsyncDisposable
{
    NatsConnection? _conn;
    CancellationTokenSource? _tapCts;
    Task? _tapTask;
    public readonly ConcurrentQueue<Sent> SentLog = new();
    public readonly ConcurrentQueue<Tapped> Tap = new();
    public long MaxPayload => _conn?.ServerInfo?.MaxPayload ?? 0;
    public string ServerVersion => _conn?.ServerInfo?.Version ?? "?";

    public async Task StartAsync(string url)
    {
        Safety.AssertLoopback(url.Replace("nats://", "http://"));
        _conn = new NatsConnection(NatsOpts.Default with { Url = url, Name = "live-nats-pipeline-publisher" });
        await _conn.ConnectAsync();
        _tapCts = new CancellationTokenSource();
        var ready = new TaskCompletionSource();
        _tapTask = Task.Run(async () =>
        {
            try
            {
                await using var sub = await _conn.SubscribeCoreAsync<string>(Safety.SubjectPrefix + ">", cancellationToken: _tapCts.Token);
                ready.TrySetResult();
                await foreach (var m in sub.Msgs.ReadAllAsync(_tapCts.Token))
                    Tap.Enqueue(new Tapped(DateTime.Now, m.Subject, m.Data ?? ""));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { ready.TrySetException(ex); }
        });
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _conn.PingAsync();
    }

    public Task PublishAsync(string subject, JToken envelope) => PublishRawAsync(subject, envelope.ToString(Formatting.None));

    public async Task PublishRawAsync(string subject, string data)
    {
        Safety.AssertSubject(subject);
        SentLog.Enqueue(new Sent(DateTime.Now, subject, data));
        await _conn!.PublishAsync(subject, data);
    }

    public async Task FlushAsync()
    {
        if (_conn != null) await _conn.PingAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try { _tapCts?.Cancel(); } catch { }
        if (_tapTask != null) { try { await _tapTask.WaitAsync(TimeSpan.FromSeconds(3)); } catch { } }
        if (_conn != null) await _conn.DisposeAsync();
    }
}

/// <summary>Broker v2.0.7 envelopes: {id, m_type, cmd, from, body, created}; v7 device = {id, category_device}.</summary>
public static class Env
{
    public static string S(string tail) => Safety.SubjectPrefix + tail;
    public static readonly string Detect = S("all.event.detect");
    public static readonly string DetectAi = S("all.event_ai.detect");
    public static readonly string Malfunction = S("all.event.malfunction");
    public static readonly string Connection = S("all.event.connection");
    public static readonly string ActionReport = S("all.event.action-report");
    public static readonly string Operation = S("all.event.operation");
    public static readonly string SyncDevice = S("all.sync.device");
    public static readonly string SyncDeviceGroup = S("all.sync.device-group");
    public static readonly string SyncEventMapping = S("all.sync.event-mapping");
    public static readonly string SyncDetection = S("all.sync.detection");

    static string Now() => DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffffzzz");

    public static JObject Envelope(string cmd, JToken? body, string from = "PidsProxy", string? id = null, string mType = "PUB")
        => new()
        {
            ["id"] = id ?? Guid.NewGuid().ToString(),
            ["m_type"] = mType,
            ["cmd"] = cmd,
            ["from"] = from,
            ["body"] = body ?? JValue.CreateNull(),
            ["created"] = Now(),
        };

    public static JToken Ref(int id, string category) => new JObject { ["id"] = id, ["category_device"] = category };

    public static JObject DetectBody(int eventId, JToken? device, string typeEvent = "Intrusion", string result = "PIR_SENSOR", string desc = "probe")
        => new()
        {
            ["id"] = eventId,
            ["category_event"] = "detection",
            ["type_event"] = typeEvent,
            ["action_reported"] = false,
            ["result"] = result,
            ["device"] = device ?? JValue.CreateNull(),
            ["device_description"] = $"[probe] {desc} (id: {eventId})",
            ["detail"] = new JObject { ["signal"] = 2000 },
            ["created_at"] = Now(),
            ["updated_at"] = Now(),
        };

    public static JObject MalfunctionBody(int eventId, JToken? device, string reason)
        => new()
        {
            ["id"] = eventId,
            ["category_event"] = "malfunction",
            ["type_event"] = "Fault",
            ["action_reported"] = false,
            ["reason"] = reason,
            ["device"] = device ?? JValue.CreateNull(),
            ["device_description"] = $"[probe] malfunction {eventId}",
            ["detail"] = new JObject { ["first_start"] = 10, ["first_end"] = 15 },
            ["created_at"] = Now(),
            ["updated_at"] = Now(),
        };

    public static JObject ConnectionBody(int eventId, JToken? device)
        => new()
        {
            ["id"] = eventId,
            ["category_event"] = "connection",
            ["type_event"] = "Connection",
            ["device"] = device ?? JValue.CreateNull(),
            ["device_description"] = $"[probe] connection {eventId}",
            ["created_at"] = Now(),
            ["updated_at"] = Now(),
        };

    public static JObject OperationBody(int eventId, JToken? device, string? reason, string? state, string? previous = null, string component = "door")
    {
        var detail = new JObject { ["component"] = component, ["device_status"] = "ACTIVATED" };
        if (state != null) detail["state"] = state;
        if (previous != null) detail["previous"] = previous;
        var o = new JObject
        {
            ["id"] = eventId,
            ["category_event"] = "operation",
            ["type_event"] = "Operation",
            ["action_reported"] = false,
            ["severity"] = "WARNING",
            ["device"] = device ?? JValue.CreateNull(),
            ["device_description"] = $"[probe] operation {eventId}",
            ["detail"] = detail,
            ["created_at"] = Now(),
            ["updated_at"] = Now(),
        };
        if (reason != null) o["reason"] = reason;
        return o;
    }

    public static JObject ActionReportBody(int actionId, JObject fromEvent, string content = "probe action", string user = "probe")
    {
        var fe = (JObject)fromEvent.DeepClone();
        fe["action_reported"] = true;
        return new JObject
        {
            ["id"] = actionId,
            ["type_event"] = "Action",
            ["content"] = content,
            ["user"] = user,
            ["from_event"] = fe,
            ["created_at"] = Now(),
            ["updated_at"] = Now(),
        };
    }

    public static JObject SyncBody(string action, int resourceId, string? categoryDevice = null)
    {
        var o = new JObject { ["action"] = action, ["resource_id"] = resourceId };
        if (categoryDevice != null) o["category_device"] = categoryDevice;
        return o;
    }
}
