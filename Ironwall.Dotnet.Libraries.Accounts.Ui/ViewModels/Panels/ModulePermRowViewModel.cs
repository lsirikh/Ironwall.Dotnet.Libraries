namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

/// <summary>
/// 권한 상세(한 그룹) 행 — 모듈 1개 × 4동작(조회/편집/삭제/제어). 더블클릭한 그룹의 전체 권한 페이지.
/// XxxEnabled=해당 모듈에 그 동작이 적용 가능한지(PermissionCatalog) — false면 비활성(▦).
/// 현재 조회 전용(체크박스 IsHitTestVisible=False) — 편집저장은 서버 v5.0 후속.
/// </summary>
public class ModulePermRowViewModel
{
    public string ModuleKey { get; init; } = string.Empty;
    public string ModuleDisplay { get; init; } = string.Empty;

    public bool View { get; init; }
    public bool Edit { get; init; }
    public bool Delete { get; init; }
    public bool Control { get; init; }

    public bool ViewEnabled { get; init; } = true;
    public bool EditEnabled { get; init; } = true;
    public bool DeleteEnabled { get; init; } = true;
    public bool ControlEnabled { get; init; }
}
