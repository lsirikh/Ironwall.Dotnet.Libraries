// 장비 그룹 · 그룹 소속 왕복 점검 (device-groups) — 서버 7.0+ 는 장비의 소속을 `group_ids`(int[]) 로 싣고
// `device_groups` 를 없앴다(api-test-server app/schemas/device.py D12). 창이 쓰는 제품 경로(DtoToModelHelper ·
// DeviceGroupDropHandler · DeviceProviderService · DeviceAssignDialogViewModel · WiringViewModel ·
// DeviceApiService 그룹 메서드)를 그대로 태우고, 판정은 항상 서버를 다시 읽어서(raw re-GET) 한다.
//
// 모든 생성물은 LRT-GRP- 접두다(정리 스윕이 LRT- 로 잡는다). 각 점검이 만든 것은 그 점검의 finally 가 지운다.
using System.Net.Http;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Newtonsoft.Json.Linq;
using MonModels = Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    const string GRP = "device-groups";

    // ================= R1: 7.0+ 응답의 group_ids 가 모델에 실리는가 =================
    [ExtraStep(GRP, 10)]
    static async Task Grp_R1_ReadGroupIds(ExtraContext ctx)
    {
        var fx = new GrpFixture(ctx);
        try
        {
            var a = await fx.Group("LRT-GRP-R1-A");
            var b = await fx.Group("LRT-GRP-R1-B");
            var ctrl = await fx.Controller(97700, "LRT-GRP-R1-CTRL");
            var s = await fx.Sensor(ctrl, 97701, "LRT-GRP-R1-S1", a, b);
            if (s <= 0) { fx.Blocked("g.R1", "fixture setup failed"); return; }

            ctx.Boot.Wire.CurrentTag = "grp/R1";
            var before = ctx.Rec.LastSeq();
            var got = await ctx.DeviceApi.GetSensorByIdAsync(s).ConfigureAwait(false);
            var model = got.Data?.ToSensorDeviceModel();
            var list = await ctx.DeviceApi.GetSensorsAsync(controllerId: ctrl, page: 1, limit: 100).ConfigureAwait(false);
            var listModel = list.Data?.FirstOrDefault(d => d.Id == s)?.ToSensorDeviceModel();
            var serverIds = await fx.SensorGroupIds(s);

            var single = Ids(model?.DeviceGroups);
            var fromList = Ids(listModel?.DeviceGroups);
            var ok = got.Success && SameSet(single, new[] { a, b }) && SameSet(fromList, new[] { a, b });
            ctx.Rec.Add("g.R1", GRP,
                "7.0+ 응답의 group_ids 가 DtoToModelHelper 를 거쳐 모델 DeviceGroups 에 실린다(단건 · 목록)",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"server group_ids=[{Csv(serverIds)}]; model(single).DeviceGroups=[{Csv(single)}]; model(list).DeviceGroups=[{Csv(fromList)}]; " +
                $"dto.GroupIds(single)=[{Csv(got.Data?.GroupIds)}] dto.DeviceGroups(single)={(got.Data?.DeviceGroups == null ? "null" : "[..]")}",
                defectAt: ok ? "" : "Devices.Ui/Helpers/DtoToModelHelper.cs To*DeviceModel: DeviceGroups = dto.DeviceGroups?.Select(g => g.Id) (group_ids 무시)",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("g.R1!", ex.Message); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= R1b: 드래그 뒤 상세 저장이 다른 소속을 지우는가 =================
    [ExtraStep(GRP, 11)]
    static async Task Grp_R1b_DragThenSave(ExtraContext ctx)
    {
        var fx = new GrpFixture(ctx);
        try
        {
            var a = await fx.Group("LRT-GRP-R1B-A");
            var b = await fx.Group("LRT-GRP-R1B-B");
            var c = await fx.Group("LRT-GRP-R1B-C");
            var ctrl = await fx.Controller(97710, "LRT-GRP-R1B-CTRL");
            var s1 = await fx.Sensor(ctrl, 97711, "LRT-GRP-R1B-S1", a, b);
            var s2 = await fx.Sensor(ctrl, 97712, "LRT-GRP-R1B-S2", a, b);
            if (s1 <= 0 || s2 <= 0) { fx.Blocked("g.R1b", "fixture setup failed"); return; }

            // ---- (a) 드래그 → 상세 저장 ----
            ctx.Boot.Wire.CurrentTag = "grp/R1b-drag";
            var before = ctx.Rec.LastSeq();
            var model = (await ctx.DeviceApi.GetSensorByIdAsync(s1).ConfigureAwait(false)).Data!.ToSensorDeviceModel();
            var models = new List<MonModels.IBaseDeviceModel> { model };
            var drop = new DeviceGroupDropHandler(ctx.DeviceApi, () => models, ctx.Boot.Log);
            var line = await drop.AssignAsync(c, "LRT-GRP-R1B-C", models).ConfigureAwait(false);
            model.DeviceName = "LRT-GRP-R1B-S1-EDITED";
            var dto = model.ToSensorDeviceDto();
            await StampUnitAsync(dto, "grp/R1b-drag", ctx.Boot.Log).ConfigureAwait(false);
            var saveSeq = ctx.Rec.LastSeq();
            var saved = await ctx.DeviceApi.UpdateSensorAsync(s1, dto).ConfigureAwait(false);
            var wire = ctx.Rec.Since(saveSeq).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
            var sent = WireGroupIds(wire?.RequestBody);
            var after = await fx.SensorGroupIds(s1);
            var ok = saved.Success && SameSet(after, new[] { a, b, c });
            ctx.Rec.Add("g.R1b-a", GRP,
                "A·B 소속 센서를 C 로 끌어 넣은 뒤 이름만 고쳐 저장해도 A·B·C 가 모두 남는다(DeviceGroupDropHandler → ToSensorDeviceDto → UpdateSensorAsync)",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"drop='{line}'; save success={saved.Success} msg='{saved.Message}'; wire group_ids={(sent == null ? "(omitted)" : "[" + Csv(sent) + "]")}; " +
                $"server group_ids after=[{Csv(after)}] expected=[{a},{b},{c}]; PATCH body={Recorder.Trunc(wire?.RequestBodyRedacted ?? "(none)", 300)}",
                defectAt: ok ? "" : "Devices.Ui/Helpers/DtoToModelHelper.cs ToSensorDeviceDto: GroupIds = model.DeviceGroups (서버는 replace_group_mappings 로 통째 교체)",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- (b) 드래그 → SYNC_DEVICE 식 단건 재조회(DeviceProviderService 병합) → 상세 저장 ----
            ctx.Boot.Wire.CurrentTag = "grp/R1b-reset";
            before = ctx.Rec.LastSeq();
            var (svc, provider) = NewProviderService(ctx);
            var m2 = (await ctx.DeviceApi.GetSensorByIdAsync(s2).ConfigureAwait(false)).Data!.ToSensorDeviceModel();
            provider.Add(m2);
            var drop2 = new DeviceGroupDropHandler(ctx.DeviceApi, () => provider.ToList(), ctx.Boot.Log);
            var line2 = await drop2.AssignAsync(c, "LRT-GRP-R1B-C", new List<MonModels.IBaseDeviceModel> { m2 }).ConfigureAwait(false);
            await svc.FetchDeviceByIdAsync("sensor", s2).ConfigureAwait(false);
            var cached = provider.OfType<MonModels.SensorDeviceModel>().First(d => d.Id == s2);
            var cachedGroups = Ids(cached.DeviceGroups);
            cached.DeviceName = "LRT-GRP-R1B-S2-EDITED";
            var dto2 = cached.ToSensorDeviceDto();
            await StampUnitAsync(dto2, "grp/R1b-reset", ctx.Boot.Log).ConfigureAwait(false);
            saveSeq = ctx.Rec.LastSeq();
            var saved2 = await ctx.DeviceApi.UpdateSensorAsync(s2, dto2).ConfigureAwait(false);
            var wire2 = ctx.Rec.Since(saveSeq).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
            var sent2 = WireGroupIds(wire2?.RequestBody);
            var after2 = await fx.SensorGroupIds(s2);
            var ok2 = saved2.Success && SameSet(after2, new[] { a, b, c });
            ctx.Rec.Add("g.R1b-b", GRP,
                "드래그 뒤 DeviceProviderService 단건 재조회(병합)를 거친 캐시 모델로 저장해도 소속이 지워지지 않는다",
                ok2 ? Verdict.PASS : Verdict.FAIL,
                $"drop='{line2}'; cache DeviceGroups after refetch=[{Csv(cachedGroups)}]; save success={saved2.Success}; " +
                $"wire group_ids={(sent2 == null ? "(omitted)" : "[" + Csv(sent2) + "]")}; server group_ids after=[{Csv(after2)}] expected=[{a},{b},{c}]",
                defectAt: ok2 ? "" : "Devices.Ui/Services/DeviceProviderService.cs UpdateDeviceProperties (null → Clear) + DtoToModelHelper GroupIds",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- (c) 음성 대조: 사람이 소속을 실제로 고친 상세 저장(옛 속성 패널의 그룹 체크 = model.DeviceGroups 교체)은 여전히 반영된다 ----
            ctx.Boot.Wire.CurrentTag = "grp/R1b-explicit";
            before = ctx.Rec.LastSeq();
            var m3 = (await ctx.DeviceApi.GetSensorByIdAsync(s1).ConfigureAwait(false)).Data!.ToSensorDeviceModel();
            m3.DeviceGroups = new List<int> { a, b };   // SensorSelectionViewModel.ApplyGroups 와 같은 방식으로 C 를 뺀다
            var dto3 = m3.ToSensorDeviceDto();
            await StampUnitAsync(dto3, "grp/R1b-explicit", ctx.Boot.Log).ConfigureAwait(false);
            saveSeq = ctx.Rec.LastSeq();
            var saved3 = await ctx.DeviceApi.UpdateSensorAsync(s1, dto3).ConfigureAwait(false);
            var wire3 = ctx.Rec.Since(saveSeq).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
            var sent3 = WireGroupIds(wire3?.RequestBody);
            var after3 = await fx.SensorGroupIds(s1);
            var ok3 = saved3.Success && SameSet(sent3, new[] { a, b }) && SameSet(after3, new[] { a, b });
            ctx.Rec.Add("g.R1b-c", GRP,
                "음성 대조: 소속을 실제로 고친 상세 저장은 group_ids 를 싣고 서버에 반영된다(생략은 '고치지 않음'일 때만)",
                ok3 ? Verdict.PASS : Verdict.FAIL,
                $"save success={saved3.Success}; wire group_ids={(sent3 == null ? "(omitted)" : "[" + Csv(sent3) + "]")}; server group_ids after=[{Csv(after3)}] expected=[{a},{b}]",
                defectAt: ok3 ? "" : "Devices.Ui/Helpers/GroupMembershipBaseline.cs IsUnchanged / BaseDeviceDto.ShouldSerializeGroupIds",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("g.R1b!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= R1c: 배정 창이 기존 소속을 보고 저장할 수 있는가 =================
    [ExtraStep(GRP, 12)]
    static async Task Grp_R1c_AssignDialog(ExtraContext ctx)
    {
        var fx = new GrpFixture(ctx);
        try
        {
            var a = await fx.Group("LRT-GRP-R1C-A");
            var ctrl = await fx.Controller(97720, "LRT-GRP-R1C-CTRL");
            var s1 = await fx.Sensor(ctrl, 97721, "LRT-GRP-R1C-S1", a);
            var s2 = await fx.Sensor(ctrl, 97722, "LRT-GRP-R1C-S2");
            if (s1 <= 0 || s2 <= 0) { fx.Blocked("g.R1c", "fixture setup failed"); return; }

            ctx.Boot.Wire.CurrentTag = "grp/R1c";
            var before = ctx.Rec.LastSeq();
            var models = new List<MonModels.IBaseDeviceModel>();
            foreach (var id in new[] { s1, s2 })
                models.Add((await ctx.DeviceApi.GetSensorByIdAsync(id).ConfigureAwait(false)).Data!.ToSensorDeviceModel());

            var vm = new DeviceAssignDialogViewModel(ctx.DeviceApi, () => models, new DeviceGroupMembershipProbe(ctx.DeviceApi, ctx.Boot.Log), ctx.Boot.Log);
            vm.Initialize(a, "LRT-GRP-R1C-A");
            var initiallyAssigned = vm.Assigned.Select(i => i.Id).ToList();
            var s2Item = vm.Available.FirstOrDefault(i => i.Id == s2);
            if (s2Item != null)
            {
                vm.SetSelection(AssignSide.Available, new[] { s2Item });
                vm.AssignSelected();
            }
            await vm.SaveAsync().ConfigureAwait(false);
            var members = await GroupMemberIds(ctx.Raw, a).ConfigureAwait(false);
            var ok = SameSet(initiallyAssigned, new[] { s1 }) && SameSet(members, new[] { s1, s2 });
            ctx.Rec.Add("g.R1c", GRP,
                "배정 창이 서버의 기존 소속을 보고 열리고, 한 대를 더 넣어 저장할 수 있다(DeviceAssignDialogViewModel.SaveAsync)",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"assigned at open=[{Csv(initiallyAssigned)}] expected=[{s1}]; dialog message='{vm.Message}'; server members after=[{Csv(members)}] expected=[{s1},{s2}]",
                defectAt: ok ? "" : "DtoToModelHelper(group_ids 미반영) → DeviceAssignDialogViewModel.Initialize 기준선 0 → AssignDelta.DriftByCount 가 저장 차단",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("g.R1c!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= G1/G2: 그룹 등록 · 수정 본문 =================
    [ExtraStep(GRP, 13)]
    static async Task Grp_G1G2_CreateUpdate(ExtraContext ctx)
    {
        var fx = new GrpFixture(ctx);
        try
        {
            // ---- G1: 패널의 등록 경로 — CreateDeviceGroupAsync(model.ToDeviceGroupDto()) ----
            ctx.Boot.Wire.CurrentTag = "grp/G1";
            var before = ctx.Rec.LastSeq();
            var draft = new MonModels.DeviceGroupModel { Name = "LRT-GRP-G1-NEW", Description = "live roundtrip" };
            var created = await ctx.DeviceApi.CreateDeviceGroupAsync(draft.ToDeviceGroupDto()).ConfigureAwait(false);
            var postWire = ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "POST");
            if (created.Success && created.Data?.Id > 0) fx.Track("device-group", created.Data.Id);
            var newId = created.Data?.Id ?? 0;
            if (newId <= 0) newId = await fx.FindGroupByName("LRT-GRP-G1-NEW");   // 응답을 못 읽었어도 서버에 남았으면 지운다
            var (gs, gj) = newId > 0 ? await ctx.Raw.Get($"devices/groups/{newId}").ConfigureAwait(false) : (0, new JObject());
            var ok1 = created.Success && gs == 200 && Str(Obj(gj["data"])?["name"]) == "LRT-GRP-G1-NEW";
            ctx.Rec.Add("g.G1", GRP, "그룹 등록(패널 경로: ToDeviceGroupDto → CreateDeviceGroupAsync)이 서버에 만들어진다",
                ok1 ? Verdict.PASS : Verdict.FAIL,
                $"success={created.Success} msg='{created.Message}'; POST body={postWire?.RequestBodyRedacted ?? "(none)"}; " +
                $"status={postWire?.Status}; response={Recorder.Trunc(postWire?.ResponseBodyRedacted ?? "", 300)}",
                defectAt: ok1 ? "" : "Messages/Dto/Devices/DeviceGroupDto.cs + DeviceApiService.CreateDeviceGroupAsync (device_count·created_at 이 extra=forbid 스키마로 나감)",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- G2: 패널의 수정 경로 — 서버 목록 → ToDeviceGroupModel → 편집 → UpdateDeviceGroupAsync(id, ToDeviceGroupDto()) ----
            ctx.Boot.Wire.CurrentTag = "grp/G2";
            var g2 = await fx.Group("LRT-GRP-G2-OLD", "before edit");
            if (g2 <= 0) { fx.Blocked("g.G2", "fixture setup failed"); return; }
            before = ctx.Rec.LastSeq();
            var loaded = (await ctx.DeviceApi.GetDeviceGroupByIdAsync(g2).ConfigureAwait(false)).Data!.ToDeviceGroupModel();
            loaded.Name = "LRT-GRP-G2-EDITED";
            loaded.Description = "after edit";
            var updated = await ctx.DeviceApi.UpdateDeviceGroupAsync(loaded.Id, loaded.ToDeviceGroupDto()).ConfigureAwait(false);
            var putWire = ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "PUT" || w.Method == "PATCH");
            var (_, uj) = await ctx.Raw.Get($"devices/groups/{g2}").ConfigureAwait(false);
            var ok2 = updated.Success && Str(Obj(uj["data"])?["name"]) == "LRT-GRP-G2-EDITED" && Str(Obj(uj["data"])?["description"]) == "after edit";
            ctx.Rec.Add("g.G2", GRP, "그룹 수정(패널 경로: ToDeviceGroupModel → 편집 → UpdateDeviceGroupAsync)이 서버에 반영된다",
                ok2 ? Verdict.PASS : Verdict.FAIL,
                $"success={updated.Success} msg='{updated.Message}'; {putWire?.Method} body={putWire?.RequestBodyRedacted ?? "(none)"}; status={putWire?.Status}; " +
                $"server name='{Str(Obj(uj["data"])?["name"])}' description='{Str(Obj(uj["data"])?["description"])}'",
                defectAt: ok2 ? "" : "Messages/Dto/Devices/DeviceGroupDto.cs + DeviceApiService.UpdateDeviceGroupAsync (id·device_count·created_at 이 extra=forbid 스키마로 나감)",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("g.G!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= G5: 100대 넘는 되돌리기 =================
    [ExtraStep(GRP, 14)]
    static async Task Grp_G5_UndoOver100(ExtraContext ctx)
    {
        var fx = new GrpFixture(ctx);
        try
        {
            var g = await fx.Group("LRT-GRP-G5");
            var ctrl = await fx.Controller(97740, "LRT-GRP-G5-CTRL");
            var s = await fx.Sensor(ctrl, 97741, "LRT-GRP-G5-S1", g);
            if (s <= 0) { fx.Blocked("g.G5", "fixture setup failed"); return; }

            // 실제 장비 1대 + 서버에 없는 id 100개 = 101개. 없는 id 는 not_found 로 답하는 멱등 계약이라
            // 서버 데이터를 건드리지 않고 상한(max_length=100)만 시험한다 — 101대를 실제로 만들면
            // 다른 작업자의 정리 스윕(limit=100)을 "확인 불가"로 만든다.
            var ids = new List<int> { s };
            for (var i = 1; i <= 100; i++) ids.Add(2_000_000_000 - i);

            ctx.Boot.Wire.CurrentTag = "grp/G5";
            var before = ctx.Rec.LastSeq();
            var models = new List<MonModels.IBaseDeviceModel>();
            var drop = new DeviceGroupDropHandler(ctx.DeviceApi, () => models, ctx.Boot.Log);
            var line = await drop.UndoAsync(new GroupDropUndo(g, "LRT-GRP-G5", ids)).ConfigureAwait(false);
            var deletes = ctx.Rec.Since(before).Where(w => w.Method == "DELETE").ToList();
            var members = await GroupMemberIds(ctx.Raw, g).ConfigureAwait(false);
            var ok = !members.Contains(s) && deletes.All(w => w.Status == 200);
            ctx.Rec.Add("g.G5", GRP, "101대 되돌리기(DeviceGroupDropHandler.UndoAsync)가 서버 상한 100 에 맞춰 나뉘어 실제로 빠진다",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"result='{line}'; DELETE calls={deletes.Count} statuses=[{string.Join(",", deletes.Select(w => w.Status))}]; " +
                $"server members after=[{Csv(members)}] (real sensor {s} must be gone); first response={Recorder.Trunc(deletes.FirstOrDefault()?.ResponseBodyRedacted ?? "", 240)}",
                defectAt: ok ? "" : "Devices.Ui/Consoles/Groups/DeviceGroupDrop.cs UndoAsync (한 번에 전부 보냄 — device_ids max_length=100)",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("g.G5!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= W-R3 / W-R4: 결선 창의 그룹 저장 기준선 =================
    [ExtraStep(GRP, 15)]
    static async Task Grp_W_WiringGroups(ExtraContext ctx)
    {
        var fx = new GrpFixture(ctx);
        try
        {
            var a = await fx.Group("LRT-GRP-W-A");
            var b = await fx.Group("LRT-GRP-W-B");
            var gone = await fx.Group("LRT-GRP-W-GONE");
            var ctrl = await fx.Controller(97750, "LRT-GRP-W-CTRL");
            var s1 = await fx.Sensor(ctrl, 97751, "LRT-GRP-W-S1", a);
            var s2 = await fx.Sensor(ctrl, 97752, "LRT-GRP-W-S2");
            if (s1 <= 0 || s2 <= 0 || gone <= 0) { fx.Blocked("g.W", "fixture setup failed"); return; }

            var groups = new List<WiringGroupInfo> { new(a, "LRT-GRP-W-A"), new(b, "LRT-GRP-W-B"), new(gone, "LRT-GRP-W-GONE") };

            // ---- W-R3: 그룹만 바꾼 저장 뒤 창이 깨끗해지는가 ----
            ctx.Boot.Wire.CurrentTag = "grp/W-R3";
            var before = ctx.Rec.LastSeq();
            var vm = await OpenWiring(ctx, ctrl, groups).ConfigureAwait(false);
            var row1 = vm.Rows.First(r => r.Row.Id == s1);
            vm.OnSelectionChanged(new[] { row1 });
            vm.ToggleGroup(vm.GroupChecks.First(g => g.Id == b));
            vm.ApplyEdit();
            await vm.SaveAsync().ConfigureAwait(false);
            var dirtyAfter = vm.HasChanges;
            var server1 = await fx.SensorGroupIds(s1);
            var groupCalls = ctx.Rec.Since(before).Count(w => w.Uri.Contains("/devices/groups/") && (w.Method == "POST" || w.Method == "DELETE"));
            var ok3 = !dirtyAfter && SameSet(server1, new[] { a, b });
            ctx.Rec.Add("g.W-R3", GRP, "결선 창에서 그룹만 바꿔 저장하면 서버에 반영되고 창은 '저장할 것 없음'이 된다(WiringViewModel.SaveAsync)",
                ok3 ? Verdict.PASS : Verdict.FAIL,
                $"status='{vm.StatusText}'; HasChanges after save={dirtyAfter}; group calls={groupCalls}; server group_ids=[{Csv(server1)}] expected=[{a},{b}]",
                defectAt: ok3 ? "" : "Devices.Ui/Consoles/Wiring/WiringViewModel.cs SaveAsync: MarkBaseline(result.OkKeys) 만 — 그룹 호출 성공분은 기준선에 안 들어감",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- W-R4: 행 PATCH 는 성공 · 그룹 호출은 실패 → 그룹 변경이 조용히 '저장됨'으로 바뀌지 않는가 ----
            ctx.Boot.Wire.CurrentTag = "grp/W-R4";
            before = ctx.Rec.LastSeq();
            var vm2 = await OpenWiring(ctx, ctrl, groups).ConfigureAwait(false);
            var row2 = vm2.Rows.First(r => r.Row.Id == s2);
            vm2.OnSelectionChanged(new[] { row2 });
            vm2.EditName = "LRT-GRP-W-S2-EDITED";
            vm2.ToggleGroup(vm2.GroupChecks.First(g => g.Id == gone));
            vm2.ApplyEdit();
            // 저장 직전에 그 그룹이 사라진다(다른 창이 지웠다) — 그룹 넣기는 404 가 된다.
            var (ds, _) = await ctx.Raw.Delete($"devices/groups/{gone}").ConfigureAwait(false);
            if (ds == 200 || ds == 204) fx.Forget("device-group", gone);
            await vm2.SaveAsync().ConfigureAwait(false);
            var nameAfter = await fx.SensorName(s2);
            var dirty2 = vm2.HasChanges;
            var rowStillPending = vm2.Rows.First(r => r.Row.Id == s2).Row.GroupsChanged;
            var ok4 = nameAfter == "LRT-GRP-W-S2-EDITED" && dirty2 && rowStillPending;
            ctx.Rec.Add("g.W-R4", GRP, "행 PATCH 성공 + 그룹 호출 실패면 그 그룹 변경은 미저장으로 남는다(조용히 기준선으로 먹지 않는다)",
                ok4 ? Verdict.PASS : Verdict.FAIL,
                $"status='{vm2.StatusText}'; server name='{nameAfter}'; HasChanges after save={dirty2}; row GroupsChanged={rowStillPending}; group DELETE(fixture)={ds}",
                defectAt: ok4 ? "" : "Devices.Ui/Consoles/Wiring/Model/WiringBoard.cs MarkBaseline(OkKeys) → row.MarkGroupBaseline() 가 실패한 그룹 변경까지 기준선으로 삼음",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("g.W!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ================= CONN: 저장된 connection.type 보존 =================
    [ExtraStep(GRP, 16)]
    static async Task Grp_Conn_TypePreserved(ExtraContext ctx)
    {
        var fx = new GrpFixture(ctx);
        try
        {
            var ctrl = await fx.Controller(97760, "LRT-GRP-CONN-CTRL", connectionType: "IP_CONVERTER");
            if (ctrl <= 0) { fx.Blocked("g.CONN", "fixture setup failed"); return; }
            var (_, seed) = await ctx.Raw.Get($"devices/controllers/{ctrl}").ConfigureAwait(false);
            var seededType = Str(Obj(Obj(seed["data"])?["connection"])?["type"]);

            ctx.Boot.Wire.CurrentTag = "grp/CONN";
            var before = ctx.Rec.LastSeq();
            var model = (await ctx.DeviceApi.GetControllerByIdAsync(ctrl).ConfigureAwait(false)).Data!.ToControllerDeviceModel();
            model.DeviceName = "LRT-GRP-CONN-CTRL-EDITED";
            var dto = model.ToControllerDeviceDto();
            await StampUnitAsync(dto, "grp/CONN", ctx.Boot.Log).ConfigureAwait(false);
            var saveSeq = ctx.Rec.LastSeq();
            var saved = await ctx.DeviceApi.UpdateControllerAsync(ctrl, dto).ConfigureAwait(false);
            var wire = ctx.Rec.Since(saveSeq).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
            string? sentType = null;
            try { sentType = (string?)JObject.Parse(wire?.RequestBody ?? "{}").SelectToken("connection.type"); } catch { }
            var (_, after) = await ctx.Raw.Get($"devices/controllers/{ctrl}").ConfigureAwait(false);
            var typeAfter = Str(Obj(Obj(after["data"])?["connection"])?["type"]);
            var ok = seededType == "IP_CONVERTER" && saved.Success && typeAfter == "IP_CONVERTER";
            ctx.Rec.Add("g.CONN", GRP, "IP_CONVERTER 제어기의 이름만 고쳐 저장해도(Dto→Model→Dto→PATCH) connection.type 이 그대로다",
                ok ? Verdict.PASS : Verdict.FAIL,
                $"seeded type={seededType}; save success={saved.Success} msg='{saved.Message}'; wire connection.type={sentType ?? "(omitted)"}; server type after={typeAfter}; " +
                $"PATCH body={Recorder.Trunc(wire?.RequestBodyRedacted ?? "(none)", 300)}",
                defectAt: ok ? "" : "Messages/Dto/Devices/ConnectionAxisDto.cs DeviceAxisWrite.BuildIpConnection (IP 가 있으면 무조건 IP_DIRECT)",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("g.CONN!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ---------------- helpers ----------------
    static (DeviceProviderService Service, DeviceProvider Provider) NewProviderService(ExtraContext ctx)
    {
        var log = ctx.Boot.Log;
        var provider = new DeviceProvider();
        var svc = new DeviceProviderService(
            logService: log,
            eventAggregator: new EventAggregator(),
            apiService: ctx.DeviceApi,
            deviceProvider: provider,
            controllerProvider: new ControllerDeviceProvider(log, provider),
            sensorProvider: new SensorDeviceProvider(log, provider),
            cameraProvider: new CameraDeviceProvider(log, provider),
            deviceGroupProvider: new DeviceGroupProvider(log),
            serverApiService: new ServerApiService(log, ctx.Boot.Api, ctx.Boot.Setup, ctx.Boot.Probe),
            serverProvider: new ServerProvider(log),
            queryPolicy: ctx.Policy);
        return (svc, provider);
    }

    /// <summary>결선 창을 연다 — WiringLauncher.SeedsFor 와 같은 투영(서버에서 받은 센서 모델 → 씨앗).</summary>
    static async Task<WiringViewModel> OpenWiring(ExtraContext ctx, int ctrlId, IReadOnlyList<WiringGroupInfo> groups)
    {
        var list = await ctx.DeviceApi.GetSensorsAsync(controllerId: ctrlId, page: 1, limit: 100).ConfigureAwait(false);
        var seeds = new List<WiringSensorSeed>();
        foreach (var sensor in (list.Data ?? new()).Select(d => d.ToSensorDeviceModel()).OrderBy(s => s.DeviceNumber))
        {
            var spec = sensor.Axes?.HardwareSpec?.Spec;
            seeds.Add(new WiringSensorSeed(
                sensor.Id,
                sensor.Axes?.Connection?.Channel,
                new SensorFacts(sensor.DeviceNumber, sensor.DeviceName ?? string.Empty,
                    !string.IsNullOrWhiteSpace(sensor.TypeAxisCode) ? sensor.TypeAxisCode! : sensor.DeviceType.ToString(),
                    sensor.Location ?? string.Empty),
                WiringSpec.Read(spec),
                WiringSpec.Validate(spec),
                sensor.DeviceGroups?.ToList()));
        }
        var apply = new WiringApplyService(new DeviceApiSensorGateway(ctx.DeviceApi), new NullDeviceProvider(), ctx.Boot.Log, ctx.Policy);
        return WiringViewModel.ForController(new WiringControllerInfo(ctrlId, 0, "LRT-GRP-W-CTRL", ""), seeds,
            new[] { "PIR" }, apply, new YesWiringDialogs(), groups);
    }

    static List<int> Ids(IEnumerable<int>? ids) => ids?.ToList() ?? new List<int>();
    static bool SameSet(IEnumerable<int>? a, IEnumerable<int> b) => a != null && new HashSet<int>(a).SetEquals(b);
    static string Csv(IEnumerable<int>? ids) => ids == null ? "null" : string.Join(",", ids);

    static List<int>? WireGroupIds(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            var arr = JObject.Parse(body)["group_ids"] as JArray;
            return arr?.Select(t => (int)t).ToList();
        }
        catch { return null; }
    }

    /// <summary>결선 창의 대화상자 — 저장 확인에 "예"만 답한다(창을 띄우지 않는다).</summary>
    sealed class YesWiringDialogs : IWiringDialogs
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult<string?>(null);
        public Task<MakeSensorsResult?> AskMakeSensorsAsync(IReadOnlyList<string> types, string defaultType, string defaultZone, IReadOnlyCollection<int> existingNumbers, int suggestedStart)
            => Task.FromResult<MakeSensorsResult?>(null);
        public Task<bool> ShowPasteReportAsync(PasteReport report) => Task.FromResult(false);
        public void RememberPasteContext(IReadOnlyCollection<int> existingNumbers, string defaultType, string defaultZone) { }
        public string? ReadClipboardText() => null;
    }

    /// <summary>한 점검의 준비물 — 만든 것을 기억했다가 역순으로 지운다.</summary>
    sealed class GrpFixture
    {
        readonly ExtraContext _ctx;
        readonly List<(string Kind, string Path, int Id)> _made = new();
        public GrpFixture(ExtraContext ctx) { _ctx = ctx; }

        public void Track(string kind, int id)
        {
            var path = kind switch { "device-group" => "devices/groups", "controller" => "devices/controllers", _ => "devices/sensors" };
            if (_made.Any(m => m.Kind == kind && m.Id == id)) return;
            _made.Add((kind, path, id));
            _ctx.Rec.Created(kind, id);
        }

        public void Forget(string kind, int id)
        {
            _made.RemoveAll(m => m.Kind == kind && m.Id == id);
            _ctx.Rec.Deleted(kind, id);
        }

        public void Blocked(string id, string why)
            => _ctx.Rec.Add(id, GRP, "device-group fixture/harness", Verdict.BLOCKED, why, blocked: "fixture/harness failure - NOT a product defect unless proven");

        public async Task<int> Group(string name, string description = "live roundtrip")
        {
            var (s, j) = await _ctx.Raw.Post("devices/groups", new { name, description }).ConfigureAwait(false);
            if (s != 200 && s != 201) { Blocked("g.fx", $"POST devices/groups {name}: HTTP {s} {Short(j)}"); return 0; }
            var id = (int)j["data"]!["id"]!;
            Track("device-group", id);
            return id;
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

        public async Task<int> Controller(int number, string name, string? connectionType = null)
        {
            object connection = connectionType == null
                ? new { ip_address = $"10.77.{number % 250}.1", ip_port = 9700 }
                : new { type = connectionType, ip_address = $"10.77.{number % 250}.1", ip_port = 9700 };
            var (s, j) = await _ctx.Raw.Post("devices/controllers", new
            {
                type_controller = "Controller",
                number_device = number,
                name_device = name,
                connection,
            }).ConfigureAwait(false);
            if (s != 201) { Blocked("g.fx", $"POST controller {name}: HTTP {s} {Short(j)}"); return 0; }
            var id = (int)j["data"]!["id"]!;
            Track("controller", id);
            return id;
        }

        public async Task<int> Sensor(int controllerId, int number, string name, params int[] groupIds)
        {
            if (controllerId <= 0 || groupIds.Any(g => g <= 0)) return 0;
            var (s, j) = await _ctx.Raw.Post("devices/sensors", new
            {
                type_sensor = "PIR",
                number_device = number,
                name_device = name,
                controller_id = controllerId,
                group_ids = groupIds,
            }).ConfigureAwait(false);
            if (s != 201) { Blocked("g.fx", $"POST sensor {name}: HTTP {s} {Short(j)}"); return 0; }
            var id = (int)j["data"]!["id"]!;
            Track("sensor", id);
            return id;
        }

        public async Task<List<int>> SensorGroupIds(int sensorId)
        {
            var (_, j) = await _ctx.Raw.Get($"devices/sensors/{sensorId}").ConfigureAwait(false);
            return ((Obj(j["data"])?["group_ids"]) as JArray)?.Select(t => (int)t).ToList() ?? new List<int>();
        }

        public async Task<string> SensorName(int sensorId)
        {
            var (_, j) = await _ctx.Raw.Get($"devices/sensors/{sensorId}").ConfigureAwait(false);
            return Str(Obj(j["data"])?["name_device"]);
        }

        public async Task DisposeAsync()
        {
            // 센서 → 제어기 → 그룹 순(그룹 매핑은 CASCADE).
            foreach (var kind in new[] { "sensor", "controller", "device-group" })
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
