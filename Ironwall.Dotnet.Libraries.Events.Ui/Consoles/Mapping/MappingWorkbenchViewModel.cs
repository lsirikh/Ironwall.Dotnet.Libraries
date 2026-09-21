using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 이벤트 맵핑 워크벤치 — 3-Pane 콘솔 화면
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 이벤트 맵핑 워크벤치. ① 매핑 목록 ② 액션 보드(카메라·스피커·경광등 탭) ③ 장비 팔레트.
/// </summary>
/// <remarks>
/// <para><b>싱글턴이 아니다</b> — 창을 열 때마다 새로 만든다. 싱글턴이면 두 번째로 열 때
/// 죽은 시각 트리에 묶인 값이 남고, Draft 가 창을 넘어 살아남는다.</para>
/// <para><b>드롭은 서버를 부르지 않는다.</b> 투입·해제·정렬은 전부 <see cref="MappingBoard"/> 에서 끝나고,
/// 서버로 나가는 것은 [적용] 한 번뿐이다. 서버에 재정렬 API 가 없어서
/// 드롭마다 보내면 20행 재배치가 20회 PATCH 가 되고 API 타임아웃이 곱해진다.</para>
/// </remarks>
public sealed class MappingWorkbenchViewModel : Screen, IDragDropHandler
{
    /// <summary>콘솔 키 — 자동화 식별자와 설정 저장의 접두사.</summary>
    public const string ConsoleKeyName = "Mapping";

    /// <summary>권한 모듈 — v8.0 에서 <c>events</c> 가 아니라 <c>integrations</c> 다.</summary>
    public const string PermissionModule = "integrations";

    private readonly IMappingWorkbenchGateway _gateway;
    private readonly IMappingDeviceSource _devices;
    private readonly IPermissionService? _permissions;
    private readonly MappingBoard _board = new();

    private readonly Dictionary<MappingActionKind, List<MappingPaletteItemViewModel>> _paletteCache = new();
    private readonly List<EventMappingReadDto> _allMappings = new();

    private CancellationTokenSource? _loadToken;
    private MappingListItemViewModel? _selectedMapping;
    private MappingActionKind _selectedKind = MappingActionKind.Camera;
    private ConsoleRailEntry? _selectedRail;
    private string _searchText = string.Empty;
    private string _paletteSearch = string.Empty;
    private string _statusText = string.Empty;
    private string _warningText = string.Empty;
    private bool _isBusy;
    private bool _isApplying;

    /// <summary>생성자.</summary>
    /// <param name="gateway">서버 왕복.</param>
    /// <param name="devices">장비 캐시(팔레트·이름 조인).</param>
    /// <param name="permissions">권한. <c>null</c> 이면 편집 가능으로 본다(미리보기·시험용).</param>
    public MappingWorkbenchViewModel(
        IMappingWorkbenchGateway gateway,
        IMappingDeviceSource devices,
        IPermissionService? permissions = null)
    {
        _gateway = gateway;
        _devices = devices;
        _permissions = permissions;

        DisplayName = "이벤트 맵핑";
        Detail = new ConsoleDetailPresenter { TypeName = "이벤트 맵핑" };
        _board.Changed += OnBoardChanged;
        BuildRail();
    }

    #region - 화면 상태 -
    /// <summary>상세 칸 상태기계(적용 막대 · 이동 차단).</summary>
    public ConsoleDetailPresenter Detail { get; }

    /// <summary>레일 항목(종류 축 3개).</summary>
    public ObservableCollection<ConsoleRailEntry> RailEntries { get; } = new();

    /// <summary>왼쪽 매핑 목록(검색 적용 후).</summary>
    public ObservableCollection<MappingListItemViewModel> Mappings { get; } = new();

    /// <summary>가운데 보드 행(지금 고른 종류 축).</summary>
    public ObservableCollection<MappingRowViewModel> BoardRows { get; } = new();

    /// <summary>오른쪽 팔레트(검색 적용 후).</summary>
    public ObservableCollection<MappingPaletteItemViewModel> PaletteItems { get; } = new();

    /// <summary>장비그룹 후보.</summary>
    public ObservableCollection<MappingGroupInfo> Groups { get; } = new();

    /// <summary>카테고리 후보(닫힌 9값).</summary>
    public IReadOnlyList<string> Categories => EventMappingRules.CATEGORIES;

    /// <summary>보드에서 고른 행 — 해제·순서 버튼이 쓴다.</summary>
    public ObservableCollection<MappingRowViewModel> SelectedBoardRows { get; } = new();

    /// <summary>팔레트에서 고른 항목 — [추가 ▶] 가 쓴다.</summary>
    public ObservableCollection<MappingPaletteItemViewModel> SelectedPaletteItems { get; } = new();

    /// <summary>지금 고른 매핑.</summary>
    public MappingListItemViewModel? SelectedMapping
    {
        get => _selectedMapping;
        set
        {
            if (ReferenceEquals(_selectedMapping, value)) return;
            // 미저장 변경이 있으면 매핑을 바꾸지 않는다 — 바꾸면 Draft 가 소리 없이 사라진다.
            if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow))
            {
                NotifyOfPropertyChange();
                return;
            }
            _selectedMapping = value;
            IsCreatingMapping = false;
            SeedForm(value?.Dto);
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(HasMapping));
            NotifyOfPropertyChange(nameof(MappingTitle));
            _ = LoadBoardAsync();
        }
    }

    /// <summary>지금 고른 종류 축.</summary>
    public MappingActionKind SelectedKind
    {
        get => _selectedKind;
        private set
        {
            if (_selectedKind == value) return;
            _selectedKind = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(BoardZoneKey));
            NotifyOfPropertyChange(nameof(KindLabel));
            RebuildBoardRows();
            RebuildPalette();
        }
    }

    /// <summary>레일 선택(종류 축과 한 몸이다).</summary>
    public ConsoleRailEntry? SelectedRail
    {
        get => _selectedRail;
        set
        {
            if (ReferenceEquals(_selectedRail, value)) return;
            _selectedRail = value;
            NotifyOfPropertyChange();
            if (value?.Tag is MappingActionKind kind) SelectedKind = kind;
        }
    }

    /// <summary>지금 보드가 받는 드롭존 키 — 종류가 바뀌면 같이 바뀐다.</summary>
    public string BoardZoneKey => MappingKindText.BoardZone(SelectedKind);

    /// <summary>팔레트 드롭존 키(해제 방향).</summary>
    public string PaletteZoneKey => MappingKindText.PaletteZone;

    /// <summary>지금 종류 축의 한국어 이름.</summary>
    public string KindLabel => MappingKindText.Label(SelectedKind);

    /// <summary>매핑을 골랐는가.</summary>
    public bool HasMapping => _selectedMapping is not null;

    /// <summary>보드 머리에 쓸 제목.</summary>
    public string MappingTitle => _selectedMapping?.Name ?? "맵핑을 고르십시오";

    /// <summary>매핑 목록 검색어.</summary>
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? string.Empty; NotifyOfPropertyChange(); RebuildMappingList(); }
    }

    /// <summary>팔레트 검색어.</summary>
    public string PaletteSearch
    {
        get => _paletteSearch;
        set { _paletteSearch = value ?? string.Empty; NotifyOfPropertyChange(); RebuildPalette(); }
    }

    /// <summary>상태줄 문구.</summary>
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; NotifyOfPropertyChange(); }
    }

    /// <summary>경고 줄 — 서버가 검사하지 않는 것들을 워크벤치가 대신 본다.</summary>
    public string WarningText
    {
        get => _warningText;
        private set { _warningText = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasWarning)); }
    }

    /// <summary>보여 줄 경고가 있는가.</summary>
    public bool HasWarning => !string.IsNullOrEmpty(WarningText);

    /// <summary>불러오는 중인가.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; NotifyOfPropertyChange(); RaiseCommandStates(); }
    }

    /// <summary>적용하는 중인가 — 이 동안 화면 전체가 입력 잠금이다.</summary>
    public bool IsApplying
    {
        get => _isApplying;
        private set { _isApplying = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsInputEnabled)); RaiseCommandStates(); }
    }

    /// <summary>입력을 받을 수 있는가(적용 중에는 전부 잠근다 — 두 번 커밋하는 사고를 막는다).</summary>
    public bool IsInputEnabled => !IsApplying;
    #endregion

    #region - 권한 -
    /// <summary>조회 권한.</summary>
    public bool CanView => _permissions?.CanView(PermissionModule) ?? true;

    /// <summary>편집 권한.</summary>
    public bool CanEdit => (_permissions?.CanEdit(PermissionModule) ?? true) && !IsApplying;

    /// <summary>해제 권한.</summary>
    public bool CanDelete => (_permissions?.CanDelete(PermissionModule) ?? true) && !IsApplying;

    /// <summary>읽기 전용인가 — 드래그 시작 자체를 막는다.</summary>
    public bool IsReadOnly => !CanEdit;

    /// <summary>편집 권한이 있는가(드래그 가능 여부와 같은 값 — 바인딩 이름을 나눠 둔다).</summary>
    public bool IsDragEnabled => CanEdit && HasMapping;

    /// <summary>권한 안내 한 줄 — 상태줄 오른쪽.</summary>
    public string PermissionText => CanEdit ? $"{PermissionModule}:edit ✓" : "읽기 전용";
    #endregion

    #region - 생명주기 -
    /// <inheritdoc/>
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_permissions is not null) _permissions.PermissionsChanged += OnPermissionsChanged;
        OnPermissionsChanged();     // ★ 1회 직접 호출 — 빠뜨리면 첫 진입 버튼이 권한과 무관하게 살아 있다
        Detail.Guard.Blocked += OnNavigationBlocked;

        await ReloadAsync().ConfigureAwait(false);
        await base.OnActivateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (_permissions is not null) _permissions.PermissionsChanged -= OnPermissionsChanged;
        Detail.Guard.Blocked -= OnNavigationBlocked;
        _loadToken?.Cancel();
        _loadToken = null;
        Detail.Reset();
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
        // 미저장 변경이 있으면 닫히지 않는다 — 막대가 흔들리며 이유를 말한다.
        => Task.FromResult(!_board.IsDirty || Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow));

    private void OnPermissionsChanged()
    {
        NotifyOfPropertyChange(nameof(CanView));
        NotifyOfPropertyChange(nameof(CanEdit));
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(IsReadOnly));
        NotifyOfPropertyChange(nameof(IsDragEnabled));
        NotifyOfPropertyChange(nameof(PermissionText));
        Detail.IsReadOnly = IsReadOnly;
        RaiseCommandStates();
    }

    private void OnNavigationBlocked(object? sender, ConsoleNavigation navigation)
        => StatusText = ConsoleDetailStateMachine.BlockedNotice;
    #endregion

    #region - 불러오기 -
    /// <summary>매핑 목록·장비 캐시를 새로 읽는다.</summary>
    public async Task ReloadAsync()
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;

        IsBusy = true;
        try
        {
            Groups.Clear();
            foreach (var group in _devices.Groups()) Groups.Add(group);
            _paletteCache.Clear();

            var result = await _gateway.ListMappingsAsync().ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                // 🔴 실패에 캐시를 비우지 않는다 — 비우면 500/503 이 "0건" 으로 보인다.
                StatusText = result.Message;
                return;
            }

            _allMappings.Clear();
            _allMappings.AddRange(result.Value ?? Array.Empty<EventMappingReadDto>());
            RebuildMappingList();

            if (_selectedMapping is not null)
            {
                var again = Mappings.FirstOrDefault(m => m.Id == _selectedMapping.Id);
                _selectedMapping = again;
            }
            _selectedMapping ??= Mappings.FirstOrDefault();
            SeedForm(_selectedMapping?.Dto);
            NotifyOfPropertyChange(nameof(SelectedMapping));
            NotifyOfPropertyChange(nameof(HasMapping));
            NotifyOfPropertyChange(nameof(MappingTitle));

            await LoadBoardAsync().ConfigureAwait(false);
            StatusText = $"맵핑 {Mappings.Count}건";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadBoardAsync()
    {
        _loadToken?.Cancel();
        var source = new CancellationTokenSource();
        _loadToken = source;
        var token = source.Token;

        _board.Clear();
        RebuildBoardRows();
        RebuildPalette();

        var mapping = _selectedMapping;
        if (mapping is null)
        {
            UpdateWarnings();
            return;
        }

        IsBusy = true;
        try
        {
            var cameras = await _gateway.ListCamerasAsync(mapping.Id, token).ConfigureAwait(false);
            var speakers = await _gateway.ListSpeakersAsync(mapping.Id, token).ConfigureAwait(false);
            var lamps = await _gateway.ListLampsAsync(mapping.Id, token).ConfigureAwait(false);

            // 늦게 도착한 응답이 새 선택을 덮어쓰지 않게 한다.
            if (token.IsCancellationRequested || !ReferenceEquals(_loadToken, source)) return;

            var problems = new List<string>();
            if (cameras.IsSuccess) _board.Load(MappingActionKind.Camera, (cameras.Value ?? Array.Empty<MappingCameraReadDto>()).Select(MappingBoardRow.FromDto));
            else problems.Add(cameras.Message);

            if (speakers.IsSuccess) _board.Load(MappingActionKind.Speaker, (speakers.Value ?? Array.Empty<MappingSpeakerReadDto>()).Select(MappingBoardRow.FromDto));
            else problems.Add(speakers.Message);

            if (lamps.IsSuccess) _board.Load(MappingActionKind.Lamp, (lamps.Value ?? Array.Empty<MappingLampReadDto>()).Select(MappingBoardRow.FromDto));
            else problems.Add(lamps.Message);

            mapping.CameraCount = _board.LiveRows(MappingActionKind.Camera).Count;
            mapping.SpeakerCount = _board.LiveRows(MappingActionKind.Speaker).Count;
            mapping.LampCount = _board.LiveRows(MappingActionKind.Lamp).Count;

            if (problems.Count > 0) StatusText = string.Join(" · ", problems.Distinct());
        }
        finally
        {
            IsBusy = false;
            RebuildBoardRows();
            RebuildPalette();
            RefreshRailCounts();
            UpdateWarnings();
        }
    }
    #endregion

    #region - 목록·팔레트 다시 그리기 -
    private void BuildRail()
    {
        RailEntries.Clear();
        foreach (var kind in MappingBoard.Kinds)
            RailEntries.Add(new ConsoleRailEntry(MappingKindText.Segment(kind), MappingKindText.Label(kind), new MappingConsoleIcon(IconOf(kind))) { Tag = kind });
        _selectedRail = RailEntries.FirstOrDefault();
    }

    private static string IconOf(MappingActionKind kind) => kind switch
    {
        MappingActionKind.Camera => "Cctv",
        MappingActionKind.Speaker => "Bullhorn",
        MappingActionKind.Lamp => "CarLightAlert",
        _ => "HelpCircleOutline",
    };

    private void RefreshRailCounts()
    {
        foreach (var entry in RailEntries)
        {
            if (entry.Tag is not MappingActionKind kind) continue;
            entry.ShowCount = true;
            entry.Count = _board.LiveRows(kind).Count;
            entry.BadCount = _board.BlockingOrphans(kind).Count;
        }
    }

    private void RebuildMappingList()
    {
        var needle = SearchText.Trim();
        var rows = _allMappings
            .Where(m => needle.Length == 0
                || (m.NameEvent ?? string.Empty).Contains(needle, StringComparison.CurrentCultureIgnoreCase)
                || EventMappingRules.CategoryLabel(m.CategoryEventMapping).Contains(needle, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        // 지운 것만 빼고 없는 것만 넣는다 — 통째로 비우면 선택이 날아간다.
        for (var i = Mappings.Count - 1; i >= 0; i--)
            if (rows.All(r => r.Id != Mappings[i].Id)) Mappings.RemoveAt(i);

        for (var i = 0; i < rows.Count; i++)
        {
            var existing = Mappings.FirstOrDefault(m => m.Id == rows[i].Id);
            if (existing is null) Mappings.Insert(Math.Min(i, Mappings.Count), new MappingListItemViewModel(rows[i]));
            else existing.Replace(rows[i]);
        }
    }

    private void RebuildBoardRows()
    {
        var rows = _board.Rows(SelectedKind);
        var selectedKeys = SelectedBoardRows.Select(r => r.Row).ToHashSet();

        BoardRows.Clear();
        foreach (var row in rows)
        {
            var vm = new MappingRowViewModel(row) { DeviceName = NameOf(row) };
            BoardRows.Add(vm);
        }

        SelectedBoardRows.Clear();
        foreach (var vm in BoardRows.Where(v => selectedKeys.Contains(v.Row))) SelectedBoardRows.Add(vm);

        NotifyOfPropertyChange(nameof(BoardRows));
        RaiseCommandStates();
    }

    private string NameOf(MappingBoardRow row)
    {
        if (row.DeviceId is not int id) return "연결 끊김";
        var info = _devices.Find(row.Kind, id);
        return info?.Name ?? $"#{id}";      // 캐시에 없으면 숫자로 — 지어내지 않는다
    }

    private void RebuildPalette()
    {
        if (!_paletteCache.TryGetValue(SelectedKind, out var cached))
        {
            cached = _devices.Devices(SelectedKind)
                             .Select(d => new MappingPaletteItemViewModel(SelectedKind, d))
                             .ToList();
            _paletteCache[SelectedKind] = cached;
        }

        foreach (var item in cached) item.IsRegistered = _board.Contains(SelectedKind, item.Id);

        var filtered = MappingPaletteFilter.Apply(cached, PaletteSearch);
        PaletteItems.Clear();
        foreach (var item in filtered) PaletteItems.Add(item);
        NotifyOfPropertyChange(nameof(PaletteItems));
    }

    private void OnBoardChanged(object? sender, EventArgs e)
    {
        RebuildBoardRows();
        RebuildPalette();
        RefreshRailCounts();
        UpdateDirty();
        UpdateWarnings();
    }

    private void UpdateDirty()
    {
        Detail.Tracker.Clear();
        if (_board.TotalAdded > 0) Detail.Tracker.Touch("added", 0, _board.TotalAdded);
        if (_board.TotalRemoved > 0) Detail.Tracker.Touch("removed", 0, _board.TotalRemoved);
        if (_board.TotalEdited > 0) Detail.Tracker.Touch("edited", 0, _board.TotalEdited);
        if (_board.TotalReordered > 0) Detail.Tracker.Touch("reordered", 0, _board.TotalReordered);

        NotifyOfPropertyChange(nameof(DraftText));
        NotifyOfPropertyChange(nameof(IsDirty));
        RaiseCommandStates();
    }

    private void UpdateWarnings()
    {
        var warnings = MappingWarnings.For(_selectedMapping?.Dto, _allMappings, _board, _devices);
        WarningText = MappingWarnings.Summarize(warnings);
        _blocked = MappingWarnings.Blocks(warnings);
        RaiseCommandStates();
    }

    private bool _blocked;
    #endregion

    #region - Draft 요약 -
    /// <summary>미저장 변경이 있는가.</summary>
    public bool IsDirty => _board.IsDirty;

    /// <summary>상태줄 Draft 요약.</summary>
    public string DraftText => _board.IsDirty
        ? $"미저장 추가 {_board.TotalAdded} · 해제 {_board.TotalRemoved} · 수정 {_board.TotalEdited} · 정렬 {_board.TotalReordered}"
        : "변경 없음";

    /// <summary>선택 요약.</summary>
    public string SelectionText => $"선택 {SelectedBoardRows.Count}건";
    #endregion

    #region - 조작(드래그와 버튼이 같은 경로를 쓴다) -
    /// <summary>팔레트에서 고른 장비를 보드에 넣는다 — 드래그의 <b>키보드·버튼 짝</b>.</summary>
    public void AddSelected() => AddDevices(SelectedPaletteItems.Select(i => i.Id).ToList(), -1);

    /// <summary>장비를 보드에 넣는다. 드래그도 버튼도 결국 여기로 온다.</summary>
    public void AddDevices(IReadOnlyList<int> deviceIds, int insertIndex)
    {
        if (!CanEdit || !HasMapping || deviceIds.Count == 0) return;

        var (added, skipped) = _board.Add(SelectedKind, deviceIds, insertIndex);
        if (added.Count == 0 && skipped.Count > 0)
        {
            StatusText = $"{skipped.Count}건은 이미 등록되어 있어 넣지 않았습니다.";
            return;
        }
        StatusText = skipped.Count > 0
            ? $"{added.Count}건 추가 — {skipped.Count}건은 이미 등록됨"
            : $"{added.Count}건 추가";
    }

    /// <summary>고른 보드 행을 해제 표시한다 — 드래그백의 <b>키보드·버튼 짝</b>.</summary>
    public void ReleaseSelected()
    {
        if (!CanDelete || SelectedBoardRows.Count == 0) return;
        var rows = SelectedBoardRows.Select(r => r.Row).ToList();
        _board.Remove(SelectedKind, rows);
        StatusText = $"{rows.Count}건 해제 — [되돌리기] 로 되살릴 수 있습니다";
    }

    /// <summary>해제 표시를 되돌린다.</summary>
    public void RestoreSelected()
    {
        if (!CanEdit || SelectedBoardRows.Count == 0) return;
        _board.Restore(SelectedKind, SelectedBoardRows.Select(r => r.Row).ToList());
    }

    /// <summary>고른 행을 한 칸 위로 — <c>Alt+↑</c> 와 ▲ 버튼이 같이 쓴다.</summary>
    public void MoveUp() => Step(-1);

    /// <summary>고른 행을 한 칸 아래로.</summary>
    public void MoveDown() => Step(1);

    private void Step(int direction)
    {
        if (!CanEdit || SelectedBoardRows.Count != 1) return;
        _board.Step(SelectedKind, SelectedBoardRows[0].Row, direction);
    }

    /// <summary>위로 옮길 수 있는가.</summary>
    public bool CanMoveUp => CanEdit && SelectedBoardRows.Count == 1
        && MappingPriority.StepTarget(_board.Rows(SelectedKind).ToList().IndexOf(SelectedBoardRows[0].Row), -1, _board.Rows(SelectedKind).Count) >= 0;

    /// <summary>아래로 옮길 수 있는가.</summary>
    public bool CanMoveDown => CanEdit && SelectedBoardRows.Count == 1
        && MappingPriority.StepTarget(_board.Rows(SelectedKind).ToList().IndexOf(SelectedBoardRows[0].Row), 1, _board.Rows(SelectedKind).Count) >= 0;

    /// <summary>추가 버튼을 누를 수 있는가.</summary>
    public bool CanAddSelected => CanEdit && HasMapping && SelectedPaletteItems.Any(i => !i.IsRegistered);

    /// <summary>해제 버튼을 누를 수 있는가.</summary>
    public bool CanReleaseSelected => CanDelete && SelectedBoardRows.Any(r => !r.IsRemoved);

    /// <summary>되돌리기를 누를 수 있는가.</summary>
    public bool CanRevert => CanEdit && (_board.IsDirty || _board.CanUndo);

    /// <summary>적용을 누를 수 있는가 — 고아가 있으면 막힌다.</summary>
    public bool CanApply => CanEdit && HasMapping && _board.IsDirty && !_blocked && !IsApplying;

    private void RaiseCommandStates()
    {
        NotifyOfPropertyChange(nameof(CanAddSelected));
        NotifyOfPropertyChange(nameof(CanReleaseSelected));
        NotifyOfPropertyChange(nameof(CanMoveUp));
        NotifyOfPropertyChange(nameof(CanMoveDown));
        NotifyOfPropertyChange(nameof(CanRevert));
        NotifyOfPropertyChange(nameof(CanApply));
        NotifyOfPropertyChange(nameof(SelectionText));
        NotifyOfPropertyChange(nameof(IsDragEnabled));
        NotifyOfPropertyChange(nameof(HasMappingEdit));
        NotifyOfPropertyChange(nameof(CanSaveMapping));
    }

    /// <summary>보드 선택이 바뀌었다(뷰가 알려 준다).</summary>
    public void OnSelectionChanged() => RaiseCommandStates();
    #endregion

    #region - 맵핑 속성 폼 (본체는 호출 1회라 즉시 전송한다) -
    private bool _isCreatingMapping;
    private string _editName = string.Empty;
    private int? _editGroupId;
    private string _editCategory = EventMappingRules.CATEGORY_NONE;
    private string _editDescription = string.Empty;
    private bool _editStatus = true;
    private string _formError = string.Empty;

    /// <summary>새 맵핑을 만드는 중인가.</summary>
    public bool IsCreatingMapping
    {
        get => _isCreatingMapping;
        private set { _isCreatingMapping = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(SaveMappingText)); RaiseFormStates(); }
    }

    /// <summary>이벤트 이름.</summary>
    public string EditName { get => _editName; set { _editName = value ?? string.Empty; NotifyOfPropertyChange(); RaiseFormStates(); } }

    /// <summary>장비그룹. <c>null</c> 은 "미지정" 이다.</summary>
    public int? EditGroupId { get => _editGroupId; set { _editGroupId = value; NotifyOfPropertyChange(); RaiseFormStates(); } }

    /// <summary>이벤트 카테고리(와이어 값).</summary>
    public string EditCategory { get => _editCategory; set { _editCategory = value ?? EventMappingRules.CATEGORY_NONE; NotifyOfPropertyChange(); RaiseFormStates(); } }

    /// <summary>설명.</summary>
    public string EditDescription { get => _editDescription; set { _editDescription = value ?? string.Empty; NotifyOfPropertyChange(); RaiseFormStates(); } }

    /// <summary>운용 여부(<c>status</c>).</summary>
    public bool EditStatus { get => _editStatus; set { _editStatus = value; NotifyOfPropertyChange(); RaiseFormStates(); } }

    /// <summary>폼 검증 실패 사유 — 사용자에게 그대로 보여도 되는 한국어다.</summary>
    public string MappingFormError { get => _formError; private set { _formError = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasFormError)); } }

    /// <summary>보여 줄 검증 오류가 있는가.</summary>
    public bool HasFormError => !string.IsNullOrEmpty(MappingFormError);

    /// <summary>저장 버튼 문구.</summary>
    public string SaveMappingText => IsCreatingMapping ? "등록" : "맵핑 저장";

    /// <summary>맵핑 본체에 바뀐 값이 있는가.</summary>
    public bool HasMappingEdit
    {
        get
        {
            if (IsCreatingMapping) return true;
            var dto = _selectedMapping?.Dto;
            if (dto is null) return false;
            return !string.Equals(EventMappingRules.NormalizeText(EditName), EventMappingRules.NormalizeText(dto.NameEvent), StringComparison.Ordinal)
                || EditGroupId != dto.DeviceGroupId
                || !string.Equals(EditCategory, dto.CategoryEventMapping ?? EventMappingRules.CATEGORY_NONE, StringComparison.Ordinal)
                || !string.Equals(EventMappingRules.NormalizeText(EditDescription), EventMappingRules.NormalizeText(dto.Description), StringComparison.Ordinal)
                || EditStatus != dto.Status;
        }
    }

    /// <summary>맵핑 저장 버튼을 누를 수 있는가.</summary>
    public bool CanSaveMapping =>
        CanEdit && HasMappingEdit
        && EventMappingRules.TryValidateMapping(EditName, EditCategory, EditDescription, out _);

    /// <summary>새 맵핑 만들기를 시작한다 — 아직 서버에 가지 않는다.</summary>
    public void BeginCreateMapping()
    {
        if (!CanEdit) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;

        IsCreatingMapping = true;
        EditName = string.Empty;
        EditGroupId = null;
        EditCategory = EventMappingRules.CATEGORY_NONE;
        EditDescription = string.Empty;
        EditStatus = true;
        MappingFormError = string.Empty;
        StatusText = "새 맵핑의 이름과 조건을 정한 뒤 [등록] 을 누르십시오.";
    }

    /// <summary>새 맵핑 만들기를 접는다.</summary>
    public void CancelCreateMapping()
    {
        IsCreatingMapping = false;
        SeedForm(_selectedMapping?.Dto);
        MappingFormError = string.Empty;
    }

    /// <summary>
    /// 맵핑 본체를 저장한다 — 생성이면 <c>POST</c>, 수정이면 <c>PATCH</c>. <b>호출 1회라 즉시 보낸다</b>.
    /// </summary>
    /// <remarks>
    /// 배선(액션 보드)은 호출이 여러 번으로 번지므로 Draft + [적용] 이지만,
    /// 본체는 한 번이라 봉투 규약대로 바로 보낸다.
    /// </remarks>
    public async Task SaveMappingAsync()
    {
        if (!CanSaveMapping) return;
        if (!EventMappingRules.TryValidateMapping(EditName, EditCategory, EditDescription, out var error))
        {
            MappingFormError = error ?? "입력값을 확인하십시오.";
            return;
        }
        MappingFormError = string.Empty;

        IsApplying = true;
        try
        {
            if (IsCreatingMapping)
            {
                var body = MappingRequestBuilder.CreateMapping(EditName, EditGroupId, EditCategory, EditDescription, EditStatus);
                var created = await _gateway.CreateMappingAsync(body).ConfigureAwait(false);
                if (!created.IsSuccess) { MappingFormError = created.Message; return; }

                IsCreatingMapping = false;
                _allMappings.Add(created.Value!);
                RebuildMappingList();
                _selectedMapping = Mappings.FirstOrDefault(m => m.Id == created.Value!.Id);
                NotifyOfPropertyChange(nameof(SelectedMapping));
                NotifyOfPropertyChange(nameof(HasMapping));
                NotifyOfPropertyChange(nameof(MappingTitle));
                StatusText = "맵핑을 등록했습니다.";
                await LoadBoardAsync().ConfigureAwait(false);
                return;
            }

            var current = _selectedMapping;
            if (current is null) return;

            var patch = MappingRequestBuilder.PatchMapping(current.Dto, EditName, EditGroupId, EditCategory, EditDescription, EditStatus);
            if (patch.IsEmpty) { StatusText = "바뀐 값이 없습니다."; return; }

            var saved = await _gateway.PatchMappingAsync(current.Id, patch).ConfigureAwait(false);
            if (!saved.IsSuccess) { MappingFormError = saved.Message; return; }

            current.Replace(saved.Value!);
            var index = _allMappings.FindIndex(m => m.Id == current.Id);
            if (index >= 0) _allMappings[index] = saved.Value!;
            SeedForm(saved.Value);
            StatusText = "맵핑을 저장했습니다.";
            UpdateWarnings();
        }
        finally
        {
            IsApplying = false;
            RaiseFormStates();
        }
    }

    private void SeedForm(EventMappingReadDto? dto)
    {
        _editName = dto?.NameEvent ?? string.Empty;
        _editGroupId = dto?.DeviceGroupId;
        _editCategory = dto?.CategoryEventMapping ?? EventMappingRules.CATEGORY_NONE;
        _editDescription = dto?.Description ?? string.Empty;
        _editStatus = dto?.Status ?? true;

        NotifyOfPropertyChange(nameof(EditName));
        NotifyOfPropertyChange(nameof(EditGroupId));
        NotifyOfPropertyChange(nameof(EditCategory));
        NotifyOfPropertyChange(nameof(EditDescription));
        NotifyOfPropertyChange(nameof(EditStatus));
        RaiseFormStates();
    }

    private void RaiseFormStates()
    {
        NotifyOfPropertyChange(nameof(HasMappingEdit));
        NotifyOfPropertyChange(nameof(CanSaveMapping));
    }

    /// <summary>툴바 [맵핑 등록] — 뷰가 부른다.</summary>
    public Task CreateMappingAsync()
    {
        BeginCreateMapping();
        return Task.CompletedTask;
    }
    #endregion

    #region - 되돌리기 · 적용 -
    /// <summary>미저장 변경을 전부 버린다. <b>서버를 한 번도 부르지 않는다.</b></summary>
    public async Task RevertAsync()
    {
        if (!CanRevert) return;
        Detail.Tracker.Clear();
        await LoadBoardAsync().ConfigureAwait(false);
        Detail.Settle("변경을 되돌렸습니다.");
        StatusText = "변경을 되돌렸습니다.";
    }

    /// <summary>
    /// 미저장 변경을 서버에 보낸다 — <b>축마다 ① 해제 ② 등록 ③ 수정</b>.
    /// </summary>
    /// <remarks>
    /// <para>보내기 전에 <b>매핑을 다시 읽어 대조</b>한다. 그 사이 누가 고쳤으면
    /// <b>아무것도 보내지 않고</b> 새로 고치기를 권한다 — 조용한 덮어쓰기가 가장 나쁜 결과다.</para>
    /// <para>끝난 뒤에는 <b>재조회</b>로 화면을 다시 세운다. 낙관적 반영으로 끝내지 않는다.</para>
    /// </remarks>
    public async Task ApplyAsync()
    {
        if (!CanApply || _selectedMapping is null) return;
        if (IsApplying) return;                       // 연타 가드 — 같은 변경을 두 번 보내지 않는다

        var mapping = _selectedMapping;
        IsApplying = true;
        var outcome = new MappingCommitOutcome();
        try
        {
            var fresh = await _gateway.GetMappingAsync(mapping.Id).ConfigureAwait(false);
            if (!fresh.IsSuccess)
            {
                outcome.Abort(fresh.Message);
                StatusText = outcome.ToMessage();
                return;
            }
            if (!string.Equals(fresh.Value!.UpdatedAt, mapping.Dto.UpdatedAt, StringComparison.Ordinal))
            {
                outcome.Abort("다른 사용자가 이 맵핑을 바꿨습니다. 새로 고친 뒤 다시 시도하십시오. 변경한 내용은 그대로 있습니다.");
                StatusText = outcome.ToMessage();
                return;
            }

            foreach (var vm in BoardRows) vm.ClearFailure();

            var plan = MappingCommitPlan.From(_board);
            foreach (var kindPlan in plan.Kinds)
            {
                if (!kindPlan.HasWork) continue;
                await ApplyKindAsync(mapping.Id, kindPlan, outcome).ConfigureAwait(false);
            }

            StatusText = outcome.ToMessage();
        }
        catch (Exception ex)
        {
            // 예외 원문은 사용자에게 내지 않는다. Draft 는 그대로 둔다.
            outcome.Abort("적용 중 오류가 났습니다. 변경은 그대로 남아 있습니다.");
            StatusText = outcome.ToMessage();
            System.Diagnostics.Trace.WriteLine($"[MappingWorkbench] apply failed: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
            if (!outcome.IsAborted)
            {
                // 성공한 것만 화면에서 정리하고, 진실은 재조회로 확정한다.
                _board.ClearUndo();                    // ★ 비우지 않으면 되돌리기가 id=0 행을 되살려 중복 등록한다
                Detail.Tracker.Clear();
                Detail.Settle(outcome.ToMessage());
                await LoadBoardAsync().ConfigureAwait(false);
                MarkFailures(outcome);
            }
        }
    }

    private async Task ApplyKindAsync(int mappingId, MappingKindPlan plan, MappingCommitOutcome outcome)
    {
        foreach (var chunk in plan.ReleaseChunks)
        {
            var rows = _board.Rows(plan.Kind).Where(r => chunk.Contains(r.ConfigId)).ToList();
            var result = await _gateway.BulkUnassignAsync(mappingId, plan.Kind, chunk).ConfigureAwait(false);
            if (!result.IsSuccess) { outcome.AcceptPatch(rows.FirstOrDefault() ?? MappingBoardRow.NewFor(plan.Kind, 0), false, result.Message); continue; }
            outcome.AcceptRelease(rows, result.Value!);
        }

        foreach (var chunk in plan.CreateChunks)
        {
            var rows = chunk.Select(c => c.Row).ToList();
            var items = chunk.Select(c => c.Item).ToList();
            var result = await _gateway.BulkCreateAsync(mappingId, plan.Kind, items).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                foreach (var row in rows) outcome.AcceptPatch(row, false, result.Message);
                continue;
            }
            outcome.AcceptCreate(rows, result.Value!);
        }

        foreach (var (row, configId, body) in plan.Patches)
        {
            var result = await _gateway.PatchConfigAsync(mappingId, plan.Kind, configId, body).ConfigureAwait(false);
            outcome.AcceptPatch(row, result.IsSuccess, result.Message);
        }
    }

    private void MarkFailures(MappingCommitOutcome outcome)
    {
        if (!outcome.HasFailure) return;
        var note = outcome.FailureNotes.FirstOrDefault();
        var failedDevices = outcome.FailedRows.Select(r => r.DeviceId).Where(id => id is not null).ToHashSet();
        foreach (var vm in BoardRows)
            if (failedDevices.Contains(vm.Row.DeviceId)) vm.MarkFailure(note);
    }
    #endregion

    #region - 드래그 판정 -
    /// <inheritdoc/>
    /// <remarks>드래그 중 끊임없이 불린다 — 서버를 부르지 않고 목록을 새로 만들지도 않는다.</remarks>
    public bool CanDrop(DragPayload payload, DropTarget target) => Verdict(payload, target).IsAllowed;

    /// <summary>드롭 판정 + 사유. 화면이 거절 사유를 말할 수 있게 갈라 둔다.</summary>
    public MappingDropVerdict Verdict(DragPayload payload, DropTarget target)
    {
        if (IsApplying) return MappingDropVerdict.Block("적용하는 중입니다.");

        if (target.ZoneKey == MappingKindText.PaletteZone)
        {
            var rows = payload.Items.OfType<MappingRowViewModel>().ToList();
            if (rows.Count != payload.Items.Count) return MappingDropVerdict.Block("보드의 행만 해제할 수 있습니다.");
            return MappingEligibility.BoardToPalette(IsReadOnly, CanDelete, rows.Count);
        }

        var zoneKind = MappingBoard.Kinds.FirstOrDefault(k => MappingKindText.BoardZone(k) == target.ZoneKey);
        if (MappingKindText.BoardZone(zoneKind) != target.ZoneKey) return MappingDropVerdict.Block("여기에는 놓을 수 없습니다.");

        if (payload.Items.Count > 0 && payload.Items.All(i => i is MappingPaletteItemViewModel))
        {
            var items = payload.Items.Cast<MappingPaletteItemViewModel>().ToList();
            var sourceKind = items[0].Kind;
            var fresh = MappingEligibility.NewDeviceIds(_board, zoneKind, items.Select(i => i.Id)).Count;
            return MappingEligibility.PaletteToBoard(sourceKind, zoneKind, IsReadOnly, HasMapping, fresh);
        }

        if (payload.Items.Count > 0 && payload.Items.All(i => i is MappingRowViewModel))
        {
            var rows = payload.Items.Cast<MappingRowViewModel>().ToList();
            return MappingEligibility.BoardReorder(rows[0].Kind, zoneKind, IsReadOnly, rows.Count);
        }

        return MappingDropVerdict.Block("여기에는 놓을 수 없습니다.");
    }

    /// <inheritdoc/>
    public void Drop(DragPayload payload, DropTarget target)
    {
        var verdict = Verdict(payload, target);
        if (!verdict.IsAllowed)
        {
            StatusText = verdict.Reason;    // 거절을 조용히 삼키지 않는다
            return;
        }

        if (target.ZoneKey == MappingKindText.PaletteZone)
        {
            var rows = payload.Items.OfType<MappingRowViewModel>().Select(r => r.Row).ToList();
            _board.Remove(SelectedKind, rows);
            StatusText = $"{rows.Count}건 해제 — [되돌리기] 로 되살릴 수 있습니다";
            return;
        }

        var index = target.IsReorder ? target.InsertionIndex : -1;

        if (payload.Items.All(i => i is MappingPaletteItemViewModel))
        {
            AddDevices(payload.Items.Cast<MappingPaletteItemViewModel>().Select(i => i.Id).ToList(), index);
            return;
        }

        var moving = payload.Items.OfType<MappingRowViewModel>().Select(r => r.Row).ToList();
        _board.Move(SelectedKind, moving, index < 0 ? _board.Rows(SelectedKind).Count : index);
    }
    #endregion
}

/// <summary>
/// 레일 아이콘 토큰 — <b>이름만</b> 담는다.
/// </summary>
/// <remarks>
/// 뷰모델이 <c>PackIcon</c> 요소를 들고 있으면 창을 다시 열 때 죽은 시각 트리에 묶인 요소를 다시 붙이게 된다.
/// 그리는 것은 뷰의 <c>DataTemplate</c> 몫이다.
/// </remarks>
/// <param name="Kind">MaterialDesign 아이콘 이름.</param>
public sealed record MappingConsoleIcon(string Kind)
{
    /// <summary>접힌 레일의 툴팁이 문자열을 찾을 때를 위해.</summary>
    public override string ToString() => Kind;
}
