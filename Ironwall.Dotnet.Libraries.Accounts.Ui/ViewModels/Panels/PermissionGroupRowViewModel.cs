using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;

/// <summary>
/// 권한 그룹 목록의 한 행(요약).
/// </summary>
/// <remarks>
/// 값이 바뀌어도 <b>행 인스턴스를 갈아 끼우지 않는다</b> — 재조회가 목록을 통째로 새로 만들면
/// 고른 그룹이 풀리고(선택 = null) 편집 중이던 매트릭스가 비어 버린다. 그래서 알림을 갖는다.
/// </remarks>
public class PermissionGroupRowViewModel : PropertyChangedBase
{
    private string _groupName = string.Empty;
    private int _userCount;
    private string _active = string.Empty;
    private int _viewCount, _editCount, _deleteCount, _controlCount;

    public int GroupId { get; init; }

    public string GroupName
    {
        get => _groupName;
        set { _groupName = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public int UserCount
    {
        get => _userCount;
        set { _userCount = value; NotifyOfPropertyChange(); }
    }

    /// <summary>"사용" / "미사용".</summary>
    public string Active
    {
        get => _active;
        set { _active = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public int ViewCount
    {
        get => _viewCount;
        set { _viewCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Summary)); }
    }

    public int EditCount
    {
        get => _editCount;
        set { _editCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Summary)); }
    }

    public int DeleteCount
    {
        get => _deleteCount;
        set { _deleteCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Summary)); }
    }

    public int ControlCount
    {
        get => _controlCount;
        set { _controlCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Summary)); }
    }

    public string Summary => $"조회 {ViewCount} · 편집 {EditCount} · 삭제 {DeleteCount} · 제어 {ControlCount}";

    public override string ToString() => GroupName;
}
