namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;

/// <summary>
/// 계정 콘솔의 키 — 자동화 식별자 · 레일 전환 · 드롭존 판정이 전부 이 한 곳을 본다.
/// (설계 정본 window-layout-system-storyboard.html L1149 · L1196-L1204)
/// </summary>
public static class AccountConsoleKeys
{
    /// <summary>커널이 <c>Console.{키}.*</c> 로 자동화 식별자를 만든다.</summary>
    public const string ConsoleKey = "Accounts";

    public const string Users = "users";
    public const string Permissions = "permissions";
    public const string Sessions = "sessions";
    public const string Grants = "grants";
    public const string Audit = "audit";
    public const string SessionSetup = "session-setup";

    /// <summary>사용자 → 권한 그룹 드롭존.</summary>
    public const string GroupZone = "account-group";

    /// <summary>레일 순서(정본) — 세션 설정만 구분선 뒤에 온다.</summary>
    public static readonly IReadOnlyList<string> RailOrder = new[]
    {
        Users, Permissions, Sessions, Grants, Audit, SessionSetup,
    };

    public static string LabelOf(string railKey) => railKey switch
    {
        Users => "사용자",
        Permissions => "권한 설정",
        Sessions => "세션 관리",
        Grants => "권한 부여",
        Audit => "감사 로그",
        SessionSetup => "세션 설정",
        _ => railKey,
    };

    /// <summary>MaterialDesign <c>PackIconKind</c> 이름. 없는 이름이면 뷰가 작은 점으로 그린다.</summary>
    public static string IconOf(string railKey) => railKey switch
    {
        Users => "AccountMultipleOutline",
        Permissions => "ShieldKeyOutline",
        Sessions => "MonitorCellphone",
        Grants => "ClockOutline",
        Audit => "ClipboardTextClockOutline",
        SessionSetup => "CogOutline",
        _ => "CircleSmall",
    };

    /// <summary>배지에 건수를 보일 것인가 — 감사 로그 · 세션 설정은 보이지 않는다(목업 L1201 · L1203).</summary>
    public static bool ShowsCount(string railKey) => railKey is Users or Permissions or Sessions or Grants;
}

/// <summary>
/// 레일 항목의 아이콘 — <b>이름만</b> 쥔다. 싱글턴 뷰모델이 시각 요소(<c>PackIcon</c>)를 쥐면 뷰가 새로 만들어질 때
/// 옛 트리에 묶인 채 남는다. 맨 문자열로 두지 않는 까닭: 접힌 레일의 말풍선(라벨 문자열)까지 아이콘으로 그려진다.
/// </summary>
/// <param name="Kind">MaterialDesign <c>PackIconKind</c> 이름.</param>
public sealed record ConsoleIconToken(string Kind)
{
    public override string ToString() => Kind;
}
