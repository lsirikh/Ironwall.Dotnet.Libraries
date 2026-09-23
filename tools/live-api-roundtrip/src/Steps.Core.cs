using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Newtonsoft.Json.Linq;
using MonModels = Ironwall.Dotnet.Monitoring.Models.Devices;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    public static IUnitScopeService UnitScope;

    public static async Task<string> RunAll(Bootstrap boot, Recorder rec)
    {
        var raw = new Raw();
        await raw.LoginAsync().ConfigureAwait(false);

        // ---- item 1 ----
        var version = await Item1_Probe(boot, rec).ConfigureAwait(false);

        var policy = new DeviceQueryPolicy(boot.Probe, boot.Log);
        var deviceApi = new DeviceApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
        var unitApi = new UnitApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);

        // ---- unit scope wiring (item 7 prerequisite) ----
        // The write services stamp unit_id through the INTERNAL UnitScopeGate, which resolves
        // IUnitScopeService via Caliburn IoC.Get. A console has no container, so we install the
        // IoC delegates ourselves - this is HARNESS wiring that mirrors what the host app's DI
        // does; without it unit_id would silently be omitted and item 7 would be vacuous.
        var nats = new NatsSetupModel { GroupNats = "unit001" };
        UnitScope = new UnitScopeService(unitApi, nats, boot.Probe, boot.Log);
        InstallIoC(UnitScope);

        // ---- item 2 ----
        await Item2_Wiring(boot, rec, raw, deviceApi, policy).ConfigureAwait(false);

        // ---- item 4 ----
        await Item4_Groups(boot, rec, raw, deviceApi).ConfigureAwait(false);

        // ---- item 7 ----
        await Item7_UnitScope(boot, rec, raw, nats).ConfigureAwait(false);

        // ---- item 3 / 5 / 6 ----
        await Item3_Components(boot, rec, raw, deviceApi, policy).ConfigureAwait(false);
        await Item5_Accounts(boot, rec, raw).ConfigureAwait(false);
        await Item6_Reports(boot, rec, raw).ConfigureAwait(false);

        // ---- item 8: unit create/delete round trip through the real service ----
        await Item8_Unit(boot, rec, raw, unitApi).ConfigureAwait(false);

        // ---- item 9: the mandated fix (D-17) - verify cleanup for real, never let an
        //      unreachable listing endpoint read as "clean" ----
        await Item9_CleanupSweep(boot, rec, raw).ConfigureAwait(false);

        return $"https://127.0.0.1:8000 (contract {boot.Probe.Contract}, info.version {version})";
    }

    static void InstallIoC(IUnitScopeService unitScope)
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IUnitScopeService)) return unitScope;
            return null;   // everything else unresolved -> the product's try/catch fallbacks apply
        };
        IoC.GetAllInstances = type => Array.Empty<object>();
        IoC.BuildUp = _ => { };
    }

    // ---------- item 4: N-02 group drag handler ----------
    public static async Task Item4_Groups(Bootstrap boot, Recorder rec, Raw raw, IDeviceApiService deviceApi)
    {
        const string TAG = "N-02";
        int ctrlId = 0, groupId = 0;
        var sensorIds = new List<int>();
        try
        {
            var (gs, gj) = await raw.Post("devices/groups", new { name = "LRT-N02-GRP", description = "live roundtrip" }).ConfigureAwait(false);
            if (gs != 201 && gs != 200)
            {
                rec.Add("4.0", TAG, "arrange device group", Verdict.BLOCKED, $"HTTP {gs} {Short(gj)}", blocked: "fixture setup failed");
                return;
            }
            groupId = (int)gj["data"]["id"];
            rec.Created("device-group", groupId);

            var (cs, cj) = await raw.Post("devices/controllers", new
            {
                type_controller = "Controller",
                number_device = 96200,
                name_device = "LRT-N02-CTRL",
                connection = new { ip_address = "10.66.1.1", ip_port = 9610 },
            }).ConfigureAwait(false);
            if (cs != 201) { rec.Add("4.0", TAG, "arrange controller", Verdict.BLOCKED, $"HTTP {cs}", blocked: "fixture setup failed"); return; }
            ctrlId = (int)cj["data"]["id"];
            rec.Created("controller", ctrlId);

            for (int i = 1; i <= 3; i++)
            {
                var (ss, sj) = await raw.Post("devices/sensors", new
                {
                    type_sensor = "PIR",
                    number_device = 96200 + i,
                    name_device = $"LRT-N02-S{i}",
                    controller_id = ctrlId,
                }).ConfigureAwait(false);
                if (ss != 201) { rec.Add("4.0", TAG, "arrange sensor", Verdict.BLOCKED, $"HTTP {ss}", blocked: "fixture setup failed"); return; }
                var sid = (int)sj["data"]["id"];
                sensorIds.Add(sid); rec.Created("sensor", sid);
            }

            // local device models, as the console would hold them
            var models = sensorIds.Select(id => (MonModels.IBaseDeviceModel)new MonModels.SensorDeviceModel
            {
                Id = id,
                DeviceGroups = new List<int>(),
            }).ToList();

            var handler = new DeviceGroupDropHandler(deviceApi, () => models, boot.Log);

            // ---- 4a: bulk assign ----
            boot.Wire.CurrentTag = TAG + "/assign";
            var before = rec.LastSeq();
            var line = await handler.AssignAsync(groupId, "LRT-N02-GRP", models).ConfigureAwait(false);
            var membersAfter = await GroupMemberIds(raw, groupId).ConfigureAwait(false);
            var assignOk = sensorIds.All(membersAfter.Contains) && membersAfter.Count == sensorIds.Count;
            rec.Add("4a", TAG, "bulk assign through DeviceGroupDropHandler.AssignAsync",
                assignOk ? Verdict.PASS : Verdict.FAIL,
                $"result='{line}'; server members = [{string.Join(",", membersAfter)}] expected [{string.Join(",", sensorIds)}]",
                defectAt: assignOk ? "" : "Devices.Ui/Consoles/Groups/DeviceGroupDrop.cs AssignAsync",
                seqs: SeqsSince(rec, before));

            // one batch call, not N calls
            var writeCalls = rec.Since(before).Count(w => w.Method == "POST" || w.Method == "DELETE");
            rec.Add("4b", TAG, "assign is ONE batch call, not one per device",
                writeCalls == 1 ? Verdict.PASS : Verdict.FAIL,
                $"write calls = {writeCalls} for {sensorIds.Count} devices",
                defectAt: writeCalls == 1 ? "" : "Devices.Ui/Consoles/Groups/DeviceGroupDrop.cs AssignAsync");

            // ---- 4c: local reflection ----
            var reflected = models.All(m => m.DeviceGroups != null && m.DeviceGroups.Contains(groupId));
            rec.Add("4c", TAG, "handler reflects membership onto the local models",
                reflected ? Verdict.PASS : Verdict.FAIL,
                $"local DeviceGroups = [{string.Join(" | ", models.Select(m => string.Join(",", m.DeviceGroups ?? new List<int>())))}]",
                defectAt: reflected ? "" : "Devices.Ui/Consoles/Groups/DeviceGroupDrop.cs Reflect");

            // ---- 4d: undo ----
            boot.Wire.CurrentTag = TAG + "/undo";
            before = rec.LastSeq();
            var undo = new GroupDropUndo(groupId, "LRT-N02-GRP", sensorIds.ToList());
            var undoLine = await handler.UndoAsync(undo).ConfigureAwait(false);
            var membersUndo = await GroupMemberIds(raw, groupId).ConfigureAwait(false);
            var undoOk = membersUndo.Count == 0;
            rec.Add("4d", TAG, "undo removes exactly what was assigned",
                undoOk ? Verdict.PASS : Verdict.FAIL,
                $"result='{undoLine}'; server members after undo = [{string.Join(",", membersUndo)}]",
                defectAt: undoOk ? "" : "Devices.Ui/Consoles/Groups/DeviceGroupDrop.cs UndoAsync",
                seqs: SeqsSince(rec, before));

            // ---- 4e: already-in devices are not re-sent ----
            boot.Wire.CurrentTag = TAG + "/replan";
            foreach (var m in models) m.DeviceGroups = new List<int> { groupId };
            var plan = DeviceGroupDrop.Plan(groupId, models);
            rec.Add("4e", TAG, "devices already in the group are planned out (no redundant write)",
                !plan.CanSend && plan.AlreadyIn == sensorIds.Count ? Verdict.PASS : Verdict.FAIL,
                $"CanSend={plan.CanSend} AlreadyIn={plan.AlreadyIn} ids=[{string.Join(",", plan.DeviceIds)}] reason='{plan.BlockReason}'",
                defectAt: (!plan.CanSend && plan.AlreadyIn == sensorIds.Count) ? "" : "Devices.Ui/Consoles/Groups/DeviceGroupDrop.cs Plan");
        }
        catch (Exception ex)
        {
            rec.Add("4!", TAG, "group round trip", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
        finally
        {
            foreach (var id in sensorIds)
            {
                var (s, _) = await raw.Delete($"devices/sensors/{id}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("sensor", id); else rec.Leftover("sensor", id, $"DELETE {s}");
            }
            if (ctrlId > 0)
            {
                var (s, _) = await raw.Delete($"devices/controllers/{ctrlId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("controller", ctrlId); else rec.Leftover("controller", ctrlId, $"DELETE {s}");
            }
            if (groupId > 0)
            {
                var (s, _) = await raw.Delete($"devices/groups/{groupId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("device-group", groupId); else rec.Leftover("device-group", groupId, $"DELETE {s}");
            }
        }
    }

    static async Task<List<int>> GroupMemberIds(Raw raw, int groupId)
    {
        var (_, jo) = await raw.Get($"devices/groups/{groupId}").ConfigureAwait(false);
        var ids = new List<int>();
        var d = jo["data"];
        var arr = (d?["device_ids"] as JArray) ?? (d?["devices"] as JArray);
        if (arr != null)
            foreach (var t in arr)
                ids.Add(t.Type == JTokenType.Object ? (int)t["id"] : (int)t);
        return ids;
    }

    // ---------- item 7: unit scope ----------
    public static async Task Item7_UnitScope(Bootstrap boot, Recorder rec, Raw raw, NatsSetupModel nats)
    {
        const string TAG = "unit";
        try
        {
            boot.Wire.CurrentTag = TAG;
            var (_, uj) = await raw.Get("units?page=1&limit=100").ConfigureAwait(false);
            var units = (uj["data"] as JArray) ?? new JArray();
            var match = units.FirstOrDefault(u => (string)u["code"] == nats.GroupNats);
            var expectedId = match == null ? (int?)null : (int)match["id"];

            var resolved = await UnitScope.ResolveAsync().ConfigureAwait(false);
            var resolveOk = expectedId != null && resolved == expectedId;
            rec.Add("7a", TAG, "UnitScopeService resolves the client's unit code to the server unit id",
                resolveOk ? Verdict.PASS : Verdict.FAIL,
                $"UnitCode={UnitScope.UnitCode} IsUnitEra={UnitScope.IsUnitEra} resolved={resolved} expected={expectedId}; " +
                $"GET /api/units -> [{string.Join(", ", units.Select(u => $"{u["id"]}:{u["code"]}"))}]",
                defectAt: resolveOk ? "" : "Devices.Ui/Services/UnitScopeService.cs ResolveCoreAsync");

            // every device write captured so far must carry that unit_id
            var deviceWrites = rec.Wire
                .Where(w => (w.Method == "POST" || w.Method == "PATCH") && w.Uri.Contains("/devices/")
                            && !w.Uri.Contains("/groups") && w.RequestBody.Length > 0)
                .ToList();
            var withUnit = deviceWrites.Where(w => HasUnitId(w.RequestBody, expectedId ?? -1)).ToList();
            var missing = deviceWrites.Except(withUnit).ToList();

            // NOTE: only CREATE paths stamp unit_id in this client (PATCH bodies are minimal by design).
            var creates = deviceWrites.Where(w => w.Method == "POST").ToList();
            var createsWithUnit = creates.Where(w => HasUnitId(w.RequestBody, expectedId ?? -1)).ToList();
            var v = creates.Count > 0 && createsWithUnit.Count == creates.Count ? Verdict.PASS : Verdict.FAIL;
            rec.Add("7b", TAG, "device CREATE writes carry the expected unit_id",
                creates.Count == 0 ? Verdict.BLOCKED : v,
                $"device POST bodies = {creates.Count}, carrying unit_id={expectedId}: {createsWithUnit.Count}. " +
                $"Examples: {string.Join(" || ", creates.Take(2).Select(w => Recorder.Trunc(w.RequestBodyRedacted, 200)))}",
                defectAt: v == Verdict.FAIL ? "Devices.Ui/Helpers/UnitScopeGate.cs StampAsync (IoC resolution) or UnitScopeService" : "",
                blocked: creates.Count == 0 ? "no device create was captured" : "");

            rec.Add("7c", TAG, "device PATCH writes: unit_id presence (informational)", Verdict.INFO,
                $"{deviceWrites.Count(w => w.Method == "PATCH")} PATCH bodies; carrying unit_id: " +
                $"{deviceWrites.Count(w => w.Method == "PATCH" && HasUnitId(w.RequestBody, expectedId ?? -1))}");
        }
        catch (Exception ex)
        {
            rec.Add("7!", TAG, "unit scope", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
    }

    static bool HasUnitId(string body, int expected)
    {
        try
        {
            var jo = JObject.Parse(body);
            var t = jo["unit_id"];
            return t != null && t.Type == JTokenType.Integer && (int)t == expected;
        }
        catch { return false; }
    }
}
