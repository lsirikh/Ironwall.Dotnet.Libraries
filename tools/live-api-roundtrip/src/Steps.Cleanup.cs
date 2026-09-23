using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    /// <summary>
    /// D-17 정정 — 정리 스윕은 "0건" 을 주장하기 전에 그 주장을 낼 근거(인증된 200 + 실제 배열)가
    /// 있는지부터 확인한다.
    /// <para>
    /// 종전 스윕(이 하네스가 프로모트되기 전, 손으로 재확인한 회차)은 <c>/api/devices</c> 가 404 를,
    /// <c>/api/units</c> 가 401(무인증)을 냈는데 그 오류 바디를 "빈 목록"으로 세어 "잔여 0" 이라고
    /// 보고했다. 이 메서드는 그 실수를 구조적으로 막는다:
    /// </para>
    /// <list type="bullet">
    /// <item>스윕은 <see cref="Raw"/> 가 이미 로그인된 상태(<c>Token</c> 있음)에서만 실행한다.</item>
    /// <item>카테고리마다 실제 목록 엔드포인트를 부르고 <b>HTTP 200 인지부터</b> 확인한다.
    ///       200 이 아니면 그 카테고리는 <b>BLOCKED("확인 불가")</b> 로 남긴다 — "0건" 으로 세지 않는다.</item>
    /// <item>200 이더라도 서버가 페이지를 나눠 줬다면(<c>total_pages &gt; 1</c>) 1페이지만으로는
    ///       전수 확인이 아니므로 역시 확인 불가로 남긴다.</item>
    /// <item>전 카테고리가 검증 가능했을 때만 PASS/FAIL 을 낸다: 하나라도 <c>LRT-</c>/<c>lrt_</c>
    ///       접두 잔여물이 남아 있으면 FAIL, 전부 0건이면 PASS.</item>
    /// <item>단 하나라도 확인 불가한 카테고리가 있으면 전체 판정은 BLOCKED 다 — 그 카테고리에
    ///       숨은 잔여물이 있어도 이 스윕은 볼 수 없기 때문이다("확인 불가" 는 "클린" 이 아니다).</item>
    /// </list>
    /// </summary>
    public static async Task Item9_CleanupSweep(Bootstrap boot, Recorder rec, Raw raw)
    {
        const string TAG = "cleanup-sweep";
        boot.Wire.CurrentTag = TAG;

        if (string.IsNullOrEmpty(raw.Token))
        {
            rec.Add("9!", TAG, "스윕 사전조건: 인증된 raw 클라이언트", Verdict.BLOCKED,
                "raw.Token 이 비어 있다 - 인증 없이 스윕하면 각 카테고리가 401 을 내고, " +
                "그 401 바디를 '빈 목록' 으로 잘못 셀 위험이 있다(D-17 재발 방지)",
                blocked: "no auth token available for sweep");
            return;
        }

        // 이 하네스가 생성하는 모든 것의 name/login_id 는 이 두 접두 중 하나로 시작한다
        // (Steps.Core.cs / Steps.Rest.cs / Steps.Wiring.cs / Steps.Unit.cs 의 arrange 코드와 일치시킨다).
        var targets = new (string Kind, string Path, Func<JObject, string> NameOf)[]
        {
            ("controller",      "devices/controllers?page=1&limit=100",  d => Str(d["name_device"])),
            ("sensor",          "devices/sensors?page=1&limit=100",      d => Str(d["name_device"])),
            ("camera",          "devices/cameras?page=1&limit=100",      d => Str(d["name_device"])),
            ("speaker",         "devices/speakers?page=1&limit=100",     d => Str(d["name_device"])),
            ("enclosure",       "devices/enclosures?page=1&limit=100",   d => Str(d["name_device"])),
            ("lamp",            "devices/lamps?page=1&limit=100",        d => Str(d["name_device"])),
            ("gate",            "devices/gates?page=1&limit=100",        d => Str(d["name_device"])),
            ("device-group",    "devices/groups?page=1&limit=100",       d => Str(d["name"])),
            ("user-group",      "user-groups?page=1&limit=100",          d => Str(d["name"])),
            ("user",            "users?page=1&limit=100",                d => Str(d["login_id"])),
            ("report-template", "reports/templates?page=1&limit=100",    d => Str(d["name"])),
            ("unit",            "units?page=1&limit=100",                d => Str(d["name"])),
            // item 10 (D-22) seeds a server — a category the sweep cannot see is exactly the D-17
            // vacuous-clean hole, so it joins the list the moment the harness starts creating them.
            ("server",          "servers?page=1&limit=100",              d => Str(d["name"])),
            // D-34 seeds action-report templates; the list has no name field, the text lives in "content".
            ("action-report-template", "events/action-report-templates", d => Str(d["content"])),
        };

        var unverifiable = new List<string>();
        var verifiedClean = new List<string>();
        var leftovers = new List<string>();

        foreach (var t in targets)
        {
            var (status, jo) = await raw.Get(t.Path).ConfigureAwait(false);
            if (status != 200)
            {
                unverifiable.Add($"{t.Kind}(HTTP {status})");
                continue;
            }

            // "pagination": null (unpaged lists such as action-report templates) is a JValue, not C# null.
            var totalPages = (int?)(jo["pagination"] as JObject)?["total_pages"] ?? 1;
            if (totalPages > 1)
            {
                unverifiable.Add($"{t.Kind}({totalPages}페이지, limit=100 로 전수 확인 불가)");
                continue;
            }

            var arr = jo["data"] as JArray ?? new JArray();
            var matches = arr.Select(x => x as JObject).Where(d => d != null)
                              .Where(d => IsOurs(t.NameOf(d!)))
                              .Select(d => $"{t.Kind}:{d!["id"]}:{t.NameOf(d)}")
                              .ToList();
            if (matches.Count > 0) leftovers.AddRange(matches);
            else verifiedClean.Add(t.Kind);
        }

        if (unverifiable.Count > 0)
        {
            rec.Add("9", TAG,
                "정리 스윕 — '잔여 0' 을 주장하려면 전 카테고리가 인증된 200 으로 조회돼야 한다",
                Verdict.BLOCKED,
                $"확인 불가 {unverifiable.Count}/{targets.Length}종: {string.Join(", ", unverifiable)}; " +
                $"검증되어 0건인 카테고리 {verifiedClean.Count}종: {string.Join(",", verifiedClean)}" +
                (leftovers.Count > 0
                    ? $"; 조회 가능했던 범위에서 이미 발견된 잔여물 {leftovers.Count}건: {string.Join(", ", leftovers)}"
                    : "; 조회 가능했던 범위에서는 잔여물 없음"),
                blocked: "일부 카테고리를 200 으로 확인하지 못했다 - 그 카테고리는 '확인 불가' 이지 " +
                         "'클린' 이 아니다(D-17: 과거 스윕은 여기서 404/401 을 '잔여 0' 으로 오판했다)");
            foreach (var l in leftovers) rec.Leftover("sweep", l, "post-run sweep (confirmed even though other categories were unverifiable)");
        }
        else if (leftovers.Count > 0)
        {
            rec.Add("9", TAG, "정리 스윕 — 전 카테고리 조회 가능, LRT-/lrt_ 접두 잔여물 발견",
                Verdict.FAIL,
                $"잔여 {leftovers.Count}건: {string.Join(", ", leftovers)}",
                defectAt: "하네스 finally 블록이 놓친 생성물이거나(각 Item 의 cleanup 순서 확인) " +
                          "서버 DELETE 가 실제로 반영되지 않았을 가능성 - 원인 분리 필요");
            foreach (var l in leftovers) rec.Leftover("sweep", l, "post-run sweep");
        }
        else
        {
            rec.Add("9", TAG, $"정리 스윕 — 전 카테고리({targets.Length}종) 인증된 200 으로 확인, LRT-/lrt_ 접두 잔여물 0건",
                Verdict.PASS,
                $"검증된 카테고리: {string.Join(",", verifiedClean)}");
        }
    }

    static bool IsOurs(string name)
        => !string.IsNullOrEmpty(name)
           && (name.StartsWith("LRT-", StringComparison.Ordinal) || name.StartsWith("lrt_", StringComparison.Ordinal));
}
