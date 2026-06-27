namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

/// <summary>
/// 권한 매트릭스 평탄화 행 — (그룹, 모듈) 단위. 읽기전용 표시용 POCO.
/// <c>UserGroupDto.Permissions.Modules</c>(Dictionary)를 그룹×모듈로 펼친 1행.
/// 4-bool은 <c>ModulePermissionDto</c>(view/edit/delete/control)와 1:1.
/// </summary>
public class PermissionRowViewModel
{
    public string GroupName { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public bool View { get; init; }
    public bool Edit { get; init; }
    public bool Delete { get; init; }
    public bool Control { get; init; }
}
