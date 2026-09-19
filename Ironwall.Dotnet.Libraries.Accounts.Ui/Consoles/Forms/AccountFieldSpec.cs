using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Forms;

/// <summary>상세 칸의 절 — 순서가 곧 화면 순서다(설계 정본 L1163-L1167).</summary>
public enum AccountFieldSection
{
    /// <summary>신원 — 사진 · 아이디 · 성명.</summary>
    Identity,
    /// <summary>인적사항 — 사번 · 전화 · 이메일 · 직급 · 부서.</summary>
    Personal,
    /// <summary>소속 · 역할 — 구분 · 권한 그룹 · 상태 · 부여 이력.</summary>
    Role,
    /// <summary>세션 요약 — 활성 N · 마지막 로그인.</summary>
    Session,
    /// <summary>잠금 — 상태 · 사유 · 잠긴 시각.</summary>
    Lock,
}

/// <summary>칸을 무엇으로 그릴 것인가.</summary>
public enum AccountFieldEditor
{
    Text,
    Choice,
    ReadOnly,
}

/// <summary>
/// 상세 칸의 한 줄 명세 — 속성 하나 = 한 줄. 폼은 이 표에서 <b>만들어진다</b>(사람마다 폼을 손으로 짜지 않는다).
/// </summary>
/// <param name="Key">칸 키. 자동화 식별자 <c>Accounts.Detail.Field.{Key}</c> 가 된다.</param>
/// <param name="Label">라벨.</param>
/// <param name="Section">어느 절에 놓을지.</param>
/// <param name="Editor">편집기 종류.</param>
/// <param name="ApiPath">서버 필드 이름 — 라벨 아랫줄에 고정폭으로 보인다.</param>
/// <param name="Read">행에서 글을 읽는다.</param>
/// <param name="Write">행에 글을 쓴다. <c>null</c> 이면 읽기 전용 칸이다.</param>
/// <param name="AllowMultiEdit">여러 명을 골랐을 때 한꺼번에 바꿀 수 있는가. 거짓이면 식별 칸으로 잠근다.</param>
/// <param name="Required">빈 값이면 적용을 막는가.</param>
/// <param name="LockReason">늘 잠긴 칸의 까닭(아이디 등). 잠긴 칸은 까닭 없이 보이지 않는다.</param>
/// <param name="Options">선택 칸의 항목.</param>
public sealed record AccountFieldSpec(
    string Key,
    string Label,
    AccountFieldSection Section,
    AccountFieldEditor Editor,
    string ApiPath,
    Func<AccountViewModel, string> Read,
    Action<AccountViewModel, string>? Write = null,
    bool AllowMultiEdit = false,
    bool Required = false,
    string? LockReason = null,
    IReadOnlyList<string>? Options = null)
{
    public bool IsWritable => Write is not null && LockReason is null;
}

/// <summary>
/// 사용자 상세 칸의 명세 표 — 기존 편집 다이얼로그(<c>EditorDialogView</c> 400×550)의 항목을 그대로 옮겼다.
/// <b>아이디 · 비밀번호는 읽기 전용 규칙을 유지</b>한다(설계 정본 L1242).
/// </summary>
public static class AccountFieldCatalog
{
    public const string IdentityLockReason = "아이디는 만든 뒤 바꿀 수 없습니다.";
    public const string GroupNote = "상태 띠의 권한 그룹 칩에 끌어 놓거나 칩을 눌러 바꿉니다.";

    public static IReadOnlyList<AccountFieldSection> SectionOrder { get; } = new[]
    {
        AccountFieldSection.Identity,
        AccountFieldSection.Personal,
        AccountFieldSection.Role,
        AccountFieldSection.Session,
        AccountFieldSection.Lock,
    };

    public static string TitleOf(AccountFieldSection section) => section switch
    {
        AccountFieldSection.Identity => "신원",
        AccountFieldSection.Personal => "인적사항",
        AccountFieldSection.Role => "소속 · 역할",
        AccountFieldSection.Session => "세션 요약",
        AccountFieldSection.Lock => "잠금",
        _ => section.ToString(),
    };

    /// <summary>절의 API 축 이름 — 제목 옆 고정폭 글자.</summary>
    public static string? AxisOf(AccountFieldSection section) => section switch
    {
        AccountFieldSection.Identity => "users",
        AccountFieldSection.Personal => "users",
        AccountFieldSection.Role => "users.role · users.group_id",
        AccountFieldSection.Session => "user-sessions",
        AccountFieldSection.Lock => "users.is_locked",
        _ => null,
    };

    /// <summary>고를 수 있는 역할 — 서버 v5.4 가 발행하는 두 가지만(레거시 5등급은 생성 시 422).</summary>
    public static IReadOnlyList<string> RoleOptions { get; } = new[]
    {
        nameof(EnumUserRole.ADMIN),
        nameof(EnumUserRole.USER),
    };

    public static IReadOnlyList<string> UsedOptions { get; } = new[] { "사용", "미사용" };

    public static IReadOnlyList<AccountFieldSpec> All { get; } = Build();

    private static IReadOnlyList<AccountFieldSpec> Build() => new[]
    {
        // ── 신원 ──────────────────────────────────────────────────────────
        new AccountFieldSpec("username", "아이디", AccountFieldSection.Identity, AccountFieldEditor.ReadOnly,
            "username", r => r.Username ?? string.Empty, null, LockReason: IdentityLockReason),
        new AccountFieldSpec("name", "성명", AccountFieldSection.Identity, AccountFieldEditor.Text,
            "name", r => r.Name ?? string.Empty, (r, v) => r.Name = v, Required: true),

        // ── 인적사항 ──────────────────────────────────────────────────────
        new AccountFieldSpec("employee_number", "사번", AccountFieldSection.Personal, AccountFieldEditor.Text,
            "employee_number", r => r.EmployeeNumber ?? string.Empty, (r, v) => r.EmployeeNumber = Blank(v)),
        new AccountFieldSpec("phone", "전화", AccountFieldSection.Personal, AccountFieldEditor.Text,
            "phone", r => r.Phone ?? string.Empty, (r, v) => r.Phone = Blank(v)),
        new AccountFieldSpec("email", "이메일", AccountFieldSection.Personal, AccountFieldEditor.Text,
            "email", r => r.EMail ?? string.Empty, (r, v) => r.EMail = Blank(v)),
        new AccountFieldSpec("position", "직급", AccountFieldSection.Personal, AccountFieldEditor.Text,
            "position", r => r.Position ?? string.Empty, (r, v) => r.Position = Blank(v), AllowMultiEdit: true),
        new AccountFieldSpec("department", "부서", AccountFieldSection.Personal, AccountFieldEditor.Text,
            "department", r => r.Department ?? string.Empty, (r, v) => r.Department = Blank(v), AllowMultiEdit: true),

        // ── 소속 · 역할 ───────────────────────────────────────────────────
        new AccountFieldSpec("role", "구분", AccountFieldSection.Role, AccountFieldEditor.Choice,
            "role", r => r.Role.ToString(), WriteRole, AllowMultiEdit: true, Required: true, Options: RoleOptions),
        new AccountFieldSpec("group", "권한 그룹", AccountFieldSection.Role, AccountFieldEditor.ReadOnly,
            "group_id", r => string.IsNullOrEmpty(r.GroupText) ? "(없음)" : r.GroupText, null, LockReason: GroupNote),
        new AccountFieldSpec("used", "상태", AccountFieldSection.Role, AccountFieldEditor.Choice,
            "is_active", r => r.UsedText, WriteUsed, AllowMultiEdit: true, Options: UsedOptions),
    };

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static void WriteRole(AccountViewModel row, string text)
    {
        if (Enum.TryParse<EnumUserRole>(text, ignoreCase: false, out var role)) row.Role = role;
    }

    private static void WriteUsed(AccountViewModel row, string text)
        => row.Used = text == "사용" ? EnumUsedType.USED : EnumUsedType.NOT_USED;
}
