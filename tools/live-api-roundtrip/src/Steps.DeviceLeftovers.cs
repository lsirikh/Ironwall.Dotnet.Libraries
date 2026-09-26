// 장비 남은 결함 왕복 점검 (device-leftovers) — U-15 후속. 코드 읽기로 찾은 후보를 제품 경로로 실서버에 태워
// 재현되는 것만 고친다. 판정은 언제나 서버를 다시 읽어서(raw re-GET) 한다.
//
//   DL2  그룹 등록이 unit_id 를 싣지 않아 기본 부대로 귀속 → 운영자 부대 장비를 그 그룹에 넣으면 거부되는가
//   DL3  RS485 경로가 저장된 connection.type 을 덮는가(접점 결선 센서의 이름만 고친 저장)
//   DL4  함체 패널의 무변경 저장이 설정 없는 팬에 enabled:false 를 지어내는가
//
// 모든 생성물은 LRT-DL- 접두(부대 코드 lrtdl…)다. 다른 작업자(accounts-left)와 동시에 돈다 — 자기 것만 지운다.
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Newtonsoft.Json.Linq;
using MonModels = Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    const string DL = "device-leftovers";

    // ================= DL2: 그룹 등록의 소속 부대 =================
    [ExtraStep(DL, 10)]
    static async Task Dl2_GroupCreateUnit(ExtraContext ctx)
    {
        var fx = new DlFixture(ctx);
        var previousIoC = IoC.GetInstance;
        try
        {
            ctx.Rec.Add("dl.2i", DL, "server contract seen by the product probe", Verdict.INFO, $"probe contract={ctx.Boot.Probe.Contract}");

            // ---- arrange: 운영자 부대 = 기본 부대(unit001)가 아닌 다른 루트 부대 ----
            ctx.Boot.Wire.CurrentTag = "dl/2-arrange";
            var unit = await fx.Unit("LRT-DL-UNIT-OP", "lrtdlunitop");
            if (unit <= 0) return;
            var scope = new UnitScopeService(ctx.UnitApi, new NatsSetupModel { GroupNats = "lrtdlunitop" }, ctx.Boot.Probe, ctx.Boot.Log);
            IoC.GetInstance = (type, key) => type == typeof(IUnitScopeService) ? scope : previousIoC(type, key);
            var resolved = await scope.ResolveAsync().ConfigureAwait(false);
            if (resolved != unit) { fx.Blocked("dl.2", $"operator scope resolved {resolved}, expected {unit}"); return; }

            var ctrl = await fx.Controller(98200, "LRT-DL-2-CTRL", unitId: unit);
            var sensor = await fx.Sensor(ctrl, 98201, "LRT-DL-2-S1", unitId: unit);
            if (sensor <= 0) return;

            // ---- act 1: 패널의 등록 경로(DeviceGroupPanelViewModel.CreateDeviceGroupLite 와 같은 사슬) ----
            ctx.Boot.Wire.CurrentTag = "dl/2-create";
            var before = ctx.Rec.LastSeq();
            var draft = new MonModels.DeviceGroupModel { Name = "LRT-DL-2-GRP", Description = "live roundtrip" };
            // 패널의 CreateDeviceGroupLite 가 쓰는 사슬 그대로(수정 전 RED 는 draft.ToDeviceGroupDto() 한 줄이었다 — unit_id 없음).
            var body = await DeviceGroupWriteRequest.ForCreateAsync(draft, ctx.Boot.Log).ConfigureAwait(false);
            var created = await ctx.DeviceApi.CreateDeviceGroupAsync(body).ConfigureAwait(false);
            var postWire = ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "POST" && w.Uri.Contains("/devices/groups"));
            var groupId = created.Data?.Id ?? 0;
            if (groupId > 0) fx.Track("device-group", groupId);
            else groupId = await fx.FindGroupByName("LRT-DL-2-GRP");
            var (_, gj) = groupId > 0 ? await ctx.Raw.Get($"devices/groups/{groupId}").ConfigureAwait(false) : (0, new JObject());
            var groupUnit = (int?)Obj(gj["data"])?["unit_id"];

            // ---- act 2: 운영자 부대의 센서를 그 그룹에 끌어 넣는다(DeviceGroupDropHandler — 장비 콘솔의 끌어 놓기) ----
            ctx.Boot.Wire.CurrentTag = "dl/2-assign";
            var assignSeq = ctx.Rec.LastSeq();
            var model = (await ctx.DeviceApi.GetSensorByIdAsync(sensor).ConfigureAwait(false)).Data!.ToSensorDeviceModel();
            var models = new List<MonModels.IBaseDeviceModel> { model };
            var drop = new DeviceGroupDropHandler(ctx.DeviceApi, () => models, ctx.Boot.Log);
            var line = groupId > 0 ? await drop.AssignAsync(groupId, "LRT-DL-2-GRP", models).ConfigureAwait(false) : "(no group)";
            var assignWire = ctx.Rec.Since(assignSeq).FirstOrDefault(w => w.Method == "POST" && w.Uri.Contains("/devices"));
            var members = groupId > 0 ? await GroupMemberIds(ctx.Raw, groupId).ConfigureAwait(false) : new List<int>();

            var ok = created.Success && groupUnit == unit && members.Contains(sensor);
            ctx.Rec.Add("dl.2", DL,
                "운영자 부대(기본 부대 아님)에서 그룹을 만들면 그 부대에 귀속되고, 운영자 부대의 센서를 끌어 넣을 수 있다",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"operator unit={unit}; POST body={postWire?.RequestBodyRedacted ?? "(none)"} status={postWire?.Status}; server group unit_id={groupUnit}; " +
                $"drop='{line}'; assign status={assignWire?.Status} response={Recorder.Trunc(assignWire?.ResponseBodyRedacted ?? "(none)", 260)}; " +
                $"server members=[{Csv(members)}] expected to contain {sensor}",
                defectAt: ok ? "" : "Devices.Ui/ViewModels/Panels/DeviceGroupPanelViewModel.cs CreateDeviceGroupLite + Messages DeviceGroupWriteDto (unit_id 미전송 → 서버 기본 부대 귀속)",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- dl.2c: 서버가 넣기를 거부하면 끌어 놓기 결과 줄에 그 까닭이 보인다 ----
            // 기본 부대 그룹(다른 창 · 예전 판이 만든 것)에 이 부대 센서를 끌어 넣으면 서버가 422 로 거부한다 — 그 까닭이 사람에게 닿는가.
            ctx.Boot.Wire.CurrentTag = "dl/2c-refused";
            var (rs, rj) = await ctx.Raw.Post("devices/groups", new { name = "LRT-DL-2C-DEFAULT-GRP", description = "default unit" }).ConfigureAwait(false);
            var defaultGroup = rs is 200 or 201 ? (int)rj["data"]!["id"]! : 0;
            if (defaultGroup > 0)
            {
                fx.Track("device-group", defaultGroup);
                var refusedSeq = ctx.Rec.LastSeq();
                var refusedLine = await drop.AssignAsync(defaultGroup, "LRT-DL-2C-DEFAULT-GRP", models).ConfigureAwait(false);
                var refusedWire = ctx.Rec.Since(refusedSeq).FirstOrDefault(w => w.Method == "POST");
                var okC = refusedWire?.Status == 422 && refusedLine.Contains("예하 부대");
                ctx.Rec.Add("dl.2c", DL, "서버가 그룹 넣기를 거부(422)하면 끌어 놓기 결과 줄에 서버가 말한 까닭이 보인다",
                    okC ? Verdict.PASS : Verdict.FAIL,
                    $"assign status={refusedWire?.Status}; drop line='{refusedLine}'",
                    defectAt: okC ? "" : "Devices.Ui/Consoles/Groups/DeviceGroupDrop.cs AssignAsync — response.Message(오류 봉투에서는 빈 값)만 적음",
                    seqs: SeqsSince(ctx.Rec, refusedSeq));
            }
            else fx.Blocked("dl.2c", $"arrange default-unit group: HTTP {rs} {Short(rj)}");

            // ---- 음성 대조: 그룹 수정(PUT)은 unit_id 를 싣지 않는다 — 서버가 현재 부대를 유지한다 ----
            if (groupId > 0)
            {
                ctx.Boot.Wire.CurrentTag = "dl/2-update";
                before = ctx.Rec.LastSeq();
                var loaded = (await ctx.DeviceApi.GetDeviceGroupByIdAsync(groupId).ConfigureAwait(false)).Data!.ToDeviceGroupModel();
                loaded.Description = "after edit";
                var updated = await ctx.DeviceApi.UpdateDeviceGroupAsync(loaded.Id, loaded.ToDeviceGroupDto()).ConfigureAwait(false);
                var putWire = ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "PUT" || w.Method == "PATCH");
                var (_, uj) = await ctx.Raw.Get($"devices/groups/{groupId}").ConfigureAwait(false);
                var unitAfter = (int?)Obj(uj["data"])?["unit_id"];
                var sentUnit = AsmParse(putWire?.RequestBody)?["unit_id"];
                var ok2 = updated.Success && sentUnit == null && unitAfter == groupUnit;
                ctx.Rec.Add("dl.2b", DL, "음성 대조: 그룹 수정(PUT)은 unit_id 를 싣지 않고 서버가 그룹의 부대를 유지한다",
                    ok2 ? Verdict.PASS : Verdict.FAIL,
                    $"success={updated.Success}; {putWire?.Method} body={putWire?.RequestBodyRedacted ?? "(none)"}; unit before={groupUnit} after={unitAfter}",
                    defectAt: ok2 ? "" : "Devices.Api/Services/DeviceApiService.cs UpdateDeviceGroupAsync / DeviceGroupWriteDto.From",
                    seqs: SeqsSince(ctx.Rec, before));
            }
        }
        catch (Exception ex) { fx.Blocked("dl.2!", ex.ToString()); }
        finally
        {
            IoC.GetInstance = previousIoC;
            await fx.DisposeAsync();
        }
    }

    // ================= DL3: 센서 connection.type 보존 =================
    [ExtraStep(DL, 11)]
    static async Task Dl3_SensorConnectionType(ExtraContext ctx)
    {
        var fx = new DlFixture(ctx);
        try
        {
            var ctrl = await fx.Controller(98300, "LRT-DL-3-CTRL");
            var contact = await fx.Sensor(ctrl, 98301, "LRT-DL-3-CONTACT", connection: new { type = "CONTROLLER_CONTACT", channel = 2 });
            var bus = await fx.Sensor(ctrl, 98302, "LRT-DL-3-RS485", connection: new { type = "RS485", channel = 5 });
            if (contact <= 0 || bus <= 0) return;

            foreach (var (id, seeded, tag) in new[] { (contact, "CONTROLLER_CONTACT", "dl.3"), (bus, "RS485", "dl.3b") })
            {
                ctx.Boot.Wire.CurrentTag = "dl/3-" + seeded;
                var (_, sj) = await ctx.Raw.Get($"devices/sensors/{id}").ConfigureAwait(false);
                var seededType = Str(Obj(Obj(sj["data"])?["connection"])?["type"]);
                var before = ctx.Rec.LastSeq();
                // 제품 경로: Dto → Model → 이름만 고침 → Model → Dto → UnitScopeGate → PATCH (센서 패널 UpdateSensorAsync 와 같은 사슬)
                var model = (await ctx.DeviceApi.GetSensorByIdAsync(id).ConfigureAwait(false)).Data!.ToSensorDeviceModel();
                model.DeviceName += "-EDITED";
                var dto = model.ToSensorDeviceDto();
                await StampUnitAsync(dto, "dl/3", ctx.Boot.Log).ConfigureAwait(false);
                var saveSeq = ctx.Rec.LastSeq();
                var saved = await ctx.DeviceApi.UpdateSensorAsync(id, dto).ConfigureAwait(false);
                var wire = ctx.Rec.Since(saveSeq).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
                var sent = AsmParse(wire?.RequestBody)?["connection"];
                var (_, aj) = await ctx.Raw.Get($"devices/sensors/{id}").ConfigureAwait(false);
                var conn = Obj(Obj(aj["data"])?["connection"]);
                var typeAfter = Str(conn?["type"]);
                var channelAfter = (int?)conn?["channel"];
                var ok = seededType == seeded && saved.Success && typeAfter == seeded && channelAfter == (seeded == "RS485" ? 5 : 2);
                ctx.Rec.Add(tag, DL,
                    seeded == "RS485"
                        ? "음성 대조: RS485 센서의 이름만 고쳐 저장하면 RS485 · 버스 주소가 그대로다"
                        : "접점 결선(CONTROLLER_CONTACT) 센서의 이름만 고쳐 저장해도(Dto→Model→Dto→PATCH) connection.type 이 그대로다",
                    ok ? Verdict.PASS : Verdict.FAIL,
                    $"seeded type={seededType}; save success={saved.Success} msg='{saved.Message}'; wire connection={J(sent)}; server connection after={J(conn)}",
                    defectAt: ok ? "" : "Messages/Dto/Devices/SensorDeviceDto.cs ConnectionAxis getter (Channel 만 있으면 무조건 RS485)",
                    seqs: SeqsSince(ctx.Rec, before));
            }
        }
        catch (Exception ex) { fx.Blocked("dl.3!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= DL3c: 부품 적용(조립 보드)도 저장된 connection.type 을 덮지 않는가 =================
    // ComponentApplyService.PatchAsync 는 모델을 거치지 않고 다시 받은 DTO 에서 새 DTO 를 만든다(CopyCommon) —
    // 모델 경로의 ConnectionTypeHint(028191dd)가 이 사슬에는 없다. 같은 결함 무리라 같은 방식으로 증명한다.
    [ExtraStep(DL, 11)]
    static async Task Dl3c_ComponentApplyConnectionType(ExtraContext ctx)
    {
        var fx = new DlFixture(ctx);
        try
        {
            var ctrl = await fx.Controller(98310, "LRT-DL-3C-CTRL", connectionType: "IP_CONVERTER");
            var contact = await fx.Sensor(ctrl, 98311, "LRT-DL-3C-CONTACT", connection: new { type = "CONTROLLER_CONTACT", channel = 3 });
            if (ctrl <= 0 || contact <= 0) return;
            var apply = new ComponentApplyService(ctx.DeviceApi, new NullDeviceProvider(), ctx.Boot.Log, ctx.Policy);

            foreach (var (kind, id, seeded, tag) in new[] { ("controller", ctrl, "IP_CONVERTER", "dl.3c-ctrl"), ("sensor", contact, "CONTROLLER_CONTACT", "dl.3c-sensor") })
            {
                ctx.Boot.Wire.CurrentTag = "dl/3c-" + kind;
                var before = ctx.Rec.LastSeq();
                MonModels.IBaseDeviceModel model = kind == "controller"
                    ? (await ctx.DeviceApi.GetControllerByIdAsync(id).ConfigureAwait(false)).Data!.ToControllerDeviceModel()
                    : (await ctx.DeviceApi.GetSensorByIdAsync(id).ConfigureAwait(false)).Data!.ToSensorDeviceModel();
                var components = model.Axes?.HardwareSpec?.Components?.ToList() ?? new List<MonModels.ComponentDefinitionModel>();
                var result = await apply.ApplyAsync(model, components, components, null).ConfigureAwait(false);
                var wire = ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
                var sent = AsmParse(wire?.RequestBody)?["connection"];
                var (_, aj) = await ctx.Raw.Get($"devices/{kind}s/{id}").ConfigureAwait(false);
                var conn = Obj(Obj(aj["data"])?["connection"]);
                var ok = result.IsSuccess && Str(conn?["type"]) == seeded;
                ctx.Rec.Add(tag, DL,
                    $"부품 적용(ComponentApplyService.ApplyAsync — 조립 보드 [적용])이 {kind} 의 저장된 connection.type({seeded})을 덮지 않는다",
                    ok ? Verdict.PASS : Verdict.FAIL,
                    $"apply='{result.Message}' success={result.IsSuccess}; PATCH HTTP {wire?.Status}; wire connection={J(sent)}; server connection after={J(conn)}",
                    defectAt: ok ? "" : "Devices.Ui/Consoles/Assembly/Register/ComponentApplyService.cs CopyCommon (저장된 connection.type 을 새 DTO 에 옮기지 않음)",
                    seqs: SeqsSince(ctx.Rec, before));
            }
        }
        catch (Exception ex) { fx.Blocked("dl.3c!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= DL3d: 부대 옮기기도 저장된 connection.type 을 덮지 않는가 =================
    // UnitDeviceApiAdapter.AssignAsync → UnitAssignRequestBuilder.Build 도 다시 받은 DTO 에서 새 DTO 를 만든다.
    [ExtraStep(DL, 11)]
    static async Task Dl3d_UnitAssignConnectionType(ExtraContext ctx)
    {
        var fx = new DlFixture(ctx);
        try
        {
            var target = await fx.Unit("LRT-DL-UNIT-3D", "lrtdlunit3d");
            var ctrl = await fx.Controller(98320, "LRT-DL-3D-CTRL", connectionType: "IP_CONVERTER");
            var contact = await fx.Sensor(ctrl, 98321, "LRT-DL-3D-CONTACT", connection: new { type = "CONTROLLER_CONTACT", channel = 4 });
            if (target <= 0 || ctrl <= 0 || contact <= 0) return;
            var adapter = new UnitDeviceApiAdapter(ctx.DeviceApi, ctx.Boot.Probe, ctx.Boot.Log);

            foreach (var (category, kind, id, seeded, tag) in new[]
                     {
                         (EnumDeviceCategory.Sensor, "sensor", contact, "CONTROLLER_CONTACT", "dl.3d-sensor"),
                         (EnumDeviceCategory.Controller, "controller", ctrl, "IP_CONVERTER", "dl.3d-ctrl"),
                     })
            {
                ctx.Boot.Wire.CurrentTag = "dl/3d-" + kind;
                var before = ctx.Rec.LastSeq();
                var result = await adapter.AssignAsync(new UnitDeviceItem(id, 0, "LRT-DL-3D", category, null), target).ConfigureAwait(false);
                var wire = ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
                var sent = AsmParse(wire?.RequestBody)?["connection"];
                var (_, aj) = await ctx.Raw.Get($"devices/{kind}s/{id}").ConfigureAwait(false);
                var data = Obj(aj["data"]);
                var conn = Obj(data?["connection"]);
                var ok = result.IsSuccess && (int?)data?["unit_id"] == target && Str(conn?["type"]) == seeded;
                ctx.Rec.Add(tag, DL,
                    $"부대 옮기기(UnitDeviceApiAdapter.AssignAsync — 부대 콘솔)가 {kind} 를 옮기면서 저장된 connection.type({seeded})을 덮지 않는다",
                    ok ? Verdict.PASS : Verdict.FAIL,
                    $"assign='{result.Message}'; PATCH HTTP {wire?.Status}; wire connection={J(sent)}; server unit_id={data?["unit_id"]} connection after={J(conn)}",
                    defectAt: ok ? "" : "Devices.Ui/Consoles/Units/Devices/UnitAssignRequestBuilder.cs CopyCommon (저장된 connection.type 을 새 DTO 에 옮기지 않음)",
                    seqs: SeqsSince(ctx.Rec, before));
            }
        }
        catch (Exception ex) { fx.Blocked("dl.3d!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= DL4: 함체 무변경 저장과 설정 없는 부품 =================
    [ExtraStep(DL, 12)]
    static async Task Dl4_EnclosureNoIntent(ExtraContext ctx)
    {
        var fx = new DlFixture(ctx);
        try
        {
            var enc = await fx.Enclosure(98400, "LRT-DL-4-ENC");
            if (enc <= 0) return;
            var (_, ej) = await ctx.Raw.Get($"devices/enclosures/{enc}").ConfigureAwait(false);
            var seededOv = Obj(Obj(Obj(ej["data"])?["device_config"])?["component_overrides"]);

            // ---- (a) 아무것도 고치지 않은 패널 저장(Dto→Model→Dto→PATCH) ----
            ctx.Boot.Wire.CurrentTag = "dl/4-nochange";
            var before = ctx.Rec.LastSeq();
            var model = (await ctx.DeviceApi.GetEnclosureByIdAsync(enc).ConfigureAwait(false)).Data!.ToEnclosureDeviceModel();
            var dto = model.ToEnclosureDeviceDto();
            await StampUnitAsync(dto, "dl/4", ctx.Boot.Log).ConfigureAwait(false);
            var saveSeq = ctx.Rec.LastSeq();
            var saved = await ctx.DeviceApi.UpdateEnclosureAsync(enc, dto).ConfigureAwait(false);
            var wire = ctx.Rec.Since(saveSeq).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
            var sentOv = AsmParse(wire?.RequestBody)?.SelectToken("device_config.component_overrides");
            var (_, aj) = await ctx.Raw.Get($"devices/enclosures/{enc}").ConfigureAwait(false);
            var ov = Obj(Obj(Obj(aj["data"])?["device_config"])?["component_overrides"]);
            var sentFan = sentOv?["fan_1"]?["enabled"];
            var serverFan = ov?["fan_1"]?["enabled"];
            var okA = saved.Success && (bool?)ov?["heater_1"]?["enabled"] == true
                      && (sentFan == null || sentFan.Type == JTokenType.Null)
                      && (serverFan == null || serverFan.Type == JTokenType.Null);
            ctx.Rec.Add("dl.4", DL,
                "설정 없는 팬(fan_1)이 있는 함체를 아무것도 고치지 않고 저장해도 fan_1.enabled 를 지어내지 않고, 히터 설정(true)은 그대로다",
                okA ? Verdict.PASS : Verdict.FAIL,
                $"seeded overrides={J(seededOv)}; model HeaterEnabled={model.HeaterEnabled} FanEnabled={model.FanEnabled}; save success={saved.Success} HTTP {wire?.Status}; " +
                $"sent overrides={J(sentOv)}; server overrides after={J(ov)}",
                defectAt: okA ? "" : "Monitoring.Models/Devices/EnclosureDeviceModel.cs FanEnabled(bool — '설정 없음'을 못 적음) → DtoToModelHelper.ToEnclosureDeviceDto → EnclosureDeviceDto.SetEnabledIfPresent",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- (b) 음성 대조: 사람이 팬을 켜면(행 VM 의 FanEnabled — 속성 폼 · 옛 패널 체크 칸이 쓰는 자리) 그 값이 나간다 ----
            ctx.Boot.Wire.CurrentTag = "dl/4-toggle";
            before = ctx.Rec.LastSeq();
            var model2 = (await ctx.DeviceApi.GetEnclosureByIdAsync(enc).ConfigureAwait(false)).Data!.ToEnclosureDeviceModel();
            var row = new EnclosureDeviceViewModel(model2, ctx.Policy) { FanEnabled = true };
            var dto2 = model2.ToEnclosureDeviceDto();
            await StampUnitAsync(dto2, "dl/4", ctx.Boot.Log).ConfigureAwait(false);
            saveSeq = ctx.Rec.LastSeq();
            var saved2 = await ctx.DeviceApi.UpdateEnclosureAsync(enc, dto2).ConfigureAwait(false);
            var wire2 = ctx.Rec.Since(saveSeq).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
            var (_, bj) = await ctx.Raw.Get($"devices/enclosures/{enc}").ConfigureAwait(false);
            var ov2 = Obj(Obj(Obj(bj["data"])?["device_config"])?["component_overrides"]);
            var okB = saved2.Success && row.FanEnabled && (bool?)ov2?["fan_1"]?["enabled"] == true && (bool?)ov2?["heater_1"]?["enabled"] == true;
            ctx.Rec.Add("dl.4b", DL, "음성 대조: 팬을 켜고 저장하면 fan_1.enabled:true 가 나가 저장되고 히터는 그대로다",
                okB ? Verdict.PASS : Verdict.FAIL,
                $"save success={saved2.Success}; sent overrides={J(AsmParse(wire2?.RequestBody)?.SelectToken("device_config.component_overrides"))}; server overrides after={J(ov2)}",
                defectAt: okB ? "" : "EnclosureDeviceViewModel.FanEnabled → EnclosureDeviceModel → ToEnclosureDeviceDto",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- (c) 음성 대조: 켜 둔 팬을 다시 끄면 enabled:false 가 나간다(꺼짐도 의도다) ----
            ctx.Boot.Wire.CurrentTag = "dl/4-off";
            before = ctx.Rec.LastSeq();
            var model3 = (await ctx.DeviceApi.GetEnclosureByIdAsync(enc).ConfigureAwait(false)).Data!.ToEnclosureDeviceModel();
            var row3 = new EnclosureDeviceViewModel(model3, ctx.Policy) { FanEnabled = false };
            var dto3 = model3.ToEnclosureDeviceDto();
            await StampUnitAsync(dto3, "dl/4", ctx.Boot.Log).ConfigureAwait(false);
            var saved3 = await ctx.DeviceApi.UpdateEnclosureAsync(enc, dto3).ConfigureAwait(false);
            var (_, cj) = await ctx.Raw.Get($"devices/enclosures/{enc}").ConfigureAwait(false);
            var ov3 = Obj(Obj(Obj(cj["data"])?["device_config"])?["component_overrides"]);
            var okC = saved3.Success && !row3.FanEnabled && (bool?)ov3?["fan_1"]?["enabled"] == false;
            ctx.Rec.Add("dl.4c", DL, "음성 대조: 켜진 팬을 끄고 저장하면 fan_1.enabled:false 가 저장된다",
                okC ? Verdict.PASS : Verdict.FAIL,
                $"save success={saved3.Success}; server overrides after={J(ov3)}",
                defectAt: okC ? "" : "EnclosureDeviceDto.DeviceConfigAxis (알려진 false 를 빠뜨림)",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("dl.4!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    /// <summary>한 점검의 준비물 — 만든 것을 기억했다가 장비 → 그룹 → 부대 순으로 지운다.</summary>
    sealed class DlFixture
    {
        readonly ExtraContext _ctx;
        readonly List<(string Kind, string Path, int Id)> _made = new();
        public DlFixture(ExtraContext ctx) { _ctx = ctx; }

        public void Track(string kind, int id)
        {
            var path = kind switch
            {
                "device-group" => "devices/groups",
                "controller" => "devices/controllers",
                "enclosure" => "devices/enclosures",
                "unit" => "units",
                _ => "devices/sensors",
            };
            if (_made.Any(m => m.Kind == kind && m.Id == id)) return;
            _made.Add((kind, path, id));
            _ctx.Rec.Created(kind, id);
        }

        public void Blocked(string id, string why)
            => _ctx.Rec.Add(id, DL, "device-leftovers fixture/harness", Verdict.BLOCKED, why, blocked: "fixture/harness failure - NOT a product defect unless proven");

        public async Task<int> Unit(string name, string code)
        {
            var res = await _ctx.UnitApi.CreateUnitAsync(new UnitCreateDto { Name = name, Code = code, IsEnable = true }).ConfigureAwait(false);
            if (!res.Success || res.Data == null) { Blocked("dl.fx", $"create unit {name}: {res.Message}"); return 0; }
            Track("unit", res.Data.Id);
            return res.Data.Id;
        }

        public async Task<int> FindGroupByName(string name)
        {
            var (s, j) = await _ctx.Raw.Get("devices/groups?page=1&limit=100").ConfigureAwait(false);
            if (s != 200) return 0;
            var hit = (j["data"] as JArray)?.FirstOrDefault(g => Str(g["name"]) == name);
            if (hit == null) return 0;
            var id = (int)hit["id"]!;
            Track("device-group", id);
            return id;
        }

        public async Task<int> Controller(int number, string name, int? unitId = null, string? connectionType = null)
        {
            var connection = new JObject { ["ip_address"] = $"10.78.{number % 250}.1", ["ip_port"] = 9800 };
            if (connectionType != null) connection["type"] = connectionType;
            var body = new JObject
            {
                ["type_controller"] = "Controller",
                ["number_device"] = number,
                ["name_device"] = name,
                ["connection"] = connection,
            };
            if (unitId != null) body["unit_id"] = unitId;
            var (s, j) = await _ctx.Raw.Post("devices/controllers", body).ConfigureAwait(false);
            if (s != 201) { Blocked("dl.fx", $"POST controller {name}: HTTP {s} {Short(j)}"); return 0; }
            var id = (int)j["data"]!["id"]!;
            Track("controller", id);
            return id;
        }

        public async Task<int> Sensor(int controllerId, int number, string name, int? unitId = null, object? connection = null)
        {
            if (controllerId <= 0) return 0;
            var body = new JObject
            {
                ["type_sensor"] = "PIR",
                ["number_device"] = number,
                ["name_device"] = name,
                ["controller_id"] = controllerId,
            };
            if (unitId != null) body["unit_id"] = unitId;
            if (connection != null) body["connection"] = JObject.FromObject(connection);
            var (s, j) = await _ctx.Raw.Post("devices/sensors", body).ConfigureAwait(false);
            if (s != 201) { Blocked("dl.fx", $"POST sensor {name}: HTTP {s} {Short(j)}"); return 0; }
            var id = (int)j["data"]!["id"]!;
            Track("sensor", id);
            return id;
        }

        /// <summary>히터(heater_1 · enabled:true) · 팬(fan_1 · 설정 없음)을 선언한 함체.</summary>
        public async Task<int> Enclosure(int number, string name)
        {
            var body = new JObject
            {
                ["type_enclosure"] = "Outdoor",
                ["number_device"] = number,
                ["name_device"] = name,
                ["connection"] = new JObject { ["ip_address"] = $"10.78.{number % 250}.4", ["ip_port"] = 9804 },
                ["hardware_spec"] = new JObject
                {
                    ["schema"] = 1,
                    ["components"] = new JArray
                    {
                        new JObject { ["key"] = "heater_1", ["type"] = "HEATER" },
                        new JObject { ["key"] = "fan_1", ["type"] = "FAN" },
                    },
                },
                ["device_config"] = new JObject
                {
                    ["schema"] = 1,
                    ["component_overrides"] = new JObject { ["heater_1"] = new JObject { ["enabled"] = true } },
                },
            };
            var (s, j) = await _ctx.Raw.Post("devices/enclosures", body).ConfigureAwait(false);
            if (s != 201) { Blocked("dl.fx", $"POST enclosure {name}: HTTP {s} {Short(j)}"); return 0; }
            var id = (int)j["data"]!["id"]!;
            Track("enclosure", id);
            return id;
        }

        public async Task DisposeAsync()
        {
            foreach (var kind in new[] { "sensor", "enclosure", "controller", "device-group", "unit" })
            {
                foreach (var m in _made.Where(x => x.Kind == kind).ToList())
                {
                    try
                    {
                        var (s, _) = await _ctx.Raw.Delete($"{m.Path}/{m.Id}").ConfigureAwait(false);
                        if (s == 200 || s == 204) _ctx.Rec.Deleted(m.Kind, m.Id);
                        else _ctx.Rec.Leftover(m.Kind, m.Id, $"DELETE {s}");
                    }
                    catch (Exception ex) { _ctx.Rec.Leftover(m.Kind, m.Id, ex.Message); }
                }
            }
            _made.Clear();
        }
    }
}
