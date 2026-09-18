using System.Text;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Helpers;

/// <summary>
/// 로그인 요청의 <c>client_id</c>(=커넥션 식별자) 단일 생성기 — 명세 §9.2.2 / §9.8.
/// <para><b>왜 필요한가</b>: 서버 기본 정책이 <c>session_concurrency_policy=allow</c>(다중 세션 공존)로 바뀌었다.
/// 우리가 <c>client_id</c> 를 보내지 않으면 세션 행의 값이 <c>null</c> 이 되어 ① 세션 관리 화면에서 어느 커넥션인지
/// 구분할 수 없고 ② 관리자가 <c>session_self_replace_enabled</c> 를 켜도 우리 세션은 self-replace 축에서 빠진다.
/// self-replace 는 <b>동일 client_id</b> 재로그인만 자기 옛 세션을 교체하므로 값이 <b>커넥션별 고유</b>해야 한다
/// (두 커넥션이 공유하면 서로 축출된다 — §9.2.2 주석).</para>
/// <para><b>형식</b>: <c>gis-monitoring:{머신명}</c>. 규약 접두는 §9.2.2 의 GIS 관제 값 <c>gis-monitoring</c> 이고,
/// 뒤에 머신명을 붙여 배포별 고유성을 만든다. 앱은 같은 PC 에서 중복 실행되지 않으므로(Redundant Execution 가드)
/// 머신 단위가 곧 커넥션 단위이며, <b>재시작해도 값이 같아</b> 비정상 종료로 남은 자기 세션을 self-replace 로 회수할 수 있다
/// (PID 를 섞으면 매 실행마다 달라져 그 회수가 불가능하다).</para>
/// <para><b>패턴</b>: 서버 검증식은 <c>^[A-Za-z0-9._:-]{1,64}$</c> 이고 위반값은 422 가 아니라 <b>무시</b>된다(로그인 가용성 우선).
/// 한글 머신명 등 허용 외 문자는 <c>-</c> 로 치환하고 64자로 잘라 항상 유효값을 만든다.</para>
/// </summary>
public static class ClientIdentity
{
    /// <summary>규약 접두 — GIS 관제 클라이언트(§9.2.2 "규약값(예): gis-monitoring").</summary>
    public const string Prefix = "gis-monitoring";

    /// <summary>서버 패턴 상한(<c>^[A-Za-z0-9._:-]{1,64}$</c>).</summary>
    private const int MaxLength = 64;

    private static string? _cached;

    /// <summary>이 커넥션의 <c>client_id</c>. 프로세스 수명 동안 불변.</summary>
    public static string Current => _cached ??= Build(SafeMachineName());

    /// <summary>테스트/진단용 — 임의 머신명으로 조립한다(패턴 정규화 포함).</summary>
    internal static string Build(string? machine)
    {
        var token = Sanitize(machine);
        var id = string.IsNullOrEmpty(token) ? Prefix : $"{Prefix}:{token}";
        return id.Length <= MaxLength ? id : id.Substring(0, MaxLength);
    }

    /// <summary>허용 문자(<c>A-Za-z0-9._:-</c>) 외는 <c>-</c> 로 치환하고 양끝 <c>-</c> 를 정리한다.</summary>
    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                     || c == '.' || c == '_' || c == ':' || c == '-';
            sb.Append(ok ? c : '-');
        }
        return sb.ToString().Trim('-');
    }

    private static string SafeMachineName()
    {
        try { return Environment.MachineName; }
        catch { return string.Empty; }   // 머신명 조회 실패 시에도 접두만으로 유효값 유지
    }
}
