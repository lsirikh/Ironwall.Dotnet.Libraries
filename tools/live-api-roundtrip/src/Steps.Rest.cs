using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;
using Newtonsoft.Json.Linq;
using MonModels = Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    // ================= item 3: N-03 component apply + register-from-preset =================
    public static async Task Item3_Components(Bootstrap boot, Recorder rec, Raw raw, IDeviceApiService api, DeviceQueryPolicy policy)
    {
        const string TAG = "N-03";
        int encId = 0, presetId = 0;
        try
        {
            var provider = new NullDeviceProvider();

            // ---------- 3a/3b: register FROM PRESET (2 components) ----------
            boot.Wire.CurrentTag = TAG + "/preset";
            var preset = new DevicePreset
            {
                Id = "lrtpreset0001",
                Name = "LRT-N03-PRESET",
                Category = EnumDeviceCategory.Enclosure,
                TypeAxisCode = "Outdoor",
                Manufacturer = "ACME-PRESET",
                Model = "ENC-9000",
                Components = new List<MonModels.ComponentDefinitionModel>
                {
                    new() { Key = "heater_1", Type = "HEATER", Label = "히터 1" },
                    new() { Key = "fan_1",    Type = "FAN",    Label = "팬 1" },
                },
                ComponentOverrides = new JObject
                {
                    ["heater_1"] = new JObject { ["enabled"] = true },
                    ["fan_1"] = new JObject { ["enabled"] = true },
                },
            };
            var info = new PresetInstanceInfo
            {
                DeviceNumber = 96300,
                DeviceName = "LRT-N03-ENC",
                Description = "KEEP-DESCRIPTION",
                IpAddress = "10.66.2.1",
                IpPort = 9620,
            };

            var request = PresetRequestBuilder.Build(preset, info);
            var registrar = new PresetRegistrar(api, provider, boot.Log, policy);
            var before = rec.LastSeq();
            var reg = await registrar.RegisterAsync(request).ConfigureAwait(false);
            presetId = reg.NewDeviceId ?? 0;
            if (presetId > 0) { encId = presetId; rec.Created("enclosure", presetId); }

            var (_, got) = presetId > 0
                ? await raw.Get($"devices/enclosures/{presetId}?include=hardware_spec,device_config,connection").ConfigureAwait(false)
                : (0, new JObject());
            var data = Obj(got["data"]);
            var comps = Obj(data?["hardware_spec"])?["components"] as JArray ?? new JArray();
            var keys = comps.Select(c => Str(c["key"])).OrderBy(x => x).ToList();
            var regOk = reg.IsSuccess && presetId > 0 && keys.SequenceEqual(new[] { "fan_1", "heater_1" });
            rec.Add("3a", TAG, "register-from-preset: device created with its 2 components attached",
                regOk ? Verdict.PASS : Verdict.FAIL,
                $"result='{reg.Message}' newId={presetId}; server components = [{string.Join(",", keys)}]",
                defectAt: regOk ? "" : "Devices.Ui/Consoles/Assembly/Register/PresetRegistrar.cs RegisterAsync",
                seqs: SeqsSince(rec, before));

            var ov = Obj(Obj(data?["device_config"])?["component_overrides"]);
            var man = Str(Obj(data?["hardware_spec"])?["manufacturer"]);
            var desc = Str(data?["description"]);
            var regWire = rec.Since(before).FirstOrDefault(w => w.Method == "POST");
            var sentBody = regWire?.RequestBody ?? "";
            var sentDesc = JsonHas(sentBody, "description");
            var sentConn = JsonHas(sentBody, "connection");
            var specOk = man == "ACME-PRESET" && ov != null && ov.Count == 2;
            rec.Add("3b", TAG, "preset carries hardware_spec scalars + component_overrides",
                specOk ? Verdict.PASS : Verdict.FAIL,
                $"manufacturer={man} component_overrides={J(ov)}",
                defectAt: specOk ? "" : "Devices.Ui/Consoles/Assembly/Register/PresetRequestBuilder.cs Build");

            // PresetInstanceInfo declares Description / IpAddress / IpPort as form inputs and
            // Validate() even range-checks IpPort - but BuildCategoryDto maps them per category.
            var infoCarried = sentDesc && sentConn;
            rec.Add("3b2", TAG, "register-from-preset carries PresetInstanceInfo.Description and IpAddress/IpPort (enclosure)",
                infoCarried ? Verdict.PASS : Verdict.FAIL,
                $"POST body has description={sentDesc}, connection={sentConn}; server description='{desc}'. " +
                $"Sent body = {Recorder.Trunc(regWire?.RequestBodyRedacted ?? "(none)", 420)}",
                defectAt: infoCarried ? "" : "Devices.Ui/Consoles/Assembly/Register/PresetRequestBuilder.cs BuildCategoryDto (Enclosure branch ~line 288: EnclosureDeviceModel built without Description/IpAddress/IpPort)");

            if (presetId <= 0) return;

            // ---------- 3c/3d: apply a component change ----------
            boot.Wire.CurrentTag = TAG + "/apply";
            var device = (MonModels.IBaseDeviceModel)new MonModels.EnclosureDeviceModel
            {
                Id = presetId,
                CategoryDevice = EnumDeviceCategory.Enclosure,
            };
            var baseline = new List<MonModels.ComponentDefinitionModel>
            {
                new() { Key = "heater_1", Type = "HEATER", Label = "히터 1" },
                new() { Key = "fan_1",    Type = "FAN",    Label = "팬 1" },
            };
            // drop fan_1, change heater_1's channel
            var desired = new List<MonModels.ComponentDefinitionModel>
            {
                new() { Key = "heater_1", Type = "HEATER", Label = "히터 1", Channel = 3 },
            };
            // the removed component's override must be sent as an explicit null
            var overrides = new JObject
            {
                ["heater_1"] = new JObject { ["enabled"] = true },
                ["fan_1"] = JValue.CreateNull(),
            };

            // Independently of the 3b2 defect, put a description + connection on the device so the
            // "all fields survive" promise is tested against fields that really exist server-side.
            await raw.Patch($"devices/enclosures/{presetId}", new
            {
                description = "KEEP-DESCRIPTION",
                connection = new { ip_address = "10.66.2.1", ip_port = 9620 },
            }).ConfigureAwait(false);

            var applySvc = new ComponentApplyService(api, provider, boot.Log, policy);
            before = rec.LastSeq();
            var applyRes = await applySvc.ApplyAsync(device, baseline, desired, overrides).ConfigureAwait(false);

            var (_, after) = await raw.Get($"devices/enclosures/{presetId}?include=hardware_spec,device_config,connection").ConfigureAwait(false);
            var ad = Obj(after["data"]);
            var acomps = Obj(ad?["hardware_spec"])?["components"] as JArray ?? new JArray();
            var akeys = acomps.Select(c => Str(c["key"])).OrderBy(x => x).ToList();
            var aov = Obj(Obj(ad?["device_config"])?["component_overrides"]);
            var chan = acomps.FirstOrDefault(c => Str(c["key"]) == "heater_1")?["channel"];

            var applyOk = applyRes.IsSuccess
                && akeys.SequenceEqual(new[] { "heater_1" })
                && (int?)chan == 3
                && aov != null && aov["fan_1"] == null && aov["heater_1"] != null;
            rec.Add("3c", TAG, "component apply: fan_1 removed + its override sent as null and gone",
                applyOk ? Verdict.PASS : Verdict.FAIL,
                $"result='{applyRes.Message}'; components=[{string.Join(",", akeys)}] heater_1.channel={J(chan)} component_overrides={J(aov)}",
                defectAt: applyOk ? "" : "Devices.Ui/Consoles/Assembly/Register/ComponentApplyService.cs PatchAsync",
                seqs: SeqsSince(rec, before));

            // ---------- the "all fields" promise ----------
            var aDesc = Str(ad?["description"]);
            var aMan = Str(Obj(ad?["hardware_spec"])?["manufacturer"]);
            var aName = Str(ad?["name_device"]);
            var aIp = Str(Obj(ad?["connection"])?["ip_address"]);
            var aPort = (int?)Obj(ad?["connection"])?["ip_port"];
            var aType = Str(ad?["type_enclosure"]);
            var kept = aDesc == "KEEP-DESCRIPTION" && aMan == "ACME-PRESET" && aName == "LRT-N03-ENC"
                       && aIp == "10.66.2.1" && aPort == 9620 && aType == "Outdoor";
            rec.Add("3d", TAG, "'all fields' promise: description / manufacturer / name / connection / type axis unchanged",
                kept ? Verdict.PASS : Verdict.FAIL,
                $"description={aDesc} manufacturer={aMan} name={aName} ip={aIp}:{aPort} type_enclosure={aType}",
                defectAt: kept ? "" : "Devices.Ui/Consoles/Assembly/Register/ComponentApplyService.cs PatchAsync CopyCommon");

            // ---------- 3e: drift guard ----------
            boot.Wire.CurrentTag = TAG + "/drift";
            before = rec.LastSeq();
            var driftRes = await applySvc.ApplyAsync(device, baseline, desired, overrides).ConfigureAwait(false);
            var writes = rec.Since(before).Count(w => w.Method == "PATCH" || w.Method == "POST");
            var driftOk = driftRes.IsConflict && writes == 0;
            rec.Add("3e", TAG, "stale baseline -> conflict, nothing written",
                driftOk ? Verdict.PASS : Verdict.FAIL,
                $"IsConflict={driftRes.IsConflict} msg='{driftRes.Message}'; write calls = {writes}",
                defectAt: driftOk ? "" : "Devices.Ui/Consoles/Assembly/Register/ComponentApplyService.cs SameComponents",
                seqs: SeqsSince(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("3!", TAG, "component round trip", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
        finally
        {
            if (encId > 0)
            {
                var (s, _) = await raw.Delete($"devices/enclosures/{encId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("enclosure", encId); else rec.Leftover("enclosure", encId, $"DELETE {s}");
            }
        }
    }

    // ================= item 5: N-06 accounts =================
    public static async Task Item5_Accounts(Bootstrap boot, Recorder rec, Raw raw)
    {
        const string TAG = "N-06";
        int userId = 0, groupId = 0;
        try
        {
            var (gs, gj) = await raw.Post("user-groups", new { name = "LRT-N06-GRP", description = "live roundtrip" }).ConfigureAwait(false);
            if (gs != 200 && gs != 201) { rec.Add("5.0", TAG, "arrange user-group", Verdict.BLOCKED, $"HTTP {gs} {Short(gj)}", blocked: "fixture setup failed"); return; }
            groupId = (int)gj["data"]["id"];
            rec.Created("user-group", groupId);

            // throwaway user - never the admin we logged in with
            var (us, uj) = await raw.Post("users", new
            {
                login_id = "lrt_throwaway_u1",
                password = "LrtThrow1!x",
                name = "LRT 폐기용 계정",
                role = "USER",
            }).ConfigureAwait(false);
            if (us != 200 && us != 201) { rec.Add("5.0", TAG, "arrange user", Verdict.BLOCKED, $"HTTP {us} {Short(uj)}", blocked: "fixture setup failed"); return; }
            userId = (int)uj["data"]["id"];
            rec.Created("user", userId);

            // ---------- 5a: AssignUserGroupAsync ----------
            boot.Wire.CurrentTag = TAG + "/assign";
            var before = rec.LastSeq();
            var assign = await boot.AccountApi.AssignUserGroupAsync(userId, groupId).ConfigureAwait(false);
            var (_, uAfter) = await raw.Get($"users/{userId}").ConfigureAwait(false);
            var gid = (int?)Obj(uAfter["data"])?["group_id"];
            var assignOk = assign.Success && gid == groupId;
            rec.Add("5a", TAG, "AssignUserGroupAsync attaches the throwaway user to the group",
                assignOk ? Verdict.PASS : Verdict.FAIL,
                $"success={assign.Success} msg='{assign.Message}'; server group_id={gid} expected={groupId}",
                defectAt: assignOk ? "" : "Accounts.Api/Services/AccountApiService.cs AssignUserGroupAsync",
                seqs: SeqsSince(rec, before));

            // ---------- 5b: permission save through the real merge path ----------
            boot.Wire.CurrentTag = TAG + "/perm";
            // seed a permission set that includes a module the matrix screen may not show
            await raw.Post($"user-groups/{groupId}/permissions", FullModules(extra: true)).ConfigureAwait(false);
            var (_, gBefore) = await raw.Get($"user-groups/{groupId}").ConfigureAwait(false);
            var modsBefore = Obj(Obj(Obj(gBefore["data"])?["permissions"])?["modules"]);

            var vm = new PermissionMatrixPanelViewModel(new EventAggregator(), boot.Log, boot.AccountApi, boot.Probe);
            await vm.ReloadForConsoleAsync().ConfigureAwait(false);
            var row = vm.Groups.FirstOrDefault(g => g.GroupId == groupId);
            if (row == null)
            {
                rec.Add("5b", TAG, "permission save through PermissionMatrixPanelViewModel", Verdict.BLOCKED,
                    $"group {groupId} not present in vm.Groups ({vm.Groups.Count} rows)", blocked: "VM could not load the group");
            }
            else
            {
                vm.LoadMatrixFor(row);
                var target = vm.Modules.FirstOrDefault(m => m.ModuleKey == "devices");
                if (target != null) { target.View = true; target.Edit = true; }
                before = rec.LastSeq();
                var outcome = await vm.SaveGroupPermissionsAsync().ConfigureAwait(false);
                var saveWire = rec.Since(before).FirstOrDefault(w => w.Method == "POST" && w.Uri.Contains("/permissions"));
                var status = saveWire?.Status ?? 0;

                var (_, gAfter) = await raw.Get($"user-groups/{groupId}").ConfigureAwait(false);
                var modsAfter = Obj(Obj(Obj(gAfter["data"])?["permissions"])?["modules"]);

                var no422 = status == 200 || status == 201;
                rec.Add("5b", TAG, "group permission save through the real merge path -> no 422",
                    no422 ? Verdict.PASS : Verdict.FAIL,
                    $"HTTP {status}; modules sent = {(saveWire == null ? 0 : CountModules(saveWire.RequestBody))}; " +
                    $"response = {Recorder.Trunc(saveWire?.ResponseBodyRedacted ?? "(none)", 300)}",
                    defectAt: no422 ? "" : "Accounts.Ui/ViewModels/Panels/PermissionMatrixPanelViewModel.cs BuildMergedModules",
                    seqs: SeqsSince(rec, before));

                // modules the screen does not show must survive
                var shown = vm.Modules.Select(m => m.ModuleKey).ToHashSet(StringComparer.Ordinal);
                var hidden = modsBefore?.Properties().Select(p => p.Name).Where(n => !shown.Contains(n)).ToList() ?? new List<string>();
                var survived = hidden.Count == 0 || hidden.All(h => modsAfter?[h] != null);
                rec.Add("5c", TAG, "modules the matrix screen does not show SURVIVE the save",
                    survived ? Verdict.PASS : Verdict.FAIL,
                    $"screen shows {shown.Count} modules; server had {modsBefore?.Count ?? 0}; not-shown = [{string.Join(",", hidden)}]; " +
                    $"after = {modsAfter?.Count ?? 0} modules",
                    defectAt: survived ? "" : "Accounts.Ui/ViewModels/Panels/PermissionMatrixPanelViewModel.cs BuildMergedModules (origin union)");
            }

            // ---------- 5d: grant with timezone-aware datetimes ----------
            boot.Wire.CurrentTag = TAG + "/grant";
            var from = DateTimeOffset.Now.AddMinutes(5);
            var until = DateTimeOffset.Now.AddDays(2);
            before = rec.LastSeq();
            var grant = await boot.AccountApi.CreateGrantAsync(userId, new GrantCreateDto
            {
                GroupId = groupId,
                ValidFrom = from.DateTime,
                ValidUntil = until.DateTime,
            }).ConfigureAwait(false);
            var grantWire = rec.Since(before).FirstOrDefault(w => w.Method == "POST" && w.Uri.Contains("/grants"));
            var tzAware = grantWire != null && WireDatesAreOffsetAware(grantWire.RequestBody);
            var grantOk = grant.Success && (grantWire?.Status == 200 || grantWire?.Status == 201);
            rec.Add("5d", TAG, "grant create sends timezone-aware datetimes and is accepted",
                (grantOk && tzAware) ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {grantWire?.Status}; tz-aware={tzAware}; body = {Recorder.Trunc(grantWire?.RequestBodyRedacted ?? "(none)", 250)}; msg='{grant.Message}'",
                defectAt: (grantOk && tzAware) ? "" : "Api/Services/ApiService.cs _jsonSettings DateTimeZoneHandling / GrantCreateDto",
                seqs: SeqsSince(rec, before));

            // revoke
            if (grant.Success && grant.Data != null && grant.Data.Id > 0)
            {
                var revoke = await boot.AccountApi.DeleteGrantAsync(grant.Data.Id).ConfigureAwait(false);
                var (_, gl) = await raw.Get($"users/{userId}/grants").ConfigureAwait(false);
                var remaining = (gl["data"] as JArray)?.Count ?? -1;
                rec.Add("5e", TAG, "grant revoke removes it",
                    revoke.Success ? Verdict.PASS : Verdict.FAIL,
                    $"revoke success={revoke.Success} msg='{revoke.Message}'; remaining grants = {remaining}",
                    defectAt: revoke.Success ? "" : "Accounts.Api/Services/AccountApiService.cs DeleteGrantAsync");
            }
        }
        catch (Exception ex)
        {
            rec.Add("5!", TAG, "accounts round trip", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
        finally
        {
            if (userId > 0)
            {
                var (s, _) = await raw.Delete($"users/{userId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("user", userId); else rec.Leftover("user", userId, $"DELETE {s}");
            }
            if (groupId > 0)
            {
                var (s, _) = await raw.Delete($"user-groups/{groupId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("user-group", groupId); else rec.Leftover("user-group", groupId, $"DELETE {s}");
            }
        }
    }

    static readonly string[] ALL_MODULES =
    {
        "devices","events","reports","cameras","users","user_groups","audit_logs","servers",
        "map","broadcast","setup_system","setup_feature","action_report_templates","integrations","files","units",
    };

    static object FullModules(bool extra)
    {
        var mods = new JObject();
        foreach (var m in ALL_MODULES)
            mods[m] = new JObject { ["view"] = m == "audit_logs" && extra, ["edit"] = false, ["delete"] = false, ["control"] = false };
        return new JObject { ["modules"] = mods };
    }

    static int CountModules(string body)
    {
        try { return (JObject.Parse(body)["modules"] as JObject)?.Count ?? 0; } catch { return -1; }
    }

    /// <summary>
    /// Reads the raw wire TEXT. JObject.Parse auto-converts ISO strings to JValue(Date) and
    /// re-rendering one drops the offset - asserting on that would be trap #4 (misreading the wire).
    /// </summary>
    static bool WireDatesAreOffsetAware(string body)
    {
        try
        {
            using var r = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(body))
            { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
            var jo = JObject.Load(r);
            var f = (string)jo["valid_from"] ?? "";
            var u = (string)jo["valid_until"] ?? "";
            return HasOffset(f) && (u.Length == 0 || HasOffset(u));
        }
        catch { return false; }
    }

    static bool HasOffset(string iso)
    {
        if (iso.Length < 10) return false;
        if (iso.EndsWith("Z", StringComparison.Ordinal)) return true;
        var timePart = iso.Substring(10);
        return timePart.Contains('+') || timePart.Contains('-');
    }

    static bool JsonHas(string body, string key)
    {
        try { return JObject.Parse(body)[key] != null; } catch { return false; }
    }

    // ================= item 6: N-09 reports =================
    public static async Task Item6_Reports(Bootstrap boot, Recorder rec, Raw raw)
    {
        const string TAG = "N-09";
        int tplId = 0;
        try
        {
            var reportApi = new ReportApiService(boot.Log, boot.Api, boot.Setup);

            // arrange: a template whose saved config has a custom title and a disabled entry
            var (cs, cj) = await raw.Post("reports/templates", new
            {
                name = "LRT-N09-TPL",
                description = "live roundtrip",
                report_type = "CUSTOM",
                default_period = "7d",
                components = new object[]
                {
                    new { id = "SUMMARY_CARD",     order = 0, enabled = true,  title = "나의 요약 제목" },
                    new { id = "DEVICE_STATUS_PIE",order = 1, enabled = true },
                    new { id = "EVENT_TREND_LINE", order = 2, enabled = false, title = "꺼진 항목 제목" },
                },
            }).ConfigureAwait(false);
            if (cs != 200 && cs != 201) { rec.Add("6.0", TAG, "arrange template", Verdict.BLOCKED, $"HTTP {cs} {Short(cj)}", blocked: "fixture setup failed"); return; }
            tplId = (int)cj["data"]["id"];
            rec.Created("report-template", tplId);

            // ---------- 6a: reorder through the real board, PATCH through the real service ----------
            boot.Wire.CurrentTag = TAG + "/patch";
            var cat = await reportApi.GetComponentsAsync().ConfigureAwait(false);
            var tpl = await reportApi.GetTemplateByIdAsync(tplId).ConfigureAwait(false);
            var saved = tpl.Data?.Components;

            var board = new TemplateComponentBoard();
            board.Load(cat.Data, saved);
            board.MarkBaseline();

            var enabled = board.Items.Where(i => i.IsEnabled).ToList();
            var idxA = board.Items.IndexOf(enabled[0]);
            var moved = board.Move(new[] { idxA }, board.Items.IndexOf(enabled[1]) + 1);
            var config = board.ToConfig();

            var before = rec.LastSeq();
            var patch = await reportApi.UpdateTemplateAsync(tplId, new ReportTemplateUpdateDto { Components = config }).ConfigureAwait(false);
            var patchWire = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");

            var (_, tAfter) = await raw.Get($"reports/templates/{tplId}").ConfigureAwait(false);
            var compsAfter = Obj(tAfter["data"])?["components"] as JArray ?? new JArray();
            var byId = compsAfter.ToDictionary(c => Str(c["id"]), c => c);

            var titleKept = Str(byId.TryGetValue("SUMMARY_CARD", out var sc) ? sc["title"] : null) == "나의 요약 제목";
            var disabledKept = byId.ContainsKey("EVENT_TREND_LINE")
                               && (bool?)byId["EVENT_TREND_LINE"]["enabled"] == false
                               && Str(byId["EVENT_TREND_LINE"]["title"]) == "꺼진 항목 제목";
            var ok6a = patch.Success && titleKept && disabledKept;
            rec.Add("6a", TAG, "template PATCH with reorder: custom title and enabled:false entries SURVIVE",
                ok6a ? Verdict.PASS : Verdict.FAIL,
                $"moved={moved} success={patch.Success} HTTP {patchWire?.Status}; title kept={titleKept} disabled entry kept={disabledKept}; " +
                $"server components = {Recorder.Trunc(compsAfter.ToString(Newtonsoft.Json.Formatting.None), 400)}",
                defectAt: ok6a ? "" : "Reports.Ui/Consoles/Templates/TemplateComponentBoard.cs ToConfig/Emit",
                seqs: SeqsSince(rec, before));

            // ---------- 6b: status filter chips ----------
            boot.Wire.CurrentTag = TAG + "/status";
            var chips = new[] { "PENDING", "GENERATING", "COMPLETED", "FAILED", "CANCELLED" };
            var bad = new List<string>();
            foreach (var chip in chips)
            {
                var b = rec.LastSeq();
                var listed = await reportApi.GetGenerationsAsync(1, 10, chip).ConfigureAwait(false);
                var w = rec.Since(b).FirstOrDefault(x => x.Method == "GET");
                if (!listed.Success || (w != null && w.Status >= 400)) bad.Add($"{chip}:HTTP{w?.Status}");
            }
            rec.Add("6b", TAG, "generations status filter accepts every chip value (no 422)",
                bad.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                bad.Count == 0 ? $"all {chips.Length} chips returned 200" : $"rejected: {string.Join(", ", bad)}",
                defectAt: bad.Count == 0 ? "" : "Reports.Api/Services/ReportApiService.cs GetGenerationsAsync (status vocabulary)");

            rec.Add("6c", TAG, "report GENERATION round trip", Verdict.BLOCKED, "skipped by design",
                blocked: "generation needs the PDF/worker pipeline; out of scope for a client-write round trip");
        }
        catch (Exception ex)
        {
            rec.Add("6!", TAG, "reports round trip", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
        finally
        {
            if (tplId > 0)
            {
                var (s, _) = await raw.Delete($"reports/templates/{tplId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("report-template", tplId); else rec.Leftover("report-template", tplId, $"DELETE {s}");
            }
        }
    }
}
