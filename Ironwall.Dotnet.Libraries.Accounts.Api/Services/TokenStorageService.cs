using System.Text;
using Newtonsoft.Json.Linq;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// <see cref="ITokenStorageService"/> 인메모리 구현. 멀티스레드(UI/HTTP 핸들러) 공유라 <c>_gate</c> lock 으로 직렬화.
/// JWT 클레임은 서명검증 없이 payload 만 read 한다(§2.3.1). DPAPI 영속은 후속(현재 메모리 전용).
/// </summary>
public class TokenStorageService : ITokenStorageService
{
    private readonly object _gate = new();
    private string? _access;
    private string? _refresh;
    private DateTime? _accessExpUtc;
    private DateTime? _refreshExpUtc;
    private string? _jti;
    private string? _userId;
    private string? _sessionId;
    private int _generation;

    public string? AccessToken          { get { lock (_gate) return _access; } }
    public string? RefreshToken         { get { lock (_gate) return _refresh; } }
    public DateTime? AccessExpiresAtUtc  { get { lock (_gate) return _accessExpUtc; } }
    public DateTime? RefreshExpiresAtUtc { get { lock (_gate) return _refreshExpUtc; } }
    public string? Jti                   { get { lock (_gate) return _jti; } }
    public string? UserId                { get { lock (_gate) return _userId; } }
    public string? SessionId             { get { lock (_gate) return _sessionId; } }
    public int Generation                { get { lock (_gate) return _generation; } }
    public bool IsAuthenticated          { get { lock (_gate) return !string.IsNullOrEmpty(_access); } }

    public void SetTokens(string accessToken, string? refreshToken = null, string? sessionId = null)
    {
        lock (_gate) { ApplyTokens(accessToken, refreshToken, sessionId); }
    }

    public bool SetTokensIfGeneration(int expectedGeneration, string accessToken, string? refreshToken = null, string? sessionId = null)
    {
        lock (_gate)
        {
            if (_generation != expectedGeneration) return false;   // Clear()가 끼어듦(강제 로그아웃) → refresh 부활 차단(FR-FL-05)
            ApplyTokens(accessToken, refreshToken, sessionId);
            return true;
        }
    }

    /// <summary>lock 보유 상태에서 호출. access 의 exp/jti/user_id/sid 디코드 + sessionId 보관.</summary>
    private void ApplyTokens(string accessToken, string? refreshToken, string? sessionId)
    {
        _access = accessToken;
        _accessExpUtc = TryGetJwtExpiryUtc(accessToken);
        _jti = TryGetJwtClaim(accessToken, "jti");
        _userId = TryGetJwtClaim(accessToken, "sub") ?? TryGetJwtClaim(accessToken, "user_id");
        if (sessionId != null) _sessionId = sessionId;
        else _sessionId ??= TryGetJwtClaim(accessToken, "sid");   // 서버가 sid 클레임 제공 시 자동 포착(seam, FR-FL-01)
        if (refreshToken != null)
        {
            _refresh = refreshToken;
            _refreshExpUtc = TryGetJwtExpiryUtc(refreshToken);
        }
    }

    public bool IsAccessTokenExpiring(TimeSpan threshold)
    {
        lock (_gate)
        {
            if (_accessExpUtc is null) return false;
            return DateTime.UtcNow + threshold >= _accessExpUtc.Value;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _access = null;
            _refresh = null;
            _accessExpUtc = null;
            _refreshExpUtc = null;
            _jti = null;
            _userId = null;
            _sessionId = null;
            _generation++;   // 세대 증가 → 진행 중 refresh 의 SetTokensIfGeneration 무효화(FR-FL-05)
        }
    }

    /// <summary>JWT payload(2번째 segment)의 exp(Unix seconds) → UTC. 파싱 실패/미보유 시 null.</summary>
    internal static DateTime? TryGetJwtExpiryUtc(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            var exp = JObject.Parse(payloadJson)["exp"];
            if (exp is null) return null;
            return DateTimeOffset.FromUnixTimeSeconds((long)exp).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>JWT payload 의 임의 문자열 클레임 read. 미보유/null/파싱실패 시 null. (jti/sub/sid 추출용 — FR-FL-01)</summary>
    internal static string? TryGetJwtClaim(string? jwt, string claim)
    {
        if (string.IsNullOrWhiteSpace(jwt)) return null;
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;
        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            var val = JObject.Parse(payloadJson)[claim];
            if (val is null || val.Type == JTokenType.Null) return null;
            var s = val.ToString();
            return string.IsNullOrEmpty(s) ? null : s;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
