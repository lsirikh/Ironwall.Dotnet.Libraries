using Caliburn.Micro;
using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;

/// <summary>
/// 상세 칸의 겉모습(머리 · 배너 · 바닥 막대)을 상태에서 뽑아 준다 — 창은 입력 다섯 개만 넣고,
/// 뷰는 <c>ConsoleDetailHost</c> 의 속성을 여기에 묶는다. 문구가 창마다 달라지지 않게 하는 단일 정본이다.
/// (설계 정본 window-layout-system-storyboard.html L2103-2154)
/// </summary>
/// <remarks>호출 스레드: UI.</remarks>
public sealed class ConsoleDetailPresenter : PropertyChangedBase
{
    public const string ReadOnlyBanner = "편집 권한이 없어 읽기 전용으로 열었습니다.";

    private string _typeName = string.Empty;
    private int _selectedCount;
    private string _singleTitle = string.Empty;
    private string _singleNumber = string.Empty;
    private bool _isCreating;
    private bool _isReadOnly;
    private string? _lastMessage;
    private string _createBanner = "필수 항목을 채우면 등록할 수 있습니다. 나머지는 등록 후 채워도 됩니다.";
    private int _shakeToken;
    private bool _isBlockedNoticeShown;

    public ConsoleDetailPresenter()
    {
        Tracker = new DirtyFieldTracker();
        Tracker.Changed += (_, _) => { _isBlockedNoticeShown = false; RaiseAll(); };
        Guard = new NavigationGuard(() => IsReadOnly ? 0 : Tracker.Count);
        Guard.Blocked += (_, _) =>
        {
            _isBlockedNoticeShown = true;
            ShakeToken++;
            RaiseAll();
        };
    }

    /// <summary>손댄 칸.</summary>
    public DirtyFieldTracker Tracker { get; }

    /// <summary>행 클릭 · 레일 전환 · [추가] · [갱신] 전에 <see cref="NavigationGuard.TryNavigate"/> 로 묻는다.</summary>
    public NavigationGuard Guard { get; }

    #region - Inputs -
    /// <summary>지금 레일의 종류 이름 — "카메라".</summary>
    public string TypeName { get => _typeName; set { _typeName = value ?? string.Empty; RaiseAll(); } }

    public int SelectedCount { get => _selectedCount; set { _selectedCount = Math.Max(0, value); RaiseAll(); } }

    /// <summary>한 개를 골랐을 때의 제목(장비명).</summary>
    public string SingleTitle { get => _singleTitle; set { _singleTitle = value ?? string.Empty; RaiseAll(); } }

    /// <summary>한 개를 골랐을 때의 식별 번호(장비번호).</summary>
    public string SingleNumber { get => _singleNumber; set { _singleNumber = value ?? string.Empty; RaiseAll(); } }

    public bool IsCreating { get => _isCreating; set { _isCreating = value; RaiseAll(); } }

    public bool IsReadOnly { get => _isReadOnly; set { _isReadOnly = value; RaiseAll(); } }

    /// <summary>등록 폼 위 안내 — 창이 필수 항목을 적는다.</summary>
    public string CreateBanner { get => _createBanner; set { _createBanner = value ?? string.Empty; RaiseAll(); } }

    /// <summary>방금 한 일 — "2건 적용했습니다", "되돌렸습니다". 다음 변경 때 지워진다.</summary>
    public string? LastMessage { get => _lastMessage; set { _lastMessage = value; RaiseAll(); } }
    #endregion

    #region - Outputs (ConsoleDetailHost 에 묶는다) -
    public ConsoleDetailState State => ConsoleDetailStateMachine.Resolve(SelectedCount, IsCreating, Tracker.Count, IsReadOnly);

    public string Kind => State switch
    {
        ConsoleDetailState.Create => "새 항목",
        _ when SelectedCount == 1 && SingleNumber.Length > 0 => $"{TypeName} · {SingleNumber}",
        _ => TypeName,
    };

    public string Title => State switch
    {
        ConsoleDetailState.None => "선택한 항목 없음",
        ConsoleDetailState.Create => $"새 {TypeName} 등록",
        _ when SelectedCount == 1 => SingleTitle,
        _ => $"{SelectedCount}개 선택",
    };

    public string Banner => State switch
    {
        ConsoleDetailState.Create => CreateBanner,
        ConsoleDetailState.ReadOnly => ReadOnlyBanner,
        _ when SelectedCount > 1 => $"{SelectedCount}개를 한꺼번에 편집합니다. 값이 다른 칸은 {ConsoleDetailStateMachine.MixedValuesText}으로 보이며, 손댄 칸만 적용됩니다.",
        _ => string.Empty,
    };

    public string FooterText => _isBlockedNoticeShown && Tracker.IsDirty
        ? ConsoleDetailStateMachine.BlockedNotice
        : ConsoleDetailStateMachine.FooterText(State, Tracker.Count, LastMessage);

    public bool IsDirty => State == ConsoleDetailState.Dirty || (State == ConsoleDetailState.Create && Tracker.IsDirty);
    public bool ShowButtons => State is not (ConsoleDetailState.None or ConsoleDetailState.ReadOnly);
    public bool CanApply => ConsoleDetailStateMachine.CanApply(State, Tracker.Count);
    /// <summary>등록 중에는 아무것도 안 채웠어도 [취소] 할 수 있다.</summary>
    public bool CanRevert => State == ConsoleDetailState.Create || CanApply;
    public string ApplyText => State == ConsoleDetailState.Create ? "등록" : "적용";
    public string RevertText => State == ConsoleDetailState.Create ? "취소" : "되돌리기";
    public bool IsFormReadOnly => State is ConsoleDetailState.ReadOnly or ConsoleDetailState.None;
    /// <summary>서랍 모드에서 상세를 밀어낼지 — <c>ConsoleShell.IsDetailRequested</c> 에 묶는다.</summary>
    public bool IsDetailRequested => SelectedCount > 0 || IsCreating;

    /// <summary>값이 오를 때마다 바닥 막대가 한 번 흔들린다.</summary>
    public int ShakeToken { get => _shakeToken; private set => _shakeToken = value; }
    #endregion

    /// <summary>적용 · 되돌리기가 끝났다 — 손댄 칸을 비우고 문구를 남긴다.</summary>
    public void Settle(string message)
    {
        _isBlockedNoticeShown = false;
        _lastMessage = message;
        Tracker.Clear();
        RaiseAll();
    }

    private void RaiseAll() => Refresh();
}
