// 서버 콘솔 부대 보존 (D-13 과 같은 결함 계열) — [ExtraStep("server-console")] 로 RunAll 이 스윕 직전에 부른다.
//
// 증명하려는 것: 다른 부대(B) 소속 서버를 서버 콘솔에서 고치거나, B 소속 장비를 서버에 배정·되돌리기 해도
// 그 서버·장비가 이 클라이언트의 부대로 조용히 옮겨지지 않는다.
//   SC-a  ServerConsoleService.SaveAsync(임계 변경)         → PATCH /servers/{id}
//   SC-b  ServerAssignHandler.AssignAsync(제어기 → PROXY)   → PATCH /devices/controllers/{id}
//   SC-c  ServerAssignHandler.UndoAsync                    → PATCH /devices/controllers/{id} (server_id null)
//   SC-d  음성 대조: ServerConsoleService.CreateAsync(부대 미지정) 는 여전히 자기 부대로 찍힌다.
// 판정은 매번 두 곳에서 한다 — 실제로 나간 본문(와이어) 과 서버 재조회(re-GET).
//
// 이름 접두는 LRT-SRV- / lrtsrv (여러 작업자가 같은 테스트 서버를 동시에 쓴다).
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Newtonsoft.Json.Linq;
using MonModels = Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    [ExtraStep("server-console", 20)]
    static async Task ServerConsoleUnitPreservation(ExtraContext ctx)
    {
        const string TAG = "SRV-UNIT";
        var boot = ctx.Boot;
        var rec = ctx.Rec;
        var raw = ctx.Raw;
        int unitBId = 0, serverId = 0, ctrlId = 0, negServerId = 0;

        try
        {
            if (ctx.Policy.Contract < EnumServerContract.V8_0)
            {
                rec.Add("SC.0", TAG, "arrange: 부대 편제(8.0 이상) 판본", Verdict.BLOCKED,
                    $"contract={ctx.Policy.Contract}", blocked: "unit_id 가 없는 판본이라 이 결함 계열이 성립하지 않는다");
                return;
            }

            // ---------- arrange: 두 번째 부대 B ----------
            boot.Wire.CurrentTag = TAG + "/unit-b";
            var unitBRes = await ctx.UnitApi.CreateUnitAsync(new UnitCreateDto
            {
                Name = "LRT-SRV-UNIT-B",
                Code = "lrtsrvunitb",
                IsEnable = true,
            }).ConfigureAwait(false);
            if (!unitBRes.Success || unitBRes.Data == null)
            {
                rec.Add("SC.0", TAG, "arrange: 두 번째 부대(B)", Verdict.BLOCKED,
                    $"success={unitBRes.Success} msg='{unitBRes.Message}'", blocked: "fixture setup failed");
                return;
            }
            unitBId = unitBRes.Data.Id;
            rec.Created("unit", unitBId);

            var ownUnitId = await UnitScope.ResolveAsync().ConfigureAwait(false);
            if (ownUnitId == null || ownUnitId == unitBId)
            {
                rec.Add("SC.0b", TAG, "arrange: 자기 부대가 해석되고 B 와 다르다", Verdict.BLOCKED,
                    $"own={ownUnitId} B={unitBId}", blocked: "자기 부대를 못 풀었거나 B 와 같다 — 시험이 공허해진다");
                return;
            }

            // ---------- arrange: 실제 콘솔이 쓰는 통로 ----------
            var axis = new ServerAxisApiService(boot.Api, boot.Setup, boot.Probe, boot.Log);
            var legacy = new ServerApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var service = new ServerConsoleService(
                axis, legacy, ctx.Policy,
                new Lazy<IUnitApiService>(() => ctx.UnitApi),
                new Lazy<IUnitScopeService>(() => UnitScope),
                boot.Log);

            boot.Wire.CurrentTag = TAG + "/categories";
            var categories = await axis.GetCategoriesAsync().ConfigureAwait(false);
            var proxy = categories.FirstOrDefault(c => string.Equals(c.TypeServer, "PROXY", StringComparison.OrdinalIgnoreCase));
            if (proxy == null)
            {
                rec.Add("SC.0c", TAG, "arrange: PROXY 분류", Verdict.BLOCKED,
                    $"categories=[{string.Join(",", categories.Select(c => c.TypeServer))}]", blocked: "PROXY 분류가 없다");
                return;
            }

            // ---------- arrange: B 소속 PROXY 서버 (raw — 준비만) ----------
            boot.Wire.CurrentTag = TAG + "/seed";
            var (ss, sj) = await raw.Post("servers", new
            {
                category_server = "PROXY",
                name = "LRT-SRV-PROXY-B",
                connection = new { ip_address = "10.88.1.1", ip_port = 8301 },
                server_config = new { thresholds = new { cpu = new { warning = 70, critical = 90 } } },
                unit_id = unitBId,
            }).ConfigureAwait(false);
            if (ss != 201 && ss != 200)
            {
                rec.Add("SC.1", TAG, "arrange: B 소속 PROXY 서버", Verdict.BLOCKED, $"HTTP {ss} {Short(sj)}", blocked: "fixture setup failed");
                return;
            }
            serverId = (int)sj["data"]["id"];
            rec.Created("server", serverId);
            if ((int?)Obj(sj["data"])?["unit_id"] != unitBId)
            {
                rec.Add("SC.1b", TAG, "arrange sanity: 서버가 B 에 들어갔다", Verdict.BLOCKED,
                    $"unit_id={Obj(sj["data"])?["unit_id"]} expected={unitBId}", blocked: "seed 가 B 에 안 들어갔다");
                return;
            }

            // ---------- arrange: B 소속 제어기 (raw — 준비만) ----------
            var (cs, cj) = await raw.Post("devices/controllers", new
            {
                type_controller = "Controller",
                number_device = 96810,
                name_device = "LRT-SRV-CTRL-B",
                connection = new { ip_address = "10.88.1.2", ip_port = 9681 },
                unit_id = unitBId,
            }).ConfigureAwait(false);
            if (cs != 201)
            {
                rec.Add("SC.2", TAG, "arrange: B 소속 제어기", Verdict.BLOCKED, $"HTTP {cs} {Short(cj)}", blocked: "fixture setup failed");
                return;
            }
            ctrlId = (int)cj["data"]["id"];
            rec.Created("controller", ctrlId);
            if ((int?)Obj(cj["data"])?["unit_id"] != unitBId)
            {
                rec.Add("SC.2b", TAG, "arrange sanity: 제어기가 B 에 들어갔다", Verdict.BLOCKED,
                    $"unit_id={Obj(cj["data"])?["unit_id"]} expected={unitBId}", blocked: "seed 가 B 에 안 들어갔다");
                return;
            }

            // ---------- SC-a: 서버 콘솔에서 임계만 고친다 ----------
            boot.Wire.CurrentTag = TAG + "/save";
            var before = rec.Wire.Count;
            var intent = new ServerWriteIntent { CpuWarning = 61, CpuCritical = 91 };   // 화면의 임계 칸 두 개
            var saved = await service.SaveAsync(serverId, intent).ConfigureAwait(false);
            var saveWire = SrvWireSince(rec, before).FirstOrDefault(w => w.Method == "PATCH" && w.Uri.Contains($"/servers/{serverId}"));
            var (_, sAfter) = await raw.Get($"servers/{serverId}").ConfigureAwait(false);
            var sData = Obj(sAfter["data"]);
            var sUnitAfter = (int?)sData?["unit_id"];
            var cpuWarnAfter = (double?)sData?.SelectToken("server_config.thresholds.cpu.warning");
            var saveBodyUnit = BodyUnit(saveWire?.RequestBody);
            var saveOk = saved.IsSuccess && saveWire != null
                         && (saveBodyUnit.Absent || saveBodyUnit.Value == unitBId)
                         && sUnitAfter == unitBId && cpuWarnAfter == 61;
            rec.Add("SC-a", TAG,
                "다른 부대(B) 서버의 임계만 서버 콘솔(ServerConsoleService.SaveAsync)로 고쳐도 소속이 B 로 남는다",
                saveOk ? Verdict.PASS : Verdict.FAIL,
                $"save success={saved.IsSuccess} msg='{saved.Message}'; own unit={ownUnitId} B={unitBId}; " +
                $"PATCH body unit_id={(saveBodyUnit.Absent ? "(absent)" : saveBodyUnit.Value?.ToString())}; " +
                $"re-GET unit_id={sUnitAfter} cpu.warning={cpuWarnAfter}; " +
                $"PATCH body = {(saveWire == null ? "(none)" : Recorder.Trunc(saveWire.RequestBodyRedacted, 400))}",
                defectAt: saveOk ? "" : "Devices.Ui/Consoles/Servers/ServerConsoleService.cs SaveAsync → StampUnitAsync (own unit stamped on edit)",
                seqs: SrvSeqs(rec, before));

            // ---------- SC-b: B 소속 제어기를 B 소속 PROXY 서버에 배정 ----------
            boot.Wire.CurrentTag = TAG + "/assign";
            var view = await service.GetAsync(serverId).ConfigureAwait(false);
            if (view == null)
            {
                rec.Add("SC-b", TAG, "arrange: 서버 행(ServerRowViewModel)", Verdict.BLOCKED, "GetAsync null", blocked: "fixture read failed");
                return;
            }
            var row = new ServerRowViewModel(view, service.Contract, new SystemClock());
            var device = (MonModels.IBaseDeviceModel)new MonModels.ControllerDeviceModel
            {
                Id = ctrlId,
                CategoryDevice = EnumDeviceCategory.Controller,
                DeviceType = EnumDeviceType.Controller,
                DeviceName = "LRT-SRV-CTRL-B",
                UnitId = unitBId,
            };
            var models = new List<MonModels.IBaseDeviceModel> { device };
            var handler = new ServerAssignHandler(service, () => models, new DraftTrayViewModel(), null, boot.Log);
            ServerAssignUndo? undo = null;
            handler.Completed += r => { if (r.Undo != null) undo = r.Undo; };

            before = rec.Wire.Count;
            var line = await handler.AssignAsync(row, models).ConfigureAwait(false);
            var assignWire = SrvWireSince(rec, before).FirstOrDefault(w => w.Method == "PATCH" && w.Uri.Contains($"/devices/controllers/{ctrlId}"));
            var (_, cAfter) = await raw.Get($"devices/controllers/{ctrlId}").ConfigureAwait(false);
            var cData = Obj(cAfter["data"]);
            var cUnitAfter = (int?)cData?["unit_id"];
            var cServerAfter = (int?)cData?["server_id"];
            var assignBodyUnit = BodyUnit(assignWire?.RequestBody);
            var assignOk = assignWire != null && assignWire.Status is >= 200 and < 300
                           && (assignBodyUnit.Absent || assignBodyUnit.Value == unitBId)
                           && cUnitAfter == unitBId && cServerAfter == serverId;
            rec.Add("SC-b", TAG,
                "B 소속 제어기를 서버에 배정(ServerAssignHandler.AssignAsync)해도 제어기 소속이 B 로 남고 server_id 가 붙는다",
                assignOk ? Verdict.PASS : Verdict.FAIL,
                $"result='{line}'; PATCH body unit_id={(assignBodyUnit.Absent ? "(absent)" : assignBodyUnit.Value?.ToString())}; " +
                $"re-GET unit_id={cUnitAfter} (B={unitBId}, own={ownUnitId}) server_id={cServerAfter} (expected {serverId}); " +
                $"PATCH body = {(assignWire == null ? "(none)" : Recorder.Trunc(assignWire.RequestBodyRedacted, 300))}",
                defectAt: assignOk ? "" : "Devices.Ui/Consoles/Servers/ServerConsoleService.cs AssignDeviceAsync → ServerAxisWriter.BuildDeviceAssign(own unit)",
                seqs: SrvSeqs(rec, before));

            // ---------- SC-c: 되돌리기 → server_id 해제, 소속은 여전히 B ----------
            boot.Wire.CurrentTag = TAG + "/undo";
            before = rec.Wire.Count;
            if (undo == null)
            {
                rec.Add("SC-c", TAG, "되돌리기", Verdict.BLOCKED, "배정이 되돌리기 정보를 돌려주지 않았다", blocked: "SC-b 가 실패해 되돌릴 것이 없다");
            }
            else
            {
                var undoLine = await handler.UndoAsync(undo).ConfigureAwait(false);
                var undoWire = SrvWireSince(rec, before).FirstOrDefault(w => w.Method == "PATCH" && w.Uri.Contains($"/devices/controllers/{ctrlId}"));
                var (_, uAfter) = await raw.Get($"devices/controllers/{ctrlId}").ConfigureAwait(false);
                var uData = Obj(uAfter["data"]);
                var uUnitAfter = (int?)uData?["unit_id"];
                var uServerToken = uData?["server_id"];
                var uServerNull = uServerToken == null || uServerToken.Type == JTokenType.Null;
                var undoBodyUnit = BodyUnit(undoWire?.RequestBody);
                var undoOk = undoWire != null && undoWire.Status is >= 200 and < 300
                             && (undoBodyUnit.Absent || undoBodyUnit.Value == unitBId)
                             && uUnitAfter == unitBId && uServerNull;
                rec.Add("SC-c", TAG,
                    "배정 되돌리기(ServerAssignHandler.UndoAsync)는 server_id 를 해제하고 소속은 B 로 둔다",
                    undoOk ? Verdict.PASS : Verdict.FAIL,
                    $"result='{undoLine}'; PATCH body unit_id={(undoBodyUnit.Absent ? "(absent)" : undoBodyUnit.Value?.ToString())}; " +
                    $"re-GET unit_id={uUnitAfter} server_id={(uServerNull ? "null" : uServerToken!.ToString())}; " +
                    $"PATCH body = {(undoWire == null ? "(none)" : Recorder.Trunc(undoWire.RequestBodyRedacted, 300))}",
                    defectAt: undoOk ? "" : "Devices.Ui/Consoles/Servers/ServerConsoleService.cs AssignDeviceAsync (undo path)",
                    seqs: SrvSeqs(rec, before));
            }

            // ---------- SC-d: 음성 대조 — 새 등록은 여전히 자기 부대로 찍힌다 ----------
            boot.Wire.CurrentTag = TAG + "/create-own-unit";
            before = rec.Wire.Count;
            var option = new ServerCategoryOption(proxy.Id, proxy.Name, ServerTypeCatalog.ParseType(proxy.TypeServer), proxy.TypeServer);
            var (createRes, newId) = await service.CreateAsync(option, new ServerWriteIntent
            {
                Name = "LRT-SRV-PROXY-NEG",
                IpAddress = "10.88.1.3",
                Port = 8303,
            }).ConfigureAwait(false);
            if (createRes.IsSuccess && newId > 0) { negServerId = newId; rec.Created("server", negServerId); }
            var createWire = SrvWireSince(rec, before).FirstOrDefault(w => w.Method == "POST" && w.Uri.EndsWith("/servers"));
            var (_, nAfter) = negServerId > 0 ? await raw.Get($"servers/{negServerId}").ConfigureAwait(false) : (0, new JObject());
            var nUnitAfter = (int?)Obj(nAfter["data"])?["unit_id"];
            var createBodyUnit = BodyUnit(createWire?.RequestBody);
            var negOk = createRes.IsSuccess && !createBodyUnit.Absent && createBodyUnit.Value == ownUnitId && nUnitAfter == ownUnitId;
            rec.Add("SC-d", TAG,
                "음성 대조: 서버 콘솔에서 부대를 고르지 않고 새로 등록하면 이 클라이언트의 부대로 찍힌다(등록 의미 불변)",
                negOk ? Verdict.PASS : Verdict.FAIL,
                $"create success={createRes.IsSuccess} msg='{createRes.Message}'; POST body unit_id={(createBodyUnit.Absent ? "(absent)" : createBodyUnit.Value?.ToString())} " +
                $"expected(own)={ownUnitId}; re-GET unit_id={nUnitAfter}",
                defectAt: negOk ? "" : "Devices.Ui/Consoles/Servers/ServerConsoleService.cs CreateAsync → StampUnitAsync",
                seqs: SrvSeqs(rec, before));
        }
        finally
        {
            if (ctrlId > 0)
            {
                var (s, _) = await raw.Delete($"devices/controllers/{ctrlId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("controller", ctrlId); else rec.Leftover("controller", ctrlId, $"DELETE {s}");
            }
            if (negServerId > 0)
            {
                var (s, _) = await raw.Delete($"servers/{negServerId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("server", negServerId); else rec.Leftover("server", negServerId, $"DELETE {s}");
            }
            if (serverId > 0)
            {
                var (s, _) = await raw.Delete($"servers/{serverId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("server", serverId); else rec.Leftover("server", serverId, $"DELETE {s}");
            }
            if (unitBId > 0)
            {
                var (s, _) = await raw.Delete($"units/{unitBId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("unit", unitBId); else rec.Leftover("unit", unitBId, $"DELETE {s}");
            }
        }
    }

    // 와이어는 번호(Seq)가 아니라 **큐 위치**로 자른다. 다른 확장 점검(events-vm)이 같은 Recorder 큐에 두 번째
    // Bootstrap 을 붙이는데, 핸들러마다 Seq 카운터가 따로라 그 뒤로는 Wire.Count > 주 핸들러 Seq 가 되어
    // rec.LastSeq()/Since() 가 방금 나간 요청을 통째로 놓친다(실측: 전체 실행에서 PATCH body = (none)).
    static List<WireExchange> SrvWireSince(Recorder rec, int mark) => rec.Wire.Skip(mark).ToList();

    static int[] SrvSeqs(Recorder rec, int mark) => SrvWireSince(rec, mark).Select(w => w.Seq).ToArray();

    /// <summary>실제로 나간 본문의 unit_id — 키가 없으면 Absent.</summary>
    static (bool Absent, int? Value) BodyUnit(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return (true, null);
        try
        {
            var t = JObject.Parse(body)["unit_id"];
            if (t == null) return (true, null);
            return (false, t.Type == JTokenType.Integer ? (int)t : null);
        }
        catch { return (true, null); }
    }
}
