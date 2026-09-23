// events-vm — 이벤트 창(조치보고 · 억제 콘솔 · 개요 통계 · 매핑 워크벤치 · 상세 적용 · 레일 · 트레이)을
// 실서버(루프백)로 왕복해 증명한다. 모든 "act" 는 제품 경로(창 VM 또는 그 VM 이 부르는 정확한 헬퍼 사슬)로 간다.
// Raw 는 준비(arrange)와 재조회(assert)에만 쓴다.
//
// 이름 규칙: 여기서 만드는 모든 것은 LRT-EVT- / lrt_evt_ 접두 — 동시에 도는 다른 작업자 잔여물과 가르기 위해서다.
// 정리: 각 스텝의 finally 가 만든 역순(조치 → 이벤트 → 매핑 → 장비 → 사용자)으로 지운다.
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Newtonsoft.Json.Linq;
using EvProvider = Ironwall.Dotnet.Libraries.Events.Ui.Services.EventProviderService;
using SupConsole = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression.SuppressionConsoleViewModel;
using SupCancel = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression.CallCancelConsoleSuppressionMessageModel;
using SupDelete = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression.CallDeleteConsoleSuppressionMessageModel;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    // ───────────────────────────── 공용 준비물 ─────────────────────────────

    /// <summary>이 파일의 스텝이 만든 서버 자원 — finally 에서 역순으로 지운다.</summary>
    sealed class EvtFixture
    {
        readonly Raw _raw;
        readonly Recorder _rec;
        public readonly List<int> Actions = new();
        public readonly List<(string Path, int Id)> Events = new();          // events/detections · malfunctions · connections
        public readonly List<(string Path, string Kind, int Id)> Devices = new();
        public readonly List<int> Users = new();
        public readonly List<int> Schedules = new();
        public readonly List<int> Mappings = new();
        public int UnitId;

        public EvtFixture(Raw raw, Recorder rec) { _raw = raw; _rec = rec; }

        /// <summary>제품 경로가 만든 조치(POST /events/actions 201)를 정리 대상으로 올린다.</summary>
        public void TrackAction(int id)
        {
            if (id <= 0 || Actions.Contains(id)) return;
            Actions.Add(id);
            _rec.Created("action", id);
        }

        /// <summary>
        /// 자기 최상위 부대 — 시드된 억제 창([시드] 평일 정기점검: 평일 00:00~02:00, 대상 all = 부대 1 + 예하)이
        /// 그 시간대의 이벤트 생성을 202 suppressed 로 삼킨다. 최상위 부대는 그 범위 밖이라 이 스텝의 이벤트가 실제로 저장된다.
        /// </summary>
        public async Task<int> EnsureUnit(string suffix)
        {
            if (UnitId > 0) return UnitId;
            var (s, j) = await _raw.Post("units", new { name = $"LRT-EVT-{suffix}-UNIT", code = $"lrtevt{suffix.ToLowerInvariant()}unit", echelon = "Company", is_enable = true }).ConfigureAwait(false);
            if (s != 200 && s != 201) throw new InvalidOperationException($"arrange unit failed: HTTP {s} {Short(j)}");
            UnitId = (int)j["data"]!["id"]!;
            _rec.Created("unit", UnitId);
            return UnitId;
        }

        public async Task<int> Device(string path, string kind, object body)
        {
            var (s, j) = await _raw.Post(path, body).ConfigureAwait(false);
            if (s != 200 && s != 201) throw new InvalidOperationException($"arrange {kind} failed: HTTP {s} {Short(j)}");
            var id = (int)j["data"]!["id"]!;
            Devices.Add((path, kind, id));
            _rec.Created(kind, id);
            return id;
        }

        public async Task<int> Event(string path, object body)
        {
            var (s, j) = await _raw.Post(path, body).ConfigureAwait(false);
            if (s != 200 && s != 201) throw new InvalidOperationException($"arrange {path} failed: HTTP {s} {Short(j)}");
            var id = (int)j["data"]!["id"]!;
            Events.Add((path, id));
            _rec.Created(path, id);
            return id;
        }

        public async Task CleanupAsync()
        {
            // 조치가 달린 원본은 409 — 조치부터 지운다. 그다음 원본 이벤트(장비 삭제는 이벤트를 지우지 않는다: SET NULL).
            foreach (var a in Actions.Distinct().Reverse()) await Del($"events/actions/{a}", "action", a).ConfigureAwait(false);
            foreach (var e in ((IEnumerable<(string Path, int Id)>)Events).Reverse()) await Del($"{e.Path}/{e.Id}", e.Path, e.Id).ConfigureAwait(false);
            // 억제 스케줄의 DELETE 는 소프트 취소다(행이 cancelled 로 남는다) — 취소한 뒤 bulk-delete 로 실제로 지운다.
            foreach (var s in ((IEnumerable<int>)Schedules).Reverse()) await DelSchedule(s).ConfigureAwait(false);
            foreach (var m in ((IEnumerable<int>)Mappings).Reverse()) await Del($"integrations/event-mappings/{m}", "event-mapping", m).ConfigureAwait(false);
            foreach (var d in ((IEnumerable<(string Path, string Kind, int Id)>)Devices).Reverse()) await Del($"{d.Path}/{d.Id}", d.Kind, d.Id).ConfigureAwait(false);
            foreach (var u in ((IEnumerable<int>)Users).Reverse()) await Del($"users/{u}", "user", u).ConfigureAwait(false);
            if (UnitId > 0) await Del($"units/{UnitId}", "unit", UnitId).ConfigureAwait(false);
        }

        async Task DelSchedule(int id)
        {
            try
            {
                await _raw.Delete($"event-suppression-schedules/{id}").ConfigureAwait(false);   // 진행 중 · 예정 → 취소
                var (s, j) = await _raw.Post("event-suppression-schedules/bulk-delete", new { ids = new[] { id } }).ConfigureAwait(false);
                var (g, _) = await _raw.Get($"event-suppression-schedules/{id}").ConfigureAwait(false);
                if (g == 404) _rec.Deleted("suppression-schedule", id);
                else _rec.Leftover("suppression-schedule", id, $"bulk-delete HTTP {s} {Short(j)}; re-GET {g}");
            }
            catch (Exception ex) { _rec.Leftover("suppression-schedule", id, "DELETE threw: " + ex.Message); }
        }

        async Task Del(string path, string kind, int id)
        {
            try
            {
                var (s, j) = await _raw.Delete(path).ConfigureAwait(false);
                if (s == 200 || s == 204 || s == 404) _rec.Deleted(kind, id);
                else _rec.Leftover(kind, id, $"DELETE {path} -> HTTP {s} {Short(j)}");
            }
            catch (Exception ex) { _rec.Leftover(kind, id, "DELETE threw: " + ex.Message); }
        }
    }

    /// <summary>
    /// Caliburn IoC 를 이 스텝 동안만 덮는다 — 창 VM 은 IoC.Get 으로 권한 · API · 계정 · 자물쇠를 찾는다.
    /// 모르는 타입은 원래 대리자(Steps.Core.InstallIoC — IUnitScopeService)로 넘긴다. Dispose 에서 복원.
    /// </summary>
    sealed class IocScope : IDisposable
    {
        readonly Func<Type, string, object> _prev;
        readonly Dictionary<Type, object> _map;
        public IocScope(Dictionary<Type, object> map)
        {
            _map = map;
            _prev = IoC.GetInstance;
            IoC.GetInstance = (t, k) => _map.TryGetValue(t, out var o) ? o : _prev?.Invoke(t, k);
        }
        public void Set(Type t, object o) => _map[t] = o;
        public void Dispose() => IoC.GetInstance = _prev;
    }

    /// <summary>창 VM 이 이벤트 버스로 내보내는 알림을 모은다(권한 없음 · 실패 · 닫기).</summary>
    sealed class DialogSink : IHandle<OpenInfoPopupMessageModel>, IHandle<CloseDialogMessageModel>
    {
        public readonly List<string> Seen = new();
        readonly TaskCompletionSource<string> _first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> First => _first.Task;
        public Task HandleAsync(OpenInfoPopupMessageModel m, CancellationToken ct)
        {
            var s = $"popup:{m.Title}|{m.Explain}";
            Seen.Add(s); _first.TrySetResult(s); return Task.CompletedTask;
        }
        public Task HandleAsync(CloseDialogMessageModel m, CancellationToken ct)
        {
            Seen.Add("close"); _first.TrySetResult("close"); return Task.CompletedTask;
        }
    }

    static async Task<string> WaitFirst(DialogSink sink, int ms = 30000)
    {
        var done = await Task.WhenAny(sink.First, Task.Delay(ms)).ConfigureAwait(false);
        return done == sink.First ? sink.First.Result : "(timeout: 창이 아무 알림도 내지 않았다)";
    }

    /// <summary>탐지 원본 1건을 서버에서 읽어 창이 쓰는 모델로 — 제품 경로(EventProviderService 페이지 조회).</summary>
    static async Task<IDetectionEventModel?> LoadDetectionViaProvider(EvProvider provider, int id)
    {
        var page = await provider.FetchDetectionEventsPageAsync(DateTime.Now.AddHours(-2), DateTime.Now.AddHours(2), 1, 100).ConfigureAwait(false);
        return page.Items?.FirstOrDefault(m => m.Id == id);
    }

    static async Task<IMalfunctionEventModel?> LoadMalfunctionViaProvider(EvProvider provider, int id)
    {
        var page = await provider.FetchMalfunctionEventsPageAsync(DateTime.Now.AddHours(-2), DateTime.Now.AddHours(2), 1, 100).ConfigureAwait(false);
        return page.Items?.FirstOrDefault(m => m.Id == id);
    }

    /// <summary>LRT-EVT 제어기 + 센서 1쌍. 번호대는 97xxx(다른 작업자와 겹치지 않게).</summary>
    static async Task<(int Ctrl, int Sensor)> ArrangeSensor(EvtFixture fx, string suffix, int number, bool ownUnit = true)
    {
        // ownUnit=false: 서버 기본 부대에 둔다 — 억제 스케줄 대상처럼 "이 클라이언트 부대" 소속이어야 할 때.
        int? unit = ownUnit ? await fx.EnsureUnit(suffix).ConfigureAwait(false) : null;
        var ctrl = await fx.Device("devices/controllers", "controller", new
        {
            unit_id = unit,
            type_controller = "Controller",
            number_device = number,
            name_device = $"LRT-EVT-{suffix}-CTRL",
            connection = new { ip_address = $"10.97.{number % 200}.1", ip_port = 9700 },
        }).ConfigureAwait(false);
        var sensor = await fx.Device("devices/sensors", "sensor", new
        {
            type_sensor = "PIR",
            number_device = number + 1,
            name_device = $"LRT-EVT-{suffix}-S1",
            controller_id = ctrl,
            unit_id = unit,
        }).ConfigureAwait(false);
        return (ctrl, sensor);
    }

    // ───────────────────────────── E1 조치보고 권한 · 빈 메모 ─────────────────────────────

    /// <summary>
    /// E1 — 조치보고 창의 권한 관문이 서버 계약과 같은가.
    /// 서버: POST /events/actions = <c>events:edit</c>(permission_map.py:37 · routers/actions.py:363, v6.3.2 태그도 동일).
    /// 운영자 프리셋 = events RC(view+control, edit 없음). 클라 관문이 control 을 보면 운영자는 통과 → 403.
    /// 운영자는 별도 사용자(lrt_evt_op1)로 로그인한다 — 서버의 세션 축출은 같은 user_id 에만 걸린다(auth.py:606-625).
    /// </summary>
    [ExtraStep("events-vm", 10)]
    static async Task EventsVm_E1_ActionReport(ExtraContext ctx)
    {
        const string TAG = "E1";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        try
        {
            var (_, sensor) = await ArrangeSensor(fx, "E1", 97100).ConfigureAwait(false);
            var detId = await fx.Event("events/detections", new
            {
                type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR",
                detail = new { signal = 5, thumbnail = "http://127.0.0.1/lrt-evt.jpg" },
            }).ConfigureAwait(false);

            // ---- 운영자 사용자: 서버 프리셋 "Preset - 운영자" 에 붙인다(그룹을 새로 만들지 않는다 — 서버가 준 정확한 등급) ----
            var (gs, gj) = await raw.Get("user-groups?page=1&limit=100").ConfigureAwait(false);
            var opGroup = (gj["data"] as JArray)?.OfType<JObject>().FirstOrDefault(g => Str(g["name"]).Contains("운영자"));
            if (gs != 200 || opGroup is null)
            {
                rec.Add("E1.0", TAG, "arrange: 서버 운영자 프리셋 그룹", Verdict.BLOCKED, $"HTTP {gs}; 운영자 프리셋 없음", blocked: "fixture missing");
                return;
            }
            var opEvents = Obj(Obj(Obj(opGroup["permissions"])?["modules"])?["events"]);
            const string OP_LOGIN = "lrt_evt_op1", OP_PW = "LrtEvtOp1!x";
            var (us, uj) = await raw.Post("users", new { login_id = OP_LOGIN, password = OP_PW, name = "LRT-EVT 운영자", role = "USER" }).ConfigureAwait(false);
            if (us != 200 && us != 201) { rec.Add("E1.0", TAG, "arrange: 운영자 사용자", Verdict.BLOCKED, $"HTTP {us} {Short(uj)}", blocked: "fixture setup failed"); return; }
            var opUserId = (int)uj["data"]!["id"]!;
            fx.Users.Add(opUserId); rec.Created("user", opUserId);
            var assign = await boot.AccountApi.AssignUserGroupAsync(opUserId, (int)opGroup["id"]!).ConfigureAwait(false);
            if (!assign.Success) { rec.Add("E1.0", TAG, "arrange: 운영자 그룹 배정", Verdict.BLOCKED, assign.Message ?? "", blocked: "fixture setup failed"); return; }

            // ---- 운영자 로그인: 두 번째 Bootstrap(자기 토큰 저장소 · 자기 ApiService) ----
            var opBoot = new Bootstrap(rec);
            await opBoot.InitAsync().ConfigureAwait(false);
            opBoot.Wire.CurrentTag = TAG + "/op-login";
            var opLogin = await opBoot.AccountApi.LoginAsync(OP_LOGIN, OP_PW).ConfigureAwait(false);
            if (opLogin?.Data is null || string.IsNullOrEmpty(opLogin.Data.AccessToken))
            {
                rec.Add("E1.0", TAG, "arrange: 운영자 로그인", Verdict.BLOCKED, opLogin?.Message ?? "(null)", blocked: "operator login failed");
                return;
            }
            opBoot.Tokens.SetTokens(opLogin.Data.AccessToken, opLogin.Data.RefreshToken, opLogin.Data.SessionId);

            // 두 번째 로그인이 하네스 주 세션(admin)을 축출하지 않았는가 — 주 ApiService 로 한 번 더 읽는다.
            var adminEvents = new EventApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            boot.Wire.CurrentTag = TAG + "/admin-alive";
            var alive = await adminEvents.GetDetectionEventByIdAsync(detId).ConfigureAwait(false);
            rec.Add("E1.1", TAG, "운영자 로그인 후에도 하네스 주 세션(admin)이 살아 있다(축출은 같은 user_id 에만)",
                alive.Success ? Verdict.PASS : Verdict.BLOCKED, $"admin GET detection {detId} success={alive.Success} msg='{alive.Message}'",
                blocked: alive.Success ? "" : "second login evicted the main session");
            if (!alive.Success) return;

            var opPerm = new PermissionService();
            opPerm.Apply(opLogin.Data.User);
            rec.Add("E1.2", TAG, "서버 운영자 프리셋의 events 등급(로그인 응답 그대로)", Verdict.INFO,
                $"server preset events={opEvents?.ToString(Newtonsoft.Json.Formatting.None)}; client CanControl={opPerm.CanControl("events")} CanEdit={opPerm.CanEdit("events")}");

            // ---- 운영자가 조치보고 창에서 [확인] — 제품 경로 그대로 ----
            var opEventApi = new EventApiService(opBoot.Log, opBoot.Api, opBoot.Setup, opBoot.Probe);
            var provider = new EvProvider(boot.Log, adminEvents);
            var model = await LoadDetectionViaProvider(provider, detId).ConfigureAwait(false);
            if (model is null) { rec.Add("E1.3", TAG, "arrange: 탐지를 창 모델로 읽기", Verdict.BLOCKED, $"detection {detId} not in provider page", blocked: "provider read failed"); return; }

            var opAccount = new AccountModel { Name = "LRT-EVT 운영자", Username = OP_LOGIN };
            using (new IocScope(new Dictionary<Type, object>
            {
                [typeof(IPermissionService)] = opPerm,
                [typeof(IEventApiService)] = opEventApi,
                [typeof(IAccountModel)] = opAccount,
                [typeof(IActionReportGuard)] = new ActionReportGuard(),
                [typeof(Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider)] = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider(),
            }))
            {
                var (outcome, wire) = await DriveDetectionReportDialog(opBoot, rec, model, opAccount, etc: false, memo: null, TAG + "/op-ok").ConfigureAwait(false);
                var post = wire.FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/events/actions"));
                // 올바른 동작: 서버가 거절할 요청을 보내지 않고 창에서 '권한 없음' 으로 멈춘다.
                var ok = post is null && outcome.StartsWith("popup:권한 없음");
                rec.Add("E1a", TAG, "events:edit 없는 운영자가 [확인] → 창이 보내기 전에 막는다(서버는 403 을 줄 요청)",
                    ok ? Verdict.PASS : Verdict.FAIL,
                    post is null
                        ? $"no POST; dialog said '{outcome}'"
                        : $"POST /events/actions went out -> HTTP {post.Status} {Recorder.Trunc(post.ResponseBodyRedacted, 220)}; dialog said '{outcome}'",
                    defectAt: ok ? "" : "Events.Ui/ViewModels/Dialogs/DetectionReportDialogViewModel.cs CanCtrlEvents (CanControl vs server events:edit)",
                    seqs: wire.Select(w => w.Seq).ToArray());
                foreach (var w in wire.Where(w => w.Method == "POST" && w.Status is 200 or 201))
                    TrackActionFromWire(fx, w);
            }

            // ---- 관리자(권한 있음) happy path: 와이어 키 · 201 · 원본 action_reported ----
            // 관리자 권한 스냅샷 — 새로 로그인하지 않는다(세션 상한 정책이 있으면 주 세션을 밀어낼 수 있다). 이미 가진 토큰으로 /auth/me.
            var adminPerm = new PermissionService();
            var (ms, mj) = await raw.Get("auth/me").ConfigureAwait(false);
            var me = (mj["data"] as JObject) ?? mj;
            adminPerm.Apply(me.ToObject<Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.AuthUserDto>()!);
            rec.Add("E1.4", TAG, "관리자 권한 스냅샷(/auth/me)", Verdict.INFO, $"HTTP {ms}; role={Str(me["role"])} CanEdit(events)={adminPerm.CanEdit("events")}");
            var adminAccount = new AccountModel { Name = "LRT-EVT 관리자", Username = "admin" };
            using (new IocScope(new Dictionary<Type, object>
            {
                [typeof(IPermissionService)] = adminPerm,
                [typeof(IEventApiService)] = adminEvents,
                [typeof(IAccountModel)] = adminAccount,
                [typeof(IActionReportGuard)] = new ActionReportGuard(),
                [typeof(Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider)] = new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider(),
            }))
            {
                var (outcome, wire) = await DriveDetectionReportDialog(boot, rec, model, adminAccount, etc: false, memo: null, TAG + "/admin-ok").ConfigureAwait(false);
                var post = wire.FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/events/actions"));
                if (post is not null) TrackActionFromWire(fx, post);
                var body = post is null ? null : JObject.Parse(post.RequestBody);
                var keys = body is null ? "" : string.Join(",", body.Properties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
                var (rs, rj) = await raw.Get($"events/detections/{detId}").ConfigureAwait(false);
                var reported = (bool?)rj["data"]?["action_reported"];
                // created_at 은 서버가 "무시됨(deprecated, v6.3.16)" 으로 받는 선언 필드다 — 있어도 422 가 아니다(INFO 로만 남긴다).
                var required = new[] { "content", "from_event_id", "type_event", "user" };
                var extra = body is null ? new List<string>() : body.Properties().Select(p => p.Name).Except(required).Except(new[] { "created_at" }).ToList();
                var ok = post?.Status == 201 && body is not null && required.All(k => body[k] is not null) && extra.Count == 0
                         && (string?)body?["type_event"] == "Action" && (int?)body?["from_event_id"] == detId
                         && reported == true && outcome == "close";
                rec.Add("E1b", TAG, "권한 있는 사용자 [확인] → POST 201, 와이어 키 {type_event,content,user,from_event_id}(+무시되는 created_at), 재조회 action_reported=true, 창 닫힘",
                    ok ? Verdict.PASS : Verdict.FAIL,
                    $"HTTP {post?.Status}; keys=[{keys}]; body={Recorder.Trunc(post?.RequestBodyRedacted ?? "(none)", 200)}; re-GET {rs} action_reported={reported}; dialog='{outcome}'",
                    defectAt: ok ? "" : "Events.Ui/ViewModels/Events/DetectionEventCardViewModel.cs SendActionDetailed",
                    seqs: wire.Select(w => w.Seq).ToArray());

                // ---- E1c: '기타' + 빈 메모 → 서버 min_length=1 이라 422. 창이 먼저 막아야 한다 ----
                var (outcome2, wire2) = await DriveDetectionReportDialog(boot, rec, model, adminAccount, etc: true, memo: "", TAG + "/etc-empty").ConfigureAwait(false);
                var post2 = wire2.FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/events/actions"));
                if (post2 is not null && post2.Status is 200 or 201) TrackActionFromWire(fx, post2);
                var ok2 = post2 is null && outcome2.StartsWith("popup:") && outcome2.Contains("내용");
                rec.Add("E1c", TAG, "'기타' + 빈 메모 [확인] → 보내지 않고 '내용을 입력' 안내(서버는 content min_length=1 로 422)",
                    ok2 ? Verdict.PASS : Verdict.FAIL,
                    post2 is null
                        ? $"no POST; dialog said '{outcome2}'"
                        : $"POST went out: body={Recorder.Trunc(post2.RequestBodyRedacted, 160)} -> HTTP {post2.Status} {Recorder.Trunc(post2.ResponseBodyRedacted, 200)}; dialog said '{outcome2}'",
                    defectAt: ok2 ? "" : "Events.Ui/ViewModels/Dialogs/DetectionReportDialogViewModel.cs ClickOk (Memo 미검증 → SendAction(\"\"))",
                    seqs: wire2.Select(w => w.Seq).ToArray());
            }
        }
        catch (Exception ex)
        {
            rec.Add("E1!", TAG, "action report round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await fx.CleanupAsync().ConfigureAwait(false);
        }
    }

    // ───────────────────────────── E2 억제 스케줄 콘솔 ─────────────────────────────

    /// <summary>
    /// E2 — 억제 콘솔 서랍으로 만든 스케줄이 서버 8.0 계약대로 나가고(<c>unit_id</c> 포함), 이름만 고친 PATCH 가
    /// 다른 칸을 건드리지 않으며, 취소 · 일괄삭제가 서버에 실제로 반영되는가.
    /// 서버 규칙(schemas/event_suppression.py:68-71): unit_id 생략 = 기본 부대 귀속 + 서버 로그 경고,
    /// "다음 차수부터 필수". 8.0 미만 쓰기 스키마에는 키가 없다(extra=forbid) → 판본으로 가른다.
    /// 대상은 이 스텝이 만든 LRT-EVT 센서 하나, 반복은 일요일 03:00~04:00, 시작은 내일 — 다른 작업자의 이벤트를 억제하지 않는다.
    /// </summary>
    [ExtraStep("events-vm", 20)]
    static async Task EventsVm_E2_Suppression(ExtraContext ctx)
    {
        const string TAG = "E2";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        try
        {
            var (_, sensor) = await ArrangeSensor(fx, "E2", 97200, ownUnit: false).ConfigureAwait(false);
            var ownUnit = await UnitScope.ResolveAsync().ConfigureAwait(false);
            var isUnitEra = boot.Probe.Contract >= Ironwall.Dotnet.Libraries.Api.Services.EnumServerContract.V8_0;

            var api = new EventSuppressionApiService(boot.Log, boot.Api, boot.Setup);
            var ea = new EventAggregator();
            var console = new SupConsole(ea, boot.Log, api, null, null);
            boot.Wire.CurrentTag = TAG + "/load";
            await console.ActivateAsync().ConfigureAwait(false);

            // ---- 서랍으로 새 스케줄 ----
            console.AddNew();
            var d = console.Drawer;
            var tomorrow = DateTime.Today.AddDays(1);
            d.Name = "LRT-EVT-E2-SUP";
            d.IsDeviceMode = true;
            d.AddSelected(new object[] { new Ironwall.Dotnet.Monitoring.Models.Devices.SensorDeviceModel { Id = sensor, DeviceName = "LRT-EVT-E2-S1" } });
            d.IsWeekly = true;
            d.WindowStartText = tomorrow.ToString("yyyy-MM-dd") + " 00:00";
            d.IsUnlimited = true;
            d.IsMonChecked = false; d.IsTueChecked = false; d.IsWedChecked = false; d.IsThuChecked = false; d.IsFriChecked = false;
            d.IsSatChecked = false; d.IsSunChecked = true;
            d.DailyStartText = "03:00";
            d.DailyEndText = "04:00";

            boot.Wire.CurrentTag = TAG + "/create";
            var mark = rec.Wire.Count;
            await d.SaveAsync().ConfigureAwait(false);
            await Task.Delay(300).ConfigureAwait(false);
            var post = rec.Wire.Skip(mark).FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/event-suppression-schedules"));
            if (post is null || post.Status is not (200 or 201))
            {
                rec.Add("E2a", TAG, "서랍 [저장] → POST 201", Verdict.FAIL,
                    $"POST={(post is null ? "(없음)" : $"HTTP {post.Status} {Recorder.Trunc(post.ResponseBodyRedacted, 300)}")}; drawer status='{d.StatusLine}' error='{d.ErrorText}' canSave={d.CanSave}",
                    defectAt: "Events.Ui/Consoles/Suppression/SuppressionDrawerViewModel.cs SaveAsync");
                return;
            }
            var sid = (int)JObject.Parse(post.ResponseBody)["data"]!["id"]!;
            fx.Schedules.Add(sid); rec.Created("suppression-schedule", sid);
            var body = JObject.Parse(post.RequestBody);

            var hasWindowEndNull = body.Property("window_end") is { } we && we.Value.Type == JTokenType.Null;
            var dailyStart = Str(body["daily_start"]);
            var dailyNoOffset = dailyStart == "03:00:00" || dailyStart == "03:00";
            var mask = (int?)body["days_of_week"];
            var sentUnit = (int?)body["unit_id"];
            var unitOk = isUnitEra ? sentUnit == ownUnit : body["unit_id"] is null;
            rec.Add("E2a", TAG, "서랍 새 스케줄(weekly · 무제한) 와이어: window_end=null 명시 · daily_start offset 없음 · days_of_week=64 · 8.0 이면 unit_id=자기 부대",
                (hasWindowEndNull && dailyNoOffset && mask == 64 && unitOk) ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {post.Status}; window_end null={hasWindowEndNull}; daily_start='{dailyStart}'; days_of_week={mask}; unit_id sent={(body["unit_id"] is null ? "(키 없음)" : sentUnit.ToString())} own={ownUnit} unitEra={isUnitEra}; body={Recorder.Trunc(post.RequestBodyRedacted, 400)}",
                defectAt: unitOk ? "" : "Events.Ui/Consoles/Suppression/SuppressionConsoleViewModel.cs SaveAsync → SuppressionRequestBuilder.BuildCreate(draft) 에 unitId 미전달");

            var (g1s, g1) = await raw.Get($"event-suppression-schedules/{sid}").ConfigureAwait(false);
            var before = g1["data"] as JObject;
            var persisted = before is not null && before["window_end"]?.Type == JTokenType.Null && (int?)before["days_of_week"] == 64
                            && Str(before["daily_start"]).StartsWith("03:00") && Str(before["recurrence_type"]) == "weekly"
                            && (before["target_device_ids"] as JArray)?.Select(x => (int)x).SequenceEqual(new[] { sensor }) == true;
            rec.Add("E2b", TAG, "재조회: 서버가 저장한 값이 서랍에서 고른 값과 같다",
                persisted ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {g1s}; {Recorder.Trunc(before?.ToString(Newtonsoft.Json.Formatting.None) ?? "(none)", 500)}",
                defectAt: persisted ? "" : "Events.Ui/Consoles/Suppression/SuppressionRequestBuilder.cs BuildCreate");

            // ---- 이름만 고친다 → 다른 칸은 전부 그대로 ----
            await console.LoadAsync().ConfigureAwait(false);
            console.Selected = console.Schedules.FirstOrDefault(r => r.Id == sid);
            if (console.Selected is null)
            {
                rec.Add("E2c", TAG, "목록에서 방금 만든 행 고르기", Verdict.BLOCKED, $"row {sid} not in console list ({console.Schedules.Count})", blocked: "list did not include the new row");
                return;
            }
            boot.Wire.CurrentTag = TAG + "/patch";
            await console.EditSelectedAsync().ConfigureAwait(false);
            console.Drawer.Name = "LRT-EVT-E2-SUP-RENAMED";
            mark = rec.Wire.Count;
            await console.Drawer.SaveAsync().ConfigureAwait(false);
            await Task.Delay(300).ConfigureAwait(false);
            var patch = rec.Wire.Skip(mark).FirstOrDefault(w => w.Method == "PATCH");
            var (g2s, g2) = await raw.Get($"event-suppression-schedules/{sid}").ConfigureAwait(false);
            var after = g2["data"] as JObject;
            var ignore = new HashSet<string> { "name", "updated_at", "status", "is_suppressing_now", "occurrence_start", "occurrence_end", "next_occurrence_start" };
            var diffs = before is null || after is null ? new List<string> { "(missing)" }
                : before.Properties().Where(p => !ignore.Contains(p.Name) && !JToken.DeepEquals(p.Value, after[p.Name]))
                        .Select(p => $"{p.Name}: {p.Value.ToString(Newtonsoft.Json.Formatting.None)} -> {after[p.Name]?.ToString(Newtonsoft.Json.Formatting.None)}").ToList();
            var renamed = Str(after?["name"]) == "LRT-EVT-E2-SUP-RENAMED";
            var patchBody = patch is null ? null : JObject.Parse(patch.RequestBody);
            var patchUnitOk = patchBody?["unit_id"] is null || (int?)patchBody["unit_id"] == (int?)before?["unit_id"];
            rec.Add("E2c", TAG, "이름만 고친 PATCH → 재조회에서 이름 외 모든 칸이 그대로(unit_id 보존 포함)",
                (patch?.Status == 200 && renamed && diffs.Count == 0 && patchUnitOk) ? Verdict.PASS : Verdict.FAIL,
                $"PATCH HTTP {patch?.Status}; renamed={renamed}; diffs=[{string.Join("; ", diffs)}]; patch unit_id={(patchBody?["unit_id"] is null ? "(키 없음 = 보존)" : patchBody["unit_id"]!.ToString())}; body={Recorder.Trunc(patch?.RequestBodyRedacted ?? "(none)", 400)}; drawer='{console.Drawer.StatusLine}'",
                defectAt: (patch?.Status == 200 && renamed && diffs.Count == 0 && patchUnitOk) ? "" : "Events.Ui/Consoles/Suppression/SuppressionRequestBuilder.cs BuildUpdate");

            // ---- 취소(soft-cancel) — 확인 팝업이 부르는 핸들러 그대로 ----
            boot.Wire.CurrentTag = TAG + "/cancel";
            await console.HandleAsync(new SupCancel { ScheduleId = sid, Name = "LRT-EVT-E2-SUP-RENAMED" }, CancellationToken.None).ConfigureAwait(false);
            var (g3s, g3) = await raw.Get($"event-suppression-schedules/{sid}").ConfigureAwait(false);
            var revoked = g3["data"]?["revoked_at"];
            var cancelled = Str(g3["data"]?["status"]) == "cancelled" || (revoked is not null && revoked.Type != JTokenType.Null);
            rec.Add("E2d", TAG, "취소 → 재조회 status=cancelled · revoked_at 기록, 콘솔 안내가 성공을 말한다",
                cancelled && console.StatusText.Contains("취소했습니다") ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {g3s}; status={Str(g3["data"]?["status"])} revoked_at={revoked}; console='{console.StatusText}'",
                defectAt: cancelled ? "" : "Events.Ui/Consoles/Suppression/SuppressionConsoleViewModel.cs HandleAsync(Cancel)");

            // ---- 일괄 하드삭제 — 확인 팝업이 부르는 핸들러 그대로 ----
            boot.Wire.CurrentTag = TAG + "/bulk-delete";
            await console.HandleAsync(new SupDelete { Ids = new List<int> { sid } }, CancellationToken.None).ConfigureAwait(false);
            var (g4s, _) = await raw.Get($"event-suppression-schedules/{sid}").ConfigureAwait(false);
            if (g4s == 404) { fx.Schedules.Remove(sid); rec.Deleted("suppression-schedule", sid); }
            rec.Add("E2e", TAG, "일괄삭제 → 재조회 404, 콘솔 판정문이 삭제를 확인",
                g4s == 404 ? Verdict.PASS : Verdict.FAIL,
                $"re-GET HTTP {g4s}; console='{console.StatusText}'",
                defectAt: g4s == 404 ? "" : "Events.Ui/Consoles/Suppression/SuppressionConsoleViewModel.cs HandleAsync(Delete)");

            // ---- E2f: 옛 억제 패널(EventUiModule 에 아직 등록됨)의 [생성] 도 같은 계약을 따르는가 ----
            var panel = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels.EventSuppressionSchedulePanelViewModel(
                ea, boot.Log, api, new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceProvider(), new Ironwall.Dotnet.Libraries.Devices.Providers.DeviceGroupProvider(boot.Log));
            panel.Name = "LRT-EVT-E2-PANEL";
            panel.TargetType = "device";
            panel.SelectedDevices.Add(new Ironwall.Dotnet.Monitoring.Models.Devices.SensorDeviceModel { Id = sensor, DeviceName = "LRT-EVT-E2-S1" });
            panel.WindowStart = tomorrow.AddHours(3);
            panel.WindowEnd = tomorrow.AddHours(4);
            boot.Wire.CurrentTag = TAG + "/panel-create";
            var markP = rec.Wire.Count;
            await panel.ClickCreate().ConfigureAwait(false);
            var postP = rec.Wire.Skip(markP).FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/event-suppression-schedules"));
            if (postP is not null && postP.Status is 200 or 201)
            {
                var pid = (int)JObject.Parse(postP.ResponseBody)["data"]!["id"]!;
                fx.Schedules.Add(pid); rec.Created("suppression-schedule", pid);
            }
            var bodyP = postP is null ? null : JObject.Parse(postP.RequestBody);
            var unitOkP = bodyP is not null && (isUnitEra ? (int?)bodyP["unit_id"] == ownUnit : bodyP["unit_id"] is null);
            rec.Add("E2f", TAG, "옛 억제 패널 [생성] 와이어도 8.0 이면 unit_id=자기 부대",
                (postP?.Status is 200 or 201 && unitOkP) ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {postP?.Status}; unit_id sent={(bodyP?["unit_id"] is null ? "(키 없음)" : bodyP["unit_id"]!.ToString())} own={ownUnit}; body={Recorder.Trunc(postP?.RequestBodyRedacted ?? "(none)", 300)}",
                defectAt: unitOkP ? "" : "Events.Ui/ViewModels/Panels/EventSuppressionSchedulePanelViewModel.cs ClickCreate (unit_id 미탑재)");
            await console.DeactivateAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            rec.Add("E2!", TAG, "suppression console round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await fx.CleanupAsync().ConfigureAwait(false);
        }
    }

    // ───────────────────────────── E3 이벤트 개요(통계) ─────────────────────────────

    /// <summary>
    /// E3 — 개요 창(DataChartPanelViewModel → EventProviderService → GET /events/statistics/dashboard → EventOverviewViewModel.Load)
    /// 이 서버 응답을 빠짐없이 · 같은 뜻으로 보이는가. 비교 기준은 <b>같은 요청의 와이어 응답</b>이라
    /// 다른 작업자가 만든 이벤트가 섞여도 판정이 흔들리지 않는다.
    /// </summary>
    [ExtraStep("events-vm", 30)]
    static async Task EventsVm_E3_Overview(ExtraContext ctx)
    {
        const string TAG = "E3";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        try
        {
            // ---- 씨앗: 탐지 2 · 경보(Alert) 1 · 장애 1 · (함체) 운영 1 — 전부 자기 부대 ----
            var (ctrl, sensor) = await ArrangeSensor(fx, "E3", 97300).ConfigureAwait(false);
            for (var i = 0; i < 2; i++)
                await fx.Event("events/detections", new { type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR" }).ConfigureAwait(false);
            await fx.Event("events/detections", new { type_event = "Alert", device_id = sensor, result = "PIR_SENSOR" }).ConfigureAwait(false);
            await fx.Event("events/malfunctions", new { type_event = "Fault", device_id = sensor, reason = "FAULT_CONTROLLER" }).ConfigureAwait(false);
            int enclosure = 0;
            try
            {
                // 운영 이벤트는 직접 POST 할 수 없다(D-8: 개폐 사유는 서버가 부품 전이에서 만든다) —
                //   문 위치 부품(DOOR_SENSOR)을 선언한 함체를 만들고 component-status 로 OPEN 을 보고한다.
                enclosure = await fx.Device("devices/enclosures", "enclosure", new
                {
                    unit_id = fx.UnitId, type_enclosure = "Outdoor", number_device = 97310, name_device = "LRT-EVT-E3-ENC",
                    hardware_spec = new { schema = 1, components = new[] { new { key = "door", type = "DOOR_SENSOR" } } },
                }).ConfigureAwait(false);
                var (ps, pj) = await raw.Patch($"devices/enclosures/{enclosure}/component-status", new { door = new { state = "OPEN", health = "OK", observed_at = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz") } }).ConfigureAwait(false);
                var (os, oj) = await raw.Get("events/operations?page=1&limit=100").ConfigureAwait(false);
                foreach (var op in (oj["data"] as JArray ?? new JArray()).OfType<JObject>().Where(o => (int?)(o["device"] as JObject)?["id"] == enclosure))
                {
                    fx.Events.Add(("events/operations", (int)op["id"]!)); rec.Created("events/operations", (int)op["id"]!);
                }
                rec.Add("E3.0", TAG, "arrange: 함체 문 OPEN 보고 → 서버가 만든 운영 이벤트", Verdict.INFO,
                    $"component-status HTTP {ps} {(ps == 200 ? "" : Short(pj))}; operations for enclosure {enclosure}: {fx.Events.Count(e => e.Path == "events/operations")} (list HTTP {os})");
            }
            catch (Exception ex) { rec.Add("E3.0", TAG, "arrange: 함체 + 운영 이벤트", Verdict.INFO, ex.Message); }

            var eventApi = new EventApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var provider = new EvProvider(boot.Log, eventApi);
            var chart = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels.DataChartPanelViewModel(new EventAggregator(), boot.Log, provider);

            // ---- 기본 기간(대시보드 기본값과 같다: 지금부터 24시간 전 ~ 지금) ----
            var end = DateTime.Now.AddMinutes(1);
            var start = end.AddDays(-1);
            chart.SetDate(start, end);
            boot.Wire.CurrentTag = TAG + "/24h";
            var mark = rec.Wire.Count;
            var dto = await chart.RefreshDashboardDtoAsync().ConfigureAwait(false);
            var wire = rec.Wire.Skip(mark).FirstOrDefault(w => w.Uri.Contains("/events/statistics/dashboard"));
            if (dto is null || wire is null || wire.Status != 200)
            {
                rec.Add("E3.1", TAG, "통계 조회", Verdict.BLOCKED, $"HTTP {wire?.Status} {Recorder.Trunc(wire?.ResponseBodyRedacted ?? "", 300)}", blocked: "dashboard fetch failed");
                return;
            }
            var server = (JObject)JObject.Parse(wire.ResponseBody)["data"]!;
            var sSum = (JObject)server["summary"]!;
            var overview = new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview.EventOverviewViewModel();
            overview.Load(dto, start, end);

            // E3a 총계
            var serverTotal = (int)sSum["total"]!;
            rec.Add("E3a", TAG, "개요 총계 = 서버 summary.total (서버 total 은 alert 를 포함한다)",
                overview.Total == serverTotal ? Verdict.PASS : Verdict.FAIL,
                $"window Total={overview.Total}; server total={serverTotal} (sensor {sSum["sensor_detection"]} camera {sSum["camera_detection"]} alert {sSum["alert"]} mal {sSum["malfunction"]} con {sSum["connection"]} act {sSum["action"]}; operation {sSum["operation"]} 별도); note='{overview.TotalNote}'",
                defectAt: overview.Total == serverTotal ? "" : "Events.Ui/Consoles/Overview/EventOverviewViewModel.cs Load (5종 재합산 — alert 누락) · Messages/Dto/Events/EventSummaryDto.cs (alert 키 없음)");

            // E3b 경보가 보이는가
            var serverAlert = (int?)sSum["alert"] ?? 0;
            var alertSlice = overview.Slices.FirstOrDefault(s => s.Name.Contains("경보"));
            rec.Add("E3b", TAG, "서버 summary.alert(사전 경보) 가 개요에 조각으로 보인다",
                serverAlert == 0 ? Verdict.INFO : (alertSlice?.Count == serverAlert ? Verdict.PASS : Verdict.FAIL),
                $"server alert={serverAlert}; window slices=[{string.Join(", ", overview.Slices.Select(s => $"{s.Name}:{s.Count}"))}]",
                defectAt: alertSlice?.Count == serverAlert ? "" : "Events.Ui/Consoles/Overview/EventOverviewModels.cs EventSeriesSpec.All (alert 계열 없음)");

            // E3c 운영 이벤트(총계 밖)가 사라지지 않는가
            var serverOp = (int?)sSum["operation"] ?? 0;
            var opShown = overview.TotalNote.Contains($"운영 {serverOp:N0}건");
            rec.Add("E3c", TAG, "서버 summary.operation(총계 밖 별도 집계)이 개요에서 조용히 사라지지 않는다",
                serverOp == 0 ? Verdict.INFO : (opShown ? Verdict.PASS : Verdict.FAIL),
                $"server operation={serverOp}; window note='{overview.TotalNote}'",
                defectAt: opShown ? "" : "Messages/Dto/Events/EventSummaryDto.cs (operation 키 없음) · EventOverviewViewModel.TotalNote");

            // E3d 함체·통문 막대
            var sEnc = (server["by_device"]?["enclosures"] as JArray) ?? new JArray();
            var sGate = (server["by_device"]?["gates"] as JArray) ?? new JArray();
            var myEnc = sEnc.OfType<JObject>().FirstOrDefault(e => (int?)e["device_id"] == enclosure);
            var facilityNames = new List<string>();
            if (Enum.TryParse<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview.OverviewDeviceGroup>("Facility", out var facility))
            {
                overview.DeviceGroup = facility;
                facilityNames = overview.Bars.Select(b => b.Name).ToList();
                overview.DeviceGroup = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview.OverviewDeviceGroup.Controller;
            }
            var encShown = myEnc is not null && facilityNames.Contains(Str(myEnc["device_name"]));
            rec.Add("E3d", TAG, "서버 by_device.enclosures/gates(함체·통문 운영 이벤트)가 개요 막대로 보인다",
                myEnc is null ? Verdict.INFO : (encShown ? Verdict.PASS : Verdict.FAIL),
                $"server enclosures={sEnc.Count} gates={sGate.Count}; mine={(myEnc is null ? "(없음)" : myEnc.ToString(Newtonsoft.Json.Formatting.None))}; window facility bars=[{string.Join(", ", facilityNames)}]",
                defectAt: encShown ? "" : "Messages/Dto/Events/EventByDeviceDto.cs (enclosures/gates 없음) · EventOverviewViewModel.OverviewDeviceGroup");

            // E3e 추이: 서버는 빈 버킷을 주지 않는다 → 화면은 기간을 고르게 채워야 끌기 수학(시간 = 폭 비례)이 맞는다
            var sSeries = (server["trend"]?["series"] as JArray) ?? new JArray();
            var firstHour = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
            var lastHour = new DateTime(end.Year, end.Month, end.Day, end.Hour, 0, 0);
            var expected = (int)(lastHour - firstHour).TotalHours + 1;
            var labels = (overview.XAxes.FirstOrDefault()?.Labels ?? Array.Empty<string>()).ToList();
            var sensorSeries = overview.Series.FirstOrDefault(s => s.Name == "센서 탐지") as LiveChartsCore.SkiaSharpView.LineSeries<int>;
            var values = sensorSeries?.Values?.ToList() ?? new List<int>();
            var nowIdx = (int)(new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, 0, 0) - firstHour).TotalHours;
            var nowBucketKey = DateTime.Now.ToString("yyyy-MM-dd HH");
            var sNow = sSeries.OfType<JObject>().FirstOrDefault(b => Str(b["time_bucket"]) == nowBucketKey);
            var dense = labels.Count == expected && values.Count == expected
                        && nowIdx >= 0 && nowIdx < values.Count && values[nowIdx] == ((int?)sNow?["sensor_detection"] ?? -1);
            rec.Add("E3e", TAG, "추이 X 축이 기간 전체를 고르게 채운다(서버의 빈 버킷 생략을 메운다) — 끌기 시각 = 화면 위치",
                dense ? Verdict.PASS : Verdict.FAIL,
                $"hours in range={expected}; server buckets={sSeries.Count}; window labels={labels.Count} values={values.Count}; now idx={nowIdx} window[now]={(nowIdx >= 0 && nowIdx < values.Count ? values[nowIdx] : -1)} server[now]={sNow?["sensor_detection"]}",
                defectAt: dense ? "" : "Events.Ui/Consoles/Overview/EventOverviewViewModel.cs RebuildTrend (서버 버킷을 그대로 등간격 배치) ↔ EventTrendRangeMath.Resolve (시간 = 폭 비례 가정)");

            // E3g 라벨: "yyyy-MM-dd HH" 를 읽어 'HH시' 로 보이는가
            var nowLabel = nowIdx >= 0 && nowIdx < labels.Count ? labels[nowIdx] : "(none)";
            var serverLabels = sSeries.OfType<JObject>().Select(b => Str(b["time_bucket"])).Take(3).ToList();
            var labelOk = nowLabel == DateTime.Now.ToString("HH") + "시" || (DateTime.Now.Hour == 0 && nowLabel == DateTime.Now.ToString("MM-dd"));
            rec.Add("E3g", TAG, "time_bucket(\"yyyy-MM-dd HH\")이 'HH시' 라벨로 읽힌다",
                labelOk ? Verdict.PASS : Verdict.FAIL,
                $"server buckets e.g. [{string.Join(", ", serverLabels)}]; window label at now='{nowLabel}'; first labels=[{string.Join(", ", labels.Take(4))}]",
                defectAt: labelOk ? "" : "Events.Ui/Consoles/Overview/EventOverviewViewModel.cs ShortLabel (DateTime.TryParse 가 \"yyyy-MM-dd HH\" 를 못 읽음)");

            // E3h 이름 없는 장비 막대
            var nameless = (server["by_device"]?["controllers"] as JArray ?? new JArray()).OfType<JObject>().Count(c => c["controller_name"]?.Type is null or JTokenType.Null)
                         + (server["by_device"]?["cameras"] as JArray ?? new JArray()).OfType<JObject>().Count(c => c["camera_name"]?.Type is null or JTokenType.Null);
            var blankBars = overview.Bars.Count(b => string.IsNullOrWhiteSpace(b.Name));
            rec.Add("E3h", TAG, "이름 없는 장비(controller_name/camera_name=null) 막대가 빈 이름으로 그려지지 않는다",
                nameless == 0 ? Verdict.INFO : (blankBars == 0 ? Verdict.PASS : Verdict.FAIL),
                nameless == 0
                    ? "서버 응답에 이름 없는 장비가 없다 — 장비 생성은 name_device min_length=1 이라 API 로는 만들 수 없다(재현 불가, 고치지 않음)"
                    : $"server nameless rows={nameless}; window blank bars={blankBars}");

            // ---- E3f: 3일 기간 — 화면은 '1일 단위' 라고 하는데 요청은 interval=hour 인가 ----
            var start3 = end.AddDays(-3);
            chart.SetDate(start3, end);
            boot.Wire.CurrentTag = TAG + "/3d";
            mark = rec.Wire.Count;
            var dto3 = await chart.RefreshDashboardDtoAsync().ConfigureAwait(false);
            var wire3 = rec.Wire.Skip(mark).FirstOrDefault(w => w.Uri.Contains("/events/statistics/dashboard"));
            var overview3 = new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview.EventOverviewViewModel();
            overview3.Load(dto3, start3, end);
            var reqInterval = System.Web.HttpUtility.ParseQueryString(new Uri(wire3?.Uri ?? "http://x/").Query)["interval"];
            var srvInterval = Str(JObject.Parse(wire3?.ResponseBody ?? "{}")["data"]?["trend"]?["interval"]);
            var consistent = (overview3.IntervalText == "1일 단위") == (srvInterval == "day");
            rec.Add("E3f", TAG, "3일 기간: 화면의 단위 표기 · 끌기 스냅(Bucket)과 서버가 실제로 집계한 interval 이 같다",
                consistent ? Verdict.PASS : Verdict.FAIL,
                $"request interval={reqInterval}; server trend.interval={srvInterval}; window IntervalText='{overview3.IntervalText}' Bucket={overview3.Bucket}; window labels={(overview3.XAxes.FirstOrDefault()?.Labels?.Count ?? 0)}",
                defectAt: consistent ? "" : "Events.Ui/ViewModels/Panels/DataChartPanelViewModel.cs RefreshDashboardDtoAsync/DataInitialize (interval \"hour\" 고정) ↔ EventTrendRangeMath.BucketFor");
        }
        catch (Exception ex)
        {
            rec.Add("E3!", TAG, "overview statistics round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await fx.CleanupAsync().ConfigureAwait(false);
        }
    }

    // ───────────────────────────── E4 이벤트 맵핑 워크벤치 ─────────────────────────────

    /// <summary>워크벤치의 장비 캐시 자리 — 이 스텝은 팔레트를 쓰지 않는다(AddDevices 는 캐시를 보지 않는다). 하네스 비계.</summary>
    sealed class EmptyMappingDeviceSource : Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.IMappingDeviceSource
    {
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingDeviceInfo> Devices(Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingActionKind kind)
            => Array.Empty<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingDeviceInfo>();
        public IReadOnlyList<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingGroupInfo> Groups()
            => Array.Empty<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingGroupInfo>();
        public Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingDeviceInfo? Find(Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingActionKind kind, int deviceId) => null;
    }

    static async Task WaitIdle(Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingWorkbenchViewModel vm, int ms = 15000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        await Task.Delay(50).ConfigureAwait(false);
        while ((vm.IsBusy || vm.IsApplying) && DateTime.UtcNow < until) await Task.Delay(50).ConfigureAwait(false);
    }

    static async Task<int> ArrangeMapping(Raw raw, Recorder rec, List<int> mappings, string name)
    {
        var (s, j) = await raw.Post("integrations/event-mappings", new { name_event = name, category_event_mapping = "NONE", status = false }).ConfigureAwait(false);
        if (s != 200 && s != 201) throw new InvalidOperationException($"arrange mapping failed: HTTP {s} {Short(j)}");
        var id = (int)j["data"]!["id"]!;
        mappings.Add(id); rec.Created("event-mapping", id);
        return id;
    }

    static async Task CleanupMappings(Raw raw, Recorder rec, List<int> mappings)
    {
        foreach (var m in mappings)
        {
            // 하위 배선을 먼저 해제하고(벌크 DELETE) 맵핑을 지운다.
            foreach (var seg in new[] { "cameras", "speakers", "lamps" })
            {
                var (ls, lj) = await raw.Get($"integrations/event-mappings/{m}/{seg}").ConfigureAwait(false);
                var data = lj["data"];
                var arr = data as JArray ?? data?["items"] as JArray ?? new JArray();
                var ids = arr.OfType<JObject>().Select(o => (int?)o["config_id"] ?? (int?)o["id"] ?? 0).Where(i => i > 0).ToList();
                if (ids.Count > 0) await raw.Delete($"integrations/event-mappings/{m}/{seg}", new { config_ids = ids }).ConfigureAwait(false);
            }
            var (ds, dj) = await raw.Delete($"integrations/event-mappings/{m}").ConfigureAwait(false);
            if (ds is 200 or 204 or 404) rec.Deleted("event-mapping", m);
            else rec.Leftover("event-mapping", m, $"DELETE -> HTTP {ds} {Short(dj)}");
        }
    }

    /// <summary>
    /// E4 — 맵핑 워크벤치 [적용] 이 서버 벌크 응답을 사실대로 반영하는가 · 동시 편집 대조가 실제로 작동하는가.
    /// (a) 편집 중 장비가 지워지면 서버는 그 장비 id 를 <c>not_found_config_ids</c> 로 돌려준다 — 그 행은 "실패" 로 남아야 한다.
    /// (b) <c>updated_at</c> 은 마이크로초 + offset 이다 — 봉투를 날짜로 읽어 문자열로 되돌리면 정밀도가 사라져 같은 초의 변경을 못 본다.
    /// </summary>
    [ExtraStep("events-vm", 40)]
    static async Task EventsVm_E4_Mapping(ExtraContext ctx)
    {
        const string TAG = "E4";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        var mappings = new List<int>();
        try
        {
            var gateway = new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingWorkbenchGateway(boot.Api, boot.Setup, boot.Probe, boot.Log);
            var speakerKind = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingActionKind.Speaker;

            var s1 = await fx.Device("devices/speakers", "speaker", new { speaker_role = "NORMAL", type_speaker = "Unknown", number_device = 97401, name_device = "LRT-EVT-E4-SPK1" }).ConfigureAwait(false);
            var s2 = await fx.Device("devices/speakers", "speaker", new { speaker_role = "NORMAL", type_speaker = "Unknown", number_device = 97402, name_device = "LRT-EVT-E4-SPK2" }).ConfigureAwait(false);

            // ---------- E4b: updated_at 정밀도 — 게이트웨이가 준 값 vs 서버가 보낸 원문 ----------
            var m1 = await ArrangeMapping(raw, rec, mappings, "LRT-EVT-E4-MAP1").ConfigureAwait(false);
            boot.Wire.CurrentTag = TAG + "/precision";
            var mark = rec.Wire.Count;
            var got = await gateway.GetMappingAsync(m1).ConfigureAwait(false);
            var w = rec.Wire.Skip(mark).FirstOrDefault(x => x.Uri.EndsWith($"/integrations/event-mappings/{m1}"));
            var rawUpdated = w is null ? null : System.Text.RegularExpressions.Regex.Match(w.ResponseBody, "\"updated_at\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
            var precise = got.IsSuccess && !string.IsNullOrEmpty(rawUpdated) && got.Value!.UpdatedAt == rawUpdated;
            rec.Add("E4b", TAG, "게이트웨이가 읽은 updated_at 이 서버 원문과 같다(마이크로초 · offset 보존)",
                precise ? Verdict.PASS : Verdict.FAIL,
                $"server raw='{rawUpdated}'; gateway dto='{got.Value?.UpdatedAt}'",
                defectAt: precise ? "" : "Events.Ui/Consoles/Mapping/MappingWorkbenchGateway.cs Parse (JsonConvert 기본 DateParseHandling → DateTime → 문자열 재서식)");

            // ---------- E4c: 같은 초 안의 남의 변경을 [적용] 전 대조가 잡는가 ----------
            string driftDetail = "(not run)";
            bool? driftCaught = null;
            for (var attempt = 1; attempt <= 3 && driftCaught is null; attempt++)
            {
                var md = await ArrangeMapping(raw, rec, mappings, $"LRT-EVT-E4-DRIFT{attempt}").ConfigureAwait(false);
                var (_, created) = await raw.Get($"integrations/event-mappings/{md}").ConfigureAwait(false);
                var t0 = created["data"]?["updated_at"]?.Value<DateTime>();
                var vmD = new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingWorkbenchViewModel(gateway, new EmptyMappingDeviceSource());
                await vmD.ReloadAsync().ConfigureAwait(false);
                vmD.SelectedMapping = vmD.Mappings.FirstOrDefault(x => x.Id == md);
                await WaitIdle(vmD).ConfigureAwait(false);
                vmD.SelectedRail = vmD.RailEntries.First(r => Equals(r.Tag, speakerKind));
                vmD.AddDevices(new[] { s1 }, -1);
                // 다른 사용자: 같은 맵핑의 설명을 바꾼다.
                var (_, patched) = await raw.Patch($"integrations/event-mappings/{md}", new { description = "LRT-EVT 다른 사용자" }).ConfigureAwait(false);
                var t1 = patched["data"]?["updated_at"]?.Value<DateTime>();
                var sameSecond = t0 is not null && t1 is not null && t0.Value.ToString("yyyyMMddHHmmss") == t1.Value.ToString("yyyyMMddHHmmss");
                boot.Wire.CurrentTag = TAG + $"/drift{attempt}";
                mark = rec.Wire.Count;
                await vmD.ApplyAsync().ConfigureAwait(false);
                await WaitIdle(vmD).ConfigureAwait(false);
                var bulk = rec.Wire.Skip(mark).FirstOrDefault(x => x.Method == "POST" && x.Uri.Contains("/bulk"));
                driftDetail = $"attempt {attempt}: t0={t0:HH:mm:ss.ffffff} t1={t1:HH:mm:ss.ffffff} sameSecond={sameSecond}; bulk POST sent={(bulk is not null)}; status='{vmD.StatusText}'";
                if (sameSecond || bulk is null) driftCaught = bulk is null;
            }
            rec.Add("E4c", TAG, "맵핑을 연 뒤 같은 초 안에 다른 사용자가 고쳤으면 [적용] 이 아무것도 보내지 않고 새로 고치기를 권한다",
                driftCaught is null ? Verdict.BLOCKED : driftCaught.Value ? Verdict.PASS : Verdict.FAIL,
                driftDetail,
                defectAt: driftCaught == false ? "Events.Ui/Consoles/Mapping/MappingWorkbenchViewModel.cs ApplyAsync UpdatedAt 대조 ← MappingWorkbenchGateway.Parse 정밀도 손실" : "",
                blocked: driftCaught is null ? "3회 모두 생성과 변경이 다른 초에 떨어져 같은 초 조건을 만들지 못했다" : "");

            // ---------- E4a: 편집 중 지워진 장비 → not_found_config_ids → 그 행은 실패로 남아야 한다 ----------
            var m2 = await ArrangeMapping(raw, rec, mappings, "LRT-EVT-E4-MAP2").ConfigureAwait(false);
            var vm = new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.MappingWorkbenchViewModel(gateway, new EmptyMappingDeviceSource());
            await vm.ReloadAsync().ConfigureAwait(false);
            vm.SelectedMapping = vm.Mappings.FirstOrDefault(x => x.Id == m2);
            await WaitIdle(vm).ConfigureAwait(false);
            vm.SelectedRail = vm.RailEntries.First(r => Equals(r.Tag, speakerKind));
            vm.AddDevices(new[] { s1, s2 }, -1);
            // 다른 사용자가 스피커 2 를 지운다(편집 도중).
            var (dels, _) = await raw.Delete($"devices/speakers/{s2}").ConfigureAwait(false);
            if (dels is 200 or 204) { fx.Devices.RemoveAll(d => d.Id == s2); rec.Deleted("speaker", s2); }
            boot.Wire.CurrentTag = TAG + "/apply";
            mark = rec.Wire.Count;
            await vm.ApplyAsync().ConfigureAwait(false);
            await WaitIdle(vm).ConfigureAwait(false);
            var bulkA = rec.Wire.Skip(mark).FirstOrDefault(x => x.Method == "POST" && x.Uri.Contains("/speakers/bulk"));
            var notFound = bulkA is null ? "(no bulk)" : (JObject.Parse(bulkA.ResponseBody)["data"]?["not_found_config_ids"]?.ToString(Newtonsoft.Json.Formatting.None) ?? "(none)");
            var rows = vm.BoardRows.Select(r => $"{r.Row.DeviceId}:{(r.Row.IsPersisted ? "saved" : "draft")}{(r.HasFailure ? "!fail" : "")}").ToList();
            var s2Row = vm.BoardRows.FirstOrDefault(r => r.Row.DeviceId == s2);
            var s1Saved = vm.BoardRows.Any(r => r.Row.DeviceId == s1 && r.Row.IsPersisted);
            var ok = s1Saved && s2Row is not null && s2Row.HasFailure && vm.StatusText.Contains("실패 1건");
            rec.Add("E4a", TAG, "편집 중 지워진 장비(서버 not_found_config_ids) 행은 실패 배지로 화면에 남고, 상태줄 '실패 1건 남음' 과 일치한다",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"bulk HTTP {bulkA?.Status} not_found={notFound}; status='{vm.StatusText}'; board rows=[{string.Join(", ", rows)}]; s2 row present={(s2Row is not null)} failure={s2Row?.HasFailure}",
                defectAt: ok ? "" : "Events.Ui/Consoles/Mapping/MappingCommitOutcome.cs AcceptCreate (not_found 행을 _settled 로 분류 → 재조회가 지움)");
        }
        catch (Exception ex)
        {
            rec.Add("E4!", TAG, "mapping workbench round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await CleanupMappings(raw, rec, mappings).ConfigureAwait(false);
            await fx.CleanupAsync().ConfigureAwait(false);
        }
    }

    // ───────────────────────────── E5 상세 [적용] 의 detail 보존 · E8 삭제 장비 이름 · E9 모르는 사유 ─────────────────────────────

    /// <summary>
    /// E5 — 상세 [적용] 은 PUT /events/{detections|malfunctions}/{id} 이고 서버는 <c>detail</c> 을 통째로 갈아 끼운다
    /// (routers/detections.py · malfunctions.py <c>event.detail = event_data.detail</c>). 클라가 모델에 없는 키를 빼고 보내면
    /// 업체 키가 사라지고, detail 이 null 인 장애는 0 으로 채워진다. 제품 경로: EventProviderService 페이지 조회 → 모델 수정 →
    /// EventProviderService.Update*Async(= 상세 [적용] 이 부르는 저장).
    /// </summary>
    [ExtraStep("events-vm", 50)]
    static async Task EventsVm_E5_DetailApply(ExtraContext ctx)
    {
        const string TAG = "E5";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        try
        {
            var (_, sensor) = await ArrangeSensor(fx, "E5", 97500).ConfigureAwait(false);
            var eventApi = new EventApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var provider = new EvProvider(boot.Log, eventApi);

            // ---- E5a: 탐지 — 모델에 있는 키 + 업체 키 ----
            var d1 = await fx.Event("events/detections", new
            {
                type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR",
                detail = new { signal = 5, thumbnail = "http://127.0.0.1/lrt-evt-e5.jpg", vendor_x = 1 },
            }).ConfigureAwait(false);
            // ---- E5b: 탐지 — 업체 키만(모델에 담기는 키 없음) ----
            var d2 = await fx.Event("events/detections", new
            {
                type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR",
                detail = new { vendor_only = "abc" },
            }).ConfigureAwait(false);
            // ---- E5c/d: 장애 — detail null · detail + 업체 키 ----
            var m1 = await fx.Event("events/malfunctions", new { type_event = "Fault", device_id = sensor, reason = "FAULT_CONTROLLER" }).ConfigureAwait(false);
            var m2 = await fx.Event("events/malfunctions", new
            {
                type_event = "Fault", device_id = sensor, reason = "FAULT_CONTROLLER",
                detail = new { first_start = 10, first_end = 20, vendor_y = "k" },
            }).ConfigureAwait(false);

            async Task CheckDetection(string id, int eventId, string title, Func<JObject?, bool> ok)
            {
                var (_, before) = await raw.Get($"events/detections/{eventId}").ConfigureAwait(false);
                var model = await LoadDetectionViaProvider(provider, eventId).ConfigureAwait(false);
                if (model is null) { rec.Add(id, TAG, title, Verdict.BLOCKED, "provider did not return the row", blocked: "read failed"); return; }
                model.Result = Ironwall.Dotnet.Libraries.Enums.EnumDetectionType.VIBRATION_SENSOR;   // 사용자가 '결과' 만 바꿨다
                boot.Wire.CurrentTag = TAG + "/" + id;
                var mark = rec.Wire.Count;
                string err = "";
                try { await provider.UpdateDetectionEventAsync(model).ConfigureAwait(false); } catch (Exception ex) { err = ex.Message; }
                var put = rec.Wire.Skip(mark).FirstOrDefault(w => w.Method == "PUT");
                var (_, after) = await raw.Get($"events/detections/{eventId}").ConfigureAwait(false);
                var detailAfter = after["data"]?["detail"] as JObject;
                var good = Str(after["data"]?["result"]) == "VIBRATION_SENSOR" && ok(detailAfter);
                rec.Add(id, TAG, title, good ? Verdict.PASS : Verdict.FAIL,
                    $"detail before={before["data"]?["detail"]?.ToString(Newtonsoft.Json.Formatting.None)}; PUT body={Recorder.Trunc(put?.RequestBodyRedacted ?? "(none)", 250)} -> HTTP {put?.Status}{(err.Length > 0 ? " err=" + err : "")}; detail after={after["data"]?["detail"]?.ToString(Newtonsoft.Json.Formatting.None)}",
                    defectAt: good ? "" : "Events.Ui/Helpers/DtoToModelHelper.cs ToDetectionEventReplaceDto/BuildDetectionDetail (모델 밖 detail 키 미보존) · Services/EventProviderService.cs UpdateDetectionEventAsync");
            }

            async Task CheckMalfunction(string id, int eventId, string title, Func<JToken?, bool> ok)
            {
                var (_, before) = await raw.Get($"events/malfunctions/{eventId}").ConfigureAwait(false);
                var model = await LoadMalfunctionViaProvider(provider, eventId).ConfigureAwait(false);
                if (model is null) { rec.Add(id, TAG, title, Verdict.BLOCKED, "provider did not return the row", blocked: "read failed"); return; }
                model.Reason = Ironwall.Dotnet.Libraries.Enums.EnumFaultType.FAULT_FENCE;   // 사용자가 '사유' 만 바꿨다
                boot.Wire.CurrentTag = TAG + "/" + id;
                var mark = rec.Wire.Count;
                string err = "";
                try { await provider.UpdateMalfunctionEventAsync(model).ConfigureAwait(false); } catch (Exception ex) { err = ex.Message; }
                var put = rec.Wire.Skip(mark).FirstOrDefault(w => w.Method == "PUT");
                var (_, after) = await raw.Get($"events/malfunctions/{eventId}").ConfigureAwait(false);
                var detailAfter = after["data"]?["detail"];
                var good = Str(after["data"]?["reason"]) == "FAULT_FENCE" && ok(detailAfter);
                rec.Add(id, TAG, title, good ? Verdict.PASS : Verdict.FAIL,
                    $"detail before={before["data"]?["detail"]?.ToString(Newtonsoft.Json.Formatting.None)}; PUT body={Recorder.Trunc(put?.RequestBodyRedacted ?? "(none)", 250)} -> HTTP {put?.Status}{(err.Length > 0 ? " err=" + err : "")}; detail after={detailAfter?.ToString(Newtonsoft.Json.Formatting.None)}",
                    defectAt: good ? "" : "Events.Ui/Helpers/DtoToModelHelper.cs ToMalfunctionEventReplaceDto (null detail → 0 채움 · 업체 키 미보존)");
            }

            await CheckDetection("E5a", d1, "탐지 '결과' 만 [적용] → detail 의 signal · thumbnail · 업체 키(vendor_x) 가 그대로 남는다",
                d => d is not null && (int?)d["signal"] == 5 && Str(d["thumbnail"]) == "http://127.0.0.1/lrt-evt-e5.jpg" && (int?)d["vendor_x"] == 1).ConfigureAwait(false);
            await CheckDetection("E5b", d2, "탐지 detail 이 업체 키뿐일 때 '결과' 만 [적용] → detail 이 null 로 지워지지 않는다",
                d => d is not null && Str(d["vendor_only"]) == "abc").ConfigureAwait(false);
            await CheckMalfunction("E5c", m1, "장애 detail=null 에서 '사유' 만 [적용] → detail 이 0 네 개로 채워지지 않는다(그대로 null)",
                d => d is null || d.Type == JTokenType.Null).ConfigureAwait(false);
            await CheckMalfunction("E5d", m2, "장애 detail(구간 + 업체 키) 에서 '사유' 만 [적용] → 구간 값과 업체 키(vendor_y)가 그대로",
                d => d is JObject o && (int?)o["first_start"] == 10 && (int?)o["first_end"] == 20 && Str(o["vendor_y"]) == "k").ConfigureAwait(false);

            // ---- E9a: 서버가 모르는 사유를 줄 수 있는가(그래야 (EnumFaultType)0 → "0" 저장 422 가 생긴다) ----
            var (us, uj) = await raw.Post("events/malfunctions", new { type_event = "Fault", device_id = sensor, reason = "FAULT_LRT_UNKNOWN" }).ConfigureAwait(false);
            if (us is 200 or 201) { var uid = (int)uj["data"]!["id"]!; fx.Events.Add(("events/malfunctions", uid)); rec.Created("events/malfunctions", uid); }
            rec.Add("E9a", TAG, "모르는 장애 사유가 서버에 저장될 수 있는가(재현 전제)", Verdict.INFO,
                us is 200 or 201
                    ? $"서버가 모르는 사유를 받았다 HTTP {us} — 재현 가능"
                    : $"서버가 거절 HTTP {us} ({Recorder.Trunc(Short(uj), 160)}) — 서버 사유 어휘 5종 = 클라 EnumFaultType 5종과 같다. 이 판본에서는 재현 불가(고치지 않음)");

            // ---- E8: 장비를 지운 뒤 — 서버는 device=null 이지만 device_description 스냅샷을 준다 ----
            var (_, ctrl2) = (0, await fx.Device("devices/sensors", "sensor", new { type_sensor = "PIR", number_device = 97509, name_device = "LRT-EVT-E5-GONE", controller_id = fx.Devices.First(d => d.Kind == "controller").Id, unit_id = fx.UnitId }).ConfigureAwait(false));
            var d3 = await fx.Event("events/detections", new { type_event = "Intrusion", device_id = ctrl2, result = "PIR_SENSOR" }).ConfigureAwait(false);
            var (dels, _) = await raw.Delete($"devices/sensors/{ctrl2}").ConfigureAwait(false);
            if (dels is 200 or 204) { fx.Devices.RemoveAll(d => d.Id == ctrl2); rec.Deleted("sensor", ctrl2); }
            var (_, gone) = await raw.Get($"events/detections/{d3}").ConfigureAwait(false);
            var serverDesc = Str(gone["data"]?["device_description"]);
            var goneModel = await LoadDetectionViaProvider(provider, d3).ConfigureAwait(false);
            var row = goneModel is null ? null : new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.DetectionEventViewModel(goneModel);
            var detail = new Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles.ConsoleDetailPresenter();
            var detailVm = new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail.EventDetailViewModel(detail, null);
            string shown = "(no row)";
            if (row is not null)
            {
                detailVm.Load(Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail.EventDetailKind.Detection, new List<object> { row }, false, false, 0);
                shown = detail.SingleTitle ?? "(null)";
            }
            var e8ok = serverDesc.Length > 0 && shown.Contains("LRT-EVT-E5-GONE");
            rec.Add("E8", TAG, "장비가 지워진 탐지의 상세 제목이 서버 device_description 스냅샷(장비 이름)을 보인다",
                serverDesc.Length == 0 ? Verdict.INFO : (e8ok ? Verdict.PASS : Verdict.FAIL),
                $"server device={gone["data"]?["device"]?.ToString(Newtonsoft.Json.Formatting.None)} device_description='{serverDesc}'; window title='{shown}'",
                defectAt: e8ok ? "" : "Events.Ui/Helpers/DtoToModelHelper.cs (device_description 미전달) · Consoles/Detail/EventDetailViewModel.cs BuildDetection \"(장비 없음)\"");
        }
        catch (Exception ex)
        {
            rec.Add("E5!", TAG, "detail apply round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await fx.CleanupAsync().ConfigureAwait(false);
        }
    }

    // ───────────────────────────── E6 레일 칩 의미 · E7 조치 트레이 ─────────────────────────────

    /// <summary>
    /// E6 — 목록 칩이 서버가 실제로 주는 사실로만 가르는가.
    /// 서버: 연결 이벤트의 type_event 는 <c>Connection</c> 하나뿐이고(utils/enums.py:126) 응답에 상태 칸이 없다(schemas/event.py:669-684).
    /// 탐지 type_event 는 Intrusion · Alert · ContactOn · ContactOff · WindyMode 다섯이다(utils/enums.py:124).
    /// </summary>
    [ExtraStep("events-vm", 60)]
    static async Task EventsVm_E6_RailChips(ExtraContext ctx)
    {
        const string TAG = "E6";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        try
        {
            var (_, sensor) = await ArrangeSensor(fx, "E6", 97600).ConfigureAwait(false);
            var eventApi = new EventApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var provider = new EvProvider(boot.Log, eventApi);

            // ---- 연결: 서버가 받는 형태 · 끊김 상태를 표현할 수 있는가 ----
            var c1 = await fx.Event("events/connections", new { type_event = "Connection", device_id = sensor }).ConfigureAwait(false);
            var (offStatus, offBody) = await raw.Post("events/connections", new { type_event = "ContactOff", device_id = sensor }).ConfigureAwait(false);
            if (offStatus is 200 or 201) { var oid = (int)offBody["data"]!["id"]!; fx.Events.Add(("events/connections", oid)); rec.Created("events/connections", oid); }
            var (_, cGet) = await raw.Get($"events/connections/{c1}").ConfigureAwait(false);
            var serverKeys = string.Join(",", ((cGet["data"] as JObject)?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>()));

            var page = await provider.FetchConnectionEventsPageAsync(DateTime.Now.AddHours(-2), DateTime.Now.AddHours(2), 1, 100).ConfigureAwait(false);
            var cModel = page.Items?.FirstOrDefault(m => m.Id == c1);
            var cRow = cModel is null ? null : new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.ConnectionEventViewModel(cModel);
            var chips = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventListFilter.ChipsFor(Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail.EventDetailKind.Connection);
            var pretends = chips.Any(c => c.Key is Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventListFilter.ChipDisconnected
                                                   or Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventListFilter.ChipConnected);
            var facts = cRow is null ? default : Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventRowFactsFactory.From(cRow);
            rec.Add("E6a", TAG, "연결 레일은 서버에 없는 '끊김 / 연결' 상태로 가르는 칩을 내지 않는다",
                !pretends ? Verdict.PASS : Verdict.FAIL,
                $"POST type_event=ContactOff -> HTTP {offStatus} ({Recorder.Trunc(Short(offBody), 140)}); server connection keys=[{serverKeys}]; window chips=[{string.Join(", ", chips.Select(c => $"{c.Key}:{c.Label}"))}]; my row StateKey='{facts.StateKey}' type={cModel?.MessageType}",
                defectAt: !pretends ? "" : "Events.Ui/Consoles/Lists/EventListFilter.cs ChipsFor(Connection) · EventRowFactsFactory (ContactOff 로 끊김 판정 — 서버는 Connection 만 준다)");

            // ---- 탐지: 다섯 유형이 '침입' 칩 하나에 섞이지 않는가 ----
            var types = new[] { "Intrusion", "Alert", "ContactOn", "ContactOff", "WindyMode" };
            var ids = new Dictionary<string, int>();
            foreach (var t in types)
                ids[t] = await fx.Event("events/detections", new { type_event = t, device_id = sensor, result = "CONTACT_SENSOR" }).ConfigureAwait(false);
            var dPage = await provider.FetchDetectionEventsPageAsync(DateTime.Now.AddHours(-2), DateTime.Now.AddHours(2), 1, 100).ConfigureAwait(false);
            var mine = (dPage.Items ?? new List<IDetectionEventModel>()).Where(m => ids.ContainsValue(m.Id)).ToList();
            var intrusionChip = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventListFilter.ChipIntrusion;
            var inIntrusion = mine.Where(m => Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventListFilter.MatchesChip(
                    Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventRowFactsFactory.From(new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.DetectionEventViewModel(m)), intrusionChip))
                .Select(m => m.MessageType.ToString()).OrderBy(x => x).ToList();
            var detChips = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventListFilter.ChipsFor(Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail.EventDetailKind.Detection);
            // 모든 유형이 '전체' 가 아닌 어떤 칩 하나에는 들어가야 한다(거르면 사라지는 유형이 없어야 한다).
            var unreachable = mine.Where(m => !detChips.Where(c => c.Key is not "all" and not "open" and not "done")
                    .Any(c => Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventListFilter.MatchesChip(
                        Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists.EventRowFactsFactory.From(new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.DetectionEventViewModel(m)), c.Key)))
                .Select(m => m.MessageType.ToString()).ToList();
            var okDet = mine.Count == 5 && inIntrusion.SequenceEqual(new[] { "Intrusion" }) && unreachable.Count == 0;
            rec.Add("E6b", TAG, "탐지 '침입' 칩은 Intrusion 만 — 접점 ON/OFF · 강풍 모드는 침입으로 세지 않고 자기 칩으로 걸린다",
                okDet ? Verdict.PASS : Verdict.FAIL,
                $"loaded mine={mine.Count}/5; '침입' chip matched=[{string.Join(",", inIntrusion)}]; unreachable by any type chip=[{string.Join(",", unreachable)}]; chips=[{string.Join(", ", detChips.Select(c => c.Label))}]",
                defectAt: okDet ? "" : "Events.Ui/Consoles/Lists/EventRowFactsFactory.cs TypeEventKey (Alert 외 전부 Intrusion) · EventListFilter.ChipsFor(Detection)");
        }
        catch (Exception ex)
        {
            rec.Add("E6!", TAG, "rail chip round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await fx.CleanupAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// E7 — 조치 트레이: (a) 보고가 서버에 만들어진 뒤 그 행이 '미조치' 로 남지 않는가 (b) 500자를 넘는 메모를 보내 422 를 받지 않는가.
    /// 트레이의 전송기는 대시보드 SendActionAsync 와 같은 사슬(임시 카드 → SendActionDetailed → POST /events/actions)이다.
    /// </summary>
    [ExtraStep("events-vm", 70)]
    static async Task EventsVm_E7_Tray(ExtraContext ctx)
    {
        const string TAG = "E7";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        try
        {
            var (_, sensor) = await ArrangeSensor(fx, "E7", 97700).ConfigureAwait(false);
            var eventApi = new EventApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var provider = new EvProvider(boot.Log, eventApi);
            var d1 = await fx.Event("events/detections", new { type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR" }).ConfigureAwait(false);
            var d2 = await fx.Event("events/detections", new { type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR" }).ConfigureAwait(false);
            var model1 = await LoadDetectionViaProvider(provider, d1).ConfigureAwait(false);
            var model2 = await LoadDetectionViaProvider(provider, d2).ConfigureAwait(false);
            if (model1 is null || model2 is null) { rec.Add("E7.0", TAG, "arrange", Verdict.BLOCKED, "provider read failed", blocked: "read failed"); return; }
            var row1 = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.DetectionEventViewModel(model1);
            var row2 = new Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.DetectionEventViewModel(model2);
            var models = new Dictionary<int, IDetectionEventModel> { [d1] = model1, [d2] = model2 };
            var ea = new EventAggregator();

            var account = new AccountModel { Name = "LRT-EVT 트레이", Username = "admin" };
            using var ioc = new IocScope(new Dictionary<Type, object>
            {
                [typeof(IEventApiService)] = eventApi,
                [typeof(IAccountModel)] = account,
                [typeof(IActionReportGuard)] = new ActionReportGuard(),
            });

            // 대시보드 SendActionAsync 와 같은 사슬 — 행이 들고 있는 모델로 임시 카드를 세워 보낸다.
            Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionReportSender sender = async (candidate, content, token) =>
            {
                var card = new DetectionEventCardViewModel(ea, boot.Log, models[candidate.EventId]);
                var result = await card.SendActionDetailed(content, "admin", token).ConfigureAwait(true);
                return result.Outcome switch
                {
                    ActionSendOutcome.Created => Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles.DraftOutcome.Applied,
                    ActionSendOutcome.GuardSkipped => Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles.DraftOutcome.Skipped,
                    _ => Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles.DraftOutcome.Failed,
                };
            };

            // ---- E7a: 적용 뒤 행 상태 ----
            var tray = new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionTrayViewModel(sender);
            tray.Enqueue(Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionTrayDrop.Plan(
                new[] { Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionTrayCandidateFactory.From(row1)!.Value }, true));
            tray.Phrase = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionTrayViewModel.Phrases[0];
            boot.Wire.CurrentTag = TAG + "/apply";
            var mark = rec.Wire.Count;
            await tray.ApplyAsync().ConfigureAwait(false);
            var post = rec.Wire.Skip(mark).FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/events/actions"));
            if (post is not null) TrackActionFromWire(fx, post);
            var (_, g1) = await raw.Get($"events/detections/{d1}").ConfigureAwait(false);
            var serverReported = (bool?)g1["data"]?["action_reported"];
            rec.Add("E7a", TAG, "트레이 [조치 적용] 201 뒤 그 행이 곧바로 '조치 있음' 이 된다(서버 action_reported 와 같다)",
                (post?.Status == 201 && serverReported == true && row1.IsActionReported) ? Verdict.PASS : Verdict.FAIL,
                $"POST HTTP {post?.Status}; server action_reported={serverReported}; row IsActionReported={row1.IsActionReported} (model.Status={model1.Status}); tray='{tray.StatusLine}'",
                defectAt: row1.IsActionReported ? "" : "Events.Ui/ViewModels/Events/DetectionEventCardViewModel.cs SendActionDetailed (응답으로 원본 상태를 갱신하지 않음) · EventDashboardViewModel.SendActionAsync");

            // ---- E7b: 500자 초과 메모 ----
            var tray2 = new Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionTrayViewModel(sender);
            tray2.Enqueue(Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionTrayDrop.Plan(
                new[] { Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionTrayCandidateFactory.From(row2)!.Value }, true));
            tray2.Phrase = Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray.ActionTrayViewModel.EtcPhrase;
            tray2.Memo = new string('가', 501);
            boot.Wire.CurrentTag = TAG + "/long-memo";
            mark = rec.Wire.Count;
            await tray2.ApplyAsync().ConfigureAwait(false);
            var post2 = rec.Wire.Skip(mark).FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/events/actions"));
            if (post2 is not null && post2.Status is 200 or 201) TrackActionFromWire(fx, post2);
            var blocked = post2 is null && !tray2.CanApply && tray2.ApplyBlockedReason.Contains("500");
            rec.Add("E7b", TAG, "트레이 '기타' 메모 501자 → 보내지 않고 500자 한도를 알린다(서버 content max_length=500 → 422)",
                blocked ? Verdict.PASS : Verdict.FAIL,
                post2 is null
                    ? $"no POST; CanApply={tray2.CanApply}; reason='{tray2.ApplyBlockedReason}'"
                    : $"POST went out len={tray2.Memo.Length} -> HTTP {post2.Status} {Recorder.Trunc(post2.ResponseBodyRedacted, 200)}; tray='{tray2.StatusLine}'",
                defectAt: blocked ? "" : "Events.Ui/Consoles/Tray/ActionTrayViewModel.cs CanApply/ApplyBlockedReason (길이 미검증) · Views/Consoles/ActionTrayView.xaml Memo MaxLength 없음");
        }
        catch (Exception ex)
        {
            rec.Add("E7!", TAG, "tray round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await fx.CleanupAsync().ConfigureAwait(false);
        }
    }

    // ───────────────────────────── E9b 무한 스크롤 페이지 겹침 ─────────────────────────────

    /// <summary>
    /// E9b — 목록은 offset 페이지(page · limit)로 한 쪽씩 붙인다. 두 쪽 사이에 새 이벤트가 생기면 서버 정렬(최신 먼저)이
    /// 한 칸 밀려 다음 쪽 첫 행이 앞 쪽 마지막 행과 같다 — 같은 Id 가 두 번 붙는다. 제품 경로(EventProviderService 페이지 조회)로
    /// 겹침이 실제로 오는지 본다(패널의 붙이기 중복 제거는 단위 테스트가 본다 — 패널은 한 쪽 100건 고정이라 여기서 재현이 비싸다).
    /// </summary>
    [ExtraStep("events-vm", 80)]
    static async Task EventsVm_E9_PageOverlap(ExtraContext ctx)
    {
        const string TAG = "E9";
        var fx = new EvtFixture(ctx.Raw, ctx.Rec);
        var boot = ctx.Boot; var rec = ctx.Rec;
        try
        {
            var (_, sensor) = await ArrangeSensor(fx, "E9", 97900).ConfigureAwait(false);
            for (var i = 0; i < 3; i++)
                await fx.Event("events/detections", new { type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR" }).ConfigureAwait(false);
            var provider = new EvProvider(boot.Log, new EventApiService(boot.Log, boot.Api, boot.Setup, boot.Probe));
            var start = DateTime.Now.AddHours(-2);
            var end = DateTime.Now.AddHours(2);
            var p1 = await provider.FetchDetectionEventsPageAsync(start, end, 1, 2).ConfigureAwait(false);
            await fx.Event("events/detections", new { type_event = "Intrusion", device_id = sensor, result = "PIR_SENSOR" }).ConfigureAwait(false);   // 두 쪽 사이의 새 이벤트
            var p2 = await provider.FetchDetectionEventsPageAsync(start, end, 2, 2).ConfigureAwait(false);
            var a = (p1.Items ?? new List<IDetectionEventModel>()).Select(m => m.Id).ToList();
            var b = (p2.Items ?? new List<IDetectionEventModel>()).Select(m => m.Id).ToList();
            var overlap = a.Intersect(b).ToList();
            rec.Add("E9b", TAG, "쪽 사이에 새 이벤트가 생기면 다음 쪽이 앞 쪽의 Id 를 다시 준다(붙이기에 중복 제거가 필요한 근거)",
                Verdict.INFO,
                $"page1 ids=[{string.Join(",", a)}]; page2 ids=[{string.Join(",", b)}]; overlap=[{string.Join(",", overlap)}] — {(overlap.Count > 0 ? "재현됨: 패널이 그대로 붙이면 같은 행이 두 번 보인다" : "이번 회차엔 겹치지 않았다")}");
        }
        catch (Exception ex)
        {
            rec.Add("E9!", TAG, "page overlap probe", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await fx.CleanupAsync().ConfigureAwait(false);
        }
    }

    static void TrackActionFromWire(EvtFixture fx, WireExchange w)
    {
        try
        {
            var id = (int?)JObject.Parse(w.ResponseBody)["data"]?["id"];
            if (id is > 0) fx.TrackAction(id.Value);
        }
        catch { /* 응답이 JSON 이 아니면 만든 것이 없다 */ }
    }

    /// <summary>조치보고 창을 제품 순서대로 연다: 활성화 → UpdateData(카드, 계정) → (기타 선택 · 메모) → ClickOk.</summary>
    static async Task<(string Outcome, List<WireExchange> Wire)> DriveDetectionReportDialog(
        Bootstrap b, Recorder rec, IDetectionEventModel model, IAccountModel account, bool etc, string? memo, string tag)
    {
        var ea = new EventAggregator();
        var sink = new DialogSink();
        ea.SubscribeOnPublishedThread(sink);
        var card = new DetectionEventCardViewModel(ea, b.Log, model);
        var dlg = new DetectionReportDialogViewModel(ea, b.Log);
        await ((IActivate)dlg).ActivateAsync().ConfigureAwait(false);
        dlg.UpdateData(card, account);
        if (etc && dlg.EtcViewModel is not null) dlg.EtcViewModel.IsSelected = true;
        if (memo is not null) dlg.Memo = memo;

        b.Wire.CurrentTag = tag;
        var mark = rec.Wire.Count;   // 두 Bootstrap 의 Seq 는 따로 센다 — 번호가 아니라 큐 위치로 자른다
        dlg.ClickOk();
        var outcome = await WaitFirst(sink).ConfigureAwait(false);
        await Task.Delay(200).ConfigureAwait(false);   // 알림 뒤 따라오는 와이어 기록을 모은다(ClickOk 는 async void)
        return (outcome, rec.Wire.Skip(mark).ToList());
    }
}
