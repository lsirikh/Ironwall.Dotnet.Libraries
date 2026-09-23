using System.Reflection;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Newtonsoft.Json.Linq;
using MonModels = Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    // ================= item 11 (D-13): unit preservation on edit =================
    // Ledger decision D-2026-09-23-1b2e76: any change that touches server data is verified
    // against the REAL API on loopback, never mocks alone. D-13 (unit-membership commit on
    // v2.6, search "소속 부대") is the one change still verified only by mocks - its own report
    // said plainly "실제 다부대 서버(total>1)로는 확인하지 못했다". This item closes that gap.
    //
    // Before D-13, UnitScopeGate stamped THIS CLIENT'S OWN unit onto every device write body.
    // Editing another unit's device silently reassigned it to the client's unit. The fix:
    // preserve unit_id already on the DTO; stamp the client's own unit only when it is null.
    // Item7_UnitScope (Steps.Core.cs) already proves the "stamp when null" half against the
    // single pre-seeded unit (unit001). It cannot prove "preserve" - that needs a SECOND unit
    // whose id differs from the client's own, which is exactly what this item creates.
    //
    // UnitScopeGate itself is `internal` to Ironwall.Dotnet.Libraries.Devices.Ui - this harness
    // is a separate assembly with no InternalsVisibleTo (and adding one would touch Devices.Ui,
    // out of scope for this harness). Devices.Ui/**'s own panel ViewModels call it directly
    // (CreateControllerAsync/UpdateControllerAsync etc. in ControllerDevicePanelViewModel), but
    // those are `private` methods wired to a WPF button click (RoutedEventArgs, a populated
    // selection grid) - unusable headlessly. Reflection on the internal static method is the
    // narrowest way to run the EXACT product code (not a reimplementation of its logic) without
    // touching Devices.Ui or standing up a full WPF panel. See StampUnitAsync below.
    public static async Task Item11_UnitPreservation(
        Bootstrap boot, Recorder rec, Raw raw, IDeviceApiService deviceApi, IUnitApiService unitApi, DeviceQueryPolicy policy)
    {
        const string TAG = "D-13";
        int unitBId = 0, ctrlId = 0, encId = 0, negCtrlId = 0;
        try
        {
            // ---------- 11.0: arrange a SECOND unit, distinct from the client's own ----------
            boot.Wire.CurrentTag = TAG + "/unit-b";
            var unitBRes = await unitApi.CreateUnitAsync(new UnitCreateDto
            {
                Name = "LRT-N11-UNIT-B",
                Code = "lrt11unitb",
                IsEnable = true,
            }).ConfigureAwait(false);
            if (!unitBRes.Success || unitBRes.Data == null)
            {
                rec.Add("11.0", TAG, "arrange: a second unit (B), distinct from the client's own unit", Verdict.BLOCKED,
                    $"success={unitBRes.Success} msg='{unitBRes.Message}'",
                    blocked: "server rejected a second unit - cannot prove multi-unit preservation for real");
                return;
            }
            unitBId = unitBRes.Data.Id;
            rec.Created("unit", unitBId);

            // cached from Item7_UnitScope's earlier resolve - no extra network round trip.
            var ownUnitId = await UnitScope.ResolveAsync().ConfigureAwait(false);
            if (ownUnitId == null || ownUnitId == unitBId)
            {
                rec.Add("11.0b", TAG, "arrange: own unit resolved and is DIFFERENT from B", Verdict.BLOCKED,
                    $"ownUnitId={ownUnitId} unitB={unitBId}",
                    blocked: "own unit did not resolve, or collided with B - the whole test would be vacuous");
                return;
            }

            // ---------- 11.1: seed a controller directly IN unit B (raw - arrange only) ----------
            var (cs, cj) = await raw.Post("devices/controllers", new
            {
                type_controller = "Controller",
                number_device = 96500,
                name_device = "LRT-N11-CTRL",
                connection = new { ip_address = "10.66.5.1", ip_port = 9650 },
                unit_id = unitBId,
            }).ConfigureAwait(false);
            if (cs != 201)
            {
                rec.Add("11.1", TAG, "arrange: seed a controller in unit B", Verdict.BLOCKED, $"HTTP {cs} {Short(cj)}", blocked: "fixture setup failed");
                return;
            }
            ctrlId = (int)cj["data"]["id"];
            rec.Created("controller", ctrlId);
            var seedUnit = (int?)Obj(cj["data"])?["unit_id"];
            if (seedUnit != unitBId)
            {
                rec.Add("11.1b", TAG, "arrange sanity: the seeded controller really landed in unit B", Verdict.BLOCKED,
                    $"server unit_id={seedUnit} expected={unitBId}", blocked: "seed did not land in unit B - cannot test preservation");
                return;
            }

            // ---------- 11a: edit through the REAL product path ----------
            // Dto (GET) -> Model (DtoToModelHelper.ToControllerDeviceModel) -> edit a harmless
            // field -> Dto (ToControllerDeviceDto) -> UnitScopeGate.StampAsync -> IDeviceApiService PATCH.
            // This is exactly what ControllerDevicePanelViewModel.UpdateControllerAsync does.
            boot.Wire.CurrentTag = TAG + "/edit";
            var before = rec.LastSeq();
            var got = await deviceApi.GetControllerByIdAsync(ctrlId).ConfigureAwait(false);
            if (!got.Success || got.Data == null)
            {
                rec.Add("11a", TAG, "edit: GetControllerByIdAsync", Verdict.BLOCKED, $"success={got.Success} msg='{got.Message}'", blocked: "fixture read failed");
                return;
            }
            var model = got.Data.ToControllerDeviceModel();
            var modelUnitOk = model.UnitId == unitBId;
            model.DeviceName = "LRT-N11-CTRL-EDITED";
            var editDto = model.ToControllerDeviceDto();
            var dtoUnitBeforeStamp = editDto.UnitId;
            await StampUnitAsync(editDto, nameof(Item11_UnitPreservation) + "/edit", boot.Log).ConfigureAwait(false);
            var dtoUnitOk = editDto.UnitId == unitBId;
            var patched = await deviceApi.UpdateControllerAsync(ctrlId, editDto).ConfigureAwait(false);
            var patchWire = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH" || w.Method == "PUT");
            var (_, after) = await raw.Get($"devices/controllers/{ctrlId}").ConfigureAwait(false);
            var afterUnit = (int?)Obj(after["data"])?["unit_id"];
            var afterName = Str(Obj(after["data"])?["name_device"]);
            var editOk = patched.Success && modelUnitOk && dtoUnitOk && afterUnit == unitBId && afterName == "LRT-N11-CTRL-EDITED";
            rec.Add("11a", TAG,
                "다른 부대(B) 소속 장비를 실제 제품 경로(Dto→Model→편집→Model→Dto→UnitScopeGate.StampAsync→PATCH)로 수정해도 소속이 보존된다(D-13)",
                editOk ? Verdict.PASS : Verdict.FAIL,
                $"model.UnitId(read-back)={model.UnitId} expected={unitBId}; dto.UnitId before StampAsync={dtoUnitBeforeStamp} after={editDto.UnitId}; " +
                $"own unit(client)={ownUnitId}; patch success={patched.Success} msg='{patched.Message}'; server unit_id after edit={afterUnit}; name={afterName}; " +
                $"PATCH body = {(patchWire == null ? "(no PATCH/PUT captured)" : Recorder.Trunc(patchWire.RequestBodyRedacted, 400))}",
                defectAt: editOk ? "" : "Devices.Ui/Helpers/DtoToModelHelper.cs ToControllerDeviceDto / UnitScopeGate.StampAsync",
                seqs: SeqsSince(rec, before));

            // ---------- 11b: ComponentApplyService (the path that bypasses the model) ----------
            // ComponentApplyService.PatchAsync builds a FRESH Dto itself (CopyCommon copies
            // source.UnitId from a re-fetched server Dto, not from any model) and calls the same
            // internal UnitScopeGate.StampAsync - this is real product code, no reflection needed
            // (ComponentApplyService lives inside Devices.Ui and already has internal access).
            //
            // Seeding goes through raw (arrange only), NOT EnclosureDeviceDto.ToEnclosureDeviceDto()
            // + CreateEnclosureAsync - a first attempt at that hit an UNRELATED live defect: when
            // HardwareSpecStore is null (no hardware_spec.components declared, i.e. every plain
            // "create enclosure" that does not go through PresetRegistrar),
            // EnclosureDeviceDto.DeviceConfigAxis unconditionally emits device_config.component_
            // overrides.heater/fan (SetEnabledIfPresent's null-HardwareSpecStore branch), and the
            // live 8.0.2 server now 422s with VALUE_NOT_ALLOWED ("'heater' 는 이 장비가 선언한
            // 부품이 아닙니다") - reproduced with wire bytes, reported below, NOT fixed (Messages/**
            // is out of scope here). Routing this fixture's seed through raw sidesteps it without
            // weakening what 11b actually asserts (ComponentApplyService's own unit preservation).
            boot.Wire.CurrentTag = TAG + "/component-apply";

            // ---- 11b0: reproduce the unrelated defect through the REAL product path (wire evidence) ----
            // Not part of the D-13 assertion - a plain "create enclosure with no components declared
            // yet" through the real DtoToModelHelper + CreateEnclosureAsync path, exactly what
            // EnclosureDevicePanelViewModel.CreateEnclosureAsync does for any enclosure not built
            // from a preset. Recorded so the wire bytes are captured even though 11b itself routes
            // around it via raw seeding (see comment below).
            var reproModel = new MonModels.EnclosureDeviceModel
            {
                DeviceNumber = 96511,
                DeviceName = "LRT-N11-ENC-REPRO",
                UnitId = unitBId,
                IpAddress = "10.66.5.9",
                IpPort = 9659,
                // HardwareSpec intentionally left unset - HardwareSpecStore stays null.
            };
            var reproDto = reproModel.ToEnclosureDeviceDto();
            await StampUnitAsync(reproDto, nameof(Item11_UnitPreservation) + "/repro-enc-create", boot.Log).ConfigureAwait(false);
            var reproBefore = rec.LastSeq();
            var reproResult = await deviceApi.CreateEnclosureAsync(reproDto).ConfigureAwait(false);
            var reproWire = rec.Since(reproBefore).FirstOrDefault(w => w.Method == "POST");
            if (reproResult.Success && reproResult.Data != null)
            {
                // future server fix - the defect is gone, clean up and record PASS.
                var reproId = reproResult.Data.Id;
                rec.Created("enclosure", reproId);
                await raw.Delete($"devices/enclosures/{reproId}").ConfigureAwait(false);
                rec.Deleted("enclosure", reproId);
                rec.Add("11b0", TAG, "(부수 발견, D-13 과 무관) 부품 미선언 함체 생성이 이제는 성공한다 — 이전 결함이 해소됨",
                    Verdict.PASS, $"create success=True; body = {Recorder.Trunc(reproWire?.RequestBodyRedacted ?? "(none)", 300)}");
            }
            else
            {
                var body = reproWire?.ResponseBodyRedacted ?? "";
                var isKnownShape = body.Contains("VALUE_NOT_ALLOWED") && body.Contains("component_overrides");
                rec.Add("11b0", TAG,
                    "(부수 발견, D-13 과 무관) hardware_spec.components 를 선언하지 않고 함체를 생성하면(프리셋 미경유, 패널의 평범한 '함체 등록') " +
                    "device_config.component_overrides.heater/fan 이 무조건 실려 서버가 422 를 낸다",
                    isKnownShape ? Verdict.FAIL : Verdict.BLOCKED,
                    $"create success={reproResult.Success} msg='{reproResult.Message}'; POST body = {Recorder.Trunc(reproWire?.RequestBodyRedacted ?? "(none)", 400)}; " +
                    $"response = {Recorder.Trunc(body, 500)}",
                    defectAt: isKnownShape ? "Messages/Dto/Devices/EnclosureDeviceDto.cs SetEnabledIfPresent (HardwareSpecStore==null branch unconditionally emits heater/fan overrides)" : "",
                    blocked: isKnownShape ? "" : "create failed for a different reason than the suspected defect - see response",
                    seqs: SeqsSince(rec, reproBefore));
            }

            var (encStatus, encJson) = await raw.Post("devices/enclosures", new
            {
                number_device = 96510,
                name_device = "LRT-N11-ENC",
                connection = new { ip_address = "10.66.5.2", ip_port = 9651 },
                unit_id = unitBId,
            }).ConfigureAwait(false);
            if (encStatus != 201)
            {
                rec.Add("11b", TAG, "arrange: enclosure seeded directly in unit B (raw)", Verdict.BLOCKED,
                    $"HTTP {encStatus} {Short(encJson)}", blocked: "fixture setup failed");
            }
            else
            {
                encId = (int)encJson["data"]["id"];
                rec.Created("enclosure", encId);

                var provider = new NullDeviceProvider();
                var applySvc = new ComponentApplyService(deviceApi, provider, boot.Log, policy);
                var device = (MonModels.IBaseDeviceModel)new MonModels.EnclosureDeviceModel { Id = encId, CategoryDevice = EnumDeviceCategory.Enclosure };
                var desired = new List<MonModels.ComponentDefinitionModel> { new() { Key = "heater_1", Type = "HEATER", Label = "히터 1" } };
                before = rec.LastSeq();
                var applyRes = await applySvc.ApplyAsync(device, new List<MonModels.ComponentDefinitionModel>(), desired, null).ConfigureAwait(false);
                var applyPatchWire = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
                var (_, encAfter) = await raw.Get($"devices/enclosures/{encId}").ConfigureAwait(false);
                var encUnitAfter = (int?)Obj(encAfter["data"])?["unit_id"];
                var encCompsAfter = Obj(Obj(encAfter["data"])?["hardware_spec"])?["components"] as JArray ?? new JArray();
                var applyOk = applyRes.IsSuccess && encUnitAfter == unitBId && encCompsAfter.Count == 1;
                rec.Add("11b", TAG,
                    "ComponentApplyService(모델을 거치지 않는 경로)도 CopyCommon + UnitScopeGate.StampAsync 로 소속 부대(B)를 보존한다(D-13)",
                    applyOk ? Verdict.PASS : Verdict.FAIL,
                    $"apply success={applyRes.IsSuccess} msg='{applyRes.Message}'; server unit_id after apply={encUnitAfter} expected={unitBId}; " +
                    $"components after = {encCompsAfter.Count}; PATCH body = {(applyPatchWire == null ? "(none)" : Recorder.Trunc(applyPatchWire.RequestBodyRedacted, 400))}",
                    defectAt: applyOk ? "" : "Devices.Ui/Consoles/Assembly/Register/ComponentApplyService.cs CopyCommon",
                    seqs: SeqsSince(rec, before));
            }

            // ---------- 11c: negative control - create with NO unit_id gets the CLIENT'S OWN unit ----------
            // Proves the other half of the branch this item exists to verify: "stamp when null"
            // must still fire. If this regressed to "never stamp", 11a/11b would trivially pass
            // for the wrong reason (nothing overwritten because nothing is ever stamped).
            boot.Wire.CurrentTag = TAG + "/negative-control";
            before = rec.LastSeq();
            var negModel = new MonModels.ControllerDeviceModel
            {
                DeviceNumber = 96520,
                DeviceName = "LRT-N11-CTRL-NEG",
                DeviceType = EnumDeviceType.Controller,
                IpAddress = "10.66.5.3",
                Port = 9652,
                // UnitId intentionally left null - the "we don't know which unit this belongs to" case.
            };
            var negDto = negModel.ToControllerDeviceDto();
            var negDtoUnitWasNull = negDto.UnitId == null;
            await StampUnitAsync(negDto, nameof(Item11_UnitPreservation) + "/create-no-unit", boot.Log).ConfigureAwait(false);
            var negCreated = await deviceApi.CreateControllerAsync(negDto).ConfigureAwait(false);
            var negWire = rec.Since(before).FirstOrDefault(w => w.Method == "POST");
            if (negCreated.Success && negCreated.Data != null) { negCtrlId = negCreated.Data.Id; rec.Created("controller", negCtrlId); }
            var (_, negAfter) = negCtrlId > 0 ? await raw.Get($"devices/controllers/{negCtrlId}").ConfigureAwait(false) : (0, new JObject());
            var negUnitAfter = (int?)Obj(negAfter["data"])?["unit_id"];
            var negOk = negDtoUnitWasNull && negCreated.Success && negDto.UnitId == ownUnitId && negUnitAfter == ownUnitId;
            rec.Add("11c", TAG, "음성 대조: unit_id 없이 새로 만들면 이 클라이언트 자신의 부대로 찍힌다(null 일 때만 채우는 분기)",
                negOk ? Verdict.PASS : Verdict.FAIL,
                $"dto.UnitId before StampAsync was null={negDtoUnitWasNull}; after StampAsync={negDto.UnitId} expected(own)={ownUnitId}; " +
                $"create success={negCreated.Success} msg='{negCreated.Message}'; server unit_id={negUnitAfter}; " +
                $"POST body = {(negWire == null ? "(none)" : Recorder.Trunc(negWire.RequestBodyRedacted, 400))}",
                defectAt: negOk ? "" : "Devices.Ui/Helpers/UnitScopeGate.cs StampAsync (stamp-when-null branch)",
                seqs: SeqsSince(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("11!", TAG, "unit preservation round trip", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
        finally
        {
            if (negCtrlId > 0)
            {
                var (s, _) = await raw.Delete($"devices/controllers/{negCtrlId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("controller", negCtrlId); else rec.Leftover("controller", negCtrlId, $"DELETE {s}");
            }
            if (encId > 0)
            {
                var (s, _) = await raw.Delete($"devices/enclosures/{encId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("enclosure", encId); else rec.Leftover("enclosure", encId, $"DELETE {s}");
            }
            if (ctrlId > 0)
            {
                var (s, _) = await raw.Delete($"devices/controllers/{ctrlId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("controller", ctrlId); else rec.Leftover("controller", ctrlId, $"DELETE {s}");
            }
            if (unitBId > 0)
            {
                var (s, _) = await raw.Delete($"units/{unitBId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("unit", unitBId); else rec.Leftover("unit", unitBId, $"DELETE {s}");
            }
        }
    }

    // ---------- reflection shim: UnitScopeGate.StampAsync is `internal` to Devices.Ui ----------
    static readonly Lazy<MethodInfo> _stampAsyncMethod = new(() =>
    {
        var asm = typeof(DtoToModelHelper).Assembly; // Ironwall.Dotnet.Libraries.Devices.Ui
        var type = asm.GetType("Ironwall.Dotnet.Libraries.Devices.Ui.Helpers.UnitScopeGate", throwOnError: true)!;
        var method = type.GetMethod("StampAsync", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException("UnitScopeGate.StampAsync not found by reflection - product code moved/renamed it.");
        return method;
    });

    /// <summary>
    /// Invokes the REAL internal <c>UnitScopeGate.StampAsync(dto, caller, log, token)</c> via
    /// reflection - not a reimplementation of its logic. Necessary because the harness is a
    /// separate assembly with no InternalsVisibleTo into Devices.Ui (adding one would touch
    /// Devices.Ui, out of scope), and the gate's only other callers are `private` panel-VM
    /// methods wired to a WPF button click.
    /// </summary>
    static async Task StampUnitAsync(BaseDeviceDto dto, string caller, ILogService log)
    {
        var task = (Task)_stampAsyncMethod.Value.Invoke(null, new object?[] { dto, caller, log, CancellationToken.None })!;
        await task.ConfigureAwait(false);
    }
}
