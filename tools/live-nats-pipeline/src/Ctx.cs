using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Newtonsoft.Json.Linq;

namespace LiveNatsPipeline;

public sealed record CardInfo(string Kind, int EventId, string? EntryId, int? DeviceId);

/// <summary>Scenario helpers: publish, wait, read the REAL pipeline state (EQM entries, symbols, cards, logs).</summary>
public sealed class Ctx
{
    public readonly Pipeline P;
    public readonly Publisher Pub;
    public readonly Recorder Rec;
    int _eventId = 7_100_000 + (int)(DateTime.Now.Ticks % 100_000) * 10;

    public Ctx(Pipeline p, Publisher pub, Recorder rec) { P = p; Pub = pub; Rec = rec; }

    public int NextEventId() => Interlocked.Increment(ref _eventId);

    public static async Task<bool> WaitUntil(Func<bool> cond, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try { if (cond()) return true; } catch { }
            await Task.Delay(20);
        }
        try { return cond(); } catch { return false; }
    }

    public static Task Settle(int ms = 350) => Task.Delay(ms);

    // ---------------- state readers ----------------
    public List<EventEntry> Entries()
    {
        var t = P.Eqm.GetType();
        var gate = t.GetField("_gate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(P.Eqm)!;
        var dict = (IDictionary)t.GetField("_entries", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(P.Eqm)!;
        lock (gate) return dict.Values.Cast<EventEntry>().ToList();
    }

    public HashSet<string> DeviceIndexIds(int deviceId, EnumDeviceType type)
    {
        var t = P.Eqm.GetType();
        var gate = t.GetField("_gate", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(P.Eqm)!;
        var dict = (IDictionary)t.GetField("_deviceIndex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(P.Eqm)!;
        lock (gate)
        {
            var key = (deviceId, type);
            return dict.Contains(key) ? new HashSet<string>(((IEnumerable<string>)dict[key]!)) : new HashSet<string>();
        }
    }

    public List<CardInfo> Cards()
        => P.Cards.ViewModelProvider.ToList().Select(c => new CardInfo(
            c is DetectionEventCardViewModel ? "det" : c is MalfunctionEventCardViewModel ? "mal" : c.GetType().Name,
            c.Model?.Id ?? 0, c.EntryId, c.Model?.Device?.Id)).ToList();

    public EnumCompositeEventStatus Dev(DevSpec d) => P.Sym(d).CompositeStatus;
    public EnumCompositeEventStatus Grp(int g) => P.GroupSymbols.TryGetValue(g, out var s) ? s.CompositeStatus : EnumCompositeEventStatus.Normal;
    public EnumDoorState Door(DevSpec d) => P.Sym(d).DoorState;

    public string EntriesText() => string.Join("; ", Entries().Select(e => $"{e.EventType}#{e.EventId}@{e.DeviceId}/{e.DeviceType}[{string.Join(",", e.GroupIds ?? new())}]{(e.IsControllerBlackout ? "BO" : "")}"));
    public string CardsText() => string.Join("; ", Cards().Select(c => $"{c.Kind}#{c.EventId}→{(c.EntryId is null ? "null" : c.EntryId[..8])}"));

    public string LogsText(long mark, params string[] needles)
    {
        var lines = P.Log.Since(mark)
            .Where(l => needles.Length == 0 || needles.Any(n => l.Message.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .Take(10)
            .Select(l => $"{l.At:HH:mm:ss.fff} {l.Level} [{l.Where}] {Recorder.Trunc(l.Message, 220)}");
        return string.Join(" ⏎ ", lines);
    }

    public int CountLogs(long mark, string needle, string? level = null)
        => P.Log.Since(mark).Count(l => l.Message.Contains(needle, StringComparison.OrdinalIgnoreCase) && (level == null || l.Level == level));

    public List<LogLine> Errors(long mark) => P.Log.Since(mark).Where(l => l.Level == "ERROR").ToList();

    public List<FakeRequest> RestSince(DateTime t) => P.Server.Requests.Where(r => r.At >= t).ToList();

    // ---------------- reset ----------------
    public async Task Reset()
    {
        await Settle(150);
        P.Eqm.DequeueAll();
        foreach (var c in P.Cards.ViewModelProvider.ToList()) { try { c.Dispose(); } catch { } }
        P.Cards.ViewModelProvider.Clear();
        foreach (var name in new[] { "_pendingEntries", "_cardByEntryId" })
        {
            var f = typeof(EventCardListPanelViewModel).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            (f?.GetValue(P.Cards) as IDictionary)?.Clear();
        }
        foreach (var s in P.DeviceSymbols.Values) { s.CompositeStatus = EnumCompositeEventStatus.Normal; s.DoorState = EnumDoorState.Unknown; }
        foreach (var s in P.GroupSymbols.Values) s.CompositeStatus = EnumCompositeEventStatus.Normal;
        P.RestoreTokens();
        await Settle(100);
    }

    // ---------------- publishers ----------------
    public async Task<string> Pub1(string subject, JObject env) { await Pub.PublishAsync(subject, env); return env.Value<string>("id")!; }

    public Task<string> Detect(DevSpec d, int eventId, string typeEvent = "Intrusion", string? envId = null, string? subject = null, string result = "PIR_SENSOR")
        => Pub1(subject ?? Env.Detect, Env.Envelope("DETECT", Env.DetectBody(eventId, Env.Ref(d.Id, d.Category), typeEvent, result), id: envId));

    public Task<string> Malfunction(DevSpec d, int eventId, string reason, string? envId = null)
        => Pub1(Env.Malfunction, Env.Envelope("MALFUNCTION", Env.MalfunctionBody(eventId, Env.Ref(d.Id, d.Category), reason), id: envId));

    public Task<string> ActionReport(JObject fromEvent, int? actionId = null)
        => Pub1(Env.ActionReport, Env.Envelope("ACTION_REPORT", Env.ActionReportBody(actionId ?? NextEventId(), fromEvent), from: "GIS"));

    public Task<string> Sync(string subject, string cmd, JObject body, string? envId = null)
        => Pub1(subject, Env.Envelope(cmd, body, from: "DBApi", id: envId));

    public EventEntry? EntryFor(int eventId, EnumEventType type) => P.Eqm.FindEntryByEventId(eventId, type);
}
