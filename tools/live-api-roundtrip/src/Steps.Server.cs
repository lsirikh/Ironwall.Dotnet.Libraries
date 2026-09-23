// Item 10 (D-22) — wired into Steps.RunAll() after item 8 and before the cleanup sweep.
// Drafted while another run owned the seeded server; wired once that round finished.
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    // ================= item 10 (draft): N-12/D-22 server axis round trip =================
    // ServerAxisContractTests.cs (Devices.Api/Tests) already proves ServerAxisWriter/Reader are
    // correct as PURE functions against synthetic JSON. This item proves the same channel against
    // the REAL loopback server end to end (create -> re-GET -> patch -> re-GET), and proves the
    // two defects this whole node chases are actually closed against a live response:
    //   22a/22d: IServerAxisApiService (what ServerConsoleService/the server console actually
    //            calls) writes the shape this server's contract expects and the value sticks.
    //   22b:     the LEGACY flat IServerApiService.CreateServerAsync (what the harness would have
    //            reached for before N-12 existed) refuses before touching the wire on an axis-era
    //            contract instead of shipping a doomed 422 body (D-22 write-side gate).
    //   22c:     the LEGACY flat IServerApiService.GetServersAsync — still the read path behind
    //            DeviceProviderService's speaker-server dropdown — resolves real IP/port off an
    //            axis-shaped response instead of silently coming back empty (D-22 read-side fix,
    //            ServerDto.OnDeserializedMethod).
    public static async Task Item10_ServerAxis(Bootstrap boot, Recorder rec, Raw raw)
    {
        const string TAG = "D-22";
        int serverId = 0;
        try
        {
            var axis = new ServerAxisApiService(boot.Api, boot.Setup, boot.Probe, boot.Log);
            var legacy = new ServerApiService(boot.Log, boot.Api, boot.Setup, boot.Probe);
            var isAxis = boot.Probe.Contract >= EnumServerContract.V7_0;

            boot.Wire.CurrentTag = TAG + "/categories";
            var categories = await axis.GetCategoriesAsync().ConfigureAwait(false);
            if (categories.Count == 0)
            {
                rec.Add("22.0", TAG, "arrange: GET /servers/categories", Verdict.BLOCKED,
                    "no server category seeded on the loopback server", blocked: "fixture missing");
                return;
            }
            var category = categories[0];

            // ---------- 22a: create through the axis-aware channel the console actually uses ----------
            boot.Wire.CurrentTag = TAG + "/create";
            var before = rec.LastSeq();
            var intent = new ServerWriteIntent
            {
                TypeServer = category.TypeServer,
                CategoryId = category.Id,
                Name = "LRT-D22-SERVER",
                IpAddress = "10.77.1.1",
                Port = 8199,
                Hostname = "lrt-d22",
            };
            var created = await axis.CreateServerAsync(intent).ConfigureAwait(false);
            if (!created.IsSuccess || created.Id <= 0)
            {
                rec.Add("22a", TAG, "IServerAxisApiService.CreateServerAsync 로 서버를 등록", Verdict.FAIL,
                    $"success={created.IsSuccess} msg='{created.Message}'",
                    defectAt: "Devices.Api/Servers/ServerAxisApiService.cs CreateServerAsync",
                    seqs: SeqsSince(rec, before));
                return;
            }
            serverId = created.Id;
            rec.Created("server", serverId);

            var (status, jo) = await raw.Get($"servers/{serverId}").ConfigureAwait(false);
            var data = jo["data"] as JObject;
            var shapeOk = status == 200 && (isAxis
                ? (string?)data?["connection"]?["ip_address"] == "10.77.1.1"
                  && (string?)data?["category_server"] == category.TypeServer
                : (string?)data?["ip_address"] == "10.77.1.1"
                  && (int?)data?["category_id"] == category.Id);
            rec.Add("22a", TAG, "등록한 서버가 실제로 서버에 남아 있고, 이 판본이 기대하는 모양(축/평면)이다",
                shapeOk ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {status}; contract={boot.Probe.Contract}; data={Short(jo)}",
                defectAt: shapeOk ? "" : "Devices.Api/Servers/ServerAxisContracts.cs ServerAxisWriter.BuildCreate",
                seqs: SeqsSince(rec, before));

            // ---------- 22b: the legacy flat channel must not ship a doomed body on an axis contract ----------
            boot.Wire.CurrentTag = TAG + "/legacy-gate";
            before = rec.LastSeq();
            var legacyResult = await legacy.CreateServerAsync(new ServerDto
            {
                CategoryId = category.Id,
                Name = "LRT-D22-LEGACY-SHOULD-NOT-SEND",
                IpAddress = "10.77.1.2",
                Port = 1,
            }).ConfigureAwait(false);
            var wentOverTheWire = rec.Since(before).Any(w => w.Tag == boot.Wire.CurrentTag);
            var gateOk = isAxis
                ? !legacyResult.Success && legacyResult.Error?.Code == "AXIS_SHAPE_REQUIRED" && !wentOverTheWire
                : legacyResult.Success && wentOverTheWire; // 6.3 loopback: legacy channel is still the normal path
            rec.Add("22b", TAG,
                isAxis
                    ? "축 계약에서 레거시 IServerApiService.CreateServerAsync 는 네트워크로 나가지 않고 거부한다(D-22)"
                    : "6.3 계약에서 레거시 채널은 평상시처럼 동작한다(회귀 없음)",
                gateOk ? Verdict.PASS : Verdict.FAIL,
                $"contract={boot.Probe.Contract}; success={legacyResult.Success} code={legacyResult.Error?.Code} " +
                $"msg='{legacyResult.Message}'; wentOverTheWire={wentOverTheWire}",
                defectAt: gateOk ? "" : "Devices.Api/Services/ServerApiService.cs CreateServerAsync (AXIS_SHAPE_REQUIRED gate)",
                seqs: SeqsSince(rec, before));
            // legacy create must never have registered a second server - only clean up if it somehow did.
            if (legacyResult.Success && legacyResult.Data is { Id: > 0 } leaked)
                { rec.Created("server", leaked.Id); await raw.Delete($"servers/{leaked.Id}").ConfigureAwait(false); rec.Deleted("server", leaked.Id); }

            // ---------- 22c: the legacy READ channel must still resolve real values off an axis response ----------
            boot.Wire.CurrentTag = TAG + "/legacy-read";
            before = rec.LastSeq();
            var legacyList = await legacy.GetServersAsync(page: 1, limit: 100, view: isAxis ? "full" : null).ConfigureAwait(false);
            var legacyDto = legacyList.Data?.FirstOrDefault(d => d.Id == serverId);
            var readOk = legacyDto is not null && legacyDto.IpAddress == "10.77.1.1" && legacyDto.Port == 8199;
            rec.Add("22c", TAG,
                "레거시 IServerApiService.GetServersAsync 가 역직렬화한 ServerDto 도 실제 접속 정보를 담는다(D-22 읽기)",
                readOk ? Verdict.PASS : Verdict.FAIL,
                $"found={legacyDto is not null} IpAddress='{legacyDto?.IpAddress}' Port={legacyDto?.Port} " +
                $"CategoryServer='{legacyDto?.CategoryServer}' CategoryId={legacyDto?.CategoryId}",
                defectAt: readOk ? "" : "Messages/Dto/Devices/ServerDto.cs OnDeserializedMethod",
                seqs: SeqsSince(rec, before));

            // ---------- 22d: patch through the axis channel, re-verify ----------
            boot.Wire.CurrentTag = TAG + "/patch";
            before = rec.LastSeq();
            var patched = await axis.PatchServerAsync(serverId, new ServerWriteIntent { Port = 8200 }).ConfigureAwait(false);
            var (pstatus, pjo) = await raw.Get($"servers/{serverId}").ConfigureAwait(false);
            var pdata = pjo["data"] as JObject;
            var patchOk = patched.IsSuccess && pstatus == 200 && (isAxis
                ? (int?)pdata?["connection"]?["ip_port"] == 8200
                : (int?)pdata?["port"] == 8200);
            rec.Add("22d", TAG, "IServerAxisApiService.PatchServerAsync 로 수정한 값이 실제로 반영된다",
                patchOk ? Verdict.PASS : Verdict.FAIL,
                $"patch success={patched.IsSuccess} msg='{patched.Message}'; re-GET HTTP {pstatus}; data={Short(pjo)}",
                defectAt: patchOk ? "" : "Devices.Api/Servers/ServerAxisContracts.cs ServerAxisWriter.BuildPatch",
                seqs: SeqsSince(rec, before));
        }
        catch (Exception ex)
        {
            rec.Add("22!", TAG, "server axis round trip", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
        finally
        {
            if (serverId > 0)
            {
                var (s, _) = await raw.Delete($"servers/{serverId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("server", serverId); else rec.Leftover("server", serverId, $"DELETE {s}");
            }
        }
    }
}
