using System.Diagnostics;
using System.IO;
using System.Reflection;
using Ironwall.Dotnet.Libraries.Sounds.Services;

namespace LiveNatsPipeline;

public static class Runner
{
    public static async Task<int> RunAsync(string label, string[] only, string outDir, string natsUrl, string reportPath, bool real = false)
    {
        var rec = new Recorder();
        var started = DateTime.Now;
        var pipeline = new Pipeline { Real = real };
        await using var pub = new Publisher();
        var meta = new Dictionary<string, object?>
        {
            ["label"] = label, ["mode"] = real ? "real inventory (dedicated account, GET only)" : "fake server (no network)", ["started"] = started.ToString("o"), ["nats"] = natsUrl, ["group"] = Safety.SubjectPrefix + "*",
            ["events_ui"] = typeof(Ironwall.Dotnet.Libraries.Events.Ui.Managers.EventQueueManager).Assembly.Location,
            ["host_linked"] = "NatsBrokerService.cs, NatsDomainService.cs, INatsBrokerService.cs, RecentEnvelopeIds.cs",
        };

        try
        {
            await pipeline.BuildAsync(outDir, natsUrl);
            await pub.StartAsync(natsUrl);
            meta["nats_server_version"] = pub.ServerVersion;
            meta["nats_max_payload"] = pub.MaxPayload;
            rec.Notes.Add($"NATS {natsUrl} (server {pub.ServerVersion}, max_payload {pub.MaxPayload} B) · 파이프라인 구독 {pipeline.Nats.Subject} + sensorway.unit999.all.> + sensorway.global.> · 발행 {Safety.SubjectPrefix}* 전용");
            rec.Notes.Add($"장비 캐시({(real ? "실서버 GET · 전용 계정 1회 로그인" : "가짜 서버")} → 라이브러리 DeviceProviderService 실경로): {pipeline.Devices.Count()}대 — " +
                          string.Join(", ", pipeline.Devices.Select(d => $"{d.Id}:{d.DeviceType}[{string.Join("/", d.DeviceGroups ?? new())}]")));

            // readiness: the pipeline must actually be receiving on unit999 before any verdict counts
            var ctx = new Ctx(pipeline, pub, rec);
            var ready = false;
            for (var i = 0; i < 20 && !ready; i++)
            {
                var mark = pipeline.Log.Mark;
                await pub.PublishAsync(Env.S("all.probe.ping"), Env.Envelope("PROBE_PING", new Newtonsoft.Json.Linq.JObject()));
                ready = await Ctx.WaitUntil(() => ctx.CountLogs(mark, "PROBE_PING") > 0, 500);
            }
            if (!ready)
            {
                rec.Add("S00", "파이프라인 수신 준비", "unit999 봉투가 파이프라인에 닿는다", "10초 동안 한 건도 닿지 않음", Verdict.BLOCKED, owner: "harness");
                return Finish(rec, meta, outDir, reportPath, label, 2);
            }
            rec.Add("S00", "파이프라인 수신 준비", "unit999 봉투가 파이프라인(호스트 라우터)에 닿는다", "PROBE_PING 수신 확인(호스트 'Unknown type_command' 경고로 관측)", Verdict.PASS, owner: "harness");

            foreach (var (id, run) in real ? RealScenarios.All(pipeline) : Scenarios.All())
            {
                if (only.Length > 0 && !only.Any(o => id.StartsWith(o, StringComparison.OrdinalIgnoreCase))) continue;
                await ctx.Reset();
                var sw = Stopwatch.StartNew();
                try { await run(ctx); }
                catch (Exception ex)
                {
                    rec.Add(id, "(시나리오 실행 중 예외)", "-", ex.GetType().Name + ": " + ex.Message, Verdict.BLOCKED, ex.ToString(), "harness");
                }
                Console.WriteLine($"   ({id} {sw.ElapsedMilliseconds} ms)");
            }

            var blocked = pipeline.Server.Requests.Where(r => r.BlockedWrite).ToList();
            rec.Notes.Add($"REST: {(real ? "실서버(ReadOnlyGate 통과분)" : "가짜 서버")} 요청 {pipeline.Server.Requests.Count}건(쓰기 차단 {blocked.Count}건: " +
                          string.Join(", ", blocked.Select(b => $"{b.Method} {b.Path}").Distinct()) + ")" + (real ? " — 행 생성·수정 0건" : " — 실제 네트워크 요청 0건"));
            rec.Notes.Add($"파이프라인 자신의 NATS 발행 {pipeline.Nats.Published.Count}건: " +
                          string.Join(", ", pipeline.Nats.Published.Select(p => p.Subject).Distinct()));
            rec.Notes.Add($"사운드(가짜) 호출 {RecordingProxy<ISoundService>.Calls.Count}건 (Redis EVENT_CALL 은 FR-19 로 제거)");
            rec.Notes.Add($"자동복구 발화 {pipeline.AutoRecoveryFired.Count}건 · 자동조치보고 발화 {pipeline.AutoReportFired.Count}건");
        }
        catch (Exception ex)
        {
            rec.Add("S99", "하네스", "-", ex.ToString(), Verdict.BLOCKED, owner: "harness");
        }
        finally
        {
            try { await pipeline.Nats?.StopAsync()!; } catch { }
        }
        return Finish(rec, meta, outDir, reportPath, label, rec.Results.Any(r => r.Verdict == Verdict.FAIL) ? 1 : 0);
    }

    static int Finish(Recorder rec, Dictionary<string, object?> meta, string outDir, string reportPath, string label, int code)
    {
        meta["finished"] = DateTime.Now.ToString("o");
        rec.WriteJson(Path.Combine(outDir, "results.json"), meta);
        var header = $"# 이벤트 라이브 파이프라인 프로브 — `{label}`\n\n" +
                     $"> 생성: {DateTime.Now:yyyy-MM-dd HH:mm:ss} · 도구 `tools/live-nats-pipeline` (WP-5) · 루프백 NATS · **그룹 `sensorway.unit999`**(헤디드 GIS 의 unit001 과 격리) · REST 는 프로세스 안 가짜 서버(쓰기 전면 차단, 테스트 서버 로그인 0회).\n" +
                     $"> 라이브러리 = `{meta["events_ui"]}` · 호스트 = 링크 컴파일({meta["host_linked"]}).\n" +
                     $"> 상세 로그: `tools/live-nats-pipeline/out/{label}/pipeline.log` · 원자료: `out/{label}/results.json`";
        var mdPath = reportPath.EndsWith(".md") && label != "run" ? reportPath.Replace(".md", $".{label}.md") : reportPath;
        rec.WriteMarkdown(mdPath, header);
        Console.WriteLine($"\n== report -> {mdPath}");
        Console.WriteLine($"== PASS {rec.Results.Count(r => r.Verdict == Verdict.PASS)} / FAIL {rec.Results.Count(r => r.Verdict == Verdict.FAIL)} / INFO {rec.Results.Count(r => r.Verdict == Verdict.INFO)} / BLOCKED {rec.Results.Count(r => r.Verdict == Verdict.BLOCKED)}");
        return code;
    }
}
