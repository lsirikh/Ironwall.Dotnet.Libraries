// events-kinds — 탐지 · 장애 · 연결 · 운영 네 종류가 이벤트 콘솔의 자기 레일(목록 → 상세)에 서버 값 그대로 닿는가(2026-10-03).
// 운영 이벤트는 API 만 있고 창이 없었다 — '운영' 레일(OperationEventPanelViewModel)을 새로 세웠다. 그 패널을 실서버로 돌린다.
//
// act 는 제품 경로(EventProviderService 페이지 조회 · 운영 패널 DataInitialize · EventDetailViewModel.Load)로만 간다.
// Raw 는 준비(arrange)와 재조회(assert)에만 쓴다. 이름은 LRT-EVT- 접두, 정리는 EvtFixture.CleanupAsync.
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Newtonsoft.Json.Linq;
using EvProvider = Ironwall.Dotnet.Libraries.Events.Ui.Services.EventProviderService;
using OpPanel = Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels.OperationEventPanelViewModel;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    /// <summary>
    /// K1 — 네 종류 이벤트를 하나씩 만들고(운영은 함체 문 OPEN 보고로 서버가 만든다), 창이 쓰는 조회 경로로 읽어
    /// 상세 칸의 종류 · 번호 · 핵심 값이 서버 원문과 같은지 본다.
    /// </summary>
    [ExtraStep("events-kinds", 95)]
    static async Task EventsKinds_K1_FourKindsInConsole(ExtraContext ctx)
    {
        const string TAG = "K1";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        try
        {
            var (_, sensor) = await ArrangeSensor(fx, "K1", 97800).ConfigureAwait(false);
            var det = await fx.Event("events/detections", new { type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR" }).ConfigureAwait(false);
            var mal = await fx.Event("events/malfunctions", new { type_event = "Fault", device_id = sensor, reason = "FAULT_SENSOR" }).ConfigureAwait(false);
            var con = await fx.Event("events/connections", new { type_event = "Connection", device_id = sensor }).ConfigureAwait(false);

            // 운영 이벤트는 직접 POST 할 수 없다(서버 D-8) — 문 위치 부품을 선언한 함체를 만들고 OPEN 을 보고한다(E3.0 과 같은 방법).
            var enclosure = await fx.Device("devices/enclosures", "enclosure", new
            {
                unit_id = fx.UnitId, type_enclosure = "Outdoor", number_device = 97810, name_device = "LRT-EVT-K1-ENC",
                hardware_spec = new { schema = 1, components = new[] { new { key = "door", type = "DOOR_SENSOR" } } },
            }).ConfigureAwait(false);
            var (ps, pj) = await raw.Patch($"devices/enclosures/{enclosure}/component-status",
                new { door = new { state = "OPEN", health = "OK", observed_at = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") } }).ConfigureAwait(false);
            var (os, oj) = await raw.Get("events/operations?page=1&limit=100").ConfigureAwait(false);
            var serverOps = (oj["data"] as JArray ?? new JArray()).OfType<JObject>()
                .Where(o => (int?)(o["device"] as JObject)?["id"] == enclosure).ToList();
            foreach (var op in serverOps) { fx.Events.Add(("events/operations", (int)op["id"]!)); rec.Created("events/operations", (int)op["id"]!); }
            rec.Add("K1.0", TAG, "arrange: 탐지 · 장애 · 연결 각 1건 + 함체 문 OPEN 보고 → 서버가 만든 운영 이벤트", Verdict.INFO,
                $"det={det} mal={mal} con={con}; component-status HTTP {ps} {(ps == 200 ? "" : Short(pj))}; operations for enclosure {enclosure}: {serverOps.Count} (list HTTP {os})");

            var eventApi = new EventApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var provider = new EvProvider(boot.Log, eventApi);
            var from = DateTime.Now.AddHours(-2);
            var to = DateTime.Now.AddHours(2);

            // ---- 탐지 · 장애 · 연결: 페이지 조회 → 행 → 상세 ----
            var dRow = (await provider.FetchDetectionEventsPageAsync(from, to, 1, 100).ConfigureAwait(false)).Items?.FirstOrDefault(m => m.Id == det);
            var mRow = (await provider.FetchMalfunctionEventsPageAsync(from, to, 1, 100).ConfigureAwait(false)).Items?.FirstOrDefault(m => m.Id == mal);
            var cRow = (await provider.FetchConnectionEventsPageAsync(from, to, 1, 100).ConfigureAwait(false)).Items?.FirstOrDefault(m => m.Id == con);

            CheckDetail("K1a", "탐지 레일: 서버 탐지 1건이 목록에 있고 상세가 '탐지 · 번호' 로 열린다", EventDetailKind.Detection, det,
                dRow is null ? null : new DetectionEventViewModel(dRow));
            CheckDetail("K1b", "장애 레일: 서버 장애 1건이 목록에 있고 상세가 '장애 · 번호' 로 열린다", EventDetailKind.Malfunction, mal,
                mRow is null ? null : new MalfunctionEventViewModel(mRow));
            CheckDetail("K1c", "연결 레일: 서버 연결 1건이 목록에 있고 상세가 '연결 · 번호' 로 열린다", EventDetailKind.Connection, con,
                cRow is null ? null : new ConnectionEventViewModel(cRow));

            // ---- 운영: 새 '운영' 레일 패널을 그대로 돌린다(기간 = 콘솔 기간) ----
            var panel = new OpPanel(new EventAggregator(), boot.Log, provider);
            panel.SetDate(from, to);
            await panel.DataInitialize().ConfigureAwait(false);
            var serverOp = serverOps.FirstOrDefault();
            var opId = serverOp is null ? 0 : (int)serverOp["id"]!;
            var opRow = panel.ViewModelProvider.FirstOrDefault(r => r.Model?.Id == opId);
            var reasonRaw = (string?)serverOp?["reason"];
            var severityRaw = (string?)serverOp?["severity"];
            var okOp = serverOp is not null && panel.IsSupported && opRow is not null
                       && opRow.Reason == reasonRaw && opRow.Severity == severityRaw
                       && opRow.DeviceLabel is not null;
            rec.Add("K1d", TAG, "운영 레일: 서버가 만든 운영 이벤트가 '운영' 목록에 사유 · 심각도 원문 그대로 들어온다",
                okOp ? Verdict.PASS : serverOp is null ? Verdict.BLOCKED : Verdict.FAIL,
                $"server op={opId} reason={reasonRaw} severity={severityRaw} detail={Recorder.Trunc(serverOp?["detail"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "null", 160)}; " +
                $"panel supported={panel.IsSupported} loaded={panel.LoadedCountText} row={(opRow is null ? "(없음)" : $"{opRow.ReasonText}/{opRow.SeverityText}/{opRow.DeviceLabel}/{opRow.DetailText}")}",
                blocked: serverOp is null ? "server made no operation event for the door report" : "",
                defectAt: okOp || serverOp is null ? "" : "Events.Ui/ViewModels/Panels/OperationEventPanelViewModel.cs · EventProviderService.FetchOperationEventsPageAsync · DtoToModelHelper.ToOperationEventModel");
            if (opRow is not null)
                CheckDetail("K1e", "운영 레일: 상세가 '운영 · 번호' 로 열리고 전부 잠겨 있다(조회 전용)", EventDetailKind.Operation, opId, opRow);
        }
        catch (Exception ex)
        {
            rec.Add("K1!", TAG, "four event kinds in console", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await fx.CleanupAsync().ConfigureAwait(false);
        }

        void CheckDetail(string id, string title, EventDetailKind kind, int eventId, object? row)
        {
            if (row is null)
            {
                rec.Add(id, TAG, title, Verdict.FAIL, $"event {eventId} not in the window's page query",
                    defectAt: "Events.Ui/Services/EventProviderService.cs Fetch*PageAsync");
                return;
            }
            var presenter = new ConsoleDetailPresenter();
            var detail = new EventDetailViewModel(presenter);
            detail.Load(kind, new[] { row }, canEdit: true, canReport: true, actionCount: 0);
            var fields = detail.Sections.SelectMany(s => s.Fields).ToList();
            var number = fields.FirstOrDefault(f => f.Label == "번호")?.Text;
            var readOnly = kind != EventDetailKind.Operation || fields.All(f => !f.IsEditable);
            var ok = presenter.TypeName == EventDetailProjection.KindLabel(kind) && number == eventId.ToString() && readOnly;
            rec.Add(id, TAG, title, ok ? Verdict.PASS : Verdict.FAIL,
                $"type={presenter.TypeName} number={number} title={presenter.SingleTitle} fields=[{string.Join(", ", fields.Select(f => $"{f.Label}={Recorder.Trunc(f.Text, 40)}{(f.IsEditable ? "*" : "")}"))}]",
                defectAt: ok ? "" : "Events.Ui/Consoles/Detail/EventDetailViewModel.cs BuildSingle");
        }
    }
}
