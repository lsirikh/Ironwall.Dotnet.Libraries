using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using static LiveNatsPipeline.Ctx;

namespace LiveNatsPipeline;

/// <summary>
/// --real mode: the device cache is the loopback test server's REAL inventory (GET only, dedicated account).
/// Events still go to sensorway.unit999.* only — the server never sees them, the headed GIS (unit001) neither.
/// </summary>
public static class RealScenarios
{
    static DevSpec Spec(IBaseDeviceModel d)
        => new(d.Id, DeviceTypeResolver.CategoryOf(d.DeviceType).ToString().ToLowerInvariant(), d.DeviceType.ToString(),
               d.DeviceNumber, d.DeviceName ?? "", (d.DeviceGroups ?? new()).ToArray(), (d as ISensorDeviceModel)?.Controller?.Id);

    public static IEnumerable<(string Id, Func<Ctx, Task> Run)> All(Pipeline p)
    {
        var byType = p.Devices.ToList().GroupBy(d => d.DeviceType).OrderBy(g => g.Key).Select(g => g.First()).ToList();
        yield return ("R00", c =>
        {
            var all = p.Devices.ToList();
            c.Rec.Add("R00", "실서버 장비 캐시(라이브러리 DeviceProviderService · GET 전용)",
                "장비가 적재되고 종류가 NONE 이 아님",
                $"{all.Count}대 · 종류 {string.Join(", ", all.GroupBy(d => d.DeviceType).Select(g => $"{g.Key}×{g.Count()}"))} · 그룹 {all.SelectMany(d => d.DeviceGroups ?? new()).Distinct().Count()}개 · 종류 NONE {all.Count(d => d.DeviceType == EnumDeviceType.NONE)}대",
                all.Count > 0 && all.All(d => d.DeviceType != EnumDeviceType.NONE) ? Verdict.PASS : Verdict.FAIL, "", "");
            return Task.CompletedTask;
        });

        foreach (var dev in byType)
        {
            var d = Spec(dev);
            yield return ($"R01.{dev.DeviceType}", async c =>
            {
                var e = c.NextEventId(); var mark = c.P.Log.Mark;
                var env = await c.Detect(d, e);
                var groups = d.Groups.Where(g => c.P.GroupSymbols.ContainsKey(g)).ToArray();
                var ok = await WaitUntil(() => c.EntryFor(e, EnumEventType.Intrusion) is { } en && en.EntryId == env && en.DeviceType == dev.DeviceType
                                              && c.Dev(d) == EnumCompositeEventStatus.Detecting && groups.All(g => c.Grp(g) == EnumCompositeEventStatus.Detecting)
                                              && c.Cards().Any(x => x.EventId == e && x.EntryId == env));
                await c.ActionReport(Env.DetectBody(e, Env.Ref(d.Id, d.Category)));
                var cleared = await WaitUntil(() => c.Entries().Count == 0 && c.Cards().Count == 0 && c.Dev(d) == EnumCompositeEventStatus.Normal);
                c.Rec.Add($"R01.{dev.DeviceType}", $"실장비 {d.Category}:{dev.DeviceType}({d.Id}, 그룹 {string.Join("/", d.Groups)}) DETECT → 조치보고",
                    "EQM(종류=캐시 종류) · 심볼·그룹 Detecting · 카드 → 조치보고로 전부 정리",
                    $"수신 반영={ok} · 정리={cleared} · EQM[{c.EntriesText()}] 카드[{c.CardsText()}] 심볼={c.Dev(d)}",
                    ok && cleared ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "DETECTION", "종류", "매핑"), ok && cleared ? "" : "WP-1");
            });
        }

        // controller with the most sensors → blackout covers exactly the union of its sensors' groups
        var ctrl = p.Devices.OfType<ISensorDeviceModel>().Where(s => s.Controller?.Id > 0)
            .GroupBy(s => s.Controller!.Id).OrderByDescending(g => g.Count()).FirstOrDefault();
        if (ctrl != null)
        {
            var ctrlDev = p.Devices.First(x => x.Id == ctrl.Key && x.DeviceType == EnumDeviceType.Controller);
            var expect = ctrl.SelectMany(s => s.DeviceGroups ?? new()).Concat(ctrlDev.DeviceGroups ?? new()).Distinct().OrderBy(g => g).ToArray();
            yield return ("R02", async c =>
            {
                var d = Spec(ctrlDev); var e = c.NextEventId(); var mark = c.P.Log.Mark;
                await c.Malfunction(d, e, "FAULT_CONTROLLER");
                var ok = await WaitUntil(() => c.EntryFor(e, EnumEventType.Fault) is { IsControllerBlackout: true } en
                                              && expect.All(g => en.GroupIds?.Contains(g) == true)
                                              && expect.Where(g => c.P.GroupSymbols.ContainsKey(g)).All(g => c.Grp(g) == EnumCompositeEventStatus.Blackout));
                var en2 = c.EntryFor(e, EnumEventType.Fault);
                c.Rec.Add("R02", $"실제어기 {ctrlDev.Id}(센서 {ctrl.Count()}대) FAULT_CONTROLLER → 블랙아웃 그룹 확장",
                    $"블랙아웃 그룹 = 소속 센서 그룹 합집합 [{string.Join(",", expect)}] · 그 그룹 심볼 Blackout",
                    $"엔트리 그룹=[{string.Join(",", en2?.GroupIds ?? new())}] · " + string.Join(" ", expect.Select(g => $"{g}:{c.Grp(g)}")),
                    ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "제어기무통신", "blackout"), ok ? "" : "WP-1");
            });
        }

        // SYNC_DEVICE UPDATED on a real sensor → real GET round trip through the library
        var sensor = p.Devices.OfType<ISensorDeviceModel>().FirstOrDefault();
        if (sensor != null)
        {
            yield return ("R03", async c =>
            {
                var mark = c.P.Log.Mark; var t0 = DateTime.Now;
                await c.Sync(Env.SyncDevice, "SYNC_DEVICE", Env.SyncBody("UPDATED", sensor.Id, "sensor"));
                var ok = await WaitUntil(() => c.CountLogs(mark, "SyncDeviceStatus") > 0, 5000);
                c.Rec.Add("R03", $"실센서 {sensor.Id} SYNC_DEVICE UPDATED(가짜 알림, unit999) → 실서버 GET 재조회",
                    "GET /devices/sensors/{id} 200 → 캐시 갱신 → 심볼 상태 동기화(쓰기 0)",
                    $"REST[{string.Join(",", c.RestSince(t0).Select(r => r.Method + " " + r.Path + ":" + r.Status))}] 처리={ok}",
                    ok ? Verdict.PASS : Verdict.FAIL, c.LogsText(mark, "SYNC_DEVICE", "Fetch"), ok ? "" : "WP-1");
            });
        }
    }
}
