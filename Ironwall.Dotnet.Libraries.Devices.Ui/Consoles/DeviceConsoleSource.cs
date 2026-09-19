using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;

/// <summary>
/// 콘솔이 목록 하나를 다루는 데 필요한 것 — 패널 뷰모델의 제네릭 타입(<c>BaseDataGridMultiPanelViewModel&lt;T&gt;</c>)을 가린다.
/// </summary>
/// <remarks>
/// 저장 · 삭제 · 재조회는 <b>패널의 기존 경로를 그대로</b> 부른다(증명된 CRUD 봉투 — 완전성 확인 · 종류축 차단 ·
/// 부대 찍기 · 재조회). 콘솔은 새 전송 경로를 만들지 않는다(device-console-redesign NFR-02 · CD-1).
/// </remarks>
public interface IDeviceConsoleSource
{
    /// <summary>패널 뷰모델(활성화 대상).</summary>
    BasePanelViewModel Panel { get; }

    /// <summary>목록의 행 뷰모델들(관찰 가능).</summary>
    IEnumerable Rows { get; }
    INotifyCollectionChanged RowsChanged { get; }
    int RowCount { get; }

    /// <summary>지금 저장 · 삭제 · 재조회 중인가.</summary>
    bool IsBusy { get; }

    /// <summary>바쁨이 끝났다(저장 · 삭제 · 재조회 한 번이 끝남). 콘솔은 이때 선택과 폼을 다시 맞춘다.</summary>
    event EventHandler? BusyEnded;

    /// <summary>그리드의 선택을 패널에 알린다 — 삭제는 패널의 선택을 본다.</summary>
    void Select(IReadOnlyList<object> rows);

    /// <summary>
    /// Draft 행을 하나 만들어 <b>목록에서 떼어</b> 돌려준다(패널의 [추가]와 같은 경로 — 번호 자동 배정 · 권한 검사 포함).
    /// 못 만들었으면 null. [등록] 을 누르기 전에는 목록에 아무것도 남지 않는다(FR-12).
    /// </summary>
    object? CreateDraft();

    /// <summary>떼어 둔 Draft 를 목록에 넣는다([등록] 직전). 이후 <see cref="Save"/> 가 그것을 서버에 만든다.</summary>
    void AdoptDraft(object draft);

    void Save();
    void Delete();
    void Reload();
}

/// <summary>
/// <see cref="BaseDataGridMultiPanelViewModel{T}"/> 를 <see cref="IDeviceConsoleSource"/> 로 감싼다. 패널 코드는 고치지 않는다.
/// </summary>
public sealed class DeviceConsoleSource<T> : IDeviceConsoleSource where T : class, ISelectableBaseViewModel
{
    private readonly BaseDataGridMultiPanelViewModel<T> _panel;
    private bool _wasBusy;

    /// <param name="panel">감쌀 패널.</param>
    /// <param name="hookUpdated">
    /// 패널의 <c>UpdateAction</c>(저장 · 삭제 · 재조회가 끝나면 울린다)에 구독을 거는 방법. 그 이벤트는 기반 클래스가 아니라
    /// 패널마다 따로 선언돼 있어 여기서 이름으로 닿을 수 없다 — 삭제는 바쁨 표지를 안 바꾸므로 이 신호가 있어야 끝남을 안다.
    /// </param>
    public DeviceConsoleSource(BaseDataGridMultiPanelViewModel<T> panel, Action<System.Action>? hookUpdated = null)
    {
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        _panel.PropertyChanged += OnPanelPropertyChanged;
        // 패널은 활성화돼 목록을 다 읽기 전까지 [갱신] 이 꺼져 있다(= 바쁨). 그 첫 끝남도 알려야 콘솔이 툴바를 다시 켠다.
        _wasBusy = IsBusy;
        hookUpdated?.Invoke(() => BusyEnded?.Invoke(this, EventArgs.Empty));
    }

    public BasePanelViewModel Panel => _panel;
    public IEnumerable Rows => _panel.ViewModelProvider;
    public INotifyCollectionChanged RowsChanged => _panel.ViewModelProvider;
    public int RowCount => _panel.ViewModelProvider.Count;

    // 패널의 저장은 async void 다 — 끝남을 기다릴 수 없어 IsSaving(저장) · ReloadButtonEnable(재조회)의 변화로 잡는다.
    public bool IsBusy => _panel.IsSaving || !_panel.ReloadButtonEnable;

    public event EventHandler? BusyEnded;

    public void Select(IReadOnlyList<object> rows) => _panel.OnSelectionChanged(rows.OfType<T>().ToList());

    public object? CreateDraft()
    {
        // 패널의 [추가]는 목록에 Draft 를 꽂는다. 전후를 비교해 새로 생긴 것을 찾아 도로 뗀다
        // (권한 없음 · 처리 중이면 아무것도 안 생긴다 → null).
        var before = new HashSet<T>(_panel.ViewModelProvider);
        _panel.OnClickInsertButton(this, new System.Windows.RoutedEventArgs());
        var draft = _panel.ViewModelProvider.FirstOrDefault(row => !before.Contains(row));
        if (draft is not null) _panel.ViewModelProvider.Remove(draft);
        return draft;
    }

    public void AdoptDraft(object draft)
    {
        if (draft is T row && !_panel.ViewModelProvider.Contains(row)) _panel.ViewModelProvider.Add(row);
    }

    public void Save() => _panel.OnClickSaveButton(this, new System.Windows.RoutedEventArgs());
    public void Delete() => _panel.OnClickDeleteButton(this, new System.Windows.RoutedEventArgs());
    public void Reload() => _panel.OnClickReloadButton(this, new System.Windows.RoutedEventArgs());

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(_panel.IsSaving) or nameof(_panel.ReloadButtonEnable))) return;

        var busy = IsBusy;
        if (_wasBusy && !busy) BusyEnded?.Invoke(this, EventArgs.Empty);
        _wasBusy = busy;
    }
}
