using System.Diagnostics;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Sounds.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Newtonsoft.Json.Linq;
using static LiveNatsPipeline.Ctx;

namespace LiveNatsPipeline;

/// <summary>
/// Scenario catalogue. Each scenario publishes real broker envelopes on sensorway.unit999.* and judges the
/// REAL pipeline's observable state (EQM entries, symbol CompositeStatus/DoorState, cards + EntryId, logs,
/// the pipeline's own NATS publishes, REST attempts on the fake server).
/// Owner column: WP-1 = Events.Ui (sync services · EQM · SEM · card panel) + host NatsDomainService/NatsBrokerService.
///               WP-2 = GMaps.Ui.
/// </summary>
public static partial class Scenarios
{
    const string W1 = "WP-1";
    const EnumCompositeEventStatus Normal = EnumCompositeEventStatus.Normal;
    const EnumCompositeEventStatus Detecting = EnumCompositeEventStatus.Detecting;
    const EnumCompositeEventStatus Faulted = EnumCompositeEventStatus.Faulted;
    const EnumCompositeEventStatus FaultedDetecting = EnumCompositeEventStatus.FaultedDetecting;
    const EnumCompositeEventStatus Blackout = EnumCompositeEventStatus.Blackout;

    public static IEnumerable<(string Id, Func<Ctx, Task> Run)> All()
    {
        yield return ("S01", S01_DetectPerType);
        yield return ("S02", S02_AiDetect);
        yield return ("S03", S03_MalfunctionPerReason);
        yield return ("S04", S04_ControllerBlackout);
        yield return ("S05", S05_DetectAndMalfunctionSameDevice);
        yield return ("S06", S06_SameEventIdAcrossKinds);
        yield return ("S07", S07_ActionReportEcho);
        yield return ("S08", S08_ActionReportUnknown);
        yield return ("S09", S09_DuplicateEnvelope);
        yield return ("S10", S10_DeviceNull);
        yield return ("S11", S11_UnknownDevice);
        yield return ("S12", S12_LegacyBody);
        yield return ("S13", S13_ArrayEnvelope);
        yield return ("S14", S14_Burst200);
        yield return ("S15", S15_ReconnectMidBurst);
        yield return ("S16", S16_Connection);
        yield return ("S17", S17_OperationEvent);
        yield return ("S18", S18_SyncDevice);
        yield return ("S19", S19_SyncEventMapping);
        yield return ("S20", S20_MalformedJson);
        yield return ("S21", S21_CmdCasing);
        yield return ("S22", S22_Oversized);
        yield return ("S23", S23_LoginGate);
        yield return ("S24", S24_AlertTypeEvent);
        yield return ("S25", S25_SyncDetection);
        yield return ("S26", S26_TwoDetectionsOneReport);
        yield return ("S27", S27_ResponseEnvelope);
        yield return ("S28", S28_FaultThenDetectRace);
        yield return ("S29", S29_ImmediateActionReport);
        yield return ("S30", S30_Burst600Cap);
    }

    // =====================================================================================
    static string Groups(Ctx c, DevSpec d) => string.Join(",", d.Groups.Select(g => $"{g}:{c.Grp(g)}"));

    static async Task<(bool ok, string actual)> ExpectDetect(Ctx c, DevSpec d, int e, string env, EnumEventType type = EnumEventType.Intrusion)
    {
        var devType = c.P.Dev(d).DeviceType;
        var ok = await WaitUntil(() =>
            c.EntryFor(e, type) is { } en && en.EntryId == env && en.DeviceType == devType
            && c.Cards().Count(x => x.EventId == e && x.EntryId == env) == 1
            && c.Dev(d) == Detecting && d.Groups.All(g => c.Grp(g) == Detecting));
        await Settle(250);
        ok = ok && c.Cards().Count(x => x.EventId == e) == 1 && c.Entries().Count(x => x.EventId == e) == 1;
        return (ok, $"EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 심볼={c.Dev(d)} 그룹[{Groups(c, d)}]");
    }

    // ---------- S01 ----------
    static async Task S01_DetectPerType(Ctx c)
    {
        foreach (var d in new[] { Inv.Smart, Inv.Multi, Inv.Pir, Inv.Fence, Inv.Compound, Inv.Camera, Inv.Ctrl1, Inv.Enclosure, Inv.Gate, Inv.Lamp, Inv.Speaker })
        {
            await c.Reset();
            var e = c.NextEventId(); var mark = c.P.Log.Mark;
            var env = await c.Detect(d, e);
            var (ok, actual) = await ExpectDetect(c, d, e, env);
            c.Rec.Add($"S01.{d.TypeValue}/{d.Category}", $"DETECT Intrusion — {d.Category}:{d.TypeValue}({d.Id})",
                "EQM Intrusion 1건(키=장비 id+종류, EntryId=봉투 id) · 장비 심볼 Detecting · 소속 그룹 Detecting · 탐지 카드 1장(EntryId 연결)",
                actual, ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "DETECTION", "매핑", "심볼", "Enqueue"), ok ? "" : W1);
        }
    }

    // ---------- S02 ----------
    static async Task S02_AiDetect(Ctx c)
    {
        var d = Inv.Camera; var e = c.NextEventId(); var mark = c.P.Log.Mark;
        var env = await c.Detect(d, e, subject: Env.DetectAi, result: "AI_DETECT");
        var (ok, actual) = await ExpectDetect(c, d, e, env);
        var recv = c.CountLogs(mark, "DETECTION 수신");
        c.Rec.Add("S02.a", "AI 탐지(all.event_ai.detect, 카메라 참조)",
            "EQM 1건 · 카메라 심볼 Detecting · 그룹 11 Detecting · 탐지 카드 1장", actual + $" · 'DETECTION 수신' 로그 {recv}회",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "DETECTION", "AI", "그룹 이벤트"), ok ? "" : W1);

        mark = c.P.Log.Mark;
        await c.ActionReport(Env.DetectBody(e, Env.Ref(d.Id, d.Category), result: "AI_DETECT"));
        var cleared = await WaitUntil(() => c.EntryFor(e, EnumEventType.Intrusion) == null && c.Cards().Count == 0 && c.Dev(d) == Normal && c.Grp(11) == Normal);
        c.Rec.Add("S02.b", "AI 탐지 조치보고 메아리 → 복원",
            "카드 닫힘 · EQM 제거 · 카메라/그룹 심볼 Normal(호스트가 EQM 밖에서 직접 칠한 색도 풀려야 함)",
            $"EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 심볼={c.Dev(d)} 그룹11={c.Grp(11)}",
            cleared ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "ACTION_REPORT", "복원", "전이"), cleared ? "" : W1);
    }

    // ---------- S03 ----------
    static async Task S03_MalfunctionPerReason(Ctx c)
    {
        var cases = new (DevSpec d, string reason)[]
        {
            (Inv.Fence, "FAULT_FENCE"), (Inv.Multi, "FAULT_MULTI"), (Inv.Smart, "FAULT_CABLE_CUTTING"),
            (Inv.Pir, "FAULT_ETC"), (Inv.Camera, "FAULT_ETC"), (Inv.Compound, "FAULT_SENSOR" /* not in server EnumFaultType */),
        };
        foreach (var (d, reason) in cases)
        {
            await c.Reset();
            var e = c.NextEventId(); var mark = c.P.Log.Mark;
            var env = await c.Malfunction(d, e, reason);
            var devType = c.P.Dev(d).DeviceType;
            var ok = await WaitUntil(() =>
                c.EntryFor(e, EnumEventType.Fault) is { } en && en.EntryId == env && en.DeviceType == devType && !en.IsControllerBlackout
                && c.Cards().Count(x => x.Kind == "mal" && x.EventId == e && x.EntryId == env) == 1
                && c.Dev(d) == Faulted && d.Groups.All(g => c.Grp(g) == Faulted));
            await Settle(200);
            c.Rec.Add($"S03.{reason}/{d.TypeValue}", $"MALFUNCTION {reason} — {d.Category}:{d.TypeValue}({d.Id})",
                "EQM Fault 1건 · 장비 심볼 Faulted · 소속 그룹 Faulted · 장애 카드 1장(EntryId 연결)" + (reason == "FAULT_SENSOR" ? " (서버 어휘 밖 사유 — 버리지 않고 표시)" : ""),
                $"EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 심볼={c.Dev(d)} 그룹[{Groups(c, d)}]",
                ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "MALFUNCTION", "심볼", "파싱", "Error"), ok ? "" : W1);
        }
    }

    // ---------- S04 ----------
    static async Task S04_ControllerBlackout(Ctx c)
    {
        var ctrl = Inv.Ctrl1; var e = c.NextEventId(); var mark = c.P.Log.Mark;
        var env = await c.Malfunction(ctrl, e, "FAULT_CONTROLLER");
        var ok = await WaitUntil(() =>
            c.EntryFor(e, EnumEventType.Fault) is { IsControllerBlackout: true } && c.Grp(11) == Blackout && c.Grp(12) == Blackout && c.Dev(ctrl) == Blackout
            && c.Cards().Any(x => x.Kind == "mal" && x.EventId == e && x.EntryId == env));
        c.Rec.Add("S04.a", "제어기 무통신(FAULT_CONTROLLER, 제어기 2113) → 그룹 검정",
            "EQM Fault(IsControllerBlackout) · 제어기 심볼 Blackout · 소속 센서 그룹 11·12(Multi 2126 이 11/12 겸속) Blackout · 장애 카드",
            $"EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 제어기={c.Dev(ctrl)} 그룹11={c.Grp(11)} 그룹12={c.Grp(12)} 그룹13={c.Grp(13)}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "제어기무통신", "blackout", "그룹 복합"), ok ? "" : W1);

        // server marks controller ERROR (side effect of the fault POST) → SYNC_DEVICE UPDATED: must NOT recover
        c.P.Server.SetStatus(ctrl.Id, "ERROR");
        mark = c.P.Log.Mark; var t0 = DateTime.Now; var ar0 = c.P.AutoRecoveryFired.Count;
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("UPDATED", ctrl.Id, "controller"));
        await WaitUntil(() => c.CountLogs(mark, "SyncDeviceStatus") > 0 || c.CountLogs(mark, "처리 실패") > 0);
        await Settle(300);
        var stillBlack = c.Grp(11) == Blackout && c.Grp(12) == Blackout && c.P.AutoRecoveryFired.Count == ar0;
        c.Rec.Add("S04.b", "제어기 ERROR 상태 SYNC_DEVICE(UPDATED) — 아직 고장",
            "재조회(GET controllers/2113) → OperationState=ERROR · 블랙아웃 유지(오소거 없음)",
            $"REST[{string.Join(",", c.RestSince(t0).Select(r => r.Path + ":" + r.Status))}] 제어기 OperationState={c.P.Sym(ctrl).OperationState} 그룹11={c.Grp(11)} 자동복구={c.P.AutoRecoveryFired.Count - ar0}",
            stillBlack ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "SYNC_DEVICE", "상태 동기화"), stillBlack ? "" : W1);

        c.P.Server.SetStatus(ctrl.Id, "ACTIVATED");
        mark = c.P.Log.Mark; t0 = DateTime.Now;
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("UPDATED", ctrl.Id, "controller"));
        var recovered = await WaitUntil(() => c.EntryFor(e, EnumEventType.Fault) == null && c.Grp(11) == Normal && c.Grp(12) == Normal && c.Dev(ctrl) == Normal, 4000);
        await Settle(600);
        var writes = c.RestSince(t0).Where(r => r.BlockedWrite).Select(r => $"{r.Method} {r.Path}").ToList();
        c.Rec.Add("S04.c", "제어기 통신 복구 SYNC_DEVICE(UPDATED, ACTIVATED) → 자동복구",
            "블랙아웃 Fault Dequeue · 그룹 11·12·제어기 Normal · 자동복구 조치보고 발화(쓰기는 프로브가 차단 — 시도 자체를 기록)",
            $"EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 그룹11={c.Grp(11)} 그룹12={c.Grp(12)} 제어기={c.Dev(ctrl)} 자동복구 발화={c.P.AutoRecoveryFired.Count - ar0} 쓰기 시도=[{string.Join(", ", writes)}]",
            recovered ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "자동", "복구", "AutoRecovery", "조치보고"), recovered ? "" : W1);

        // S04.d controller-owned auto recovery: blackout then a DETECT from a sensor of that controller
        await c.Reset();
        var e2 = c.NextEventId();
        await c.Malfunction(ctrl, e2, "FAULT_CONTROLLER");
        await WaitUntil(() => c.Grp(12) == Blackout);
        mark = c.P.Log.Mark; ar0 = c.P.AutoRecoveryFired.Count;
        var e3 = c.NextEventId();
        await c.Detect(Inv.Smart, e3);
        var ok4 = await WaitUntil(() => c.EntryFor(e2, EnumEventType.Fault) == null && c.Grp(11) == Detecting && c.Grp(12) == Normal && c.Dev(ctrl) == Normal && c.Dev(Inv.Smart) == Detecting);
        await Settle(300);
        c.Rec.Add("S04.d", "블랙아웃 중 그 제어기 소속 센서(2131) 탐지 → 제어기-소유 자동복구",
            "제어기 Fault 제거(자동복구) · 그룹11 Detecting · 그룹12 Normal · 제어기 심볼 Normal · 센서 Detecting",
            $"EQM[{c.EntriesText()}] 그룹11={c.Grp(11)} 그룹12={c.Grp(12)} 제어기={c.Dev(ctrl)} 센서={c.Dev(Inv.Smart)} 자동복구 발화={c.P.AutoRecoveryFired.Count - ar0}",
            ok4 ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "자동", "전이", "Enqueue"), ok4 ? "" : W1);
    }

    // ---------- S05 ----------
    static async Task S05_DetectAndMalfunctionSameDevice(Ctx c)
    {
        var d = Inv.Smart; var e1 = c.NextEventId(); var e2 = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Detect(d, e1);
        await WaitUntil(() => c.Dev(d) == Detecting);
        await c.Malfunction(d, e2, "FAULT_CABLE_CUTTING");
        var ok = await WaitUntil(() => c.Dev(d) == FaultedDetecting && c.Grp(11) == FaultedDetecting && c.Entries().Count == 2 && c.Cards().Count == 2);
        c.Rec.Add("S05.a", "같은 장비: 탐지 → 장애 순서",
            "장비·그룹11 FaultedDetecting(우선순위 Blackout>FaultedDetecting>Faulted>Detecting) · EQM 2건 · 카드 2장",
            $"EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 심볼={c.Dev(d)} 그룹11={c.Grp(11)}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "전이", "상태"), ok ? "" : W1);

        // clear malfunction only → back to Detecting
        mark = c.P.Log.Mark;
        await c.ActionReport(Env.MalfunctionBody(e2, Env.Ref(d.Id, d.Category), "FAULT_CABLE_CUTTING"));
        var ok2 = await WaitUntil(() => c.Dev(d) == Detecting && c.Grp(11) == Detecting && c.EntryFor(e2, EnumEventType.Fault) == null && c.EntryFor(e1, EnumEventType.Intrusion) != null);
        c.Rec.Add("S05.b", "FaultedDetecting 에서 장애만 조치보고",
            "장애 엔트리만 제거 → 장비·그룹 Detecting 으로 내려감(탐지 유지)",
            $"EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 심볼={c.Dev(d)} 그룹11={c.Grp(11)}",
            ok2 ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "ACTION_REPORT", "전이"), ok2 ? "" : W1);

        await c.Reset();
        var e3 = c.NextEventId(); var e4 = c.NextEventId(); mark = c.P.Log.Mark; var ar0 = c.P.AutoRecoveryFired.Count;
        await c.Malfunction(d, e3, "FAULT_CABLE_CUTTING");
        await WaitUntil(() => c.Dev(d) == Faulted);
        await c.Detect(d, e4);
        await WaitUntil(() => c.Dev(d) == Detecting && c.Entries().Count == 1, 3000);
        await Settle(500);
        c.Rec.Add("S05.c", "같은 장비: 장애 → 탐지 순서(참고)",
            "설계상 '탐지 = 센서 생존' 자동복구: 장애 엔트리 제거 → Detecting (FaultedDetecting 아님)",
            $"EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 심볼={c.Dev(d)} 그룹11={c.Grp(11)} 자동복구 발화={c.P.AutoRecoveryFired.Count - ar0}",
            Verdict.INFO, c.LogsText(mark, "자동", "복구", "AutoRecovery"), "");
    }

    // ---------- S06 ----------
    static async Task S06_SameEventIdAcrossKinds(Ctx c)
    {
        var n = c.NextEventId(); var mark = c.P.Log.Mark;
        var envDet = await c.Detect(Inv.Smart, n);
        var envMal = await c.Malfunction(Inv.Pir, n, "FAULT_ETC");
        await WaitUntil(() => c.Cards().Count == 2 && c.Cards().All(x => x.EntryId != null) && c.Entries().Count == 2, 4000);
        await Settle(300);
        var det = c.Cards().FirstOrDefault(x => x.Kind == "det");
        var mal = c.Cards().FirstOrDefault(x => x.Kind == "mal");
        var pairOk = det?.EntryId == envDet && mal?.EntryId == envMal;
        c.Rec.Add("S06.a", $"같은 이벤트 번호({n})를 탐지(2131)·장애(2114)가 공유 — 카드↔큐 엔트리 짝",
            "탐지 카드 EntryId=탐지 봉투, 장애 카드 EntryId=장애 봉투(번호만으로 짝짓지 않음)",
            $"탐지카드→{det?.EntryId?[..8]} (정답 {envDet[..8]}) · 장애카드→{mal?.EntryId?[..8]} (정답 {envMal[..8]}) · EQM[{c.EntriesText()}]",
            pairOk ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "EntryId", "매칭"), pairOk ? "" : W1);

        mark = c.P.Log.Mark;
        await c.ActionReport(Env.MalfunctionBody(n, Env.Ref(Inv.Pir.Id, "sensor"), "FAULT_ETC"));
        await WaitUntil(() => c.Cards().Count == 1, 3000);
        await Settle(400);
        var left = c.Cards();
        var ok = left.Count == 1 && left[0].Kind == "det" && c.EntryFor(n, EnumEventType.Intrusion) != null && c.EntryFor(n, EnumEventType.Fault) == null
                 && c.Dev(Inv.Smart) == Detecting && c.Dev(Inv.Pir) == Normal;
        c.Rec.Add("S06.b", $"ACTION_REPORT(from_event.category_event=malfunction, id {n}) — 같은 번호의 탐지가 살아 있을 때",
            "장애 카드·장애 엔트리만 닫힘 · 탐지 카드/엔트리 유지 · 2131 Detecting · 2114 Normal",
            $"남은 카드[{c.CardsText()}] EQM[{c.EntriesText()}] 2131={c.Dev(Inv.Smart)} 2114={c.Dev(Inv.Pir)}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "ACTION_REPORT", "카드", "종결"), ok ? "" : W1);
    }

    // ---------- S07 ----------
    static async Task S07_ActionReportEcho(Ctx c)
    {
        var d = Inv.Fence; var e = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Detect(d, e);
        await WaitUntil(() => c.Cards().Any(x => x.EventId == e && x.EntryId != null));
        await c.ActionReport(Env.DetectBody(e, Env.Ref(d.Id, d.Category)));
        var ok = await WaitUntil(() => c.Cards().Count == 0 && c.Entries().Count == 0 && c.Dev(d) == Normal && c.Grp(13) == Normal);
        c.Rec.Add("S07.a", "탐지 조치보고 메아리(ACTION_REPORT, from_event=detection)",
            "탐지 카드 닫힘 · EQM 제거 · 심볼/그룹13 Normal", $"카드[{c.CardsText()}] EQM[{c.EntriesText()}] 심볼={c.Dev(d)} 그룹13={c.Grp(13)}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "ACTION_REPORT"), ok ? "" : W1);

        await c.Reset();
        d = Inv.Multi; e = c.NextEventId(); mark = c.P.Log.Mark;
        await c.Malfunction(d, e, "FAULT_MULTI");
        await WaitUntil(() => c.Cards().Any(x => x.EventId == e && x.EntryId != null));
        await c.ActionReport(Env.MalfunctionBody(e, Env.Ref(d.Id, d.Category), "FAULT_MULTI"));
        ok = await WaitUntil(() => c.Cards().Count == 0 && c.Entries().Count == 0 && c.Dev(d) == Normal && c.Grp(11) == Normal && c.Grp(12) == Normal);
        c.Rec.Add("S07.b", "장애 조치보고 메아리(ACTION_REPORT, from_event=malfunction)",
            "장애 카드 닫힘 · EQM 제거 · 심볼/그룹11·12 Normal", $"카드[{c.CardsText()}] EQM[{c.EntriesText()}] 심볼={c.Dev(d)} 그룹11={c.Grp(11)} 그룹12={c.Grp(12)}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "ACTION_REPORT"), ok ? "" : W1);
    }

    // ---------- S08 ----------
    static async Task S08_ActionReportUnknown(Ctx c)
    {
        var e = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Detect(Inv.Smart, e);
        await WaitUntil(() => c.Cards().Any(x => x.EntryId != null));
        await c.ActionReport(Env.DetectBody(e + 5_000_000, Env.Ref(Inv.Smart.Id, "sensor")));
        await c.ActionReport(new JObject { ["id"] = 0, ["category_event"] = "detection" });
        await Settle(800);
        var ok = c.Cards().Count == 1 && c.Entries().Count == 1 && c.Dev(Inv.Smart) == Detecting && c.Errors(mark).Count == 0;
        c.Rec.Add("S08", "모르는 이벤트/ id 0 의 ACTION_REPORT",
            "no-op(다른 카드·엔트리·심볼 불변) · ERROR 로그 없음",
            $"카드[{c.CardsText()}] EQM[{c.EntriesText()}] 심볼={c.Dev(Inv.Smart)} ERROR={c.Errors(mark).Count}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "ACTION_REPORT"), ok ? "" : W1);
    }

    // ---------- S09 ----------
    static async Task S09_DuplicateEnvelope(Ctx c)
    {
        var e = c.NextEventId(); var env = Guid.NewGuid().ToString(); var mark = c.P.Log.Mark;
        var sound0 = RecordingProxy<ISoundService>.Calls.Count;
        await c.Detect(Inv.Smart, e, envId: env);
        await c.Detect(Inv.Smart, e, envId: env);
        await WaitUntil(() => c.Cards().Count >= 1 && c.Entries().Count >= 1);
        await Settle(800);
        var cards = c.Cards().Count(x => x.EventId == e); var sounds = RecordingProxy<ISoundService>.Calls.Count - sound0;
        var ok = cards == 1 && c.Entries().Count == 1;
        c.Rec.Add("S09.a", "같은 봉투(id 동일)를 두 번 수신(브로커 재전송·이중 구독 모사)",
            "봉투 id 로 중복 제거(명세 §2.2: 메시지마다 새 uuid — 같은 id = 같은 메시지) → EQM 1건 · 카드 1장 · 탐지음 1회",
            $"EQM {c.Entries().Count}건 · 카드 {cards}장[{c.CardsText()}] · 탐지음 호출 {sounds}회",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "DETECTION 수신", "Enqueue", "EntryId"), ok ? "" : W1);

        // S09.b the same envelope id reused for a DIFFERENT device (misbehaving publisher)
        await c.Reset();
        var e1 = c.NextEventId(); var e2 = c.NextEventId(); env = Guid.NewGuid().ToString(); mark = c.P.Log.Mark;
        await c.Detect(Inv.Smart, e1, envId: env);
        await WaitUntil(() => c.Dev(Inv.Smart) == Detecting);
        await c.Detect(Inv.Pir, e2, envId: env);
        await WaitUntil(() => c.Dev(Inv.Pir) == Detecting);
        await Settle(400);
        var afterSecond = $"EQM[{c.EntriesText()}] 2131 인덱스={c.DeviceIndexIds(Inv.Smart.Id, EnumDeviceType.SmartSensor2).Count} 카드[{c.CardsText()}]";
        await c.ActionReport(Env.DetectBody(e2, Env.Ref(Inv.Pir.Id, "sensor")));
        await c.ActionReport(Env.DetectBody(e1, Env.Ref(Inv.Smart.Id, "sensor")));
        await WaitUntil(() => c.Cards().Count == 0, 3000);
        await Settle(500);
        var stale = c.DeviceIndexIds(Inv.Smart.Id, EnumDeviceType.SmartSensor2).Count;
        var ok2 = c.Dev(Inv.Smart) == Normal && stale == 0 && c.Entries().Count == 0 && c.Cards().Count == 0;
        c.Rec.Add("S09.b", "같은 봉투 id 가 다른 장비(2131→2114) 탐지에 재사용된 뒤 둘 다 조치보고",
            "두 이벤트가 각자 추적되거나(키 충돌 없음) 두 번째가 거부됨 → 조치 후 2131·2114 Normal, EQM 인덱스 잔여 0",
            $"두 번째 수신 직후: {afterSecond} → 조치 후: 2131={c.Dev(Inv.Smart)} 2114={c.Dev(Inv.Pir)} 2131 장비인덱스 잔여={stale} EQM[{c.EntriesText()}] 카드[{c.CardsText()}]",
            ok2 ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "Enqueue", "ACTION_REPORT", "EntryId"), ok2 ? "" : W1);
    }

    // ---------- S10 ----------
    static async Task S10_DeviceNull(Ctx c)
    {
        var e = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Pub1(Env.Detect, Env.Envelope("DETECT", Env.DetectBody(e, null)));
        var e2 = c.NextEventId();
        await c.Pub1(Env.Malfunction, Env.Envelope("MALFUNCTION", Env.MalfunctionBody(e2, null, "FAULT_ETC")));
        await WaitUntil(() => c.Cards().Count == 2, 3000);
        await Settle(400);
        var ok = c.Cards().Count == 2 && c.Entries().Count == 0 && c.Errors(mark).Count == 0;
        c.Rec.Add("S10", "device:null 탐지·장애(삭제된 장비, 브로커 §6.1/6.2)",
            "카드 2장(스냅샷) · EQM 0건 · 심볼 없음 · ERROR 없음",
            $"카드[{c.CardsText()}] EQM[{c.EntriesText()}] ERROR={c.Errors(mark).Count}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "장비 없음", "삭제", "Error"), ok ? "" : W1);
    }

    // ---------- S11 ----------
    static async Task S11_UnknownDevice(Ctx c)
    {
        // a sensor the server has but our cache has not seen yet (DETECT raced ahead of SYNC_DEVICE CREATED, 브로커 §6.1 '순서')
        var late = new DevSpec(2301, "sensor", "Fence", 204, "PRB-LATE-01", new[] { 13 }, 2115);
        c.P.Server.Put(late);
        var e = c.NextEventId(); var mark = c.P.Log.Mark; var t0 = DateTime.Now;
        await c.Pub1(Env.Detect, Env.Envelope("DETECT", Env.DetectBody(e, Env.Ref(late.Id, "sensor"))));
        await WaitUntil(() => c.Cards().Count >= 1, 3000);
        await Settle(800);
        var gets = c.RestSince(t0).Select(r => r.Path).ToList();
        var entry = c.EntryFor(e, EnumEventType.Intrusion);
        var ok = entry != null && gets.Any(p => p.Contains("/sensors/2301"));
        c.Rec.Add("S11.a", "캐시에 없는 센서 2301 의 DETECT(서버엔 있음 — SYNC_DEVICE CREATED 보다 먼저 도착)",
            "명세 §6.1: 캐시 미스면 GET /devices/sensors/2301 한 번 → 캐시에 넣고 처리(이벤트를 버리지 않음) → EQM 1건(Fence) · 그룹13 Detecting",
            $"GET 시도=[{string.Join(",", gets)}] EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 그룹13={c.Grp(13)}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "DETECTION", "종류", "캐시"), ok ? "" : W1);
        c.P.Server.Remove(late.Id);

        await c.Reset();
        e = c.NextEventId(); mark = c.P.Log.Mark; t0 = DateTime.Now;
        await c.Pub1(Env.Detect, Env.Envelope("DETECT", Env.DetectBody(e, Env.Ref(2399, "controller"))));
        await WaitUntil(() => c.Cards().Count >= 1, 3000);
        await Settle(800);
        gets = c.RestSince(t0).Select(r => r.Path).ToList();
        var entry2 = c.EntryFor(e, EnumEventType.Intrusion);
        c.Rec.Add("S11.b", "어디에도 없는 제어기 2399 의 DETECT(서버 404 = 삭제된 장비)",
            "S11.a 와 같은 규칙: 조회 → 404 → 스냅샷 카드만, 큐 엔트리 없음(sensor/controller 카테고리 간 동작이 같아야 함)",
            $"GET 시도=[{string.Join(",", gets)}] EQM[{c.EntriesText()}] 카드[{c.CardsText()}]",
            entry2 == null && gets.Count > 0 ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "DETECTION", "매핑", "장비"), entry2 == null && gets.Count > 0 ? "" : W1);
    }

    // ---------- S12 ----------
    static async Task S12_LegacyBody(Ctx c)
    {
        var d = Inv.Smart; var e = c.NextEventId(); var mark = c.P.Log.Mark;
        var legacyDevice = new JObject
        {
            ["id"] = d.Id, ["number_device"] = d.Number, ["name_device"] = d.Name, ["type_device"] = "SmartSensor2",
            ["status"] = "ACTIVATED", ["is_enable"] = true, ["version"] = "",
            ["device_groups"] = new JArray(new JObject { ["id"] = 11, ["name"] = "구역 11" }),
        };
        var env = await c.Pub1(Env.Detect, Env.Envelope("DETECT", Env.DetectBody(e, legacyDevice)));
        var (ok, actual) = await ExpectDetect(c, d, e, env);
        c.Rec.Add("S12", "v7 이전 전문(device 에 type_device + device_groups 중첩, category_device 없음)",
            "옛 서버 전문도 무회귀 처리 — EQM(SmartSensor2) · 심볼/그룹11 Detecting · 카드", actual,
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "DETECTION"), ok ? "" : W1);
    }

    // ---------- S13 ----------
    static async Task S13_ArrayEnvelope(Ctx c)
    {
        var e1 = c.NextEventId(); var e2 = c.NextEventId(); var mark = c.P.Log.Mark;
        var env1 = Env.Envelope("DETECT", Env.DetectBody(e1, Env.Ref(Inv.Smart.Id, "sensor")));
        var env2 = Env.Envelope("DETECT", Env.DetectBody(e2, Env.Ref(Inv.Pir.Id, "sensor")));
        await c.Pub.PublishAsync(Env.Detect, new JArray(env1, env2));
        await WaitUntil(() => c.Cards().Count == 2 && c.Entries().Count == 2 && c.Dev(Inv.Smart) == Detecting && c.Dev(Inv.Pir) == Detecting, 3000);
        await Settle(400);
        var ok = c.Cards().Count == 2 && c.Entries().Count == 2 && c.Dev(Inv.Smart) == Detecting && c.Dev(Inv.Pir) == Detecting;
        c.Rec.Add("S13", "배열 봉투 [DETECT, DETECT] 한 메시지(호스트 ParseMessageItems 가 지원하는 형태)",
            "두 항목 모두: 카드 2장 · EQM 2건 · 2131/2114 Detecting (호스트와 라이브러리가 같은 형태를 받아야 함)",
            $"카드[{c.CardsText()}] EQM[{c.EntriesText()}] 2131={c.Dev(Inv.Smart)} 2114={c.Dev(Inv.Pir)} ERROR={c.Errors(mark).Count}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "오류", "Error", "JObject", "DETECTION"), ok ? "" : W1);
    }

    static readonly DevSpec[] Sensors = { Inv.Smart, Inv.Smart2, Inv.Multi, Inv.Pir, Inv.Fence, Inv.Compound };

    // ---------- S14 ----------
    static async Task S14_Burst200(Ctx c)
    {
        var (sent, enq, cards, lat, drainMs) = await Burst(c, 200, 5000, null);
        var p50 = Pct(lat, 50); var p95 = Pct(lat, 95); var max = lat.Count > 0 ? lat.Max() : -1;
        var ok = enq == sent && cards == sent && p95 < 1000;
        c.Rec.Add("S14", $"폭주 {sent}건 / 5초(센서 6종 순환, 고유 이벤트 번호)",
            "유실 0(EQM {sent} · 카드 {sent}) · 게시→Enqueue 지연 p95 < 1 s",
            $"EQM {enq}/{sent} · 카드 {cards}/{sent} · 지연 p50 {p50:F0} ms · p95 {p95:F0} ms · max {max:F0} ms · 마지막 게시 후 전량 반영까지 {drainMs} ms",
            ok ? Verdict.PASS : Verdict.FAIL, $"'DETECTION Enqueue 완료' 로그 {lat.Count}건 기준", ok ? "" : W1);
    }

    static double Pct(List<double> xs, int p) { if (xs.Count == 0) return -1; var s = xs.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, (int)Math.Ceiling(p / 100.0 * s.Count) - 1)]; }

    static async Task<(int sent, int enq, int cards, List<double> lat, long drainMs)> Burst(Ctx c, int n, int spanMs, Func<int, Task>? atStep)
    {
        var mark = c.P.Log.Mark;
        var sentAt = new Dictionary<int, DateTime>();
        var ids = new List<int>();
        var sw = Stopwatch.StartNew();
        Task? side = null;
        for (var i = 0; i < n; i++)
        {
            var e = c.NextEventId(); ids.Add(e);
            sentAt[e] = DateTime.Now;
            await c.Detect(Sensors[i % Sensors.Length], e);
            if (atStep != null && side == null) { var t = atStep(i); if (!t.IsCompleted || i == n / 2) side = t; }
            var due = (long)((i + 1) * (double)spanMs / n);
            var wait = due - sw.ElapsedMilliseconds;
            if (wait > 0) await Task.Delay((int)wait);
        }
        if (side != null) await side;
        var lastSent = DateTime.Now;
        var idSet = new HashSet<int>(ids);
        await WaitUntil(() => c.Entries().Count(x => idSet.Contains(x.EventId)) >= n && c.Cards().Count(x => idSet.Contains(x.EventId)) >= n, 15000);
        var drain = (long)(DateTime.Now - lastSent).TotalMilliseconds;
        await Settle(500);
        var lat = new List<double>();
        foreach (var l in c.P.Log.Since(mark).Where(l => l.Message.StartsWith("DETECTION Enqueue 완료")))
        {
            var k = l.Message.LastIndexOf("eventId=", StringComparison.Ordinal);
            if (k > 0 && int.TryParse(l.Message[(k + 8)..].Trim(), out var eid) && sentAt.TryGetValue(eid, out var t0))
                lat.Add((l.At - t0).TotalMilliseconds);
        }
        return (n, c.Entries().Count(x => idSet.Contains(x.EventId)), c.Cards().Count(x => idSet.Contains(x.EventId)), lat, drain);
    }

    // ---------- S15 ----------
    static async Task S15_ReconnectMidBurst(Ctx c)
    {
        var mark = c.P.Log.Mark;
        var reconnectMs = 0L;
        var (sent, enq, cards, lat, drainMs) = await Burst(c, 200, 5000, async i =>
        {
            if (i != 100) return;
            var sw = Stopwatch.StartNew();
            await c.P.ReconnectAsync();
            reconnectMs = sw.ElapsedMilliseconds;
        });
        c.Rec.Add("S15.a", "폭주 200건 중간(100건째)에 파이프라인 NATS 클라이언트를 버리고 새로 연결(브로커는 건드리지 않음)",
            "재연결 성공 · 끊긴 동안의 유실은 명세상 허용(RF-8: 재연결 뒤 전량 재적재) — 유실 건수를 기록",
            $"재연결 {reconnectMs} ms · EQM {enq}/{sent} · 카드 {cards}/{sent} · 유실 {sent - enq}건 · 재연결 후 재동기화 동작=없음(관측)",
            Verdict.INFO, c.LogsText(mark, "Subscription", "Dispose", "Connect", "timed out"), "");

        await c.Reset();
        var e = c.NextEventId(); mark = c.P.Log.Mark;
        await c.Detect(Inv.Fence, e);
        await WaitUntil(() => c.Cards().Count == 1 && c.Entries().Count == 1, 3000);
        await Settle(800);
        var recv = c.CountLogs(mark, "DETECTION 수신"); var cardsN = c.Cards().Count;
        var ok = recv == 1 && cardsN == 1 && c.Entries().Count == 1;
        c.Rec.Add("S15.b", "재연결 뒤 단건 DETECT — 중복 구독/유실 없음",
            "라이브러리 'DETECTION 수신' 1회 · 카드 1장 · EQM 1건",
            $"'DETECTION 수신' {recv}회 · 카드 {cardsN}장 · EQM {c.Entries().Count}건",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "DETECTION"), ok ? "" : W1);
    }

    // ---------- S16 ----------
    static async Task S16_Connection(Ctx c)
    {
        var e = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Pub1(Env.Connection, Env.Envelope("CONNECTION", Env.ConnectionBody(e, Env.Ref(Inv.Ctrl1.Id, "controller"))));
        await Settle(1000);
        var errs = c.Errors(mark);
        var ok = c.Cards().Count == 0 && c.Entries().Count == 0 && errs.Count == 0;
        c.Rec.Add("S16", "CONNECTION 이벤트(v2.0 body, 소비자 없음 — 브로커 §6.3)",
            "조용히 무시(카드·EQM 없음, ERROR 로그 없음 — 명세 §2.4 N-2: 모르는/미소비 메시지는 버리되 오류가 아님)",
            $"카드 {c.Cards().Count} · EQM {c.Entries().Count} · ERROR {errs.Count}건: {string.Join(" / ", errs.Select(x => $"[{x.Where}] {Recorder.Trunc(x.Message, 160)}"))}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark), ok ? "" : W1);
    }

    // ---------- S17 ----------
    static async Task S17_OperationEvent(Ctx c)
    {
        async Task Op(string id, DevSpec? d, string? reason, string? state, EnumDoorState expected, string what, string? previous = null, string component = "door")
        {
            var mark = c.P.Log.Mark; var e = c.NextEventId();
            await c.Pub1(Env.Operation, Env.Envelope("OPERATION_EVENT",
                Env.OperationBody(e, d is null ? null : Env.Ref(d.Id, d.Category), reason, state, previous, component), from: "DBApi"));
            if (d != null) await WaitUntil(() => c.Door(d) == expected, 1500);
            await Settle(300);
            var actualDoor = d is null ? EnumDoorState.Unknown : c.Door(d);
            var noSide = c.Cards().Count == 0 && c.Entries().Count == 0;
            var ok = (d is null || actualDoor == expected) && noSide && c.Errors(mark).Count == 0;
            var unknownWarn = c.CountLogs(mark, "Unknown command") + c.CountLogs(mark, "Unknown type_command");
            c.Rec.Add(id, what, $"DoorState={expected} · 카드/EQM 없음 · ERROR 없음",
                $"DoorState={actualDoor} · 카드 {c.Cards().Count} · EQM {c.Entries().Count} · ERROR {c.Errors(mark).Count} · 호스트 'Unknown command' 경고 {unknownWarn}건",
                ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "OPERATION", "DoorState", "Unknown"), ok ? "" : W1);
        }

        await Op("S17.a", Inv.Enclosure, "ENCLOSURE_DOOR_OPEN", "OPEN", EnumDoorState.Open, "OPERATION_EVENT 함체 문 열림(v2.0 detail.state=OPEN)", "CLOSED");
        await Op("S17.b", Inv.Enclosure, "ENCLOSURE_DOOR_CLOSED", "CLOSED", EnumDoorState.Closed, "OPERATION_EVENT 함체 문 닫힘", "OPEN");
        await Op("S17.c", Inv.Gate, "GATE_OPEN", "OPEN", EnumDoorState.Open, "OPERATION_EVENT 통문 열림(구동부 actuator)", "RUNNING", "actuator");
        await Op("S17.d", Inv.Gate, null, "RUNNING", EnumDoorState.Open, "통문 detail.state=RUNNING, reason 없음(서버는 RUNNING 진입에 이벤트를 안 만들지만 들어오면)", "OPEN", "actuator");
        await Op("S17.e", Inv.Gate, "GATE_CLOSED", "CLOSED", EnumDoorState.Closed, "OPERATION_EVENT 통문 닫힘", "RUNNING", "actuator");
        await Op("S17.f", Inv.Enclosure, "ENCLOSURE_TEMP_HIGH", null, EnumDoorState.Closed, "함체 환경 임계(온도) — 개폐 무관 → 형태 불변");
        await Op("S17.g", null, "ENCLOSURE_DOOR_OPEN", "OPEN", EnumDoorState.Unknown, "device:null 운영 이벤트(지워진 함체)");

        // contact fallback on enclosure: DETECT ContactOn/ContactOff → door shape only, no card/queue
        var mark2 = c.P.Log.Mark;
        await c.Reset();
        var e1 = c.NextEventId();
        await c.Detect(Inv.Enclosure, e1, typeEvent: "ContactOn", result: "CONTACT");
        var open = await WaitUntil(() => c.Door(Inv.Enclosure) == EnumDoorState.Open, 1500);
        await Settle(400);
        var noSide = c.Cards().Count == 0 && c.Entries().Count == 0;
        var e2 = c.NextEventId();
        await c.Detect(Inv.Enclosure, e2, typeEvent: "ContactOff", result: "CONTACT");
        var closed = await WaitUntil(() => c.Door(Inv.Enclosure) == EnumDoorState.Closed, 1500);
        await Settle(400);
        var ok = open && closed && noSide && c.Cards().Count == 0 && c.Entries().Count == 0;
        c.Rec.Add("S17.h", "함체 접점 DETECT ContactOn → ContactOff (FR-13③ 접점 폴백)",
            "DoorState Open → Closed · 카드/EQM/탐지음 없음",
            $"열림 반영={open} 닫힘 반영={closed} · 카드 {c.Cards().Count} · EQM {c.Entries().Count}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark2, "접점", "DoorState"), ok ? "" : W1);
    }

    // ---------- S18 ----------
    static async Task S18_SyncDevice(Ctx c)
    {
        // a) CREATED → cache gains the device → a DETECT on it resolves from cache
        var created = new DevSpec(2300, "sensor", "Fence", 205, "PRB-NEW-01", new[] { 13 }, 2115);
        c.P.Server.Put(created);
        var mark = c.P.Log.Mark; var t0 = DateTime.Now;
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("CREATED", created.Id, "sensor"));
        var inCache = await WaitUntil(() => c.P.Devices.Any(x => x.Id == created.Id), 3000);
        var e = c.NextEventId();
        await c.Detect(created, e);
        var ok = await WaitUntil(() => c.EntryFor(e, EnumEventType.Intrusion) is { DeviceType: EnumDeviceType.Fence } && c.Grp(13) == Detecting, 3000);
        c.Rec.Add("S18.a", "SYNC_DEVICE CREATED {action, resource_id, category_device} → 새 센서 2300 → 그 장비 DETECT",
            "GET /devices/sensors/2300 → 캐시 추가 → DETECT 가 캐시에서 종류(Fence)·그룹(13) 해석 → EQM · 그룹13 Detecting (심볼은 지도가 새로 붙이기 전이라 없음)",
            $"캐시 추가={inCache} REST[{string.Join(",", c.RestSince(t0).Select(r => r.Path + ":" + r.Status))}] EQM[{c.EntriesText()}] 그룹13={c.Grp(13)}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "SYNC_DEVICE", "DETECTION", "Fetch"), ok ? "" : W1);

        // d) DELETED while that device has an active detection — EQM entries go (EB3 RemoveByDevice), the card stays reportable
        //    (EVT-E2E-022 · broker N-5 snapshot; decision 2026-09-30: removing the card would hide a still-open server event)
        mark = c.P.Log.Mark;
        c.P.Server.Remove(created.Id);
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("DELETED", created.Id, "sensor"));
        var gone = await WaitUntil(() => !c.P.Devices.Any(x => x.Id == created.Id), 3000);
        await Settle(500);
        var orphan = c.EntryFor(e, EnumEventType.Intrusion) != null;
        var cardKept = c.Cards().Any(x => x.Kind == "det" && x.EventId == e);
        var groupFreed = c.Grp(13) == Normal;
        var okd = gone && !orphan && cardKept && groupFreed;
        c.Rec.Add("S18.b", "활성 탐지가 있는 센서 2300 의 SYNC_DEVICE DELETED",
            "캐시에서 제거 · 그 장비의 EQM 엔트리 제거(EventQueueManager.RemoveByDevice — 고아 이벤트 방지) · 그룹13 Normal · 카드는 남아 조치 가능(EVT-E2E-022 · 브로커 N-5 스냅숏)",
            $"캐시 제거={gone} · EQM 잔류={orphan}[{c.EntriesText()}] · 카드 유지={cardKept}[{c.CardsText()}] · 그룹13={c.Grp(13)}",
            okd ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "SYNC_DEVICE", "RemoveByDevice", "EB3"), okd ? "" : W1);

        // b) UPDATED with membership change
        await c.Reset();
        c.P.Server.SetGroups(Inv.Smart.Id, 11, 13);
        mark = c.P.Log.Mark; var since = DateTime.Now;
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("UPDATED", Inv.Smart.Id, "sensor"));
        var moved = await WaitUntil(() => c.P.Dev(Inv.Smart).DeviceGroups?.Contains(13) == true, 3000);
        await Settle(300);
        var notice = c.P.EaRec.OfType<DeviceGroupMembershipChangedMessage>(since).ToList();
        var e2 = c.NextEventId();
        await c.Detect(Inv.Smart, e2);
        var g13 = await WaitUntil(() => c.Grp(13) == Detecting && c.Grp(11) == Detecting, 3000);
        var okb = moved && notice.Any(n => n.GroupIds.Contains(13)) && g13;
        c.Rec.Add("S18.c", "SYNC_DEVICE UPDATED — 2131 소속 11 → 11,13",
            "캐시 group_ids 갱신 · DeviceGroupMembershipChangedMessage(13) 발행 · 다음 DETECT 에 그룹13 도 Detecting",
            $"캐시 갱신={moved} · 소속 변경 알림=[{string.Join(" | ", notice.Select(n => string.Join(",", n.GroupIds)))}] · DETECT 후 그룹11={c.Grp(11)} 그룹13={c.Grp(13)}",
            okb ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "SYNC_DEVICE", "소속"), okb ? "" : W1);
        c.P.Server.SetGroups(Inv.Smart.Id, 11);
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("UPDATED", Inv.Smart.Id, "sensor"));
        await WaitUntil(() => c.P.Dev(Inv.Smart).DeviceGroups?.Contains(13) == false, 3000);

        // c) UPDATED status ERROR → ACTIVATED on a sensor
        mark = c.P.Log.Mark;
        c.P.Server.SetStatus(Inv.Pir.Id, "ERROR");
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("UPDATED", Inv.Pir.Id, "sensor"));
        var err = await WaitUntil(() => c.P.Sym(Inv.Pir).OperationState == EnumOperationState.ERROR, 3000);
        c.P.Server.SetStatus(Inv.Pir.Id, "ACTIVATED");
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("UPDATED", Inv.Pir.Id, "sensor"));
        var act = await WaitUntil(() => c.P.Sym(Inv.Pir).OperationState == EnumOperationState.ACTIVATED, 3000);
        c.Rec.Add("S18.d", "SYNC_DEVICE UPDATED 상태 ERROR → ACTIVATED(센서 2114)",
            "심볼 OperationState ERROR → ACTIVATED", $"ERROR 반영={err} · ACTIVATED 반영={act}",
            err && act ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "상태 동기화", "SYNC_DEVICE"), err && act ? "" : W1);

        // e) legacy body (type_device only, pre-v7)
        mark = c.P.Log.Mark; t0 = DateTime.Now;
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", new JObject { ["action"] = "UPDATED", ["resource_id"] = Inv.Fence.Id, ["type_device"] = "Fence" });
        var legacyOk = await WaitUntil(() => c.CountLogs(mark, "SyncDeviceStatus") > 0, 3000);
        c.Rec.Add("S18.e", "SYNC_DEVICE 옛 body {action, resource_id, type_device:'Fence'}",
            "옛 서버 무회귀: 카테고리 해석(Fence→sensor) → GET sensors/2200 → 상태 동기화",
            $"REST[{string.Join(",", c.RestSince(t0).Select(r => r.Path + ":" + r.Status))}] 처리={legacyOk}",
            legacyOk ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "SYNC_DEVICE"), legacyOk ? "" : W1);

        // f) unknown category
        mark = c.P.Log.Mark;
        await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("UPDATED", 77, "robot"));
        await Settle(800);
        var errs = c.Errors(mark).Count;
        c.Rec.Add("S18.f", "SYNC_DEVICE 모르는 category_device 'robot'",
            "경고 1줄로 버림(명세 N-1/N-2) · ERROR·예외 없음",
            $"ERROR {errs}건 · 로그: {c.LogsText(mark, "SYNC_DEVICE", "robot")}", errs == 0 ? Verdict.PASS : Verdict.FAIL, "", errs == 0 ? "" : W1);
    }

    // ---------- S19 ----------
    static async Task S19_SyncEventMapping(Ctx c)
    {
        var since = DateTime.Now; var mark = c.P.Log.Mark; var env = Guid.NewGuid().ToString();
        await c.Sync(Env.SyncEventMapping, "SYNC_EVENT_MAPPING", Env.SyncBody("UPDATED", 55), env);
        await c.Sync(Env.SyncEventMapping, "SYNC_EVENT_MAPPING", Env.SyncBody("UPDATED", 55), env);
        await c.Sync(Env.SyncEventMapping, "SYNC_EVENT_MAPPING", Env.SyncBody("DELETED", 56));
        await WaitUntil(() => c.P.EaRec.OfType<EventMappingsChangedMessage>(since).Count() >= 2, 3000);
        await Settle(400);
        var msgs = c.P.EaRec.OfType<EventMappingsChangedMessage>(since).ToList();
        var ok = msgs.Count == 2 && c.Errors(mark).Count == 0;
        c.Rec.Add("S19", "SYNC_EVENT_MAPPING (같은 봉투 2회 + 다른 봉투 1회)",
            "화면 알림 EventMappingsChangedMessage 2건(같은 봉투 id 재수신은 1건으로) · ERROR 없음",
            $"알림 {msgs.Count}건: {string.Join(" | ", msgs.Select(m => m.ToString()))}", ok ? Verdict.PASS : Verdict.FAIL,
            c.LogsText(mark, "SYNC_EVENT_MAPPING"), ok ? "" : W1);
    }

    // ---------- S20 ----------
    static async Task S20_MalformedJson(Ctx c)
    {
        var mark = c.P.Log.Mark;
        await c.Pub.PublishRawAsync(Env.Detect, "{\"id\":\"broken\",\"m_type\":\"PUB\",\"cmd\":\"DETECT\",\"body\":{\"id\":1,");
        await c.Pub.PublishRawAsync(Env.Detect, "not json at all");
        await c.Pub.PublishRawAsync(Env.Detect, "");
        await c.Pub.PublishRawAsync(Env.Detect, "\"just a string\"");
        await c.Pub1(Env.Detect, Env.Envelope("DETECT", new JValue(42)));
        var e = c.NextEventId();
        var env = await c.Detect(Inv.Fence, e);
        var (ok, actual) = await ExpectDetect(c, Inv.Fence, e, env);
        c.Rec.Add("S20", "깨진 JSON 4종 + body 가 숫자인 봉투 뒤에 정상 DETECT",
            "파이프라인 생존 — 뒤따르는 정상 DETECT 가 그대로 처리(EQM·심볼·카드)",
            actual + $" · 그 사이 ERROR {c.Errors(mark).Count}건", ok ? Verdict.PASS : Verdict.FAIL,
            c.LogsText(mark, "파싱", "오류", "Error", "JSON"), ok ? "" : W1);
    }

    // ---------- S21 ----------
    static async Task S21_CmdCasing(Ctx c)
    {
        var e = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Pub1(Env.Detect, Env.Envelope("detect", Env.DetectBody(e, Env.Ref(Inv.Smart.Id, "sensor"))));
        await WaitUntil(() => c.Cards().Count > 0 || c.Entries().Count > 0, 2000);
        await Settle(700);
        var card = c.Cards().Count; var entries = c.Entries().Count;
        var consistent = (card == 0 && entries == 0) || (card == 1 && entries == 1 && c.Dev(Inv.Smart) == Detecting);
        c.Rec.Add("S21", "cmd 소문자 'detect'",
            "호스트(카드·탐지음)와 라이브러리(EQM·심볼)가 같은 판정 — 둘 다 받거나 둘 다 버림(카드만 뜨고 심볼·자동조치·계기가 모르는 반쪽 상태 금지)",
            $"카드 {card}장 · EQM {entries}건 · 심볼={c.Dev(Inv.Smart)}", consistent ? Verdict.PASS : Verdict.FAIL,
            c.LogsText(mark, "DETECTION", "type_command"), consistent ? "" : W1);
    }

    // ---------- S22 ----------
    static async Task S22_Oversized(Ctx c)
    {
        var max = c.Pub.MaxPayload;
        var e = c.NextEventId(); var mark = c.P.Log.Mark;
        var big = Env.DetectBody(e, Env.Ref(Inv.Pir.Id, "sensor"));
        var size = (int)Math.Min(max > 0 ? max * 0.9 : 900_000, 900_000);
        big["detail"]!["blob"] = new string('x', size);
        var env = Env.Envelope("DETECT", big);
        var sw = Stopwatch.StartNew();
        await c.Pub.PublishAsync(Env.Detect, env);
        var (ok, actual) = await ExpectDetect(c, Inv.Pir, e, env.Value<string>("id")!);
        c.Rec.Add("S22.a", $"대형 본문 DETECT(~{size / 1024} KB, max_payload {max / 1024} KB 이내)",
            "정상 처리(EQM·심볼·카드)", actual + $" · {sw.ElapsedMilliseconds} ms", ok ? Verdict.PASS : Verdict.FAIL,
            c.LogsText(mark, "DETECTION", "Error"), ok ? "" : W1);

        mark = c.P.Log.Mark;
        string pubResult;
        try
        {
            var over = Env.DetectBody(c.NextEventId(), Env.Ref(Inv.Pir.Id, "sensor"));
            over["detail"]!["blob"] = new string('y', (int)(max > 0 ? max + 4096 : 1_100_000));
            await c.Pub.PublishAsync(Env.Detect, Env.Envelope("DETECT", over));
            await c.Pub.FlushAsync();
            pubResult = "발행 호출 성공(클라가 막지 않음)";
        }
        catch (Exception ex) { pubResult = $"발행 거부: {ex.GetType().Name}: {Recorder.Trunc(ex.Message, 120)}"; }
        await c.Reset();
        var e2 = c.NextEventId();
        var env2 = await c.Detect(Inv.Fence, e2);
        var (ok2, actual2) = await ExpectDetect(c, Inv.Fence, e2, env2);
        c.Rec.Add("S22.b", "max_payload 초과 본문 뒤 정상 DETECT",
            "초과분은 브로커/발행기에서 거부 · 파이프라인은 영향 없음(다음 DETECT 정상)",
            $"{pubResult} · 이후 정상 DETECT: {actual2}", ok2 ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "Error", "payload"), ok2 ? "" : W1);
    }

    // ---------- S23 ----------
    static async Task S23_LoginGate(Ctx c)
    {
        c.P.Tokens.Clear();
        var e = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Detect(Inv.Smart, e);
        await c.Malfunction(Inv.Pir, c.NextEventId(), "FAULT_ETC");
        await Settle(1200);
        var dropped = c.Cards().Count == 0 && c.Entries().Count == 0 && c.Dev(Inv.Smart) == Normal;
        c.P.RestoreTokens();
        var e2 = c.NextEventId();
        var env2 = await c.Detect(Inv.Smart, e2);
        var (ok2, actual2) = await ExpectDetect(c, Inv.Smart, e2, env2);
        var ok = dropped && ok2;
        c.Rec.Add("S23", "로그인 게이트: 미인증 상태 DETECT·MALFUNCTION → 인증 후 DETECT",
            "미인증 중 수신은 카드·EQM·심볼 모두 드롭(호스트·라이브러리 동일) → 인증 후 정상 처리",
            $"미인증 중: 카드 {(dropped ? 0 : c.Cards().Count)} EQM 드롭={dropped} · 인증 후: {actual2}", ok ? Verdict.PASS : Verdict.FAIL,
            c.LogsText(mark, "DETECTION"), ok ? "" : W1);
    }

    // ---------- S24 ----------
    static async Task S24_AlertTypeEvent(Ctx c)
    {
        var e = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Detect(Inv.Smart, e, typeEvent: "Alert");
        await WaitUntil(() => c.Cards().Count > 0 || c.Entries().Count > 0, 2000);
        await Settle(600);
        var counts = c.P.Eqm.GetActiveCounts();
        var before = $"EQM[{c.EntriesText()}] 활성계수(탐지,장애)=({counts.Detection},{counts.Fault}) 카드[{c.CardsText()}] 심볼={c.Dev(Inv.Smart)} 그룹11={c.Grp(11)}";
        await c.ActionReport(Env.DetectBody(e, Env.Ref(Inv.Smart.Id, "sensor"), typeEvent: "Alert"));
        await Settle(800);
        c.Rec.Add("S24", "type_event=Alert(사전 경보) 탐지 → 조치보고",
            "(명세는 표시 규칙 미정) 관측: 큐 등록 여부 · 심볼/계기 반영 여부 · 조치보고로 정리되는지",
            $"수신 후: {before} → 조치 후: EQM[{c.EntriesText()}] 카드[{c.CardsText()}]", Verdict.INFO, c.LogsText(mark, "DETECTION", "Alert"), "");
    }

    // ---------- S25 ----------
    static async Task S25_SyncDetection(Ctx c)
    {
        var n = c.NextEventId(); var mark = c.P.Log.Mark; var t0 = DateTime.Now;
        await c.Malfunction(Inv.Pir, n, "FAULT_ETC");
        await c.Detect(Inv.Smart, n);
        await WaitUntil(() => c.Cards().Count == 2, 3000);
        var det = Env.DetectBody(n, Env.Ref(Inv.Smart.Id, "sensor"));
        det["detail"] = new JObject { ["signal"] = 2000, ["thumbnail"] = "http://127.0.0.1:9/thumb-probe.jpg", ["frame_width"] = 1280, ["frame_height"] = 720 };
        c.P.Server.PutDetection(det);
        await c.Sync(Env.SyncDetection, "SYNC_DETECTION", new JObject { ["action"] = "UPDATED", ["resource_id"] = n, ["category_event"] = "detection" });
        var ok = await WaitUntil(() => c.CountLogs(mark, $"썸네일 갱신: Event({n})") > 0, 3000);
        c.Rec.Add("S25", $"SYNC_DETECTION UPDATED(id {n}) — 같은 번호의 장애 카드가 먼저 있음",
            "활성 탐지만 재조회(GET /events/detections/{n}) → 탐지 카드 썸네일 갱신(장애 카드 아님)",
            $"REST[{string.Join(",", c.RestSince(t0).Where(r => r.Path.Contains("detections")).Select(r => r.Path + ":" + r.Status))}] 갱신 로그={ok}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "SYNC_DETECTION", "썸네일"), ok ? "" : W1);
    }

    // ---------- S26 ----------
    static async Task S26_TwoDetectionsOneReport(Ctx c)
    {
        var d = Inv.Compound; var e1 = c.NextEventId(); var e2 = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Detect(d, e1); await c.Detect(d, e2);
        await WaitUntil(() => c.Entries().Count == 2 && c.Cards().Count(x => x.EntryId != null) == 2, 3000);
        await c.ActionReport(Env.DetectBody(e1, Env.Ref(d.Id, d.Category)));
        await WaitUntil(() => c.Cards().Count == 1, 3000);
        await Settle(400);
        var mid = $"1건 조치 후: 심볼={c.Dev(d)} 그룹13={c.Grp(13)} EQM {c.Entries().Count}";
        var okMid = c.Dev(d) == Detecting && c.Grp(13) == Detecting && c.Entries().Count == 1;
        await c.ActionReport(Env.DetectBody(e2, Env.Ref(d.Id, d.Category)));
        var okEnd = await WaitUntil(() => c.Dev(d) == Normal && c.Grp(13) == Normal && c.Entries().Count == 0 && c.Cards().Count == 0, 3000);
        c.Rec.Add("S26", "같은 장비(2201) 탐지 2건 → 1건씩 조치보고",
            "첫 조치 뒤에도 Detecting 유지(남은 1건) → 둘째 조치 뒤 Normal", $"{mid} → 2건 조치 후: 심볼={c.Dev(d)} 그룹13={c.Grp(13)} EQM {c.Entries().Count} 카드 {c.Cards().Count}",
            okMid && okEnd ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "ACTION_REPORT", "전이"), okMid && okEnd ? "" : W1);
    }

    // ---------- S27 ----------
    static async Task S27_ResponseEnvelope(Ctx c)
    {
        var e = c.NextEventId(); var mark = c.P.Log.Mark;
        var rsp = Env.Envelope("DETECT", Env.DetectBody(e, Env.Ref(Inv.Smart.Id, "sensor")), mType: "RSP");
        rsp["success"] = true; rsp["req_id"] = Guid.NewGuid().ToString();
        await c.Pub1(Env.Detect, rsp);
        await Settle(1200);
        var card = c.Cards().Count; var entries = c.Entries().Count;
        var ok = card == 0 && entries == 0 && c.Dev(Inv.Smart) == Normal;
        c.Rec.Add("S27", "m_type=RSP 인 DETECT 봉투(응답 메시지 — 새 탐지가 아님)",
            "호스트·라이브러리 모두 탐지로 처리하지 않음(호스트는 RSP 를 ProcessResponseAsync 로 보냄)",
            $"카드 {card} · EQM {entries} · 심볼={c.Dev(Inv.Smart)}", ok ? Verdict.PASS : Verdict.FAIL,
            c.LogsText(mark, "DETECTION", "RSP", "req_id"), ok ? "" : W1);
    }

    // ---------- S28 ----------
    static async Task S28_FaultThenDetectRace(Ctx c)
    {
        foreach (var gap in new[] { 30, 1500 })
        {
            await c.Reset();
            var d = Inv.Pir; var e1 = c.NextEventId(); var e2 = c.NextEventId(); var mark = c.P.Log.Mark; var t0 = DateTime.Now;
            var ar0 = c.P.AutoRecoveryFired.Count;
            await c.Malfunction(d, e1, "FAULT_ETC");
            await Task.Delay(gap);
            await c.Detect(d, e2);
            await WaitUntil(() => c.Dev(d) == Detecting && c.EntryFor(e1, EnumEventType.Fault) == null, 3000);
            await Settle(1000);
            var skipped = c.CountLogs(mark, "카드 없음") > 0;
            var writes = c.RestSince(t0).Where(r => r.BlockedWrite).Select(r => $"{r.Method} {r.Path}").ToList();
            var mal = c.Cards().FirstOrDefault(x => x.Kind == "mal");
            var dangling = mal != null && mal.EntryId != null && c.P.Eqm.GetEntry(mal.EntryId) == null;
            var ok = writes.Count > 0 && !skipped;
            c.Rec.Add($"S28.{gap}ms", $"장애 → {gap} ms 뒤 같은 장비(2114) 탐지 — 자동복구가 카드를 찾는가",
                "탐지 = 센서 생존 → 장애 엔트리 자동복구 + 자동복구 조치보고(API) 시도 — 두 이벤트 사이 간격과 무관해야 함",
                $"자동복구 발화={c.P.AutoRecoveryFired.Count - ar0} · '카드 없음 — API 스킵'={skipped} · 쓰기 시도=[{string.Join(", ", writes)}] · 남은 장애 카드={(mal is null ? "없음" : $"#{mal.EventId}→{mal.EntryId?[..8]}")}{(dangling ? "(가리키는 EQM 엔트리 없음)" : "")} · EQM[{c.EntriesText()}]",
                ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "AutoRecovery", "자동복구", "카드", "EnqueueCard"), ok ? "" : W1);
        }
    }

    // ---------- S29 ----------
    static async Task S29_ImmediateActionReport(Ctx c)
    {
        var d = Inv.Smart; var e = c.NextEventId(); var mark = c.P.Log.Mark;
        await c.Detect(d, e);
        await c.ActionReport(Env.DetectBody(e, Env.Ref(d.Id, d.Category)));   // no wait — the card may still be in the batch buffer
        await Settle(1500);
        var ok = c.Cards().Count == 0 && c.Entries().Count == 0 && c.Dev(d) == Normal && c.Grp(11) == Normal;
        c.Rec.Add("S29", "DETECT 직후(대기 없이) 그 이벤트의 ACTION_REPORT — 카드가 아직 묶음 버퍼에 있을 수 있는 순간",
            "유령 카드 없음 · EQM 제거 · 심볼/그룹11 Normal",
            $"카드[{c.CardsText()}] EQM[{c.EntriesText()}] 심볼={c.Dev(d)} 그룹11={c.Grp(11)}",
            ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "ACTION_REPORT", "EnqueueCard", "올리지"), ok ? "" : W1);
    }

    // ---------- S30 ----------
    static async Task S30_Burst600Cap(Ctx c)
    {
        var (sent, enq, cards, lat, drainMs) = await Burst(c, 600, 5000, null);
        var shown = c.Cards().Count;
        c.Rec.Add("S30", $"폭주 {sent}건 / 5초(120 msg/s) — 표시 카드 상한(500) 넘김",
            "EQM 은 전부 보존 · 카드는 상한까지(오래된 카드 정리) · 지연 유지 — 상한 초과분의 카드↔EQM 불일치는 설계상 기록(EB2)",
            $"EQM {enq}/{sent} · 표시 카드 {shown} (이번 폭주 카드 {cards}) · 지연 p50 {Pct(lat, 50):F0} ms · p95 {Pct(lat, 95):F0} ms · max {(lat.Count > 0 ? lat.Max() : -1):F0} ms · 전량 반영 {drainMs} ms",
            Verdict.INFO, c.LogsText(c.P.Log.Mark - 400, "EB2", "하드캡", "포화"), "");
    }
}
