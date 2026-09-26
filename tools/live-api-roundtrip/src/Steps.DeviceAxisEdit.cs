// 장비 상세의 축 값 편집 왕복 점검 (device-axis-edit) — 2026-09-27 완성도 수정 D-2.
//
// 전에는 접속 · 하드웨어 · 부대 · 운용 설정 칸이 "이번 판에서는 읽기 전용" 이었다. 이제 상세 폼이 그 칸들을
// DeviceAxisWriter → IDeviceApiService.PatchDeviceAxesAsync(좁은 PATCH, 보낸 키만 바뀐다)로 보낸다.
// 이 점검은 제품 경로 그대로(명세 → 본문 → 서비스 → 서버) 값을 쓰고, 서버를 다시 읽어(raw re-GET) 확인하고,
// 원래 값으로 되돌린 뒤 만든 장비를 지운다 — 남는 것이 없어야 한다.
//
//   ax.1  접속 방식 · 채널 · 모델 · 펌웨어 · 온도 상한을 한 번에 쓰면 그 값만 바뀐다(부품 배열 · 히터 설정 · 이름 · IP · 부대 보존)
//   ax.2  다시 읽은 모델에서 상세 칸이 보낸 값을 그대로 보인다(폼이 재조회 뒤 맞춰 보는 그 읽기)
//   ax.3  빈 칸(모델 · 온도 상한)은 서버에서 지워진다(JSON null = 삭제)
//   ax.4  원래 값으로 되돌리면 처음 읽은 값과 같다
//
// 생성물은 LRT-AX- 접두다. 자기 것만 지운다(DlFixture).
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    const string AX = "device-axis-edit";

    [ExtraStep(AX, 20)]
    static async Task Ax1_DeviceAxisEditRoundTrip(ExtraContext ctx)
    {
        var fx = new DlFixture(ctx);
        try
        {
            ctx.Rec.Add("ax.0", AX, "server contract seen by the product probe", Verdict.INFO,
                $"probe contract={ctx.Boot.Probe.Contract} axis={ctx.Policy.IsAxisContract}");
            if (!ctx.Policy.IsAxisContract) { fx.Blocked("ax.0b", "server is not an axis contract (7.0+) - axis edit path does not apply"); return; }

            // ---- arrange: 히터 · 팬을 선언한 함체(히터는 enabled:true 설정까지) ----
            ctx.Boot.Wire.CurrentTag = "ax/arrange";
            // 접속 방식을 명시해 만든다 — 접속 방식은 지울 수 없는 칸(NOT NULL)이라, 비어 있던 장비로는 "원래 값으로 되돌리기"를 증명할 수 없다.
            var (cs, cj) = await ctx.Raw.Post("devices/enclosures", new JObject
            {
                ["type_enclosure"] = "Outdoor",
                ["number_device"] = 98700,
                ["name_device"] = "LRT-AX-ENC",
                ["connection"] = new JObject { ["type"] = "IP_DIRECT", ["ip_address"] = "10.78.200.4", ["ip_port"] = 9804 },
                ["hardware_spec"] = new JObject
                {
                    ["schema"] = 1,
                    ["model"] = "LRT-AX-M0",
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
            }).ConfigureAwait(false);
            if (cs != 201) { fx.Blocked("ax.0a", $"POST enclosure: HTTP {cs} {Short(cj)}"); return; }
            var id = (int)cj["data"]!["id"]!;
            fx.Track("enclosure", id);
            var original = await DataOf(ctx, id);
            if (original is null) { fx.Blocked("ax.0c", "arrange re-GET failed"); return; }

            var isUnitEra = ctx.Policy.Contract >= Ironwall.Dotnet.Libraries.Api.Services.EnumServerContract.V8_0;
            var specs = DevicePropertyCatalog.For(EnumDeviceCategory.Enclosure, isAxisContract: true, isUnitEra: isUnitEra);
            DevicePropertySpec S(string key) => specs.Single(s => s.Key == key);
            var writer = new DeviceAxisWriter(ctx.DeviceApi, ctx.Policy, ctx.Boot.Log);

            // ---- ax.1: 한 번의 적용 — 제품 경로(DeviceAxisWriter) ----
            ctx.Boot.Wire.CurrentTag = "ax/1-write";
            var before = ctx.Rec.LastSeq();
            var model = (await ctx.DeviceApi.GetEnclosureByIdAsync(id).ConfigureAwait(false)).Data!.ToEnclosureDeviceModel();
            var edits = new[]
            {
                (S("connection.type"), "IP_CONVERTER"),
                (S("connection.channel"), "7"),
                (S("hardware_spec.model"), "LRT-AX-M1"),
                (S("hardware_spec.firmware"), "9.9.9"),
                (S("device_config.thresholds.temperature.high"), "55.5"),
            };
            var result = await writer.ApplyAsync(new[] { model }, edits).ConfigureAwait(false);
            var patchWire = ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "PATCH");
            var sent = AsmParse(patchWire?.RequestBody);
            var after = await DataOf(ctx, id);

            var ok1 = result.IsSuccess && after is not null
                && Str(after.SelectToken("connection.type")) == "IP_CONVERTER"
                && (int?)after.SelectToken("connection.channel") == 7
                && Str(after.SelectToken("hardware_spec.model")) == "LRT-AX-M1"
                && Str(after.SelectToken("hardware_spec.firmware")) == "9.9.9"
                && (double?)after.SelectToken("device_config.thresholds.temperature.high") == 55.5
                // 보내지 않은 것은 그대로
                && ComponentKeys(after) == ComponentKeys(original)
                && (bool?)after.SelectToken("device_config.component_overrides.heater_1.enabled") == true
                && Str(after["name_device"]) == Str(original["name_device"])
                && Str(after.SelectToken("connection.ip_address")) == Str(original.SelectToken("connection.ip_address"))
                && (int?)after["unit_id"] == (int?)original["unit_id"]
                // 본문이 좁다 — 부품 배열 · 이름 · 관측을 싣지 않았다
                && sent is not null && sent.SelectToken("hardware_spec.components") is null && sent["name_device"] is null && sent["device_status"] is null;
            ctx.Rec.Add("ax.1", AX,
                "상세의 접속 방식 · 채널 · 모델 · 펌웨어 · 온도 상한을 한 번에 적용하면 그 값만 바뀌고 부품 · 히터 설정 · 이름 · IP · 부대는 그대로다",
                ok1 ? Verdict.PASS : Verdict.FAIL,
                $"writer: sent={result.SentCount} failed={result.FailedCount} msg='{result.Message}'; PATCH {Recorder.Trunc(patchWire?.Uri ?? "(none)", 120)} status={patchWire?.Status} body={Recorder.Trunc(patchWire?.RequestBodyRedacted ?? "(none)", 400)}; " +
                $"after: type={Str(after?.SelectToken("connection.type"))} channel={after?.SelectToken("connection.channel")} model={Str(after?.SelectToken("hardware_spec.model"))} fw={Str(after?.SelectToken("hardware_spec.firmware"))} temp.high={after?.SelectToken("device_config.thresholds.temperature.high")} " +
                $"components=[{ComponentKeys(after)}] (was [{ComponentKeys(original)}]) heater_1.enabled={after?.SelectToken("device_config.component_overrides.heater_1.enabled")} unit={after?["unit_id"]} (was {original["unit_id"]})",
                defectAt: ok1 ? "" : "Devices.Ui/Consoles/Properties/DeviceAxisWriter.cs · DeviceAxisPatchBuilder.cs · Devices.Api DeviceApiService.PatchDeviceAxesAsync",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- ax.2: 재조회한 모델에서 상세 칸이 보낸 값을 그대로 읽는다(콘솔의 적용 뒤 맞춰 보기) ----
            var reread = (await ctx.DeviceApi.GetEnclosureByIdAsync(id).ConfigureAwait(false)).Data!.ToEnclosureDeviceModel();
            var row = new EnclosureDeviceViewModel(reread, ctx.Policy);
            var readBack = edits.Select(e => (e.Item1.Key, Sent: e.Item2, Read: DevicePropertyAccessor.ReadText(row, e.Item1))).ToList();
            var ok2 = readBack.All(x => SameNumberOrText(x.Sent, x.Read));
            ctx.Rec.Add("ax.2", AX, "다시 읽은 장비에서 상세 칸이 보낸 값을 그대로 보인다(재조회 뒤 '값이 저장되지 않았습니다' 오경보가 없다)",
                ok2 ? Verdict.PASS : Verdict.FAIL,
                string.Join("; ", readBack.Select(x => $"{x.Key}: sent='{x.Sent}' read='{x.Read}'")),
                defectAt: ok2 ? "" : "Devices.Ui/Consoles/Properties/DevicePropertyCatalog.cs AxisReader(저장 값 그대로 읽기) · Helpers/DtoToModelHelper.cs 축 매핑");

            // ---- ax.3: 빈 칸은 지운다 ----
            ctx.Boot.Wire.CurrentTag = "ax/3-clear";
            before = ctx.Rec.LastSeq();
            var clear = await writer.ApplyAsync(new[] { reread }, new[]
            {
                (S("hardware_spec.model"), ""),
                (S("device_config.thresholds.temperature.high"), ""),
            }).ConfigureAwait(false);
            var cleared = await DataOf(ctx, id);
            var ok3 = clear.IsSuccess && cleared is not null
                && IsAbsent(cleared.SelectToken("hardware_spec.model"))
                && IsAbsent(cleared.SelectToken("device_config.thresholds.temperature.high"))
                && Str(cleared.SelectToken("hardware_spec.firmware")) == "9.9.9"
                && ComponentKeys(cleared) == ComponentKeys(original);
            ctx.Rec.Add("ax.3", AX, "모델 · 온도 상한을 비워 적용하면 서버에서 지워지고 나머지는 그대로다",
                ok3 ? Verdict.PASS : Verdict.FAIL,
                $"writer: sent={clear.SentCount} msg='{clear.Message}'; body={Recorder.Trunc(ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "PATCH")?.RequestBodyRedacted ?? "(none)", 300)}; " +
                $"after: model={cleared?.SelectToken("hardware_spec.model")?.ToString() ?? "(absent)"} temp.high={cleared?.SelectToken("device_config.thresholds.temperature.high")?.ToString() ?? "(absent)"} fw={Str(cleared?.SelectToken("hardware_spec.firmware"))} components=[{ComponentKeys(cleared)}]",
                defectAt: ok3 ? "" : "Devices.Ui/Consoles/Properties/DeviceAxisPatchBuilder.cs 빈 칸 = JSON null",
                seqs: SeqsSince(ctx.Rec, before));

            // ---- ax.4: 되돌리기 — 처음 읽은 값으로(없던 값은 지운다) ----
            ctx.Boot.Wire.CurrentTag = "ax/4-restore";
            before = ctx.Rec.LastSeq();
            var restoreModel = (await ctx.DeviceApi.GetEnclosureByIdAsync(id).ConfigureAwait(false)).Data!.ToEnclosureDeviceModel();
            var restore = await writer.ApplyAsync(new[] { restoreModel }, new[]
            {
                (S("connection.type"), AxStr(original.SelectToken("connection.type")) ?? "IP_DIRECT"),
                (S("connection.channel"), original.SelectToken("connection.channel")?.ToString() ?? ""),
                (S("hardware_spec.model"), AxStr(original.SelectToken("hardware_spec.model")) ?? ""),
                (S("hardware_spec.firmware"), AxStr(original.SelectToken("hardware_spec.firmware")) ?? ""),
                (S("device_config.thresholds.temperature.high"), original.SelectToken("device_config.thresholds.temperature.high")?.ToString() ?? ""),
            }).ConfigureAwait(false);
            var restored = await DataOf(ctx, id);
            var keys = new[] { "connection.type", "connection.channel", "hardware_spec.model", "hardware_spec.firmware", "device_config.thresholds.temperature.high" };
            var diffs = keys.Where(k => !JToken.DeepEquals(Norm(original.SelectToken(k)), Norm(restored?.SelectToken(k)))).ToList();
            var ok4 = restore.IsSuccess && restored is not null && diffs.Count == 0 && ComponentKeys(restored) == ComponentKeys(original);
            ctx.Rec.Add("ax.4", AX, "원래 값으로 되돌리면 처음 읽은 값과 같다(없던 값은 다시 지워진다)",
                ok4 ? Verdict.PASS : Verdict.FAIL,
                $"writer: sent={restore.SentCount} msg='{restore.Message}'; differing keys=[{string.Join(",", diffs)}] " +
                string.Join(" ", keys.Select(k => $"{k}: was={original.SelectToken(k)?.ToString() ?? "(absent)"} now={restored?.SelectToken(k)?.ToString() ?? "(absent)"}")),
                defectAt: ok4 ? "" : "Devices.Ui/Consoles/Properties/DeviceAxisWriter.cs",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("ax.!", ex.ToString()); }
        finally { await fx.DisposeAsync(); }
    }

    // ---- ax.5: 카메라 — 제어 프로토콜 · 동작 모드(기상 · 주야 · 녹화)를 쓰고 되돌린다 ----
    // ---- ax.6: 소속 부대 — 다른 부대로 옮겼다가 원래 부대로 되돌린다(8.0 이상) ----
    [ExtraStep(AX, 21)]
    static async Task Ax5_CameraModesAndUnitRoundTrip(ExtraContext ctx)
    {
        var fx = new DlFixture(ctx);
        var cameraId = 0;
        try
        {
            if (!ctx.Policy.IsAxisContract) { fx.Blocked("ax.5b", "server is not an axis contract (7.0+)"); return; }
            var isUnitEra = ctx.Policy.Contract >= Ironwall.Dotnet.Libraries.Api.Services.EnumServerContract.V8_0;
            var specs = DevicePropertyCatalog.For(EnumDeviceCategory.Camera, isAxisContract: true, isUnitEra: isUnitEra);
            DevicePropertySpec S(string key) => specs.Single(s => s.Key == key);
            var writer = new DeviceAxisWriter(ctx.DeviceApi, ctx.Policy, ctx.Boot.Log);

            ctx.Boot.Wire.CurrentTag = "ax/5-arrange";
            var (cs, cj) = await ctx.Raw.Post("devices/cameras", new JObject
            {
                ["type_camera"] = "FIXED",
                ["number_device"] = 98701,
                ["name_device"] = "LRT-AX-CAM",
                ["connection"] = new JObject { ["ip_address"] = "10.78.201.5", ["ip_port"] = 80, ["protocol"] = "NONE" },
            }).ConfigureAwait(false);
            if (cs != 201) { fx.Blocked("ax.5a", $"POST camera: HTTP {cs} {Short(cj)}"); return; }
            cameraId = (int)cj["data"]!["id"]!;
            ctx.Rec.Created("camera", cameraId);
            var (_, oj) = await ctx.Raw.Get($"devices/cameras/{cameraId}").ConfigureAwait(false);
            var original = Obj(oj["data"]);

            // ax.5 — 쓰기
            ctx.Boot.Wire.CurrentTag = "ax/5-write";
            var before = ctx.Rec.LastSeq();
            var model = (await ctx.DeviceApi.GetCameraByIdAsync(cameraId).ConfigureAwait(false)).Data!.ToCameraDeviceModel();
            var result = await writer.ApplyAsync(new[] { model }, new[]
            {
                (S("connection.protocol"), "ONVIF"),
                (S("device_config.modes.weather_mode"), "FOG"),
                (S("device_config.modes.day_night_mode"), "NIGHT"),
                (S("device_config.modes.is_record"), "true"),
            }).ConfigureAwait(false);
            var (_, aj) = await ctx.Raw.Get($"devices/cameras/{cameraId}").ConfigureAwait(false);
            var after = Obj(aj["data"]);
            var ok5 = result.IsSuccess && after is not null
                && Str(after.SelectToken("connection.protocol")) == "ONVIF"
                && Str(after.SelectToken("device_config.modes.weather_mode")) == "FOG"
                && Str(after.SelectToken("device_config.modes.day_night_mode")) == "NIGHT"
                && (bool?)after.SelectToken("device_config.modes.is_record") == true
                && Str(after.SelectToken("connection.ip_address")) == "10.78.201.5";
            var row = new CameraDeviceViewModel((await ctx.DeviceApi.GetCameraByIdAsync(cameraId).ConfigureAwait(false)).Data!.ToCameraDeviceModel());
            var shown = DevicePropertyAccessor.ReadText(row, S("device_config.modes.weather_mode"));
            ctx.Rec.Add("ax.5", AX, "카메라의 제어 프로토콜 · 기상 모드 · 주야 모드 · 녹화를 상세에서 적용하면 그대로 저장되고 IP 는 그대로다",
                ok5 && shown == "FOG" ? Verdict.PASS : Verdict.FAIL,
                $"writer: sent={result.SentCount} msg=[{result.Message}]; body={Recorder.Trunc(ctx.Rec.Since(before).FirstOrDefault(w => w.Method == "PATCH")?.RequestBodyRedacted ?? "(none)", 300)}; " +
                $"after: protocol={Str(after?.SelectToken("connection.protocol"))} modes={after?.SelectToken("device_config.modes")?.ToString(Newtonsoft.Json.Formatting.None)} ip={Str(after?.SelectToken("connection.ip_address"))}; form reads weather_mode=[{shown}]",
                defectAt: ok5 ? "" : "Devices.Ui/Consoles/Properties/DevicePropertyCatalog.cs CameraMode · DeviceAxisWriter",
                seqs: SeqsSince(ctx.Rec, before));

            // 되돌리기(모드는 지정 안 함 = 지운다)
            ctx.Boot.Wire.CurrentTag = "ax/5-restore";
            var back = (await ctx.DeviceApi.GetCameraByIdAsync(cameraId).ConfigureAwait(false)).Data!.ToCameraDeviceModel();
            var protocolBack = Str(original?.SelectToken("connection.protocol"));
            var restore = await writer.ApplyAsync(new[] { back }, new[]
            {
                (S("connection.protocol"), protocolBack.Length > 0 ? protocolBack : "NONE"),
                (S("device_config.modes.weather_mode"), Str(original?.SelectToken("device_config.modes.weather_mode"))),
                (S("device_config.modes.day_night_mode"), Str(original?.SelectToken("device_config.modes.day_night_mode"))),
                (S("device_config.modes.is_record"), original?.SelectToken("device_config.modes.is_record")?.ToString().ToLowerInvariant() ?? ""),
            }).ConfigureAwait(false);
            var (_, rj) = await ctx.Raw.Get($"devices/cameras/{cameraId}").ConfigureAwait(false);
            var restored = Obj(rj["data"]);
            var keys = new[] { "connection.protocol", "device_config.modes.weather_mode", "device_config.modes.day_night_mode", "device_config.modes.is_record" };
            var diffs = keys.Where(k => !JToken.DeepEquals(Norm(original?.SelectToken(k)), Norm(restored?.SelectToken(k)))).ToList();
            ctx.Rec.Add("ax.5r", AX, "카메라 값을 원래대로 되돌리면 처음 읽은 값과 같다",
                restore.IsSuccess && diffs.Count == 0 ? Verdict.PASS : Verdict.FAIL,
                $"writer: sent={restore.SentCount} msg=[{restore.Message}]; differing=[{string.Join(",", diffs)}] " +
                string.Join(" ", keys.Select(k => $"{k}: was={original?.SelectToken(k)?.ToString() ?? "(absent)"} now={restored?.SelectToken(k)?.ToString() ?? "(absent)"}")));

            // ax.6 — 소속 부대 옮기기 · 되돌리기
            if (!isUnitEra) { ctx.Rec.Add("ax.6", AX, "unit move", Verdict.INFO, "server below 8.0 - unit field is hidden"); return; }
            ctx.Boot.Wire.CurrentTag = "ax/6-unit";
            var unitB = await fx.Unit("LRT-AX-UNIT-B", "lrtaxunitb");
            if (unitB <= 0) return;
            var originalUnit = (int?)original?["unit_id"];
            before = ctx.Rec.LastSeq();
            var cam = (await ctx.DeviceApi.GetCameraByIdAsync(cameraId).ConfigureAwait(false)).Data!.ToCameraDeviceModel();
            var moved = await writer.ApplyAsync(new[] { cam }, new[] { (S("unit_id"), unitB.ToString(System.Globalization.CultureInfo.InvariantCulture)) }).ConfigureAwait(false);
            var (_, mj) = await ctx.Raw.Get($"devices/cameras/{cameraId}").ConfigureAwait(false);
            var unitAfterMove = (int?)Obj(mj["data"])?["unit_id"];
            var cam2 = (await ctx.DeviceApi.GetCameraByIdAsync(cameraId).ConfigureAwait(false)).Data!.ToCameraDeviceModel();
            var movedBack = await writer.ApplyAsync(new[] { cam2 }, new[] { (S("unit_id"), originalUnit?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "") }).ConfigureAwait(false);
            var (_, bj) = await ctx.Raw.Get($"devices/cameras/{cameraId}").ConfigureAwait(false);
            var unitAfterBack = (int?)Obj(bj["data"])?["unit_id"];
            var ok6 = moved.IsSuccess && movedBack.IsSuccess && unitAfterMove == unitB && unitAfterBack == originalUnit && originalUnit != null;
            ctx.Rec.Add("ax.6", AX, "상세의 소속 부대를 다른 부대로 바꾸면 저장되고, 원래 부대로 되돌리면 처음 값과 같다",
                ok6 ? Verdict.PASS : Verdict.FAIL,
                $"original unit={originalUnit}; move -> {unitB}: sent={moved.SentCount} msg=[{moved.Message}] server={unitAfterMove}; back -> {originalUnit}: sent={movedBack.SentCount} msg=[{movedBack.Message}] server={unitAfterBack}",
                defectAt: ok6 ? "" : "Devices.Ui/Consoles/Properties/DevicePropertyCatalog.cs unit_id · DeviceAxisWriter",
                seqs: SeqsSince(ctx.Rec, before));
        }
        catch (Exception ex) { fx.Blocked("ax.5!", ex.ToString()); }
        finally
        {
            if (cameraId > 0)
            {
                try
                {
                    var (s, _) = await ctx.Raw.Delete($"devices/cameras/{cameraId}").ConfigureAwait(false);
                    if (s is 200 or 204) ctx.Rec.Deleted("camera", cameraId);
                    else ctx.Rec.Leftover("camera", cameraId, $"DELETE {s}");
                }
                catch (Exception ex) { ctx.Rec.Leftover("camera", cameraId, ex.Message); }
            }
            await fx.DisposeAsync();   // 부대 B — 카메라를 먼저 지운 뒤
        }
    }

    static async Task<JObject?> DataOf(ExtraContext ctx, int enclosureId)
    {
        var (s, j) = await ctx.Raw.Get($"devices/enclosures/{enclosureId}").ConfigureAwait(false);
        return s == 200 ? j["data"] as JObject : null;
    }

    static string ComponentKeys(JObject? device)
        => string.Join(",", (device?.SelectToken("hardware_spec.components") as JArray ?? new JArray())
            .Select(c => Str(c["key"])).OrderBy(k => k, StringComparer.Ordinal));

    static string? AxStr(JToken? token) => token is null || token.Type == JTokenType.Null ? null : token.ToString();

    static bool IsAbsent(JToken? token) => token is null || token.Type == JTokenType.Null;

    static JToken Norm(JToken? token) => token is null || token.Type == JTokenType.Null ? JValue.CreateNull() : token;

    static bool SameNumberOrText(string a, string b)
        => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase)
           || (double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
               && double.TryParse(b, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
               && x.Equals(y));
}
