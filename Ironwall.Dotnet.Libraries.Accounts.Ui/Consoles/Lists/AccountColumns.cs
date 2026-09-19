namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Lists;

/// <summary>사용자 목록의 열 한 줄 — 키 · 머리글 · 기본 열 여부.</summary>
/// <param name="Key">열 키. 화면(XAML)의 <c>ConsoleColumns.Key</c> 와 같아야 한다.</param>
/// <param name="Header">머리글.</param>
/// <param name="IsDefault">기본으로 보이는 열인가. 거짓이면 "열" 메뉴에서만 켠다.</param>
public sealed record AccountColumn(string Key, string Header, bool IsDefault);

/// <summary>
/// 사용자 목록의 열 명세 — <b>13열 → 기본 6열</b>. 나머지는 상세 칸과 "열" 메뉴가 맡는다.
/// (설계 정본 window-layout-system-storyboard.html L1153-L1156 · 결정 L-D2 L1643)
/// </summary>
/// <remarks>
/// 화면은 열을 XAML 로 선언하고 이 표와 <b>키가 같은지</b> 스스로 검사한다(<c>Assert</c>, Debug 전용) —
/// 표와 화면이 갈라지면 "열 n/m" 숫자가 거짓이 되고 "열" 메뉴가 없는 열을 껐다 켠다.
/// 열 정렬 · 열 재정렬은 제공하지 않는다(화면 순서 ≠ 저장 순서 — drag-wireframe L445).
/// </remarks>
public static class AccountColumns
{
    /// <summary>기본 6열 — 목업 L1155 의 순서 그대로.</summary>
    public static readonly IReadOnlyList<AccountColumn> Defaults = new[]
    {
        new AccountColumn("lock", "잠금", true),
        new AccountColumn("identity", "아이디", true),
        new AccountColumn("name", "성명", true),
        new AccountColumn("role", "구분", true),
        new AccountColumn("org", "부서·직급", true),
        new AccountColumn("used", "상태", true),
    };

    /// <summary>
    /// "열" 메뉴에서만 켜는 열 — 목업 L1156 의 넷(사번 · 전화 · 이메일 · 번호) + <b>권한 그룹</b>.
    /// 그룹 열은 목업에 없지만 사용자→그룹 드래그의 결과를 목록에서 바로 보려면 필요하다(기본은 꺼 둔다).
    /// </summary>
    public static readonly IReadOnlyList<AccountColumn> Extras = new[]
    {
        new AccountColumn("group", "권한 그룹", false),
        new AccountColumn("employee", "사번", false),
        new AccountColumn("phone", "전화", false),
        new AccountColumn("email", "이메일", false),
        new AccountColumn("no", "번호", false),
    };

    public static IReadOnlyList<AccountColumn> All { get; } = Defaults.Concat(Extras).ToList();

    /// <summary>화면이 선언한 열 키들이 이 표와 같은가(순서 포함).</summary>
    public static bool Matches(IEnumerable<string> declaredKeys)
        => declaredKeys.SequenceEqual(All.Select(c => c.Key), StringComparer.Ordinal);
}
