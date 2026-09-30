using System.Text;
using System.Text.Json;
using Sso.Client.Sdk;

namespace SsoTokenProbe;

/// <summary>
/// P1 스파이크 — SSO 앱 토큰의 실제 클레임을 실측한다(버림 코드).
///
/// <para><b>무엇을 확인하나</b>: PRD 가 "미검증" 으로 남겨 둔 것들 —
/// U17(토큰의 실제 <c>iss</c> 가 서버 디스커버리 값과 같은가) · <c>aud=gop-api</c> 실림 ·
/// <c>typ</c>/<c>alg</c>/<c>kid</c>/<c>jti</c>/<c>sid</c>/<c>sub</c> 존재 · 수명 3600초 ·
/// <c>refresh_token</c> 유무 · SDK 가 내는 <c>SsoOutcome</c> 값.</para>
///
/// <para><b>서버에 쓰지 않는다</b> — 에이전트 파이프에서 토큰을 받아 <b>열어보기만</b> 한다.
/// GOP 로는 아무 요청도 보내지 않는다.</para>
///
/// <para><b>서명 검증은 하지 않는다</b> — 그것은 GOP 몫이다(교환 규칙 #1·#2).
/// 여기서는 헤더·페이로드를 base64url 로 풀어 <b>값만</b> 본다.</para>
/// </summary>
internal static class Program
{
    private static readonly string ClientId =
        Environment.GetEnvironmentVariable("SSO_PROBE_CLIENT_ID") ?? "gis-monitoring";

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        var dumpTokens = args.Contains("--dump-tokens");

        // 오프라인 판정 모드 — 등록·에이전트 없이 판정부만 벡터로 검증한다.
        //   합성 토큰은 SSO 가 준 참조 시험의 공장(app_claims/sign)이 만든 것이라
        //   '우리 판정이 SSO 가 내는 모양을 제대로 읽는가' 를 실제로 본다.
        var offline = Array.IndexOf(args, "--verify-file") >= 0
            ? args[Array.IndexOf(args, "--verify-file") + 1]
            : null;
        if (offline is not null) return VerifyVectors(offline);

        Line("SSO 앱 토큰 프로브 (P1 스파이크)");
        Line($"client_id = {ClientId}");
        if (dumpTokens)
            Line("⚠ --dump-tokens : 토큰 원문을 파일로 남긴다. 표본 전달용이며 끝나면 지울 것.");
        Console.WriteLine();

        using var client = new SsoAgentClient(ClientId);
        Line($"파이프 주소 : {client.Address}");

        // ── 1) 디스커버리 ────────────────────────────────────────
        var disco = await client.DiscoverAsync().ConfigureAwait(false);
        Line($"DiscoverAsync → {disco.Outcome}  {Short(disco.Detail)}");
        if (!disco.IsOk)
        {
            Line("에이전트에 닿지 못했다. 트레이 에이전트가 떠 있는지 확인하라.");
            return 2;
        }

        var d = disco.Value!;
        Line($"  salp_version = {d.SalpVersion}");
        Line($"  issuer(ⓒ 에이전트가 서버에 닿는 주소) = {d.Issuer}");
        Line($"  features = {string.Join(", ", d.Features ?? Array.Empty<string>())}");
        Console.WriteLine();

        // ── 2) 앱 토큰 발급 ──────────────────────────────────────
        //    티켓 인자 없이 = 갱신·401 복구가 쓰는 바로 그 경로(C-16).
        Line("SignInAsync([]) — 갱신·401 복구가 쓰는 경로");
        var signIn = await client.SignInAsync(Array.Empty<string>()).ConfigureAwait(false);
        Line($"  → {signIn.Outcome}  {Short(signIn.Detail)}");

        if (!signIn.IsOk)
        {
            Line("");
            Line("토큰을 받지 못했다. 값으로 온 결과이므로 예외가 아니다 — 그것 자체가 FR-01 의 확인 항목이다.");
            Line($"기대 대응: {Guidance(signIn.Outcome)}");
            return 3;
        }

        var t = signIn.Value!;
        Line($"  session_id   = {t.SessionId}");
        Line($"  x_client_id  = {t.XClientId}");
        Line($"  token_type   = {t.TokenType}");
        Line($"  expires_in   = {t.ExpiresIn}");
        Line($"  refresh_token= {(string.IsNullOrEmpty(t.RefreshToken) ? "(없음)" : $"있음 · {t.RefreshToken.Length}자")}");
        Console.WriteLine();

        // ── 3) 토큰 해부 ────────────────────────────────────────
        Line("access_token 해부 (서명 검증 없음 — 값만 본다)");
        if (!TryDecode(t.AccessToken, out var header, out var payload, out var why))
        {
            Line($"  해부 실패: {why}");
            return 4;
        }

        Line("  [헤더]");
        foreach (var kv in Flatten(header)) Line($"    {kv}");
        Line("  [페이로드]");
        foreach (var kv in Flatten(payload)) Line($"    {kv}");
        Console.WriteLine();

        // ── 4) 계약 판정 ────────────────────────────────────────
        Line("계약 판정");
        var fail = 0;
        fail += Check("typ = at+jwt", Str(header, "typ") == "at+jwt", Str(header, "typ"));
        fail += Check("alg = RS256", Str(header, "alg") == "RS256", Str(header, "alg"));
        fail += Check("kid 존재", !string.IsNullOrEmpty(Str(header, "kid")), Str(header, "kid"));
        fail += Check("iss 존재", !string.IsNullOrEmpty(Str(payload, "iss")), Str(payload, "iss"));
        fail += Check("aud = gop-api 포함", HasAud(payload, "gop-api"), Str(payload, "aud"));
        fail += Check("client_id = " + ClientId, Str(payload, "client_id") == ClientId, Str(payload, "client_id"));
        fail += Check("sub 존재(= GOP account_users.id)", !string.IsNullOrEmpty(Str(payload, "sub")), Str(payload, "sub"));
        fail += Check("sid 존재", !string.IsNullOrEmpty(Str(payload, "sid")), Str(payload, "sid"));
        fail += Check("jti 존재", !string.IsNullOrEmpty(Str(payload, "jti")), Str(payload, "jti"));

        var iat = Num(payload, "iat");
        var exp = Num(payload, "exp");
        var life = (iat is not null && exp is not null) ? exp - iat : null;
        fail += Check("exp − iat = 3600", life == 3600, life?.ToString() ?? "(계산 불가)");

        // ⚠ 이 둘은 '경고' 다 — 실패가 아니라 기록해야 할 사실.
        Console.WriteLine();
        Line("대조 (판정이 아니라 기록)");
        Line($"  토큰 iss(ⓑ)                = {Str(payload, "iss")}");
        Line($"  에이전트 디스커버리 issuer(ⓒ) = {d.Issuer}");
        Line(Str(payload, "iss") == d.Issuer
            ? "  → 두 값이 같다"
            : "  → 두 값이 다르다. 뜻이 다른 자리라 정상일 수 있다 — GOP 의 OIDC_ISSUER 는 **서버 디스커버리(ⓐ)** 에서 가져가야 한다");
        Line($"  sub 가 숫자인가            = {(long.TryParse(Str(payload, "sub"), out _) ? "예" : "아니오 — GOP account_users.id 전제가 깨진다")}");

        if (dumpTokens) DumpSamples(t.AccessToken, header, payload);

        Console.WriteLine();
        Line(fail == 0 ? "판정: 전부 충족" : $"판정: {fail}건 불충족 — 위 표시를 보라");
        return fail == 0 ? 0 : 1;
    }

    /// <summary>
    /// 오프라인 판정 — 합성 토큰 묶음(JSON: 이름 → JWT)을 읽어 각 토큰의 판정 결과를 표로 낸다.
    /// <b>서명은 보지 않는다</b>(GOP 몫). 여기서 보는 것은 "우리가 클레임을 제대로 읽는가" 뿐이다.
    /// </summary>
    private static int VerifyVectors(string path)
    {
        Line($"오프라인 판정 — {path}");
        Console.WriteLine();

        var json = JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        var bad = 0;
        foreach (var p in json.EnumerateObject())
        {
            if (!TryDecode(p.Value.GetString() ?? "", out var h, out var pay, out var why))
            {
                Line($"  {p.Name,-20} 해부 실패: {why}");
                bad++;
                continue;
            }

            var iat = Num(pay, "iat"); var exp = Num(pay, "exp");
            var life = (iat is not null && exp is not null) ? exp - iat : null;
            var cid = Str(pay, "client_id");

            Line($"  {p.Name,-20} typ={Str(h, "typ"),-11} alg={Str(h, "alg"),-6} kid={(string.IsNullOrEmpty(Str(h, "kid")) ? "(없음)" : "있음"),-6} " +
                 $"aud={(HasAud(pay, "gop-api") ? "gop-api" : "(없음/불일치)"),-14} client_id={cid,-16} " +
                 $"sub={(long.TryParse(Str(pay, "sub"), out _) ? "숫자" : "비숫자"),-6} 수명={(life?.ToString() ?? "?")}");
        }

        Console.WriteLine();
        Line(bad == 0
            ? "판정부가 모든 벡터를 읽었다 — 클레임 추출·aud 배열/문자열·sub 숫자 판정 동작 확인"
            : $"{bad}건 해부 실패");
        return bad == 0 ? 0 : 1;
    }

    // ── 도우미 ──────────────────────────────────────────────────

    private static string Guidance(SsoOutcome o) => o switch
    {
        SsoOutcome.AgentNotRunning => "기존 id/pw 로그인으로 폴백(FR-01 · S2)",
        SsoOutcome.VersionMismatch => "기존 id/pw 로그인으로 폴백 — 에이전트 판본 불일치",
        SsoOutcome.NoActiveSession => "로그인 화면 → SignInInteractiveAsync (반복 금지)",
        SsoOutcome.UnknownClient => "에이전트 세션 유무로 안내를 가른다 — 세션 없으면 '에이전트에서 로그인', 있으면 '관리자에게 앱 등록 요청'(T29)",
        SsoOutcome.ConsentRequired => "사용자 허용 창을 기다린다 — 등록 경로의 실행 파일이 아니면 뜬다(T30)",
        _ => "값으로 온 실패다. 재시도 루프에 넣지 않는다",
    };

    private static int Check(string name, bool ok, string actual)
    {
        Line($"  {(ok ? "OK  " : "FAIL")} {name,-34} 실제: {(string.IsNullOrEmpty(actual) ? "(없음)" : actual)}");
        return ok ? 0 : 1;
    }

    private static bool TryDecode(string jwt, out JsonElement header, out JsonElement payload, out string why)
    {
        header = default; payload = default; why = string.Empty;
        var parts = jwt.Split('.');
        if (parts.Length < 2) { why = $"마디가 {parts.Length}개 — JWT 가 아니다"; return false; }
        try
        {
            header = JsonDocument.Parse(B64Url(parts[0])).RootElement.Clone();
            payload = JsonDocument.Parse(B64Url(parts[1])).RootElement.Clone();
            return true;
        }
        catch (Exception ex) { why = ex.Message; return false; }
    }

    private static byte[] B64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    private static IEnumerable<string> Flatten(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object) yield break;
        foreach (var p in e.EnumerateObject())
        {
            var v = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString() ?? "",
                JsonValueKind.Array => "[" + string.Join(", ", p.Value.EnumerateArray().Select(x => x.ToString())) + "]",
                _ => p.Value.ToString(),
            };
            // 토큰 자체가 길 수 있으니 줄을 접는다.
            yield return $"{p.Name} = {(v.Length > 120 ? v[..120] + "…" : v)}";
        }
    }

    private static string Str(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString())
            : "";

    private static long? Num(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : null;

    private static bool HasAud(JsonElement payload, string want)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("aud", out var aud)) return false;
        if (aud.ValueKind == JsonValueKind.String) return aud.GetString() == want;
        if (aud.ValueKind == JsonValueKind.Array)
            return aud.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == want);
        return false;
    }

    /// <summary>서버팀이 요청한 '토큰 표본' 을 파일로 남긴다(원문 포함 — 전달 후 지울 것).</summary>
    private static void DumpSamples(string accessToken, JsonElement header, JsonElement payload)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "samples");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{ClientId}-app-token.txt");
        var sb = new StringBuilder();
        sb.AppendLine($"# {ClientId} 앱 토큰 표본 (개발 서버) — 전달 후 삭제");
        sb.AppendLine("## header");
        foreach (var kv in Flatten(header)) sb.AppendLine("  " + kv);
        sb.AppendLine("## payload");
        foreach (var kv in Flatten(payload)) sb.AppendLine("  " + kv);
        sb.AppendLine("## raw");
        sb.AppendLine(accessToken);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        Line($"  표본 저장: {path}");
    }

    private static string Short(string s) => string.IsNullOrWhiteSpace(s) ? "" : $"({s.Trim()})";
    private static void Line(string s) => Console.WriteLine(s);
}
