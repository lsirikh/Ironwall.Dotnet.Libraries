// 부품 조립기 · 프리셋 등록 창의 실 API 왕복(LRT_ONLY=assembly-vm).
//
// 모든 "act" 는 제품 경로다 — CatalogService(/devices/spec) → AssemblyViewModel.ForDevice(GET 한 모델) →
// 보드 조작(선택 · 빼기 · 팔레트 더하기 · 속성 칸 비우기) → CommitAsync → ComponentApplyService → PATCH,
// 그리고 RegisterFromPresetViewModel(입력 → RegisterAsync → PresetRegistrar → POST).
// 손으로 만든 DTO 는 준비(arrange)에만 쓴다. 판정은 와이어 바이트와 서버 재조회(raw GET)로 한다.
//
// 이 파일이 만드는 장비는 전부 "LRT-ASM-" 접두이고 finally 에서 지운다(정리 스윕은 LRT- 접두를 본다).
using System.IO;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MonModels = Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    const string ASM_TAG = "assembly-vm";

    /// <summary>
    /// 조립기(ForDevice) 한 대로 R1 · R3 · R2 · R4a 를 차례로 본다 — 매 단계 장비를 다시 GET 해 새 창을 여는 것과 같이 한다.
    /// </summary>
    [ExtraStep("assembly-vm", 10)]
    static async Task AssemblyVm_EditDevice(ExtraContext ctx)
    {
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        int encId = 0;
        try
        {
            var provider = new NullDeviceProvider();
            var catalog = new CatalogService(ctx.DeviceApi, ctx.Policy, boot.Log);
            var applySvc = new ComponentApplyService(ctx.DeviceApi, provider, boot.Log, ctx.Policy);
            var store = AsmTempStore(out var storeDir);

            // ---------- 카탈로그: 실제 /devices/spec ----------
            boot.Wire.CurrentTag = ASM_TAG + "/spec";
            var specBefore = rec.LastSeq();
            var loaded = await catalog.EnsureLoadedAsync().ConfigureAwait(false);
            var specWire = rec.Since(specBefore).FirstOrDefault(w => w.Uri.Contains("/devices/spec"));
            var heaterRaw = AsmCatalogEntry(specWire?.ResponseBody, "HEATER");
            var heaterInfo = catalog.Find("HEATER");
            rec.Add("asm.0", ASM_TAG, "catalog loaded from the live GET /devices/spec", loaded && heaterInfo != null ? Verdict.INFO : Verdict.BLOCKED,
                $"loaded={loaded}; HEATER wire entry = {heaterRaw}; reader OverrideParams=[{string.Join(",", heaterInfo?.OverrideParams ?? Array.Empty<string>())}]",
                blocked: loaded ? "" : "catalog did not load", seqs: SeqsSince(rec, specBefore));
            if (!loaded) return;

            // ---------- arrange: an enclosure with door · heater_1 · fan_1, overrides, a threshold ----------
            boot.Wire.CurrentTag = ASM_TAG + "/arrange";
            var arrangePreset = new DevicePreset
            {
                Id = "lrtasm0001",
                Name = "LRT-ASM-ARRANGE",
                Category = EnumDeviceCategory.Enclosure,
                TypeAxisCode = "Outdoor",
                Components = new List<MonModels.ComponentDefinitionModel>
                {
                    new() { Key = "door", Type = "DOOR_SENSOR" },
                    new() { Key = "heater_1", Type = "HEATER", Label = "LRT-HEATER-LABEL" },
                    new() { Key = "fan_1", Type = "FAN", Label = "LRT-FAN-LABEL" },
                },
                ComponentOverrides = new JObject
                {
                    ["heater_1"] = new JObject { ["enabled"] = true },
                    ["fan_1"] = new JObject { ["enabled"] = true },
                },
                Thresholds = new JObject { ["temperature"] = new JObject { ["high"] = 45 } },
            };
            var registrar = new PresetRegistrar(ctx.DeviceApi, provider, boot.Log, ctx.Policy);
            var reg = await registrar.RegisterAsync(PresetRequestBuilder.Build(arrangePreset,
                new PresetInstanceInfo { DeviceNumber = 96701, DeviceName = "LRT-ASM-ENC-EDIT" })).ConfigureAwait(false);
            encId = reg.NewDeviceId ?? 0;
            if (encId <= 0)
            {
                rec.Add("asm.a", ASM_TAG, "arrange enclosure", Verdict.BLOCKED, $"register: {reg.Message}", blocked: "fixture setup failed");
                return;
            }
            rec.Created("enclosure", encId);

            // ================= R1: override rows exist on the REAL server shape =================
            boot.Wire.CurrentTag = ASM_TAG + "/R1";
            var vm1 = await AsmOpenForDeviceAsync(ctx, encId, catalog, store, applySvc);
            var heaterItem = vm1?.BoardItems.FirstOrDefault(i => i.Slot.Key == "heater_1");
            if (heaterItem != null) vm1!.OnBoardSelectionChanged(new[] { heaterItem });
            var rows = vm1?.OverrideRows.Select(r => $"{r.Name}={r.Text}").ToList() ?? new List<string>();
            var enabledRow = vm1?.OverrideRows.FirstOrDefault(r => r.Name == "enabled");
            var r1Ok = heaterItem != null && enabledRow != null && enabledRow.Text == "true";
            rec.Add("asm.R1", ASM_TAG, "selecting the heater slot shows an 'enabled' override row (live /spec override_params is a dict)",
                r1Ok ? Verdict.PASS : Verdict.FAIL,
                $"board=[{string.Join(",", vm1?.BoardItems.Select(i => i.Slot.Key) ?? Array.Empty<string>())}] inspected={vm1?.Inspected?.Slot.Key} " +
                $"OverrideRows=[{string.Join(",", rows)}] HasOverrideRows={vm1?.HasOverrideRows}",
                defectAt: r1Ok ? "" : "Devices.Ui/Consoles/Assembly/Catalog/ComponentCatalogReader.cs ReadNames/Collect (dict keyed by parameter name read as one object)");

            // ================= R3: remove fan_1 on the board -> apply =================
            boot.Wire.CurrentTag = ASM_TAG + "/R3";
            var vm3 = await AsmOpenForDeviceAsync(ctx, encId, catalog, store, applySvc);
            var fanItem = vm3?.BoardItems.FirstOrDefault(i => i.Slot.Key == "fan_1");
            if (vm3 != null && fanItem != null)
            {
                vm3.OnBoardSelectionChanged(new[] { fanItem });
                vm3.RemoveSelected();
                var diff = vm3.DiffSummary;
                var before = rec.LastSeq();
                await vm3.CommitAsync().ConfigureAwait(false);
                var patch = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
                var sent = AsmParse(patch?.RequestBody);
                var sentFan = sent?.SelectToken("device_config.component_overrides.fan_1");
                var (_, after) = await raw.Get($"devices/enclosures/{encId}").ConfigureAwait(false);
                var d = Obj(after["data"]);
                var keys = AsmKeys(d);
                var ov = Obj(Obj(d?["device_config"])?["component_overrides"]);
                var th = Obj(Obj(d?["device_config"])?["thresholds"]);
                var r3Ok = patch?.Status == 200
                    && sentFan != null && sentFan.Type == JTokenType.Null
                    && keys.SequenceEqual(new[] { "door", "heater_1" })
                    && ov?["fan_1"] == null
                    && (bool?)ov?["heater_1"]?["enabled"] == true
                    && (double?)th?["temperature"]?["high"] == 45;
                rec.Add("asm.R3", ASM_TAG, "board remove fan_1 -> commit: PATCH carries component_overrides.fan_1:null, fan_1 gone, heater_1 override + threshold intact",
                    r3Ok ? Verdict.PASS : Verdict.FAIL,
                    $"diff='{diff.Replace(Environment.NewLine, " | ")}' status='{vm3.StatusText}' HTTP {patch?.Status}; sent fan_1={J(sentFan)}; " +
                    $"server components=[{string.Join(",", keys)}] overrides={J(ov)} thresholds={J(th)}; PATCH body={Recorder.Trunc(patch?.RequestBodyRedacted ?? "(none)", 500)}",
                    defectAt: r3Ok ? "" : "Devices.Ui/Consoles/Assembly/AssemblyViewModel.cs ApplyToDeviceAsync / Model/AssemblyBoard.cs ToOverrides / Register/ComponentApplyService.cs PatchAsync",
                    seqs: SeqsSince(rec, before));
            }
            else rec.Add("asm.R3", ASM_TAG, "board remove fan_1", Verdict.BLOCKED, "fan_1 slot not on the board", blocked: "fixture state");

            // ================= R2: clear the label text box of heater_1 -> apply =================
            boot.Wire.CurrentTag = ASM_TAG + "/R2";
            var vm2 = await AsmOpenForDeviceAsync(ctx, encId, catalog, store, applySvc);
            var heater2 = vm2?.BoardItems.FirstOrDefault(i => i.Slot.Key == "heater_1");
            if (vm2 != null && heater2 != null)
            {
                vm2.OnBoardSelectionChanged(new[] { heater2 });
                // What the WPF binding does when the operator empties the TextBox (no TargetNullValue): "" not null.
                vm2.Inspected!.Slot.Label = "";
                vm2.Inspected!.Slot.Position = "";
                var diff = vm2.DiffSummary;
                var before = rec.LastSeq();
                await vm2.CommitAsync().ConfigureAwait(false);
                var patch = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
                var sent = AsmParse(patch?.RequestBody);
                var sentHeater = (sent?.SelectToken("hardware_spec.components") as JArray)?.FirstOrDefault(c => Str(c["key"]) == "heater_1");
                var (_, after) = await raw.Get($"devices/enclosures/{encId}").ConfigureAwait(false);
                var comp = (Obj(Obj(after["data"])?["hardware_spec"])?["components"] as JArray)?.FirstOrDefault(c => Str(c["key"]) == "heater_1") as JObject;
                var r2Ok = patch?.Status == 200 && comp != null && comp["label"] == null && comp["position"] == null
                    && sentHeater?["label"] == null && sentHeater?["position"] == null;
                rec.Add("asm.R2", ASM_TAG, "clearing the inspector label/position text box sends no \"\" (no 422 EMPTY_STRING) and the label is gone on re-GET",
                    r2Ok ? Verdict.PASS : Verdict.FAIL,
                    $"diff='{diff.Replace(Environment.NewLine, " | ")}' status='{vm2.StatusText}' HTTP {patch?.Status}; sent heater_1={J(sentHeater)}; server heater_1={J(comp)}; " +
                    $"response={Recorder.Trunc(patch?.ResponseBodyRedacted ?? "(none)", 400)}",
                    defectAt: r2Ok ? "" : "Devices.Ui/Consoles/Assembly/Model/AssemblySlot.cs ToDefinition / Register/PresetRequestBuilder.cs ToComponentDto (blank string not normalized to null)",
                    seqs: SeqsSince(rec, before));
            }
            else rec.Add("asm.R2", ASM_TAG, "clear label", Verdict.BLOCKED, "heater_1 slot not on the board", blocked: "fixture state");

            // ================= R4a: add FAN from the palette -> apply =================
            boot.Wire.CurrentTag = ASM_TAG + "/R4a";
            var vm4 = await AsmOpenForDeviceAsync(ctx, encId, catalog, store, applySvc);
            var fanPalette = vm4?.Palette.FirstOrDefault(p => p.Code == "FAN");
            if (vm4 != null && fanPalette != null)
            {
                vm4.AddFromPalette(fanPalette);                    // keyboard fallback == drop path
                var newKey = vm4.Inspected?.Slot.Key ?? "(none)";
                var before = rec.LastSeq();
                await vm4.CommitAsync().ConfigureAwait(false);
                var patch = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
                var sent = AsmParse(patch?.RequestBody);
                var sentNew = sent?.SelectToken($"device_config.component_overrides.{newKey}");
                var sentHeater = sent?.SelectToken("device_config.component_overrides.heater_1");
                var (_, after) = await raw.Get($"devices/enclosures/{encId}").ConfigureAwait(false);
                var ov = Obj(Obj(Obj(after["data"])?["device_config"])?["component_overrides"]);
                var serverNewEnabled = (ov?[newKey] as JObject)?["enabled"];
                var keys = AsmKeys(Obj(after["data"]));
                // The catalog's FAN override_params.enabled declares no default, and the board set no intent for the
                // new slot -> nothing may be invented; heater_1's existing intent must ride along untouched.
                var r4aOk = patch?.Status == 200 && keys.Contains(newKey)
                    && ((sentNew as JObject)?["enabled"] is not { Type: not JTokenType.Null })
                    && (serverNewEnabled == null || serverNewEnabled.Type == JTokenType.Null)
                    && (bool?)ov?["heater_1"]?["enabled"] == true;
                rec.Add("asm.R4a", ASM_TAG, "adding FAN from the palette does not silently write enabled:false (no stale model default)",
                    r4aOk ? Verdict.PASS : Verdict.FAIL,
                    $"new key={newKey} status='{vm4.StatusText}' HTTP {patch?.Status}; sent {newKey}={J(sentNew)} heater_1={J(sentHeater)}; " +
                    $"server overrides={J(ov)} components=[{string.Join(",", keys)}]",
                    defectAt: r4aOk ? "" : "Devices.Ui/Consoles/Assembly/Register/ComponentApplyService.cs PatchAsync Enclosure (dto.FanEnabled = origin.FanEnabled onto a newly declared FAN)",
                    seqs: SeqsSince(rec, before));
            }
            else rec.Add("asm.R4a", ASM_TAG, "add FAN", Verdict.BLOCKED, "FAN not in palette", blocked: "catalog/palette");

            // ================= R6 (found while fixing R4): clearing the heater's 'enabled' override row -> apply =================
            boot.Wire.CurrentTag = ASM_TAG + "/R6";
            var vm6 = await AsmOpenForDeviceAsync(ctx, encId, catalog, store, applySvc);
            var heater6 = vm6?.BoardItems.FirstOrDefault(i => i.Slot.Key == "heater_1");
            if (vm6 != null && heater6 != null) vm6.OnBoardSelectionChanged(new[] { heater6 });
            var row6 = vm6?.OverrideRows.FirstOrDefault(r => r.Name == "enabled");
            if (vm6 != null && row6 != null)
            {
                var textBefore = row6.Text;
                row6.Text = "";                                   // operator empties the override box = "no intent"
                var diff = vm6.DiffSummary;
                var before = rec.LastSeq();
                await vm6.CommitAsync().ConfigureAwait(false);
                var patch = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
                var sent = AsmParse(patch?.RequestBody);
                var sentHeater = sent?.SelectToken("device_config.component_overrides.heater_1");
                var (_, after) = await raw.Get($"devices/enclosures/{encId}").ConfigureAwait(false);
                var ov = Obj(Obj(Obj(after["data"])?["device_config"])?["component_overrides"]);
                var serverEnabled = (ov?["heater_1"] as JObject)?["enabled"];
                var r6Ok = patch?.Status == 200 && (serverEnabled == null || serverEnabled.Type == JTokenType.Null);
                rec.Add("asm.R6", ASM_TAG, "clearing the heater 'enabled' override row removes the intent on the server (not silently kept)",
                    r6Ok ? Verdict.PASS : Verdict.FAIL,
                    $"row text before='{textBefore}' diff='{diff.Replace(Environment.NewLine, " | ")}' status='{vm6.StatusText}' HTTP {patch?.Status}; " +
                    $"sent heater_1={J(sentHeater)}; server overrides={J(ov)}",
                    defectAt: r6Ok ? "" : "Devices.Ui/Consoles/Assembly/Model/AssemblyBoard.cs ToOverrides (cleared entry not sent) + Register/ComponentApplyService.cs (computed HeaterEnabled re-sends the old value)",
                    seqs: SeqsSince(rec, before));
            }
            else rec.Add("asm.R6", ASM_TAG, "clear override row", Verdict.BLOCKED,
                $"no 'enabled' override row on heater_1 (rows=[{string.Join(",", vm6?.OverrideRows.Select(r => r.Name) ?? Array.Empty<string>())}])",
                blocked: "needs R1 (override rows) first");

            try { Directory.Delete(storeDir, true); } catch (IOException) { }
        }
        catch (Exception ex)
        {
            rec.Add("asm!", ASM_TAG, "assembly edit-device round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await AsmDeleteAsync(raw, rec, "enclosures", "enclosure", encId).ConfigureAwait(false);
        }
    }

    /// <summary>R4b · R5 — 프리셋 등록 창(RegisterFromPresetViewModel)으로 만든다.</summary>
    [ExtraStep("assembly-vm", 11)]
    static async Task AssemblyVm_RegisterFromPreset(ExtraContext ctx)
    {
        var boot = ctx.Boot; var rec = ctx.Rec; var raw = ctx.Raw;
        int encId = 0, camId = 0, gateId = 0;
        string storeDir = "";
        try
        {
            var provider = new NullDeviceProvider();
            var catalog = new CatalogService(ctx.DeviceApi, ctx.Policy, boot.Log);
            await catalog.EnsureLoadedAsync().ConfigureAwait(false);
            var registrar = new PresetRegistrar(ctx.DeviceApi, provider, boot.Log, ctx.Policy);
            var store = AsmTempStore(out storeDir);
            Func<EnumDeviceCategory, IReadOnlyCollection<int>> noneUsed = _ => Array.Empty<int>();

            // ================= R4b: a preset's enabled:true survives registration and a no-change panel save =================
            boot.Wire.CurrentTag = ASM_TAG + "/R4b";
            var composed = new DevicePreset
            {
                Id = "lrtasm0002",
                Name = "LRT-ASM-COMPOSED",
                Category = EnumDeviceCategory.Enclosure,
                TypeAxisCode = "Outdoor",
                Components = new List<MonModels.ComponentDefinitionModel>
                {
                    new() { Key = "heater_1", Type = "HEATER" },
                    new() { Key = "fan_1", Type = "FAN" },
                },
                ComponentOverrides = new JObject { ["heater_1"] = new JObject { ["enabled"] = true } },
            };
            var regVm = new RegisterFromPresetViewModel(store, catalog, registrar, Array.Empty<MonModels.IControllerDeviceModel>(), noneUsed, EnumDeviceCategory.Enclosure, composed);
            await ((IActivate)regVm).ActivateAsync().ConfigureAwait(false);
            regVm.DeviceNumber = "96702";
            regVm.DeviceName = "LRT-ASM-ENC-REG";
            regVm.IpAddress = "10.66.7.2";
            regVm.IpPort = "9702";
            var before = rec.LastSeq();
            await regVm.RegisterAsync().ConfigureAwait(false);
            encId = regVm.RegisteredDeviceId ?? 0;
            if (encId > 0) rec.Created("enclosure", encId);
            var post = rec.Since(before).FirstOrDefault(w => w.Method == "POST");
            var postBody = AsmParse(post?.RequestBody);

            MonModels.EnclosureDeviceModel? model = null;
            if (encId > 0)
            {
                var got = await ctx.DeviceApi.GetEnclosureByIdAsync(encId).ConfigureAwait(false);
                model = got.Success && got.Data != null ? got.Data.ToEnclosureDeviceModel() : null;
            }
            // fan_1 carries no intent in the preset (and the FAN definition declares no default) -> nothing may be invented.
            var postFanEnabled = postBody?.SelectToken("device_config.component_overrides.fan_1.enabled");
            JObject? regOv = null;
            if (encId > 0) { var (_, rj) = await raw.Get($"devices/enclosures/{encId}").ConfigureAwait(false); regOv = Obj(Obj(Obj(rj["data"])?["device_config"])?["component_overrides"]); }
            var serverFanEnabled = (regOv?["fan_1"] as JObject)?["enabled"];
            var regOk = encId > 0 && (bool?)postBody?.SelectToken("device_config.component_overrides.heater_1.enabled") == true
                        && model?.HeaterComponentKey == "heater_1" && model.HeaterEnabled
                        && (postFanEnabled == null || postFanEnabled.Type == JTokenType.Null)
                        && (serverFanEnabled == null || serverFanEnabled.Type == JTokenType.Null);
            rec.Add("asm.R4b1", ASM_TAG, "register window: preset heater_1.enabled:true is POSTed and reads back as HeaterEnabled=true; fan_1 (no preset intent) gets no invented enabled:false",
                regOk ? Verdict.PASS : Verdict.FAIL,
                $"msg='{regVm.Message}' problems=[{string.Join(" / ", regVm.Problems)}] id={encId}; POST overrides={J(postBody?.SelectToken("device_config.component_overrides"))}; " +
                $"model HeaterComponentKey={model?.HeaterComponentKey} HeaterEnabled={model?.HeaterEnabled} FanComponentKey={model?.FanComponentKey} FanEnabled={model?.FanEnabled}; " +
                $"server overrides after register={J(regOv)}",
                defectAt: regOk ? "" : "Devices.Ui/Consoles/Assembly/Register/PresetRequestBuilder.cs Build / RegisterFromPresetViewModel.RegisterAsync",
                seqs: SeqsSince(rec, before));

            if (model != null)
            {
                boot.Wire.CurrentTag = ASM_TAG + "/R4b-panel";
                var dto = model.ToEnclosureDeviceDto();           // no change - operator just presses save
                await StampUnitAsync(dto, "assembly-vm/R4b-panel", boot.Log).ConfigureAwait(false);
                before = rec.LastSeq();
                var saved = await ctx.DeviceApi.UpdateEnclosureAsync(encId, dto).ConfigureAwait(false);
                var patch = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
                var body = AsmParse(patch?.RequestBody);
                var (_, after) = await raw.Get($"devices/enclosures/{encId}").ConfigureAwait(false);
                var ov = Obj(Obj(Obj(after["data"])?["device_config"])?["component_overrides"]);
                var panelOk = saved.Success && (bool?)body?.SelectToken("device_config.component_overrides.heater_1.enabled") == true
                              && (bool?)ov?["heater_1"]?["enabled"] == true;
                rec.Add("asm.R4b2", ASM_TAG, "a no-change panel save of that enclosure still sends and keeps heater_1.enabled:true",
                    panelOk ? Verdict.PASS : Verdict.FAIL,
                    $"success={saved.Success} msg='{saved.Message}' HTTP {patch?.Status}; sent overrides={J(body?.SelectToken("device_config.component_overrides"))}; server overrides={J(ov)}",
                    defectAt: panelOk ? "" : "Devices.Ui/Helpers/DtoToModelHelper.cs ToEnclosureDeviceDto / Messages EnclosureDeviceDto.DeviceConfigAxis",
                    seqs: SeqsSince(rec, before));

                // Observation only (not assembly-owned): the panel's model holds FanEnabled as a plain bool, so a component
                // with NO intent on the server is saved back as an explicit enabled:false by a no-change panel save.
                rec.Add("asm.R4b3", ASM_TAG, "observation: no-change PANEL save and a component without intent (fan_1)", Verdict.INFO,
                    $"server fan_1 before save={J(regOv?["fan_1"])}; panel save sent fan_1={J(body?.SelectToken("device_config.component_overrides.fan_1"))}; " +
                    $"server fan_1 after save={J(ov?["fan_1"])} — the panel model (EnclosureDeviceModel.FanEnabled: bool) cannot say 'no intent'; " +
                    "path = Devices.Ui/Helpers/DtoToModelHelper.cs ToEnclosureDeviceDto + Messages EnclosureDeviceDto.SetEnabledIfPresent (not assembly-owned)");
            }

            // ================= R5a: camera seed =================
            boot.Wire.CurrentTag = ASM_TAG + "/R5-camera";
            var camVm = new RegisterFromPresetViewModel(store, catalog, registrar, Array.Empty<MonModels.IControllerDeviceModel>(), noneUsed, EnumDeviceCategory.Camera);
            await ((IActivate)camVm).ActivateAsync().ConfigureAwait(false);
            camVm.SelectedPreset = camVm.Presets.FirstOrDefault(p => p.Id == DevicePresetSeeds.CameraPtzColdId);
            camVm.DeviceNumber = "96703";
            camVm.DeviceName = "LRT-ASM-CAM-REG";
            camVm.IpAddress = "10.66.7.3";
            camVm.IpPort = "80";
            camVm.UserName = "lrt";
            camVm.UserPassword = "lrt-pass";
            before = rec.LastSeq();
            await camVm.RegisterAsync().ConfigureAwait(false);
            camId = camVm.RegisteredDeviceId ?? 0;
            if (camId > 0) rec.Created("camera", camId);
            var camPost = rec.Since(before).FirstOrDefault(w => w.Method == "POST");
            var camBody = AsmParse(camPost?.RequestBody);
            var sentModes = Obj(camBody?.SelectToken("device_config.modes"));
            JObject? camData = null;
            if (camId > 0) { var (_, cj) = await raw.Get($"devices/cameras/{camId}").ConfigureAwait(false); camData = Obj(cj["data"]); }
            var camModes = Obj(Obj(camData?["device_config"])?["modes"]);
            var camKeys = AsmKeys(camData);
            var camOk = camId > 0 && sentModes != null && Str(sentModes["camera_mode"]) == "NORMAL" && (bool?)sentModes["is_record"] == true
                        && Str(camData?["type_camera"]) == "PTZ"
                        && camKeys.SequenceEqual(new[] { "heater", "ir", "nic", "ptz", "wiper" })
                        && Str(camModes?["camera_mode"]) == "NORMAL" && Str(camModes?["day_night_mode"]) == "AUTO" && (bool?)camModes?["is_record"] == true;
            rec.Add("asm.R5a", ASM_TAG, "register window, camera seed: POST carries device_config.modes; re-GET type axis, components, modes",
                camOk ? Verdict.PASS : Verdict.FAIL,
                $"msg='{camVm.Message}' problems=[{string.Join(" / ", camVm.Problems)}] id={camId}; sent modes={J(sentModes)} connection={J(camBody?["connection"])}; " +
                $"server type_camera={Str(camData?["type_camera"])} components=[{string.Join(",", camKeys)}] modes={J(camModes)}; " +
                $"response={Recorder.Trunc(camPost?.ResponseBodyRedacted ?? "(none)", 300)}",
                defectAt: camOk ? "" : "Devices.Ui/Consoles/Assembly/Register/PresetRequestBuilder.cs Build (camera)",
                seqs: SeqsSince(rec, before));

            // ================= R5b: gate seed =================
            boot.Wire.CurrentTag = ASM_TAG + "/R5-gate";
            var gateVm = new RegisterFromPresetViewModel(store, catalog, registrar, Array.Empty<MonModels.IControllerDeviceModel>(), noneUsed, EnumDeviceCategory.Gate);
            await ((IActivate)gateVm).ActivateAsync().ConfigureAwait(false);
            gateVm.SelectedPreset = gateVm.Presets.FirstOrDefault(p => p.Id == DevicePresetSeeds.GateSlidingId);
            gateVm.DeviceNumber = "96704";
            gateVm.DeviceName = "LRT-ASM-GATE-REG";
            before = rec.LastSeq();
            await gateVm.RegisterAsync().ConfigureAwait(false);
            gateId = gateVm.RegisteredDeviceId ?? 0;
            if (gateId > 0) rec.Created("gate", gateId);
            var gatePost = rec.Since(before).FirstOrDefault(w => w.Method == "POST");
            var gateBody = AsmParse(gatePost?.RequestBody);
            JObject? gateData = null;
            if (gateId > 0) { var (_, gj) = await raw.Get($"devices/gates/{gateId}").ConfigureAwait(false); gateData = Obj(gj["data"]); }
            var gateKeys = AsmKeys(gateData);
            var gateOk = gateId > 0 && Str(gateData?["type_gate"]) == "Sliding"
                         && gateKeys.SequenceEqual(new[] { "actuator", "door", "limit_close", "limit_open", "nic" });
            rec.Add("asm.R5b", ASM_TAG, "register window, gate seed: POST succeeds; re-GET type axis + components (connection: whatever the window can send)",
                gateOk ? Verdict.PASS : Verdict.FAIL,
                $"msg='{gateVm.Message}' problems=[{string.Join(" / ", gateVm.Problems)}] id={gateId}; ShowsConnection={gateVm.ShowsConnection}; " +
                $"POST has connection={gateBody?["connection"] != null} link_info={gateBody?["link_info"] != null} ({J(gateBody?["connection"])}); " +
                $"server type_gate={Str(gateData?["type_gate"])} components=[{string.Join(",", gateKeys)}] connection={J(gateData?["connection"])}; " +
                $"response={Recorder.Trunc(gatePost?.ResponseBodyRedacted ?? "(none)", 300)}",
                defectAt: gateOk ? "" : "Devices.Ui/Consoles/Assembly/Register/PresetRequestBuilder.cs Build (gate)",
                seqs: SeqsSince(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("asm!!", ASM_TAG, "register-from-preset round trip", Verdict.BLOCKED, ex.ToString(), blocked: "harness exception");
        }
        finally
        {
            await AsmDeleteAsync(raw, rec, "enclosures", "enclosure", encId).ConfigureAwait(false);
            await AsmDeleteAsync(raw, rec, "cameras", "camera", camId).ConfigureAwait(false);
            await AsmDeleteAsync(raw, rec, "gates", "gate", gateId).ConfigureAwait(false);
            if (storeDir.Length > 0) { try { Directory.Delete(storeDir, true); } catch (IOException) { } }
        }
    }

    #region - assembly-vm helpers -
    /// <summary>GET(제품 서비스) → 모델 → ForDevice → 활성화. 조립기 창을 새로 여는 것과 같다.</summary>
    static async Task<AssemblyViewModel?> AsmOpenForDeviceAsync(ExtraContext ctx, int id, CatalogService catalog, DevicePresetStore store, ComponentApplyService applySvc)
    {
        var got = await ctx.DeviceApi.GetEnclosureByIdAsync(id).ConfigureAwait(false);
        if (!got.Success || got.Data == null) return null;
        var model = got.Data.ToEnclosureDeviceModel();
        var vm = AssemblyViewModel.ForDevice(model, EnumDeviceCategory.Enclosure, catalog, store, applySvc, new AsmHarnessDialogs());
        await ((IActivate)vm).ActivateAsync().ConfigureAwait(false);
        return vm;
    }

    static DevicePresetStore AsmTempStore(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "lrt-asm-" + Guid.NewGuid().ToString("N"));
        var store = new DevicePresetStore(Path.Combine(dir, "presets.json"));
        store.Load();
        return store;
    }

    static async Task AsmDeleteAsync(Raw raw, Recorder rec, string path, string kind, int id)
    {
        if (id <= 0) return;
        var (s, _) = await raw.Delete($"devices/{path}/{id}").ConfigureAwait(false);
        if (s == 200 || s == 204) rec.Deleted(kind, id); else rec.Leftover(kind, id, $"DELETE {s}");
    }

    static JObject? AsmParse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try { return JObject.Parse(body); } catch (JsonException) { return null; }
    }

    static List<string> AsmKeys(JObject? data)
        => ((Obj(data?["hardware_spec"])?["components"] as JArray) ?? new JArray())
            .Select(c => Str(c["key"])).OrderBy(x => x, StringComparer.Ordinal).ToList();

    /// <summary>와이어로 받은 /devices/spec 에서 그 유형의 줄을 그대로 — 단위 테스트 픽스처의 원본.</summary>
    static string AsmCatalogEntry(string? specBody, string code)
    {
        var jo = AsmParse(specBody);
        var list = jo?.SelectToken("data.vocabularies.component_type") as JArray;
        var hit = list?.FirstOrDefault(e => Str(e["code"]) == code);
        return hit == null ? "(not found)" : hit.ToString(Formatting.None);
    }

    sealed class AsmHarnessDialogs : IAssemblyDialogs
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);
        public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult<string?>(null);
        public Task<RepeatExpandSpec?> AskRepeatExpandAsync(IReadOnlyList<PaletteItemViewModel> palette, PaletteItemViewModel? preselected, IReadOnlyCollection<string> existingKeys)
            => Task.FromResult<RepeatExpandSpec?>(null);
        public Task<int?> OpenRegisterAsync(DevicePreset preset) => Task.FromResult<int?>(null);
    }
    #endregion
}
