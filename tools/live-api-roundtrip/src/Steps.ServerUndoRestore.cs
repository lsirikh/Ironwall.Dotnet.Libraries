// 서버 창 [되돌리기] 이전 서버 복원 (2026-09-27 전수 조사 P1) — [ExtraStep("server-undo")] 로 RunAll 이 스윕 직전에 부른다.
//
// 결함: ServerAssignHandler 가 이전 서버를 모델에서만 읽었다(스피커만 서버 축이 있다). 카메라 · 제어기의 이전 서버는
//       늘 "없음" 이었고, 7.0+ 에서 되돌리기가 server_id:null 을 보내 실제 배정(NVR-A)을 지웠다. 칩도 "서버 없음" 이었다.
// 증명: 서버 A 에 붙은 카메라를 실제 서버 콘솔 뷰모델(ServerMonitorViewModel)로 서버 B 에 배정하고 [되돌리기] 하면
//       카메라가 A 로 돌아간다. 판정은 실제로 나간 본문(와이어)과 서버 재조회(re-GET) 두 곳에서 한다.
//   SU-a  칩: 적재 직후 후보 칩이 카메라의 실제 서버(A) 이름을 보인다(예전: "서버 없음")
//   SU-b  배정: PATCH /devices/cameras/{id} {"server_id":B} → re-GET server_id == B
//   SU-c  되돌리기: PATCH /devices/cameras/{id} {"server_id":A} → re-GET server_id == A (예전: null)
//
// 이름 접두는 LRT-UNDO- (여러 작업자가 같은 테스트 서버를 동시에 쓴다). 만든 것은 finally 에서 지운다.
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json.Linq;
using MonModels = Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    [ExtraStep("server-undo", 30)]
    static async Task ServerUndoRestoresPreviousServer(ExtraContext ctx)
    {
        const string TAG = "SRV-UNDO";
        var boot = ctx.Boot;
        var rec = ctx.Rec;
        var raw = ctx.Raw;
        int serverA = 0, serverB = 0, cameraId = 0;
        const string nameA = "LRT-UNDO-NVR-A";
        const string nameB = "LRT-UNDO-NVR-B";

        try
        {
            if (ctx.Policy.Contract < EnumServerContract.V7_0)
            {
                rec.Add("SU.0", TAG, "arrange: 축 계약(7.0 이상) 판본", Verdict.BLOCKED,
                    $"contract={ctx.Policy.Contract}", blocked: "6.3 은 카메라를 서버에 배정할 입구가 없다");
                return;
            }

            // ---------- arrange: NVR 서버 A · B, A 에 붙은 카메라 (raw — 준비만) ----------
            boot.Wire.CurrentTag = TAG + "/seed";
            foreach (var (name, port) in new[] { (nameA, 8411), (nameB, 8412) })
            {
                var (ss, sj) = await raw.Post("servers", new
                {
                    category_server = "NVR_API",
                    name,
                    connection = new { ip_address = "10.88.4.1", ip_port = port },
                }).ConfigureAwait(false);
                if (ss != 201 && ss != 200)
                {
                    rec.Add("SU.1", TAG, $"arrange: NVR 서버 {name}", Verdict.BLOCKED, $"HTTP {ss} {Recorder.Trunc(sj?.ToString() ?? "", 300)}", blocked: "fixture setup failed");
                    return;
                }
                var id = (int)sj["data"]!["id"]!;
                rec.Created("server", id);
                if (name == nameA) serverA = id; else serverB = id;
            }

            var (cs, cj) = await raw.Post("devices/cameras", new JObject
            {
                ["type_camera"] = "FIXED",
                ["number_device"] = 98741,
                ["name_device"] = "LRT-UNDO-CAM",
                ["server_id"] = serverA,
                ["connection"] = new JObject { ["ip_address"] = "10.88.4.2", ["ip_port"] = 80, ["protocol"] = "NONE" },
            }).ConfigureAwait(false);
            if (cs != 201)
            {
                rec.Add("SU.2", TAG, "arrange: 서버 A 에 붙은 카메라", Verdict.BLOCKED, $"HTTP {cs} {Recorder.Trunc(cj?.ToString() ?? "", 300)}", blocked: "fixture setup failed");
                return;
            }
            cameraId = (int)cj["data"]!["id"]!;
            rec.Created("camera", cameraId);
            if ((int?)(cj["data"] as JObject)?["server_id"] != serverA)
            {
                rec.Add("SU.2b", TAG, "arrange sanity: 카메라가 서버 A 에 붙었다", Verdict.BLOCKED,
                    $"server_id={(cj["data"] as JObject)?["server_id"]} expected={serverA}", blocked: "seed 가 A 에 안 붙었다");
                return;
            }

            // ---------- 실제 콘솔: 통로 · 뷰모델 ----------
            var axis = new ServerAxisApiService(boot.Api, boot.Setup, boot.Probe, boot.Log);
            var legacy = new ServerApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var service = new ServerConsoleService(axis, legacy, ctx.Policy,
                new Lazy<IUnitApiService>(() => ctx.UnitApi), new Lazy<IUnitScopeService>(() => UnitScope), boot.Log);

            // 화면이 쥔 카메라 모델 — 서버 축이 없다(결함의 전제 그대로).
            var devices = new DeviceProvider();
            devices.CollectionEntity.Add(new MonModels.CameraDeviceModel
            {
                Id = cameraId,
                CategoryDevice = EnumDeviceCategory.Camera,
                DeviceName = "LRT-UNDO-CAM",
            });
            var vm = new ServerMonitorViewModel(new EventAggregator(), boot.Log, service, devices, new SystemClock());

            boot.Wire.CurrentTag = TAG + "/open";
            await ((IActivate)vm).ActivateAsync().ConfigureAwait(false);
            var chip = vm.AssignCandidates.FirstOrDefault(c => c.Id == cameraId);
            var chipOk = chip?.ServerText == nameA;
            rec.Add("SU-a", TAG,
                "서버 창을 열면 카메라 후보 칩이 서버가 말하는 실제 서버(A) 이름을 보인다",
                chipOk ? Verdict.PASS : Verdict.FAIL,
                $"chip ServerText='{chip?.ServerText}' expected='{nameA}' (예전: '서버 없음'); candidates={vm.AssignCandidates.Count}",
                defectAt: chipOk ? "" : "Devices.Ui/Consoles/Servers/ServerMonitorViewModel.cs ServerNameOf ← ServerAssignHandler.RefreshServerMapAsync");

            var rowB = vm.Rows.FirstOrDefault(r => r.Id == serverB);
            if (rowB == null || chip == null)
            {
                rec.Add("SU-b", TAG, "arrange: 서버 B 행 · 카메라 칩", Verdict.BLOCKED,
                    $"rowB={(rowB == null ? "없음" : "있음")} chip={(chip == null ? "없음" : "있음")} rows={vm.Rows.Count}", blocked: "목록에 준비물이 보이지 않는다");
                return;
            }

            // ---------- SU-b: B 에 배정 ----------
            boot.Wire.CurrentTag = TAG + "/assign";
            var before = rec.Wire.Count;
            vm.OnRowsSelected(new List<object> { rowB });
            await vm.AssignSelectionAsync(new[] { chip }).ConfigureAwait(false);
            var assignWire = SrvWireSince(rec, before).FirstOrDefault(w => w.Method == "PATCH" && w.Uri.Contains($"/devices/cameras/{cameraId}"));
            var (_, aAfter) = await raw.Get($"devices/cameras/{cameraId}").ConfigureAwait(false);
            var serverAfterAssign = (int?)(aAfter["data"] as JObject)?["server_id"];
            var assignOk = assignWire != null && assignWire.Status is >= 200 and < 300 && serverAfterAssign == serverB && vm.CanUndoAssign;
            rec.Add("SU-b", TAG,
                "서버 창에서 카메라를 서버 B 에 배정하면 server_id 가 B 가 되고 [되돌리기]가 켜진다",
                assignOk ? Verdict.PASS : Verdict.FAIL,
                $"status='{vm.StatusText}'; re-GET server_id={serverAfterAssign} (B={serverB}); CanUndo={vm.CanUndoAssign}; " +
                $"PATCH body = {(assignWire == null ? "(none)" : Recorder.Trunc(assignWire.RequestBodyRedacted, 200))}",
                defectAt: assignOk ? "" : "Devices.Ui/Consoles/Servers/ServerAssignHandler.cs AssignAsync",
                seqs: SrvSeqs(rec, before));

            // ---------- SU-c: 되돌리기 → A 로 돌아간다 ----------
            boot.Wire.CurrentTag = TAG + "/undo";
            before = rec.Wire.Count;
            await vm.UndoAssignAsync().ConfigureAwait(false);
            var undoWire = SrvWireSince(rec, before).FirstOrDefault(w => w.Method == "PATCH" && w.Uri.Contains($"/devices/cameras/{cameraId}"));
            var (_, uAfter) = await raw.Get($"devices/cameras/{cameraId}").ConfigureAwait(false);
            var uToken = (uAfter["data"] as JObject)?["server_id"];
            var serverAfterUndo = uToken == null || uToken.Type == JTokenType.Null ? (int?)null : (int)uToken;
            var undoBody = undoWire?.RequestBody;
            var bodyServer = string.IsNullOrWhiteSpace(undoBody) ? null : JObject.Parse(undoBody)["server_id"];
            var undoOk = undoWire != null && undoWire.Status is >= 200 and < 300
                         && bodyServer?.Type == JTokenType.Integer && (int)bodyServer == serverA
                         && serverAfterUndo == serverA;
            rec.Add("SU-c", TAG,
                "[되돌리기]는 카메라를 이전 서버(A)로 되돌린다 — server_id 를 지우지 않는다",
                undoOk ? Verdict.PASS : Verdict.FAIL,
                $"status='{vm.StatusText}'; PATCH body server_id={(bodyServer == null ? "(absent)" : bodyServer.ToString(Newtonsoft.Json.Formatting.None))} expected={serverA}; " +
                $"re-GET server_id={(serverAfterUndo?.ToString() ?? "null")} expected={serverA} (예전: null)",
                defectAt: undoOk ? "" : "Devices.Ui/Consoles/Servers/ServerAssignHandler.cs PreviousAsync / UndoAsync",
                seqs: SrvSeqs(rec, before));

            await ((IDeactivate)vm).DeactivateAsync(true).ConfigureAwait(false);
        }
        finally
        {
            if (cameraId > 0)
            {
                var (s, _) = await raw.Delete($"devices/cameras/{cameraId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("camera", cameraId); else rec.Leftover("camera", cameraId, $"DELETE {s}");
            }
            foreach (var id in new[] { serverB, serverA })
            {
                if (id <= 0) continue;
                var (s, _) = await raw.Delete($"servers/{id}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("server", id); else rec.Leftover("server", id, $"DELETE {s}");
            }
        }
    }
}
