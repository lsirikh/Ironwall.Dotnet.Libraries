namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

/// <summary>
/// 권한 그룹 목록 행 — 그룹 단위 요약(verb별 활성 모듈 수). 더블클릭 시 상세 매트릭스로 진입.
/// 플랫 (그룹×모듈) 리스트 대신 그룹당 1행 + 한눈 요약(2-3).
/// </summary>
public class PermissionGroupRowViewModel
{
    public int GroupId { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public int UserCount { get; init; }
    public string Active { get; init; } = string.Empty;   // "사용"/"미사용"

    /// <summary>예약 5등급(ADMIN/MAINTAINER/OPERATOR/VIEWER/GUEST) 여부 — true면 삭제·개명 금지(매트릭스 편집만 허용).</summary>
    public bool IsReserved { get; init; }

    public int ViewCount { get; init; }
    public int EditCount { get; init; }
    public int DeleteCount { get; init; }
    public int ControlCount { get; init; }

    /// <summary>한눈 요약 — "조회 8 · 편집 3 · 삭제 0 · 제어 1".</summary>
    public string Summary => $"조회 {ViewCount} · 편집 {EditCount} · 삭제 {DeleteCount} · 제어 {ControlCount}";
}
