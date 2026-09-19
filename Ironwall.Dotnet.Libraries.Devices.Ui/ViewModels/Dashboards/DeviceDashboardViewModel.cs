using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.ByComponent;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Groups;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;

/// <summary>
/// 장비 콘솔 — 레일(그룹 + 7 카테고리 + 부품으로 찾기) · 목록 · 상세 3단.
/// </summary>
/// <remarks>
/// <para>이 뷰모델은 <b>전송 경로를 갖지 않는다.</b> 저장 · 삭제 · 재조회는 카테고리별 패널 뷰모델의 기존 경로를
/// <see cref="IDeviceConsoleSource"/> 로 감싸 그대로 부른다(device-console-redesign NFR-02 · CD-1).
/// 유일한 예외는 장비 → 그룹 끌어 놓기로, 기존에도 패널 밖(배정 다이얼로그)에서 API 를 직접 부르던 길이다.</para>
/// <para>싱글턴이다 — 창을 닫을 때 선택 · 미적용 변경 · 구독을 전부 내려놓는다.</para>
/// </remarks>
public class DeviceDashboardViewModel : BasePanelViewModel, IDevicePropertyOptions
{
    public const string ConsoleKey = "devices";
    public const string GroupsRailKey = "groups";
    public const string ByComponentRailKey = "by-component";

    #region - Ctors -
    public DeviceDashboardViewModel(IEventAggregator eventAggregator
                                , ILogService log
                                , DeviceTabControlViewModel tabControlViewModel
                                , ControllerDevicePanelViewModel controllerDevicePanelViewModel
                                , SensorDevicePanelViewModel sensorDevicePanelViewModel
                                , CameraDevicePanelViewModel cameraDevicePanelViewModel
                                , SpeakerDevicePanelViewModel speakerDevicePanelViewModel
                                , EnclosureDevicePanelViewModel enclosureDevicePanelViewModel
                                , LampDevicePanelViewModel lampDevicePanelViewModel
                                , GateDevicePanelViewModel gateDevicePanelViewModel
                                , DeviceGroupPanelViewModel deviceGroupPanelViewModel
                                , DeviceProvider deviceProvider
                                , DeviceGroupProvider deviceGroupProvider
                                , ControllerDeviceProvider controllerDeviceProvider
                                , ServerProvider serverProvider
                                , IDeviceApiService deviceApiService
                                , ICatalogService catalogService
                                ) : base(eventAggregator, log)
    {
        TabControlViewModel = tabControlViewModel;
        ControllerPanelViewModel = controllerDevicePanelViewModel;
        SensorPanelViewModel = sensorDevicePanelViewModel;
        CameraPanelViewModel = cameraDevicePanelViewModel;
        SpeakerPanelViewModel = speakerDevicePanelViewModel;
        EnclosurePanelViewModel = enclosureDevicePanelViewModel;
        LampPanelViewModel = lampDevicePanelViewModel;
        GatePanelViewModel = gateDevicePanelViewModel;
        DeviceGroupPanelViewModel = deviceGroupPanelViewModel;

        DeviceProvider = deviceProvider;
        _groupProvider = deviceGroupProvider;
        _controllerProvider = controllerDeviceProvider;
        _serverProvider = serverProvider;

        // 패널의 UpdateAction 은 기반 클래스가 아니라 패널마다 선언돼 있다 — 구독 방법을 람다로 넘긴다.
        _sources = new Dictionary<string, IDeviceConsoleSource>(StringComparer.Ordinal)
        {
            [GroupsRailKey] = new DeviceConsoleSource<DeviceGroupViewModel>(deviceGroupPanelViewModel, h => deviceGroupPanelViewModel.UpdateAction += h),
            [RailKeyOf(EnumDeviceCategory.Controller)] = new DeviceConsoleSource<ControllerDeviceViewModel>(controllerDevicePanelViewModel, h => controllerDevicePanelViewModel.UpdateAction += h),
            [RailKeyOf(EnumDeviceCategory.Sensor)] = new DeviceConsoleSource<SensorDeviceViewModel>(sensorDevicePanelViewModel, h => sensorDevicePanelViewModel.UpdateAction += h),
            [RailKeyOf(EnumDeviceCategory.Camera)] = new DeviceConsoleSource<CameraDeviceViewModel>(cameraDevicePanelViewModel, h => cameraDevicePanelViewModel.UpdateAction += h),
            [RailKeyOf(EnumDeviceCategory.Speaker)] = new DeviceConsoleSource<SpeakerDeviceViewModel>(speakerDevicePanelViewModel, h => speakerDevicePanelViewModel.UpdateAction += h),
            [RailKeyOf(EnumDeviceCategory.Enclosure)] = new DeviceConsoleSource<EnclosureDeviceViewModel>(enclosureDevicePanelViewModel, h => enclosureDevicePanelViewModel.UpdateAction += h),
            [RailKeyOf(EnumDeviceCategory.Lamp)] = new DeviceConsoleSource<LampDeviceViewModel>(lampDevicePanelViewModel, h => lampDevicePanelViewModel.UpdateAction += h),
            [RailKeyOf(EnumDeviceCategory.Gate)] = new DeviceConsoleSource<GateDeviceViewModel>(gateDevicePanelViewModel, h => gateDevicePanelViewModel.UpdateAction += h),
        };

        _typeAxis = new Dictionary<EnumDeviceCategory, TypeAxisPanelSupport>
        {
            [EnumDeviceCategory.Controller] = controllerDevicePanelViewModel.TypeAxis,
            [EnumDeviceCategory.Sensor] = sensorDevicePanelViewModel.TypeAxis,
            [EnumDeviceCategory.Camera] = cameraDevicePanelViewModel.TypeAxis,
            [EnumDeviceCategory.Speaker] = speakerDevicePanelViewModel.TypeAxis,
            [EnumDeviceCategory.Enclosure] = enclosureDevicePanelViewModel.TypeAxis,
            [EnumDeviceCategory.Lamp] = lampDevicePanelViewModel.TypeAxis,
            [EnumDeviceCategory.Gate] = gateDevicePanelViewModel.TypeAxis,
        };

        Detail = new ConsoleDetailPresenter();
        Form = new DevicePropertyFormViewModel(Detail, this);
        _deviceApiService = deviceApiService;
        _catalogService = catalogService;
        GroupDrop = new DeviceGroupDropHandler(deviceApiService, () => DeviceProvider.OfType<IBaseDeviceModel>(), log);

        RailEntries = new ObservableCollection<ConsoleRailEntry>();
        GroupChips = new ObservableCollection<DeviceGroupViewModel>();
        BuildRail();
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        foreach (var source in _sources.Values) source.BusyEnded += OnSourceBusyEnded;
        DeviceProvider.CollectionEntity.CollectionChanged += OnDevicesChanged;
        _groupProvider.CollectionEntity.CollectionChanged += OnGroupsChanged;
        GroupDrop.Completed += OnGroupDropCompleted;
        Detail.Guard.Blocked += OnNavigationBlocked;

        BuildRail();            // 계약 세대가 바뀌었을 수 있다(부품으로 찾기는 축 계약에서만).
        RefreshRailCounts();
        RefreshGroupChips();

        await TabControlViewModel.ActivateAsync();
        await SwitchRailAsync(_railKey ?? GroupsRailKey, force: true);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await base.OnDeactivateAsync(close, cancellationToken);

        foreach (var source in _sources.Values) source.BusyEnded -= OnSourceBusyEnded;
        DeviceProvider.CollectionEntity.CollectionChanged -= OnDevicesChanged;
        _groupProvider.CollectionEntity.CollectionChanged -= OnGroupsChanged;
        GroupDrop.Completed -= OnGroupDropCompleted;
        Detail.Guard.Blocked -= OnNavigationBlocked;
        DetachRows();

        // 싱글턴 — 다음에 열 때 옛 선택 · 미적용 변경 · Draft 가 남아 있으면 안 된다.
        _pending = null;
        _draft = null;
        _lastGroupUndo = null;
        Form.Clear();
        Detail.Reset();
        SearchText = string.Empty;
        StatusText = string.Empty;
        NotifyOfPropertyChange(nameof(CanUndoGroupDrop));

        TabControlViewModel.Items.Clear();
        await TabControlViewModel.DeactivateItemAsync(TabControlViewModel.ActiveItem, true);
        await TabControlViewModel.DeactivateAsync(true);
    }
    #endregion

    #region - Rail -
    /// <summary>레일에서 항목을 골랐다(뷰가 부른다). 막혔으면 false — 뷰는 레일 선택을 <see cref="SelectedRail"/> 로 되돌린다.</summary>
    public async Task<bool> SelectRailAsync(string key)
    {
        if (string.Equals(key, _railKey, StringComparison.Ordinal)) return true;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SwitchRail)) { NotifyOfPropertyChange(nameof(SelectedRail)); return false; }

        await SwitchRailAsync(key, force: false);
        return true;
    }

    public ConsoleRailEntry? SelectedRail
    {
        get => RailEntries.FirstOrDefault(e => e.Key == _railKey);
        set
        {
            if (value is null || value.Key == _railKey) return;
            _ = SelectRailAsync(value.Key);
        }
    }

    private async Task SwitchRailAsync(string key, bool force)
    {
        if (!RailEntries.Any(e => e.Key == key)) key = GroupsRailKey;
        if (!force && key == _railKey) return;

        try
        {
            DetachRows();
            _draft = null;
            _pending = null;
            Form.Clear();
            Detail.Reset();

            // 패널의 활성화 수명주기는 그대로 — 활성화가 목록을 채우고 권한 · 종류 축을 준비한다.
            if (TabControlViewModel.ActiveItem is not null)
                await TabControlViewModel.DeactivateItemAsync(TabControlViewModel.ActiveItem, true);

            _railKey = key;
            _current = _sources.TryGetValue(key, out var source) ? source : null;
            Category = CategoryOfRail(key);

            if (_current is not null)
            {
                await TabControlViewModel.ActivateItemAsync(_current.Panel);
                AttachRows(_current);
            }
            else if (key == ByComponentRailKey)
            {
                // 늦게 만든다 — 이 뷰모델은 계약 판정 전에 만들어질 수 있고, 조회 뷰모델은 만들 때의 계약으로 굳는다.
                if (ByComponent is null || !ByComponent.IsAvailable)
                {
                    ByComponent = new ByComponentViewModel(_deviceApiService, _catalogService, _log);
                    NotifyOfPropertyChange(nameof(ByComponent));
                }
            }

            Detail.TypeName = RailEntries.First(e => e.Key == key).Label;
            Columns = key == GroupsRailKey ? DeviceColumnCatalog.ForGroups()
                    : Category is { } category ? DeviceColumnCatalog.For(category, ContractGate.IsAxisUi)
                    : Array.Empty<DeviceColumnSpec>();
        }
        catch (Exception ex)
        {
            _log?.Error($"[DeviceConsole] 레일 전환 실패({key}) — {ex.Message}");
        }

        NotifyOfPropertyChange(nameof(SelectedRail));
        NotifyOfPropertyChange(nameof(IsByComponent));
        NotifyOfPropertyChange(nameof(IsListVisible));
        NotifyOfPropertyChange(nameof(IsGroupsRail));
        NotifyOfPropertyChange(nameof(CanDragToGroup));
        RefreshToolbar();
        RefreshStatus();
    }

    private void BuildRail()
    {
        var wanted = new List<ConsoleRailEntry> { new(GroupsRailKey, "그룹", "FolderMultipleOutline") { ShowCount = true } };
        foreach (var category in DeviceRailCounter.RailOrder)
            wanted.Add(new ConsoleRailEntry(RailKeyOf(category), CategoryLabel(category), CategoryIcon(category)) { ShowCount = true, Tag = category, HasSeparatorAbove = category == DeviceRailCounter.RailOrder[0] });

        // 부품으로 찾기는 축 계약(7.0+)에서만 있는 조회다 — 6.3 에서는 항목 자체를 내지 않는다.
        if (ContractGate.IsAxisUi)
            wanted.Add(new ConsoleRailEntry(ByComponentRailKey, "부품으로 찾기", "Magnify") { HasSeparatorAbove = true, ShowCount = false });

        if (RailEntries.Select(e => e.Key).SequenceEqual(wanted.Select(e => e.Key))) return;

        RailEntries.Clear();
        foreach (var entry in wanted) RailEntries.Add(entry);
    }

    private void RefreshRailCounts()
    {
        var counts = DeviceRailCounter.Count(DeviceProvider.OfType<IBaseDeviceModel>());
        foreach (var count in counts)
        {
            var entry = RailEntries.FirstOrDefault(e => e.Key == RailKeyOf(count.Category));
            if (entry is null) continue;
            entry.Count = count.Total;
            entry.BadCount = count.Fault;
        }

        var groups = RailEntries.FirstOrDefault(e => e.Key == GroupsRailKey);
        if (groups is not null) groups.Count = _groupProvider.CollectionEntity.Count;

        var (total, fault) = DeviceRailCounter.Totals(counts);
        RailFooterText = $"전체 {total}대 / 장애 {fault}대";
    }

    private void RefreshGroupChips()
    {
        GroupChips.Clear();
        foreach (var group in _groupProvider.CollectionEntity.Where(g => g.Id > 0).OrderBy(g => g.Name, StringComparer.CurrentCulture))
            GroupChips.Add(new DeviceGroupViewModel(group));
        NotifyOfPropertyChange(nameof(HasGroupChips));
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshRailCounts();

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRailCounts();
        RefreshGroupChips();
    }
    #endregion

    #region - List -
    /// <summary>그리드의 선택이 바뀌었다(뷰가 부른다). 미적용 변경이 있으면 막고 false — 뷰는 선택을 <see cref="Form"/> 의 행으로 되돌린다.</summary>
    public bool OnRowsSelected(IList selected)
    {
        var rows = selected?.Cast<object>().ToList() ?? new List<object>();
        if (SameRows(rows, Form.Rows) && !Detail.IsCreating) return true;

        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return false;

        _draft = null;
        _current?.Select(rows);
        LoadForm(rows, isCreating: false);
        RefreshToolbar();
        RefreshStatus();
        return true;
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            var next = value ?? string.Empty;
            if (_searchText == next) return;
            _searchText = next;
            NotifyOfPropertyChange();
            Rows?.Refresh();
            RefreshStatus();
        }
    }

    private void AttachRows(IDeviceConsoleSource source)
    {
        var view = new ListCollectionView((IList)source.Rows) { Filter = MatchesSearch };
        Rows = view;
        _rowsChanged = source.RowsChanged;
        _rowsChanged.CollectionChanged += OnRowsChanged;
    }

    private void DetachRows()
    {
        if (_rowsChanged is not null) _rowsChanged.CollectionChanged -= OnRowsChanged;
        _rowsChanged = null;
        Rows = null;
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshStatus();

    private bool MatchesSearch(object row)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        var needle = _searchText.Trim();
        return Contains(RowText(row, "DeviceNumber"), needle) || Contains(RowText(row, "DeviceName"), needle) || Contains(RowText(row, "Name"), needle);

        static bool Contains(string? text, string needle) => text?.Contains(needle, StringComparison.CurrentCultureIgnoreCase) == true;
    }
    #endregion

    #region - Toolbar -
    public void Add()
    {
        if (_current is null || !CanAdd) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;

        // Draft 는 목록에서 뗀 채로 폼에만 물린다 — [등록] 전에는 목록에 아무것도 남지 않는다(FR-12).
        var draft = _current.CreateDraft();
        if (draft is null)
        {
            StatusText = "지금은 추가할 수 없다 — 처리 중이거나 권한이 없다";
            return;
        }

        _draft = draft;
        _current.Select(Array.Empty<object>());
        ClearGridSelectionRequested?.Invoke(this, EventArgs.Empty);
        LoadForm(new[] { draft }, isCreating: true);
        RefreshToolbar();
    }

    public void Delete()
    {
        if (_current is null || !CanDelete) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return;

        // 패널의 삭제는 확인 팝업을 띄우고, 확인되면 지운 뒤 UpdateAction 을 울린다 — 그때 BusyEnded 로 돌아온다.
        _pending = new PendingOperation(PendingKind.Delete, Form.Rows.Count, 0, Array.Empty<int>(), Array.Empty<(DevicePropertySpec, string)>());
        _current.Delete();
    }

    public void Reload()
    {
        if (_current is null)
        {
            if (IsByComponent && ByComponent is not null) _ = ByComponent.SearchAsync();
            return;
        }
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;

        _pending = new PendingOperation(PendingKind.Reload, 0, 0, RowIds(Form.Rows), Array.Empty<(DevicePropertySpec, string)>());
        _current.Reload();
        RefreshToolbar();
    }

    public bool CanAdd => _current is not null && !_current.IsBusy && DevicePermissionGate.CanEdit();
    public string? AddBlockedReason => _current is null ? "이 화면에서는 추가할 수 없습니다." : !DevicePermissionGate.CanEdit() ? "권한이 없습니다." : _current.IsBusy ? "처리 중입니다." : null;

    public bool CanDelete => _current is not null && !_current.IsBusy && Form.Rows.Count > 0 && !Detail.IsCreating && DevicePermissionGate.CanDelete();
    public string? DeleteBlockedReason => _current is null ? "이 화면에서는 삭제할 수 없습니다."
        : !DevicePermissionGate.CanDelete() ? "권한이 없습니다."
        : Form.Rows.Count == 0 || Detail.IsCreating ? "삭제할 항목을 먼저 고르세요."
        : _current.IsBusy ? "처리 중입니다." : null;

    public bool CanReload => _current is null ? IsByComponent : !_current.IsBusy;

    private void RefreshToolbar()
    {
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(AddBlockedReason));
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(DeleteBlockedReason));
        NotifyOfPropertyChange(nameof(CanReload));
    }
    #endregion

    #region - Detail -
    /// <summary>[적용] · [등록].</summary>
    public void Apply()
    {
        if (_current is null || _current.IsBusy) return;

        var creating = Detail.IsCreating;
        var commit = Form.Commit();
        if (!commit.IsWritten)
        {
            Detail.LastMessage = commit.Message;
            return;
        }

        // 저장이 끝난 뒤 서버 값과 맞춰 보려고 무엇을 썼는지 적어 둔다 — 패널의 저장은 성공 여부를 돌려주지 않는다.
        var written = Form.Fields.Where(f => f.IsTouched && !f.IsLocked).Select(f => (f.Spec, f.Text)).ToList();

        if (creating && _draft is not null) _current.AdoptDraft(_draft);

        _pending = new PendingOperation(creating ? PendingKind.Create : PendingKind.Update, commit.RowCount, commit.FieldCount, RowIds(Form.Rows), written);
        Detail.Settle(creating ? "등록하는 중…" : "적용하는 중…");
        _current.Save();
        RefreshToolbar();
    }

    /// <summary>[되돌리기] · [취소] — 서버 호출 0.</summary>
    public void Revert()
    {
        if (Detail.IsCreating)
        {
            _draft = null;
            Form.Clear();
            Detail.Reset();
            Detail.TypeName = SelectedRail?.Label ?? string.Empty;
            RefreshToolbar();
            return;
        }

        Form.Revert();
        Detail.Settle("되돌렸다");
    }

    private void LoadForm(IReadOnlyList<object> rows, bool isCreating)
    {
        var readOnly = !DevicePermissionGate.CanEdit();

        if (_railKey == GroupsRailKey)
            Form.Load(rows, DeviceGroupPropertySpecs.All, default, isCreating, readOnly);
        else if (Category is { } category)
            Form.Load(rows, category, ContractGate.IsAxisUi, isCreating, readOnly);
        else
            Form.Clear();

        Detail.IsCreating = isCreating;
        Detail.IsReadOnly = readOnly;
        Detail.SelectedCount = isCreating ? 0 : rows.Count;
        Detail.SingleTitle = rows.Count == 1 ? RowText(rows[0], "DeviceName") ?? RowText(rows[0], "Name") ?? string.Empty : string.Empty;
        Detail.SingleNumber = rows.Count == 1 ? RowText(rows[0], "DeviceNumber") ?? string.Empty : string.Empty;
        Detail.CreateBanner = $"새 {Detail.TypeName} — 필수 칸을 채우고 [등록] 을 누르면 그때 서버에 만든다.";
        Detail.LastMessage = null;
    }

    private void OnNavigationBlocked(object? sender, ConsoleNavigation navigation)
        => SelectionRestoreRequested?.Invoke(this, Form.Rows);

    private void OnSourceBusyEnded(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _current)) return;

        RefreshRailCounts();
        RefreshToolbar();

        var pending = _pending;
        _pending = null;
        if (pending is null) { RefreshStatus(); return; }

        var rows = _current!.Rows.Cast<object>().ToList();

        switch (pending.Kind)
        {
            case PendingKind.Create:
            {
                // 패널은 실패한 Draft(Id≤0)를 목록에 남긴다 — 남아 있으면 등록되지 않은 것이다.
                var leftover = _draft is not null && rows.Contains(_draft) && RowId(_draft) <= 0;
                if (leftover)
                {
                    LoadForm(new[] { _draft! }, isCreating: true);
                    Detail.LastMessage = "등록되지 않았다 — 안내를 확인하고 다시 [등록] 한다";
                    break;
                }

                _draft = null;
                var created = rows.Where(r => RowId(r) > 0).OrderByDescending(RowId).FirstOrDefault();
                Reselect(created is null ? Array.Empty<object>() : new[] { created }, "등록했다");
                break;
            }

            case PendingKind.Update:
            {
                var again = rows.Where(r => pending.RowIds.Contains(RowId(r))).ToList();
                var mismatch = again.Count == 0 ? null : pending.Written
                    .Where(w => again.Any(r => !string.Equals(DevicePropertyAccessor.ReadText(r, w.Spec), w.Text, StringComparison.Ordinal)))
                    .Select(w => w.Spec.Label)
                    .FirstOrDefault();

                Reselect(again, mismatch is null
                    ? ConsoleDetailStateMachine.AppliedMessage(pending.RowCount, pending.FieldCount)
                    : $"'{mismatch}' 이(가) 서버 값과 다르다 — 저장이 거절됐을 수 있다. 안내를 확인한다");
                break;
            }

            case PendingKind.Delete:
                Reselect(rows.Where(r => pending.RowIds.Contains(RowId(r))).ToList(), null);
                break;

            default:
                Reselect(rows.Where(r => pending.RowIds.Contains(RowId(r))).ToList(), "갱신했다");
                break;
        }

        RefreshStatus();
    }

    /// <summary>재조회로 행 인스턴스가 바뀐다 — 같은 Id 의 새 행을 다시 고른다.</summary>
    private void Reselect(IReadOnlyList<object> rows, string? message)
    {
        _current?.Select(rows);
        LoadForm(rows, isCreating: false);
        if (message is not null) Detail.LastMessage = message;
        SelectionRestoreRequested?.Invoke(this, rows);
        RefreshToolbar();
    }
    #endregion

    #region - Group drop -
    /// <summary>키보드 폴백 — 고른 장비를 그룹에 넣는다(끌어 놓기와 같은 경로).</summary>
    public async Task AssignSelectionToGroupAsync(DeviceGroupViewModel group)
    {
        if (group is null || !CanDragToGroup) return;
        await GroupDrop.AssignAsync(group.Id, group.Name, DeviceGroupDropHandler.ModelsOf(Form.Rows));
    }

    public async Task UndoGroupDropAsync()
    {
        if (_lastGroupUndo is null) return;
        await GroupDrop.UndoAsync(_lastGroupUndo);
    }

    public bool CanUndoGroupDrop => _lastGroupUndo is not null;

    /// <summary>장비 목록에서만 그룹으로 끌 수 있다(그룹 목록 · 부품으로 찾기에서는 핸들을 내지 않는다).</summary>
    public bool CanDragToGroup => _current is not null && _railKey != GroupsRailKey && DevicePermissionGate.CanEdit();

    private void OnGroupDropCompleted(string line, GroupDropUndo? undo)
    {
        _lastGroupUndo = undo;
        StatusText = line;
        NotifyOfPropertyChange(nameof(CanUndoGroupDrop));

        // 그룹 칸은 읽기 전용 표시다 — 손댄 칸이 없을 때만 폼을 다시 읽는다(미적용 변경을 덮지 않는다).
        if (!Detail.Tracker.IsDirty && !Detail.IsCreating && Form.Rows.Count > 0) LoadForm(Form.Rows, isCreating: false);
    }
    #endregion

    #region - IDevicePropertyOptions -
    public IReadOnlyList<PropertyOption> OptionsFor(DevicePropertySpec spec, EnumDeviceCategory category)
    {
        switch (spec.OptionSource)
        {
            case DevicePropertyOptionSource.TypeAxis:
                return _typeAxis.TryGetValue(category, out var axis)
                    ? axis.Options.Select(o => new PropertyOption(o.Display, o.Code)).ToList()
                    : Array.Empty<PropertyOption>();

            case DevicePropertyOptionSource.ExtraAxis:
                return _typeAxis.TryGetValue(category, out var extra)
                    ? extra.ExtraAxes.Where(a => spec.VocabularyName is null || a.Field == spec.VocabularyName || a.Field == spec.ApiPath)
                           .SelectMany(a => a.Values)
                           .Select(o => new PropertyOption(o.Display, o.Code))
                           .ToList()
                    : Array.Empty<PropertyOption>();

            case DevicePropertyOptionSource.Controllers:
                return _controllerProvider.CollectionEntity
                    .Select(c => new PropertyOption($"{c.DeviceName} (#{c.DeviceNumber})", null, c))
                    .ToList();

            case DevicePropertyOptionSource.Servers:
                return _serverProvider.CollectionEntity
                    .Select(s => new PropertyOption(s.Name ?? $"서버 {s.Id}", null, s))
                    .ToList();

            default:
                return Array.Empty<PropertyOption>();
        }
    }
    #endregion

    #region - Contract -
    /// <summary>
    /// 서버 계약 게이트(device-console-v8 FR-07) — 축 UI 표시 여부와 "판본 미확정" 배너.
    /// </summary>
    public DeviceContractGateViewModel ContractGate { get; } = new();

    /// <summary>배너의 [다시 확인] — 판본을 재확인하고, 세대가 바뀌었으면 장비를 그 계약으로 다시 읽는다.</summary>
    public async void OnClickRefreshContract()
    {
        var wasAxis = ContractGate.IsAxisUi;
        var ok = await ContractGate.RefreshContractAsync();
        if (!ok || ContractGate.IsAxisUi == wasAxis) return;

        try
        {
            // 6.3 쿼리로 읽어 둔 캐시는 새 계약에서 틀린 모양이다 — 전량 재조회.
            var provider = IoC.Get<IDeviceProviderService>();
            if (provider != null) await provider.FetchAllDevicesAsync();

            BuildRail();
            await SwitchRailAsync(_railKey ?? GroupsRailKey, force: true);
        }
        catch (Exception ex)
        {
            _log?.Error($"[DeviceDashboard] 판본 전환 후 재조회 실패 — {ex.Message}");
        }
    }
    #endregion

    #region - Status -
    private void RefreshStatus()
    {
        var shown = (Rows as ListCollectionView)?.Count ?? 0;
        var total = _current?.RowCount ?? 0;
        // 부품으로 찾기는 제 화면 안에 결과 줄이 있다 — 같은 말을 두 번 하지 않는다.
        ListStatusText = IsByComponent ? string.Empty
            : shown == total ? $"목록 {total}건 · 선택 {Form.Rows.Count}"
            : $"목록 {shown}건(전체 {total}) · 선택 {Form.Rows.Count}";
    }
    #endregion

    #region - Helpers -
    public static string RailKeyOf(EnumDeviceCategory category) => category.ToString().ToLowerInvariant();

    private EnumDeviceCategory? CategoryOfRail(string key)
        => DeviceRailCounter.RailOrder.Cast<EnumDeviceCategory?>().FirstOrDefault(c => RailKeyOf(c!.Value) == key);

    private static string CategoryLabel(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => "제어기",
        EnumDeviceCategory.Sensor => "센서",
        EnumDeviceCategory.Camera => "카메라",
        EnumDeviceCategory.Speaker => "스피커",
        EnumDeviceCategory.Enclosure => "함체",
        EnumDeviceCategory.Lamp => "경광등",
        EnumDeviceCategory.Gate => "통문",
        _ => category.ToString(),
    };

    // MDIX PackIconKind 이름 — 뷰가 문자열을 PackIcon 으로 바꾼다. 없는 이름이면 아이콘만 빈다(레일은 그대로 동작).
    private static string CategoryIcon(EnumDeviceCategory category) => category switch
    {
        EnumDeviceCategory.Controller => "Chip",
        EnumDeviceCategory.Sensor => "Radar",
        EnumDeviceCategory.Camera => "Cctv",
        EnumDeviceCategory.Speaker => "Bullhorn",
        EnumDeviceCategory.Enclosure => "ServerNetwork",
        EnumDeviceCategory.Lamp => "AlarmLight",
        EnumDeviceCategory.Gate => "Gate",
        _ => "Devices",
    };

    private static bool SameRows(IReadOnlyList<object> a, IReadOnlyList<object> b)
        => a.Count == b.Count && !a.Except(b).Any();

    private static IReadOnlyList<int> RowIds(IEnumerable<object> rows) => rows.Select(RowId).Where(id => id > 0).ToList();

    private static int RowId(object row)
    {
        var type = row.GetType();
        if (type.GetProperty("Id")?.GetValue(row) is int id) return id;
        return type.GetProperty("Model")?.GetValue(row) is IBaseDeviceModel model ? model.Id : 0;
    }

    private static string? RowText(object row, string property)
        => row.GetType().GetProperty(property)?.GetValue(row)?.ToString();
    #endregion

    #region - Properties -
    public ObservableCollection<ConsoleRailEntry> RailEntries { get; }
    public ObservableCollection<DeviceGroupViewModel> GroupChips { get; }
    public bool HasGroupChips => GroupChips.Count > 0;

    public ConsoleDetailPresenter Detail { get; }
    public DevicePropertyFormViewModel Form { get; }
    public ByComponentViewModel? ByComponent { get; private set; }
    public DeviceGroupDropHandler GroupDrop { get; }

    public EnumDeviceCategory? Category { get; private set; }
    public bool IsByComponent => _railKey == ByComponentRailKey;
    public bool IsGroupsRail => _railKey == GroupsRailKey;
    public bool IsListVisible => !IsByComponent;

    public ICollectionView? Rows
    {
        get => _rows;
        private set { _rows = value; NotifyOfPropertyChange(); }
    }

    /// <summary>지금 목록의 열 명세 — 바뀌면 뷰가 그리드의 열을 다시 만든다.</summary>
    public IReadOnlyList<DeviceColumnSpec> Columns
    {
        get => _columns;
        private set { _columns = value; NotifyOfPropertyChange(); }
    }

    public string RailFooterText
    {
        get => _railFooterText;
        private set { _railFooterText = value; NotifyOfPropertyChange(); }
    }

    public string ListStatusText
    {
        get => _listStatusText;
        private set { _listStatusText = value; NotifyOfPropertyChange(); }
    }

    /// <summary>상태 띠의 한 줄 소식(그룹 넣기 결과 등).</summary>
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>그리드의 선택을 이 행들로 맞춰 달라(막힌 이동의 원복 · 재조회 뒤 재선택).</summary>
    public event EventHandler<IReadOnlyList<object>>? SelectionRestoreRequested;

    /// <summary>그리드의 선택을 비워 달라([추가] 로 새 등록에 들어갈 때).</summary>
    public event EventHandler? ClearGridSelectionRequested;

    public DeviceProvider DeviceProvider { get; }
    public DeviceTabControlViewModel TabControlViewModel { get; }
    public ControllerDevicePanelViewModel ControllerPanelViewModel { get; }
    public SensorDevicePanelViewModel SensorPanelViewModel { get; }
    public CameraDevicePanelViewModel CameraPanelViewModel { get; }
    public SpeakerDevicePanelViewModel SpeakerPanelViewModel { get; }
    public EnclosureDevicePanelViewModel EnclosurePanelViewModel { get; }
    public LampDevicePanelViewModel LampPanelViewModel { get; }
    public GateDevicePanelViewModel GatePanelViewModel { get; }
    public DeviceGroupPanelViewModel DeviceGroupPanelViewModel { get; }
    #endregion

    #region - Attributes -
    private enum PendingKind { Create, Update, Delete, Reload }

    private sealed record PendingOperation(PendingKind Kind, int RowCount, int FieldCount, IReadOnlyList<int> RowIds, IReadOnlyList<(DevicePropertySpec Spec, string Text)> Written);

    private readonly Dictionary<string, IDeviceConsoleSource> _sources;
    private readonly Dictionary<EnumDeviceCategory, TypeAxisPanelSupport> _typeAxis;
    private readonly DeviceGroupProvider _groupProvider;
    private readonly ControllerDeviceProvider _controllerProvider;
    private readonly ServerProvider _serverProvider;
    private readonly IDeviceApiService _deviceApiService;
    private readonly ICatalogService _catalogService;

    private IDeviceConsoleSource? _current;
    private INotifyCollectionChanged? _rowsChanged;
    private ICollectionView? _rows;
    private IReadOnlyList<DeviceColumnSpec> _columns = Array.Empty<DeviceColumnSpec>();
    private string? _railKey;
    private object? _draft;
    private PendingOperation? _pending;
    private GroupDropUndo? _lastGroupUndo;
    private string _searchText = string.Empty;
    private string _railFooterText = string.Empty;
    private string _listStatusText = string.Empty;
    private string _statusText = string.Empty;
    #endregion
}
