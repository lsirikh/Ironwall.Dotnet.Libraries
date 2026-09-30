using System.Security.Cryptography;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// 파이프 이름 · 토큰(순수 함수). 이름 = <c>ironwall-camhost-{GIS pid}-{토큰}</c>.
/// 토큰은 실행 인자로 호스트에 전달되고 Hello 에서 다시 대조된다 — 이름을 추측한 다른 프로세스는 붙지 못한다
/// (파이프는 같은 사용자 전용 · 인스턴스 1개 · 클라이언트 pid 대조까지 3중).
/// </summary>
public static class PipeNaming
{
    public const string Prefix = "ironwall-camhost-";
    public const int TokenHexLength = 32;

    /// <summary>128비트 난수 토큰(소문자 hex 32자).</summary>
    public static string CreateToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenHexLength / 2)).ToLowerInvariant();

    public static bool IsValidToken(string? token)
    {
        if (token is null || token.Length != TokenHexLength) return false;
        foreach (var c in token)
        {
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!hex) return false;
        }
        return true;
    }

    public static string Build(int gisProcessId, string token)
    {
        if (gisProcessId <= 0) throw new ArgumentOutOfRangeException(nameof(gisProcessId));
        if (!IsValidToken(token)) throw new ArgumentException("토큰 형식 오류", nameof(token));
        return $"{Prefix}{gisProcessId}-{token}";
    }

    /// <summary>이름이 이 pid · 토큰 규칙에 맞는지(호스트가 인자 검증에 쓴다).</summary>
    public static bool Matches(string? pipeName, int gisProcessId, string? token)
        => pipeName is not null && IsValidToken(token) && gisProcessId > 0
           && string.Equals(pipeName, $"{Prefix}{gisProcessId}-{token}", StringComparison.Ordinal);

    /// <summary>시간 일정 비교(토큰 대조).</summary>
    public static bool TokensEqual(string? a, string? b)
    {
        if (a is null || b is null) return false;
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(a), System.Text.Encoding.ASCII.GetBytes(b));
    }
}
