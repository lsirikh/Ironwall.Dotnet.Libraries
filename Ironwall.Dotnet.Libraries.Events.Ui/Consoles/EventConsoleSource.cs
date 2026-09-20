using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles;

/// <summary>
/// 이벤트 콘솔이 목록 하나를 다루는 데 필요한 것 — 패널 뷰모델의 제네릭 타입(<c>BaseDataGridMultiPanelViewModel&lt;T&gt;</c>)을 가린다.
/// </summary>
/// <remarks>
/// 조회 · 저장 · 삭제 · 재조회는 <b>패널의 기존 경로를 그대로</b> 부른다(events-console PRD NFR-01).
/// 콘솔은 새 전송 경로를 만들지 않는다 — 장비 콘솔의 <c>IDeviceConsoleSource</c> 와 같은 계약이다.
/// </remarks>
public interface IEventConsoleSource
{
    /// <summary>패널 뷰모델(활성화 대상).</summary>
    BasePanelViewModel Panel { get; }

    /// <summary>목록의 행 뷰모델들(관찰 가능).</summary>
    IEnumerable Rows { get; }
    INotifyCollectionChanged RowsChanged { get; }
    int RowCount { get; }

    /// <summary>지금 저장 · 삭제 · 재조회 중인가.</summary>
    bool IsBusy { get; }

    /// <summary>바쁨이 끝났다(저장 · 삭제 · 재조회 · 첫 조회 한 번이 끝남).</summary>
    event EventHandler? BusyEnded;

    /// <summary>그리드의 선택을 패널에 알린다 — 삭제는 패널의 선택을 본다.</summary>
    void Select(IReadOnlyList<object> rows);

    /// <summary>조회 기간을 바꾼다(전송 없음).</summary>
    void SetDate(DateTime start, DateTime end);

    /// <summary>캐시를 버린다(기간이 바뀌었을 때).</summary>
    void InvalidateCache();

    /// <summary>패널의 조회를 부른다 — 기간 칩 · [갱신] 이 쓴다.</summary>
    void Search();

    /// <summary>
    /// 패널의 저장을 부른다. <b>시작했으면 true</b> — 권한이 없거나 다른 일을 하는 중이면 패널은 아무 말 없이 돌아온다.
    /// false 면 <see cref="BusyEnded"/> 는 오지 않는다(기다리면 안 된다).
    /// </summary>
    bool Save();

    /// <summary>패널의 [추가]를 부른다 — 번호 자동 배정 · 권한 검사가 그 안에 있다.</summary>
    void Insert();

    /// <summary>패널의 삭제를 부른다 — 확인 팝업이 뜨고, 취소하면 아무 일도 없다.</summary>
    void Delete();

    /// <summary>패널의 재조회를 부른다.</summary>
    bool Reload();

    /// <summary>진행 중인 조회를 중단한다 — 옆 창의 [취소] 가 하던 일을 되살린다(R14).</summary>
    void CancelQuery();

    /// <summary>"불러온 12 / 340건" — 패널이 만든 글자 그대로.</summary>
    string LoadedCountText { get; }

    /// <summary>서버에 더 불러올 쪽이 남았는가 — 거르기가 "불러온 범위 안" 임을 알리는 데 쓴다.</summary>
    bool HasMorePages { get; }
}

/// <summary>
/// <see cref="BaseDataGridMultiPanelViewModel{T}"/> 를 <see cref="IEventConsoleSource"/> 로 감싼다. 패널 코드는 고치지 않는다.
/// </summary>
/// <remarks>호출 스레드: UI. 끝남 통지는 작업 스레드에서 올 수 있어 한 번 UI 스레드로 옮긴다(PRD NFR-03).</remarks>
public sealed class EventConsoleSource<T> : IEventConsoleSource where T : class, ISelectableBaseViewModel
{
    private readonly BaseDataGridMultiPanelViewModel<T> _panel;
    private readonly System.Action<DateTime, DateTime>? _setDate;
    private readonly System.Action? _search;
    private readonly System.Action? _invalidate;
    private readonly Func<string>? _loadedText;
    private readonly Func<bool>? _hasMore;
    private readonly System.Action? _cancelQuery;
    private bool _wasBusy;

    /// <param name="panel">감쌀 패널.</param>
    /// <param name="hookUpdated">
    /// 패널의 <c>UpdateAction</c>(조회 · 저장 · 삭제가 끝나면 울린다)에 구독을 거는 방법. 그 이벤트는 기반 클래스가 아니라
    /// 패널마다 따로 선언돼 있어 여기서 이름으로 닿을 수 없다 — 삭제는 바쁨 표지를 안 바꾸므로 이 신호가 있어야 끝남을 안다.
    /// </param>
    public EventConsoleSource(BaseDataGridMultiPanelViewModel<T> panel,
                              System.Action<System.Action>? hookUpdated = null,
                              System.Action<DateTime, DateTime>? setDate = null,
                              System.Action? search = null,
                              System.Action? invalidateCache = null,
                              Func<string>? loadedCountText = null,
                              Func<bool>? hasMorePages = null,
                              System.Action? cancelQuery = null)
    {
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        _setDate = setDate;
        _search = search;
        _invalidate = invalidateCache;
        _loadedText = loadedCountText;
        _hasMore = hasMorePages;
        _cancelQuery = cancelQuery;
        _panel.PropertyChanged += OnPanelPropertyChanged;
        // 패널은 활성화돼 목록을 다 읽기 전까지 [갱신] 이 꺼져 있다(= 바쁨). 그 첫 끝남도 알려야 콘솔이 툴바를 다시 켠다.
        _wasBusy = IsBusy;
        hookUpdated?.Invoke(RaiseBusyEnded);
    }

    public BasePanelViewModel Panel => _panel;
    public IEnumerable Rows => _panel.ViewModelProvider;
    public INotifyCollectionChanged RowsChanged => _panel.ViewModelProvider;
    public int RowCount => _panel.ViewModelProvider.Count;

    // 패널의 저장은 async void 다 — 끝남을 기다릴 수 없어 IsSaving(저장) · ReloadButtonEnable(재조회)의 변화로 잡는다.
    public bool IsBusy => _panel.IsSaving || !_panel.ReloadButtonEnable;

    public string LoadedCountText => _loadedText?.Invoke() ?? $"{RowCount}건";

    public bool HasMorePages => _hasMore?.Invoke() ?? false;

    public event EventHandler? BusyEnded;

    public void Select(IReadOnlyList<object> rows)
    {
        var selected = rows.OfType<T>().ToList();

        // 패널은 고른 행에 IsSelected 를 켜기만 한다 — 고르지 않게 된 행을 끄는 것은 그리드의 몫이었다.
        foreach (var row in _panel.ViewModelProvider)
            if (row.IsSelected && !selected.Contains(row)) row.IsSelected = false;

        _panel.OnSelectionChanged(selected);
    }

    public void SetDate(DateTime start, DateTime end) => _setDate?.Invoke(start, end);
    public void InvalidateCache() => _invalidate?.Invoke();
    public void Search() => _search?.Invoke();
    public void CancelQuery() => _cancelQuery?.Invoke();

    public void Insert() => _panel.OnClickInsertButton(this, new System.Windows.RoutedEventArgs());
    public bool Save() => Started(() => _panel.OnClickSaveButton(this, new System.Windows.RoutedEventArgs()));
    public void Delete() => _panel.OnClickDeleteButton(this, new System.Windows.RoutedEventArgs());
    public bool Reload() => Started(() => _panel.OnClickReloadButton(this, new System.Windows.RoutedEventArgs()));

    /// <summary>
    /// 패널의 버튼 경로는 async void 다 — 받아들였는지 돌려주지 않는다. 받아들였다면 첫 await 전에(= 이 호출 안에서)
    /// 바쁨 표지를 켠다. 그 순간을 본다(끝까지 동기로 끝나 표지가 도로 꺼져도 "켜졌던 적"은 남는다).
    /// </summary>
    private bool Started(System.Action call)
    {
        var started = false;
        void Watch(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(_panel.IsSaving) or nameof(_panel.ReloadButtonEnable)) started |= IsBusy;
        }

        _panel.PropertyChanged += Watch;
        try { call(); }
        finally { _panel.PropertyChanged -= Watch; }
        return started;
    }

    /// <summary>
    /// 패널은 끝남을 <b>작업 스레드에서</b> 알릴 수 있다(재조회를 ConfigureAwait(false) 로 기다린 뒤 UpdateAction 을 울린다).
    /// 콘솔은 이 신호로 그리드 선택과 상세를 만지므로 여기서 한 번 UI 스레드로 옮긴다.
    /// </summary>
    private void RaiseBusyEnded() => Execute.BeginOnUIThread(() => BusyEnded?.Invoke(this, EventArgs.Empty));

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(_panel.IsSaving) or nameof(_panel.ReloadButtonEnable))) return;

        var busy = IsBusy;
        var ended = _wasBusy && !busy;
        _wasBusy = busy;
        if (ended) RaiseBusyEnded();
    }
}
