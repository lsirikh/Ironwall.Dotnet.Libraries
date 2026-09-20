using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;

/// <summary>
/// 권한 설정 화면의 콘솔 쪽 껍데기 — <b>목록 칸 = 그룹 목록 · 상세 칸 = 매트릭스 + 구성원</b>.
/// 세 뷰를 번갈아 교체하던 것을 한 화면으로 편다(설계 정본 L1238 · 결정 L-D7 L1648).
/// </summary>
/// <remarks>
/// <para>전송 경로를 새로 만들지 않는다 — 저장은 <see cref="PermissionMatrixPanelViewModel.OnClickSave"/>
/// (= <c>POST /user-groups/{id}/permissions</c> <b>전체 교체 1회</b>, 본문은 원본 ∪ 화면).</para>
/// <para>칠하는 동안 서버 호출은 0이다. 드래그 페인팅 · Space · 행/열 전체 토글이 <b>같은 값</b>을 만든다.</para>
/// </remarks>
public sealed class PermissionMatrixConsoleViewModel : PropertyChangedBase, IPermissionMatrix
{
    private readonly PermissionMatrixPanelViewModel _panel;
    private readonly Func<bool> _canEdit;
    private readonly Dictionary<string, (bool View, bool Edit, bool Delete, bool Control)> _baseline = new(StringComparer.Ordinal);
    private readonly ILogService? _log;
    private PermissionGroupRowViewModel? _selected;
    private bool _showMembers;
    private bool _isSaving;
    private string? _lastMessage;
    private Func<bool>? _guard;
    private int _paintDepth;

    public PermissionMatrixConsoleViewModel(PermissionMatrixPanelViewModel panel, Func<bool> canEdit, ILogService? log = null)
    {
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        _log = log;
        _canEdit = canEdit ?? throw new ArgumentNullException(nameof(canEdit));
        Painter = new PermissionPainter(this);

        // Space · 체크박스 클릭은 행을 직접 고친다(페인팅 경로를 타지 않는다) — 그 변화도 미적용 건수에 들어와야 한다.
        _panel.Modules.CollectionChanged += OnModulesChanged;
    }

    private void OnModulesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        foreach (var row in e.OldItems?.OfType<ModulePermRowViewModel>() ?? Enumerable.Empty<ModulePermRowViewModel>())
            row.PropertyChanged -= OnModuleRowChanged;
        foreach (var row in e.NewItems?.OfType<ModulePermRowViewModel>() ?? Enumerable.Empty<ModulePermRowViewModel>())
            row.PropertyChanged += OnModuleRowChanged;
        RaiseAll();
    }

    private void OnModuleRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModulePermRowViewModel.View) or nameof(ModulePermRowViewModel.Edit)
            or nameof(ModulePermRowViewModel.Delete) or nameof(ModulePermRowViewModel.Control))
            RaiseDirty();
    }

    public PermissionMatrixPanelViewModel Panel => _panel;
    public PermissionPainter Painter { get; }

    public ObservableCollection<PermissionGroupRowViewModel> Groups => _panel.Groups;
    public ObservableCollection<ModulePermRowViewModel> Modules => _panel.Modules;
    public ObservableCollection<AuthUserDto> Members => _panel.Members;

    /// <summary>고른 그룹. 바뀌면 매트릭스를 다시 읽고 기준선을 새로 찍는다.</summary>
    public PermissionGroupRowViewModel? SelectedGroup
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;

            // 칠해 둔 변경이 있으면 다른 그룹으로 못 간다 — 가면 조용히 버려진다(사용자 폼과 같은 계약).
            if (IsDirty && !(_guard?.Invoke() ?? true))
            {
                SelectionRestoreRequested?.Invoke(this, _selected);
                NotifyOfPropertyChange();
                return;
            }

            _selected = value;
            _panel.LoadMatrixFor(value);
            MarkBaseline();
            _showMembers = false;
            RaiseAll();
            if (value is not null) _ = LoadMembersIfNeededAsync();
        }
    }

    /// <summary>문지기를 건너뛰고 고른다 — 레일을 떠날 때처럼 이미 버리기로 정한 경우에만.</summary>
    public void ForceSelect(PermissionGroupRowViewModel? row)
    {
        if (ReferenceEquals(_selected, row)) return;
        _selected = row;
        _panel.LoadMatrixFor(row);
        MarkBaseline();
        _showMembers = false;
        LastMessage = null;
        RaiseAll();
    }

    /// <summary>
    /// 그룹을 바꿔도 되는지 묻는 문지기(콘솔의 <c>NavigationGuard</c>). 막으면 false 를 돌려주고
    /// 화면은 <see cref="SelectionRestoreRequested"/> 로 옛 선택을 되살린다.
    /// </summary>
    public void UseNavigationGuard(Func<bool> guard) => _guard = guard;

    /// <summary>그리드의 선택을 이 그룹으로 되돌려 달라(막힌 이동).</summary>
    public event EventHandler<PermissionGroupRowViewModel?>? SelectionRestoreRequested;

    /// <summary>상세 칸의 칩 — 거짓이면 매트릭스, 참이면 구성원.</summary>
    public bool ShowMembers
    {
        get => _showMembers;
        set
        {
            if (_showMembers == value) return;
            _showMembers = value;
            RaiseAll();
            if (value) _ = LoadMembersIfNeededAsync();
        }
    }

    public bool ShowMatrix => !_showMembers;

    public string GroupTitle => _selected?.GroupName ?? "선택한 그룹 없음";
    public string? CatalogWarning => _panel.CatalogWarning;
    public bool HasCatalogWarning => _panel.HasCatalogWarning;

    /// <summary>가운데 칸 머리의 안내 — 목업 L1207 "모듈 16종 전부를 한 번에 보냅니다".</summary>
    public string SendAllNote => _panel.Modules.Count == 0 ? string.Empty : $"모듈 {_panel.Modules.Count}종 전부를 한 번에 보냅니다";

    /// <summary>요약의 미적용 건수.</summary>
    public string DirtyCountText => IsDirty ? $"{DirtyCount}건" : "없음";

    /// <summary>상태 띠 — "모듈 N · 표시 N".</summary>
    public string ModuleCountText => _panel.Modules.Count == 0 ? string.Empty : $"모듈 {_panel.Modules.Count} · 표시 {_panel.Modules.Count}";

    /// <summary>요약 — 켜진 모듈 수 / 전체.</summary>
    public string EnabledModuleText
        => _panel.Modules.Count == 0 ? "—" : $"{_panel.Modules.Count(m => m.View || m.Edit || m.Delete || m.Control)} / {_panel.Modules.Count}";

    public string MemberCountText => _selected is null ? "—" : _selected.UserCount.ToString();

    /// <summary>제어 권한이 켜진 모듈 — 서버가 실제로 집행하는 곳은 일부뿐이다.</summary>
    public string ControlModuleText
    {
        get
        {
            var names = _panel.Modules.Where(m => m.Control).Select(m => m.ModuleDisplay).ToList();
            return names.Count == 0 ? "없음" : string.Join(" · ", names);
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set { _isSaving = value; RaiseAll(); }
    }

    public bool CanEdit => _canEdit() && _selected is not null && !_isSaving;

    /// <summary>기준선과 다른 칸 수 — 상세 칸의 고정 적용 막대가 이 수를 보인다.</summary>
    public int DirtyCount
    {
        get
        {
            var count = 0;
            foreach (var row in _panel.Modules)
            {
                if (!_baseline.TryGetValue(row.ModuleKey, out var b)) { count += 4; continue; }
                if (row.View != b.View) count++;
                if (row.Edit != b.Edit) count++;
                if (row.Delete != b.Delete) count++;
                if (row.Control != b.Control) count++;
            }
            return count;
        }
    }

    public bool IsDirty => DirtyCount > 0;

    #region - IPermissionMatrix -
    public int RowCount => _panel.Modules.Count;

    public bool IsEnabled(PermissionCell cell)
    {
        if (!CanEdit) return false;
        var row = RowAt(cell.Row);
        return row is not null && cell.Verb switch
        {
            0 => row.ViewEnabled,
            1 => row.EditEnabled,
            2 => row.DeleteEnabled,
            3 => row.ControlEnabled,
            _ => false,
        };
    }

    public bool Get(PermissionCell cell)
    {
        var row = RowAt(cell.Row);
        return row is not null && cell.Verb switch
        {
            0 => row.View,
            1 => row.Edit,
            2 => row.Delete,
            3 => row.Control,
            _ => false,
        };
    }

    public void Set(PermissionCell cell, bool value)
    {
        // 화면의 IsEnabled 는 1차 방어다 — 바인딩 · 폴백 · 시험이 우회할 수 있으므로 여기서도 막는다.
        if (!IsEnabled(cell)) return;

        var row = RowAt(cell.Row);
        if (row is null) return;
        switch (cell.Verb)
        {
            case 0: row.View = value; break;
            case 1: row.Edit = value; break;
            case 2: row.Delete = value; break;
            case 3: row.Control = value; break;
            default: return;
        }
        RaiseDirty();
    }
    #endregion

    #region - Keyboard · button fallbacks (드래그 전용 UI 를 만들지 않는다) -
    /// <summary>Space — 포커스 칸 하나를 뒤집는다.</summary>
    public bool ToggleCell(int row, int verb)
    {
        var cell = new PermissionCell(row, verb);
        if (!IsEnabled(cell)) return false;
        Set(cell, !Get(cell));
        return true;
    }

    /// <summary>행 전체 토글 — 그 행에 켤 수 있는 칸이 하나라도 꺼져 있으면 전부 켜고, 아니면 전부 끈다.</summary>
    public bool ToggleRow(int row)
    {
        var cells = Enumerable.Range(0, PermissionPaintMath.VerbCount)
                              .Select(v => new PermissionCell(row, v))
                              .Where(IsEnabled)
                              .ToList();
        if (cells.Count == 0) return false;

        var turnOn = cells.Any(c => !Get(c));
        using (BeginPaintBatch())
            foreach (var cell in cells) Set(cell, turnOn);
        return true;
    }

    /// <summary>열 전체 토글 — 머리글을 누르면 그 동작을 켤 수 있는 모든 모듈에 같은 값을 준다.</summary>
    public bool ToggleVerb(int verb)
    {
        var cells = Enumerable.Range(0, RowCount)
                              .Select(r => new PermissionCell(r, verb))
                              .Where(IsEnabled)
                              .ToList();
        if (cells.Count == 0) return false;

        var turnOn = cells.Any(c => !Get(c));
        using (BeginPaintBatch())
            foreach (var cell in cells) Set(cell, turnOn);
        return true;
    }
    #endregion

    #region - Apply · revert -
    /// <summary>[적용] — 전체 교체 <b>1회</b>. 성공 여부는 패널이 팝업으로 알린다.</summary>
    public async Task<PermissionSaveOutcome> ApplyAsync()
    {
        if (!CanEdit || !IsDirty) return PermissionSaveOutcome.Fail("바꾼 칸이 없습니다.");

        PermissionSaveOutcome outcome;
        IsSaving = true;
        try
        {
            outcome = await _panel.SaveGroupPermissionsAsync().ConfigureAwait(true);
        }
        finally { IsSaving = false; }

        // ★ 성공했을 때만 기준선을 옮긴다. 실패에도 옮기면 미적용 표시가 사라지고 서버는 옛 값을 쥔 채 남는다.
        if (outcome.Success)
        {
            LastMessage = null;
            MarkBaseline();
        }
        else
        {
            LastMessage = outcome.Reason;
        }
        RaiseAll();
        return outcome;
    }

    /// <summary>방금 한 일 · 막힌 까닭 한 줄.</summary>
    public string? LastMessage
    {
        get => _lastMessage;
        private set { _lastMessage = value; NotifyOfPropertyChange(); }
    }

    /// <summary>[되돌리기] — 마지막으로 읽은 서버 값으로 되돌린다(서버 호출 0).</summary>
    public void Revert()
    {
        foreach (var row in _panel.Modules)
        {
            if (!_baseline.TryGetValue(row.ModuleKey, out var b)) continue;
            row.View = b.View;
            row.Edit = b.Edit;
            row.Delete = b.Delete;
            row.Control = b.Control;
        }
        RaiseAll();
    }

    /// <summary>지금 값을 기준선으로 삼는다(불러온 직후 · 저장 직후).</summary>
    public void MarkBaseline()
    {
        _baseline.Clear();
        foreach (var row in _panel.Modules)
            _baseline[row.ModuleKey] = (row.View, row.Edit, row.Delete, row.Control);
        RaiseAll();
    }

    public void Reset()
    {
        _selected = null;
        _showMembers = false;
        _baseline.Clear();
        _panel.LoadMatrixFor(null);
        RaiseAll();
    }
    #endregion

    private ModulePermRowViewModel? RowAt(int index)
        => index >= 0 && index < _panel.Modules.Count ? _panel.Modules[index] : null;

    private async Task LoadMembersIfNeededAsync()
    {
        try { await _panel.LoadMembersFor(_selected); }
        catch (Exception ex)
        {
            // 조용히 삼키면 "구성원 0명" 으로 보인다 — 한 줄로 알리고 기록한다.
            LastMessage = "구성원을 불러오지 못했습니다.";
            _log?.Error($"[AccountConsole] 구성원 로드 실패: {ex.Message}");
        }
        RaiseAll();
    }

    /// <summary>
    /// 칠하는 한 번(누름 · 한 걸음 · 놓음) 동안 알림을 묶는다 — 칸마다 알리면 한 번 쓸 때 수십 번 다시 그린다.
    /// </summary>
    public IDisposable BeginPaintBatch() => new PaintBatch(this);

    private void RaiseDirty()
    {
        if (_paintDepth > 0) return;        // 묶음이 끝날 때 한 번만 알린다
        NotifyOfPropertyChange(nameof(DirtyCount));
        NotifyOfPropertyChange(nameof(IsDirty));
        NotifyOfPropertyChange(nameof(EnabledModuleText));
        NotifyOfPropertyChange(nameof(ControlModuleText));
        NotifyOfPropertyChange(nameof(DirtyCountText));
    }

    private sealed class PaintBatch : IDisposable
    {
        private readonly PermissionMatrixConsoleViewModel _owner;
        public PaintBatch(PermissionMatrixConsoleViewModel owner) { _owner = owner; _owner._paintDepth++; }
        public void Dispose()
        {
            if (--_owner._paintDepth > 0) return;
            _owner._paintDepth = 0;
            _owner.RaiseDirty();
        }
    }

    private void RaiseAll() => Refresh();
}
