// events-tray-templates — 이벤트 콘솔 완성도 수정 패스(2026-09-27): 조치 트레이가
//   ① 조치보고 문구 관리 목록(서버 /api/events/action-report-templates, display_order 순)을 문구로 쓰고
//   ② 그 문구로 [조치 적용] 하면 트레이와 같은 전송 사슬(대시보드 SendActionAsync → 임시 카드 → POST /events/actions)로 나가며
//   ③ 적용 뒤 상세가 '미조치' 로 남지 않고 서버에 다시 물어 새 상태 · 조치 내역을 보이는가.
// 제품 경로: 진짜 EventDashboardViewModel(진짜 패널 · EventProviderService · EventApiService) 을 루프백 서버에 붙인다.
// Raw 는 준비(arrange)와 재조회(assert)에만 쓴다. 만든 것은 LRT-EVT- 접두 — finally 에서 지운다.
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Newtonsoft.Json.Linq;
using DevProvider = Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider;
using EvProviderService = Ironwall.Dotnet.Libraries.Events.Ui.Services.EventProviderService;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    [ExtraStep("events-tray-templates", 90)]
    static async Task EventsTray_TemplatesApplyRequery(ExtraContext ctx)
    {
        const string TAG = "ET";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        var templateIds = new List<int>();
        EventDashboardViewModel dash = null;
        try
        {
            // ---------- arrange: 관리 문구 2건 — 제품 API 로 등록(표시 순서 B=900 · A=901 → B 가 A 앞) ----------
            var templateApi = new ActionReportTemplateApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            const string PHRASE_B = "LRT-EVT-TRAY-문구-B", PHRASE_A = "LRT-EVT-TRAY-문구-A";
            foreach (var (content, order) in new[] { (PHRASE_A, 901), (PHRASE_B, 900) })
            {
                var created = await templateApi.CreateTemplateAsync(new ActionReportTemplateCreateDto { Content = content, DisplayOrder = order }).ConfigureAwait(false);
                if (!created.Success || created.Data is null)
                {
                    rec.Add("ET.0", TAG, "arrange: 조치보고 문구 등록", Verdict.BLOCKED,
                        $"content='{content}' success={created.Success} status={created.StatusCode} msg='{created.Message}'", blocked: "fixture setup failed");
                    return;
                }
                templateIds.Add(created.Data.Id);
                rec.Created("action-report-template", created.Data.Id);
            }

            // ---------- ET.1 문구 원천: 서버 목록을 display_order 순으로 ----------
            var source = new ActionReportPhraseSource(() => templateApi, boot.Log);
            boot.Wire.CurrentTag = TAG + "/phrases";
            var set = await source.LoadAsync().ConfigureAwait(false);
            var ib = set.Phrases.ToList().IndexOf(PHRASE_B);
            var ia = set.Phrases.ToList().IndexOf(PHRASE_A);
            var phrasesOk = set.FromServer && ib >= 0 && ia > ib;
            rec.Add("ET.1", TAG, "조치 트레이 문구 = 조치보고 문구 관리 목록(서버) · display_order 순서 그대로",
                phrasesOk ? Verdict.PASS : Verdict.FAIL,
                $"fromServer={set.FromServer}; count={set.Phrases.Count}; index B={ib} A={ia}; head=[{string.Join(" | ", set.Phrases.Take(6))}]",
                defectAt: phrasesOk ? "" : "Events.Ui/Consoles/Tray/ActionReportPhraseSource.cs LoadAsync/Order");

            // ---------- arrange: 센서 + 탐지 1건 ----------
            var (_, sensor) = await ArrangeSensor(fx, "ET", 97900).ConfigureAwait(false);
            var detId = await fx.Event("events/detections", new { type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR" }).ConfigureAwait(false);

            // ---------- 진짜 대시보드 — 진짜 패널이 서버에서 목록을 읽는다 ----------
            var eventApi = new EventApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var devices = new DevProvider();
            var events = new EventProvider();
            var providerService = new EvProviderService(boot.Log, eventApi, devices, events);
            var ea = new EventAggregator();
            var account = new AccountModel { Name = "LRT-EVT 트레이", Username = "admin" };

            using var ioc = new IocScope(new Dictionary<Type, object>
            {
                [typeof(IEventAggregator)] = ea,
                [typeof(Ironwall.Dotnet.Libraries.Base.Services.ILogService)] = boot.Log,
                [typeof(IEventApiService)] = eventApi,
                [typeof(EventProvider)] = events,
                [typeof(DevProvider)] = devices,
                [typeof(IAccountModel)] = account,
                [typeof(IActionReportGuard)] = new ActionReportGuard(),
                [typeof(IActionReportTemplateApiService)] = templateApi,
            });

            dash = new EventDashboardViewModel(
                ea, boot.Log,
                new EventTabControlViewModel(ea, boot.Log),
                new DetectionEventPanelViewModel(ea, boot.Log, providerService, devices, events),
                new MalfunctionEventPanelViewModel(ea, boot.Log, providerService, devices, events),
                new ConnectionEventPanelViewModel(ea, boot.Log, providerService, devices, events),
                new ActionEventPanelViewModel(ea, boot.Log, providerService, events),
                new EventInfoViewModel(devices, events, providerService, ea, boot.Log),
                new CameraEventInfoViewModel(events, ea, boot.Log),
                new DataChartPanelViewModel(ea, boot.Log, providerService),
                phraseSource: source);

            boot.Wire.CurrentTag = TAG + "/open";
            await ((IActivate)dash).ActivateAsync().ConfigureAwait(false);
            await dash.RefreshPhrasesAsync().ConfigureAwait(false);
            var options = dash.Tray.PhraseOptions.ToList();
            var trayOk = options.IndexOf(PHRASE_B) >= 0 && options.IndexOf(PHRASE_A) > options.IndexOf(PHRASE_B)
                         && options.Last() == ActionTrayViewModel.EtcPhrase && dash.Tray.PhrasesFromServer;
            rec.Add("ET.2", TAG, "창을 열면 트레이 문구 칸이 관리 목록으로 바뀐다('기타' 는 맨 끝)",
                trayOk ? Verdict.PASS : Verdict.FAIL,
                $"fromServer={dash.Tray.PhrasesFromServer}; options({options.Count})=[{string.Join(" | ", options.Take(4))} … {options.Last()}]",
                defectAt: trayOk ? "" : "Events.Ui/ViewModels/Dashboards/EventDashboardViewModel.cs RefreshPhrasesAsync · ActionTrayViewModel.ApplyPhrases");

            // ---------- 탐지 레일 → 그 행을 고른다 ----------
            boot.Wire.CurrentTag = TAG + "/list";
            await dash.SelectRailAsync(EventDashboardViewModel.DetectionRailKey).ConfigureAwait(false);
            var row = dash.DetectionPanelViewModel.ViewModelProvider.FirstOrDefault(r => r.Model?.Id == detId);
            if (row is null)
            {
                rec.Add("ET.3", TAG, "arrange: 탐지 레일에서 방금 만든 행 찾기", Verdict.BLOCKED,
                    $"detection {detId} not in the panel's first page ({dash.DetectionPanelViewModel.ViewModelProvider.Count} rows)", blocked: "list read failed");
                return;
            }
            dash.SetSelection(new object[] { row });
            var statusBefore = StatusField(dash);

            // ---------- [트레이에 담기] → 관리 문구 B → [조치 적용] ----------
            dash.QueueSelection();
            dash.Tray.Phrase = PHRASE_B;
            boot.Wire.CurrentTag = TAG + "/apply";
            var before = rec.LastSeq();
            var summary = await dash.ApplyTrayAsync().ConfigureAwait(false);
            for (var i = 0; i < 100 && dash.DetailView.Actions.State != ActionHistoryState.Loaded && dash.DetailView.Actions.State != ActionHistoryState.Failed; i++)
                await Task.Delay(100).ConfigureAwait(false);

            var wire = SeqsSince(rec, before);
            var exchanges = rec.Since(before).ToList();
            var post = exchanges.FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/events/actions"));
            if (post is not null) TrackActionFromWire(fx, post);
            var sentContent = post is null ? null : (string)JObject.Parse(post.RequestBody)["content"];
            var requery = exchanges.FirstOrDefault(w => w.Method == "GET" && w.Uri.Contains($"/events/detections/{detId}/actions"));

            var (gs, gj) = await raw.Get($"events/detections/{detId}").ConfigureAwait(false);
            var serverReported = (bool?)gj["data"]?["action_reported"];
            var statusAfter = StatusField(dash);
            var historyLines = dash.DetailView.Actions.Lines.Select(l => l.Content).ToList();

            var sentOk = post?.Status == 201 && sentContent == PHRASE_B && summary.Applied == 1;
            rec.Add("ET.3", TAG, "관리 문구로 [조치 적용] → POST /events/actions 201, 본문 content = 고른 관리 문구",
                sentOk ? Verdict.PASS : Verdict.FAIL,
                $"POST HTTP {post?.Status}; content='{sentContent}'; applied={summary.Applied} failed={summary.Failed} skipped={summary.Skipped}; tray='{dash.Tray.StatusLine}'",
                defectAt: sentOk ? "" : "Events.Ui/ViewModels/Dashboards/EventDashboardViewModel.cs SendActionAsync · Consoles/Tray/ActionTrayViewModel.cs",
                seqs: wire);

            var detailOk = serverReported == true && row.IsActionReported && statusBefore == "미조치" && statusAfter.StartsWith("조치 ")
                           && requery is not null && historyLines.Contains(PHRASE_B);
            rec.Add("ET.4", TAG, "적용 뒤 상세가 '미조치' 로 남지 않는다 — 상태 칸이 바뀌고 조치 내역을 서버에 다시 물어 방금 문구가 보인다",
                detailOk ? Verdict.PASS : Verdict.FAIL,
                $"server re-GET {gs} action_reported={serverReported}; row IsActionReported={row.IsActionReported}; detail status '{statusBefore}' → '{statusAfter}'; " +
                $"re-query GET …/{detId}/actions HTTP {requery?.Status}; history state={dash.DetailView.Actions.State} lines=[{string.Join(" | ", historyLines)}]",
                defectAt: detailOk ? "" : "Events.Ui/ViewModels/Dashboards/EventDashboardViewModel.cs ApplyTrayAsync/RefreshDetailAfterReport · Consoles/Detail/EventDetailViewModel.cs RefreshAfterReport",
                seqs: wire);
        }
        catch (Exception ex)
        {
            rec.Add("ET!", TAG, "tray templates round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            try { if (dash is not null) await ((IDeactivate)dash).DeactivateAsync(true).ConfigureAwait(false); } catch { /* 창 닫기 실패는 정리와 무관 */ }
            await fx.CleanupAsync().ConfigureAwait(false);
            foreach (var id in templateIds)
            {
                var (s, _) = await raw.Delete($"events/action-report-templates/{id}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("action-report-template", id);
                else rec.Leftover("action-report-template", id, $"DELETE {s}");
            }
        }

        static string StatusField(EventDashboardViewModel d)
            => d.DetailView.Sections.SelectMany(s => s.Fields).FirstOrDefault(f => f.Key == "status")?.Text ?? "(없음)";
    }
}
