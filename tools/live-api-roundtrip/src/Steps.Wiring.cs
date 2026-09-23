using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    // ---------- item 1: contract probe ----------
    public static async Task<string> Item1_Probe(Bootstrap boot, Recorder rec)
    {
        boot.Wire.CurrentTag = "probe";
        var ok = await boot.Probe.ResolveAsync().ConfigureAwait(false);
        var c = boot.Probe.Contract;
        var raw = boot.Probe.RawVersion ?? "(null)";
        var expected = EnumServerContract.V8_0;
        var pass = ok && c == expected;
        rec.Add("1", "contract", "IServerContractProbe resolves V8_0 against the local server",
            pass ? Verdict.PASS : Verdict.FAIL,
            $"resolved={ok} IsResolved={boot.Probe.IsResolved} Contract={c} RawVersion={raw}",
            defectAt: pass ? "" : "Ironwall.Dotnet.Libraries.Api/Services/ServerContractProbe.cs (fetch/Normalize)");
        return raw;
    }

    // ---------- item 2: N-04 wiring ----------
    public static async Task Item2_Wiring(Bootstrap boot, Recorder rec, Raw raw, IDeviceApiService deviceApi, DeviceQueryPolicy policy)
    {
        const string TAG = "N-04";
        int ctrlId = 0;
        var sensorIds = new List<int>();
        var extraSensorIds = new List<int>();
        try
        {
            // ---- arrange (raw): controller + 3 sensors that already carry hardware_spec siblings ----
            var (st, jo) = await raw.Post("devices/controllers", new
            {
                type_controller = "Controller",
                number_device = 96100,
                name_device = "LRT-N04-CTRL",
                connection = new { ip_address = "10.66.0.1", ip_port = 9600 },
            }).ConfigureAwait(false);
            if (st != 201)
            {
                rec.Add("2.0", TAG, "arrange controller", Verdict.BLOCKED, $"HTTP {st} {Short(jo)}", blocked: "fixture setup failed");
                return;
            }
            ctrlId = (int)jo["data"]["id"];
            rec.Created("controller", ctrlId);

            for (int i = 1; i <= 3; i++)
            {
                var (ss, sj) = await raw.Post("devices/sensors", new
                {
                    type_sensor = "PIR",
                    number_device = 96100 + i,
                    name_device = $"LRT-N04-S{i}",
                    controller_id = ctrlId,
                    hardware_spec = new
                    {
                        schema = 1,
                        manufacturer = "ACME-KEEP",
                        model = "MDL-KEEP",
                        serial = $"SN-KEEP-{i}",
                        spec = new { sibling = "KEEP-ME", vendor_note = $"note-{i}" },
                    },
                }).ConfigureAwait(false);
                if (ss != 201)
                {
                    rec.Add("2.0", TAG, "arrange sensor", Verdict.BLOCKED, $"HTTP {ss} {Short(sj)}", blocked: "fixture setup failed");
                    return;
                }
                var sid = (int)sj["data"]["id"];
                sensorIds.Add(sid);
                rec.Created("sensor", sid);
            }

            var gateway = new DeviceApiSensorGateway(deviceApi);
            var svc = new WiringApplyService(gateway, new NullDeviceProvider(), boot.Log, policy);

            // ================= 2a: place 3 sensors =================
            boot.Wire.CurrentTag = TAG + "/place";
            var board = await LoadBoard(raw, ctrlId).ConfigureAwait(false);
            var keys = board.Rows.OrderBy(r => r.Facts.Number).Select(r => r.Key).ToList();
            board.Place(keys[0], 1, 0);
            board.Place(keys[1], 1, 1);
            board.Place(keys[2], 2, 0);
            var before = rec.LastSeq();
            var res = await svc.ApplyAsync(ctrlId, board).ConfigureAwait(false);
            var w0 = await SpecOf(raw, sensorIds[0]).ConfigureAwait(false);
            var w1 = await SpecOf(raw, sensorIds[1]).ConfigureAwait(false);
            var w2 = await SpecOf(raw, sensorIds[2]).ConfigureAwait(false);
            var placeOk = res.IsSuccess
                && (int?)w0?["wiring"]?["line"] == 1 && (int?)w0?["wiring"]?["order"] == 1
                && (int?)w1?["wiring"]?["line"] == 1 && (int?)w1?["wiring"]?["order"] == 2
                && (int?)w2?["wiring"]?["line"] == 2 && (int?)w2?["wiring"]?["order"] == 1;
            rec.Add("2a", TAG, "place 3 sensors -> hardware_spec.spec.wiring written",
                placeOk ? Verdict.PASS : Verdict.FAIL,
                $"apply='{res.Message}' ok={res.OkCount}/{res.SentCount}; server wiring = {J(w0?["wiring"])} / {J(w1?["wiring"])} / {J(w2?["wiring"])}",
                defectAt: placeOk ? "" : "Devices.Ui/Consoles/Wiring/Register/WiringApplyService.cs PatchAsync",
                seqs: SeqsSince(rec, before));

            // ---- siblings + top-level hardware_spec keys survived? ----
            var hs = await HardwareSpecOf(raw, sensorIds[0]).ConfigureAwait(false);
            var sib = Str(Obj(hs?["spec"])?["sibling"]);
            var man = Str(hs?["manufacturer"]);
            var mdl = Str(hs?["model"]);
            var ser = Str(hs?["serial"]);
            var note = Str(Obj(hs?["spec"])?["vendor_note"]);
            var survived = sib == "KEEP-ME" && man == "ACME-KEEP" && mdl == "MDL-KEEP" && ser == "SN-KEEP-1" && note == "note-1";
            rec.Add("2b", TAG, "other hardware_spec keys + spec siblings SURVIVE the wiring PATCH",
                survived ? Verdict.PASS : Verdict.FAIL,
                $"manufacturer={man} model={mdl} serial={ser} spec.sibling={sib} spec.vendor_note={note}",
                defectAt: survived ? "" : "Messages/Dto/Devices/HardwareSpecDto.cs ShouldSerialize* axis gates");

            // ================= 2c: move one =================
            boot.Wire.CurrentTag = TAG + "/move";
            var board2 = await LoadBoard(raw, ctrlId).ConfigureAwait(false);
            var k2 = board2.Rows.OrderBy(r => r.Facts.Number).Select(r => r.Key).ToList();
            board2.Unplace(k2[1]);
            board2.Place(k2[1], 2, 1);
            before = rec.LastSeq();
            var resMove = await svc.ApplyAsync(ctrlId, board2).ConfigureAwait(false);
            var mv = await SpecOf(raw, sensorIds[1]).ConfigureAwait(false);
            var moveOk = resMove.IsSuccess && (int?)mv?["wiring"]?["line"] == 2 && (int?)mv?["wiring"]?["order"] == 2;
            rec.Add("2c", TAG, "move one sensor to another line/order",
                moveOk ? Verdict.PASS : Verdict.FAIL,
                $"apply='{resMove.Message}'; server wiring = {J(mv?["wiring"])}",
                defectAt: moveOk ? "" : "Devices.Ui/Consoles/Wiring/Register/WiringApplyService.cs PatchAsync",
                seqs: SeqsSince(rec, before));

            // ================= 2d: UNPLACE -> V-19 =================
            boot.Wire.CurrentTag = TAG + "/unplace";
            var board3 = await LoadBoard(raw, ctrlId).ConfigureAwait(false);
            var row3 = board3.Rows.First(r => r.Id == sensorIds[2]);
            board3.Unplace(row3.Key);
            before = rec.LastSeq();
            var resUn = await svc.ApplyAsync(ctrlId, board3).ConfigureAwait(false);
            var sent = rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
            var wireHasNull = sent != null && WireSaysWiringNull(sent.RequestBody);
            rec.Add("2d-wire", TAG, "V-19 client puts an EXPLICIT wiring:null on the wire when unplacing",
                wireHasNull ? Verdict.PASS : Verdict.FAIL,
                $"PATCH body = {(sent == null ? "(no PATCH sent)" : Recorder.Trunc(sent.RequestBodyRedacted, 400))}",
                defectAt: wireHasNull ? "" : "Devices.Ui/Consoles/Wiring/Model/WiringPlacement.cs WiringSpec.MergePatch + HardwareSpecDto.ShouldSerializeSpec",
                seqs: SeqsSince(rec, before));

            var hs3 = await HardwareSpecOf(raw, sensorIds[2]).ConfigureAwait(false);
            var specObj = hs3?["spec"] as JObject;
            var wiringTok = specObj?["wiring"];
            var wiringGone = specObj != null && (wiringTok == null || wiringTok.Type == JTokenType.Null);
            var sibKept = Str(specObj?["sibling"]) == "KEEP-ME" && Str(hs3?["manufacturer"]) == "ACME-KEEP";
            rec.Add("2d-server", TAG, "V-19 server really DELETED spec.wiring, siblings kept",
                (wiringGone && sibKept) ? Verdict.PASS : Verdict.FAIL,
                $"apply='{resUn.Message}'; hardware_spec = {Recorder.Trunc(J(hs3), 320)}",
                defectAt: (wiringGone && sibKept) ? "" : "client dropped the explicit null, or server merge did not delete");

            // ================= 2e: second save must not see a false drift =================
            boot.Wire.CurrentTag = TAG + "/resave";
            var board4 = await LoadBoard(raw, ctrlId).ConfigureAwait(false);
            var row4 = board4.Rows.First(r => r.Id == sensorIds[2]);
            board4.Place(row4.Key, 2, 3);
            before = rec.LastSeq();
            var resRe = await svc.ApplyAsync(ctrlId, board4).ConfigureAwait(false);
            var noFalseDrift = !resRe.IsConflict && resRe.IsSuccess;
            rec.Add("2e", TAG, "re-save right after an unplace does NOT raise a false drift conflict",
                noFalseDrift ? Verdict.PASS : Verdict.FAIL,
                $"IsConflict={resRe.IsConflict} IsSuccess={resRe.IsSuccess} msg='{resRe.Message}'",
                defectAt: noFalseDrift ? "" : "Devices.Ui/Consoles/Wiring/Register/WiringApplyService.cs DriftOf (reading a deleted key)",
                seqs: SeqsSince(rec, before));

            // ================= 2f: create-with-wiring (POST path) =================
            boot.Wire.CurrentTag = TAG + "/create";
            var board5 = await LoadBoard(raw, ctrlId).ConfigureAwait(false);
            var newRow = board5.AddRow(new SensorFacts(96190, "LRT-N04-NEW", "PIR", ""));
            board5.Place(newRow.Key, 1, 4);
            before = rec.LastSeq();
            var resNew = await svc.ApplyAsync(ctrlId, board5).ConfigureAwait(false);
            var postWire = rec.Since(before).FirstOrDefault(w => w.Method == "POST");
            var newId = resNew.Rows.Where(r => r.IsCreate && r.Ok).Select(r => r.NewId ?? 0).FirstOrDefault();
            if (newId > 0) { extraSensorIds.Add(newId); rec.Created("sensor", newId); }
            JObject newSpec = newId > 0 ? await SpecOf(raw, newId).ConfigureAwait(false) : null;
            var createOk = resNew.IsSuccess && newId > 0
                && (int?)newSpec?["wiring"]?["line"] == 1 && (int?)newSpec?["wiring"]?["order"] == 5;
            rec.Add("2f", TAG, "create-with-wiring: POST carries hardware_spec.spec.wiring in one call",
                createOk ? Verdict.PASS : Verdict.FAIL,
                $"apply='{resNew.Message}' newId={newId}; server wiring = {J(newSpec?["wiring"])}; POST body = {(postWire == null ? "(none)" : Recorder.Trunc(postWire.RequestBodyRedacted, 320))}",
                defectAt: createOk ? "" : "Devices.Ui/Consoles/Wiring/Register/WiringApplyService.cs CreateAsync",
                seqs: SeqsSince(rec, before));

            // ================= 2g: drift -> send NOTHING =================
            boot.Wire.CurrentTag = TAG + "/drift";
            var board6 = await LoadBoard(raw, ctrlId).ConfigureAwait(false);
            var driftRow = board6.Rows.First(r => r.Id == sensorIds[0]);
            board6.Unplace(driftRow.Key);
            board6.Place(driftRow.Key, 1, 6);
            // out-of-band change between load and save
            await raw.Patch($"devices/sensors/{sensorIds[0]}", new { name_device = "LRT-N04-S1-HIJACKED" }).ConfigureAwait(false);
            before = rec.LastSeq();
            var resDrift = await svc.ApplyAsync(ctrlId, board6).ConfigureAwait(false);
            var writes = rec.Since(before).Where(w => w.Method == "PATCH" || w.Method == "POST" || w.Method == "DELETE").ToList();
            var driftOk = resDrift.IsConflict && !resDrift.IsSuccess && writes.Count == 0;
            rec.Add("2g", TAG, "out-of-band drift between load and save -> service sends NOTHING",
                driftOk ? Verdict.PASS : Verdict.FAIL,
                $"IsConflict={resDrift.IsConflict} msg='{resDrift.Message}'; write calls after drift = {writes.Count}",
                defectAt: driftOk ? "" : "Devices.Ui/Consoles/Wiring/Register/WiringApplyService.cs DriftOf / ApplyAsync step 2",
                seqs: SeqsSince(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("2!", TAG, "wiring round trip", Verdict.BLOCKED, ex.Message, blocked: "harness exception, not a proven product defect");
        }
        finally
        {
            foreach (var id in sensorIds.Concat(extraSensorIds))
            {
                var (s, _) = await raw.Delete($"devices/sensors/{id}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("sensor", id); else rec.Leftover("sensor", id, $"DELETE {s}");
            }
            if (ctrlId > 0)
            {
                var (s, _) = await raw.Delete($"devices/controllers/{ctrlId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("controller", ctrlId); else rec.Leftover("controller", ctrlId, $"DELETE {s}");
            }
        }
    }

    // ---------- helpers ----------
    static async Task<WiringBoard> LoadBoard(Raw raw, int ctrlId)
    {
        var (status, jo) = await raw.Get($"devices/sensors?controller_id={ctrlId}&page=1&limit=100&include=hardware_spec").ConfigureAwait(false);
        if (status != 200)
            throw new InvalidOperationException($"LoadBoard fixture read failed: HTTP {status} {Short(jo)}");
        var rows = new List<(int, int?, SensorFacts, WiringPlacement, string, IReadOnlyList<int>)>();
        var arr = jo["data"] as JArray ?? new JArray();
        foreach (var d in arr)
        {
            var spec = Obj(Obj(d["hardware_spec"])?["spec"]);
            rows.Add((
                (int)d["id"],
                (int?)Obj(d["connection"])?["channel"],
                new SensorFacts(
                    (int)(d["number_device"] ?? 0),
                    Str(d["name_device"]),
                    Str(d["type_sensor"]),
                    Str(Obj(d["geolocation"])?["location"])),
                WiringSpec.Read(spec),
                null,
                (d["group_ids"] as JArray)?.Select(x => (int)x).ToList()));
        }
        var b = new WiringBoard();
        b.Load(rows);
        return b;
    }

    static async Task<JObject> HardwareSpecOf(Raw raw, int sensorId)
    {
        var (_, jo) = await raw.Get($"devices/sensors/{sensorId}?include=hardware_spec").ConfigureAwait(false);
        return Obj(Obj(jo["data"])?["hardware_spec"]);
    }

    static async Task<JObject> SpecOf(Raw raw, int sensorId)
    {
        var hs = await HardwareSpecOf(raw, sensorId).ConfigureAwait(false);
        return Obj(hs?["spec"]);
    }

    /// <summary>JSON null-safe object access - a JSON null is a JValue, and indexing it throws.</summary>
    static JObject Obj(JToken t) => t as JObject;
    static string Str(JToken t) => (t == null || t.Type == JTokenType.Null) ? "" : (string)t;

    /// <summary>Reads the RAW WIRE TEXT (not a DTO) to prove the explicit null is present.</summary>
    static bool WireSaysWiringNull(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            var jo = JObject.Parse(body);
            var tok = jo["hardware_spec"]?["spec"]?["wiring"];
            return tok != null && tok.Type == JTokenType.Null;
        }
        catch { return false; }
    }

    static int[] SeqsSince(Recorder rec, int before) => rec.Since(before).Select(w => w.Seq).ToArray();
    static string Short(JObject jo) => Recorder.Trunc(jo?.ToString(Formatting.None) ?? "", 300);
    static string J(JToken t) => t == null ? "(null)" : t.ToString(Formatting.None);
}
