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
        set { _groupName = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(ChipText)); }
    }

    public int UserCount
    {
        get => _userCount;
        set { _userCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(ChipText)); }
    }

    /// <summary>
    /// 칩 라벨 "이름 · 인원" — 커널 <c>Console.Chip</c> 은 Content 를 글자(string)로 그려 굵은 폭을 미리 잡는다
    /// (선택해도 칩 폭이 변하지 않는 U-12 관용구). 그래서 Run 조합 대신 한 문자열로 준다.
    /// </summary>
    public string ChipText => $"{GroupName} · {UserCount}";

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
