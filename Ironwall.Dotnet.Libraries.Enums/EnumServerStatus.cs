namespace Ironwall.Dotnet.Libraries.Enums;

/// <summary>
/// 서버 상태 (§8.3)
/// <para>v7.0 에서 <c>UNKNOWN</c>(보고받은 적 없음 — 등록 기본값)이 더해져 4종이다. 기존 서수를 바꾸지 않도록 <b>끝에</b> 붙였다.</para>
/// </summary>
public enum EnumServerStatus
{
    NORMAL,
    WARNING,
    ERROR,
    UNKNOWN
}
