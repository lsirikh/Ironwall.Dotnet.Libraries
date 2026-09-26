using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Servers;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
/****************************************************************************
   Purpose      : 서버 모니터 콘솔 — 레일 · 목록 + 지표 띠 · 상세 (N-12)
   Created By   : GHLee
   Created On   : 9/20/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>배정 트레이의 칩 하나 — 끌어서 서버 행에 놓는다.</summary>
public sealed class ServerAssignCandidateViewModel : PropertyChangedBase
{
    public ServerAssignCandidateViewModel(IBaseDeviceModel model, string categoryLabel, string serverText)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Name = string.IsNullOrWhiteSpace(model.DeviceName) ? $"장비 {model.Id}" : model.DeviceName!;
        CategoryLabel = categoryLabel;
        ServerText = serverText;
    }

    public IBaseDeviceModel Model { get; }
    public int Id => Model.Id;
    public string Name { get; }
    public string CategoryLabel { get; }
    public string ServerText { get; }
    public override string ToString() => Name;
}

/// <summary>
/// 서버 모니터 — 레일(유형별 + 시스템 이벤트) · 목록 + 지표 띠 · 상세 340.
/// </summary>
/// <remarks>
/// <para>정본: <c>window-layout-system-storyboard.html</c> L1330-1365 · 드래그 와이어프레임 L368-372 · L432.</para>
/// <para><b>이 콘솔은 상태를 쓰지 않는다.</b> 상태는 관측 값이고 보고 입구는 서버 매니저의 것이다.</para>
/// <para>싱글턴이다 — 닫을 때 선택 · 미적용 변경 · 구독을 전부 내려놓고, <b>돌고 있던 상세 적재를 취소</b>한다.</para>
/// </remarks>
public class ServerMonitorViewModel : Screen
{
    public const string ConsoleKey = "Servers";

    #region - Ctors -
    public ServerMonitorViewModel(
        IEventAggregator eventAggregator,
        ILogService log,
        IServerConsoleService service,
        DeviceProvider deviceProvider,
        IClock clock,
        Lazy<IServerConsoleDialogs>? dialogs = null)
    {
        _events = eventAggregator;
        _log = log;
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _devices = deviceProvider ?? throw new ArgumentNullException(nameof(deviceProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _dialogs = dialogs;

        DisplayName = "서버";
        Detail = new ConsoleDetailPresenter { TypeName = "서버" };
        Tray = new DraftTrayViewModel();
        Assign = new ServerAssignHandler(_service, () => _devices.OfType<IBaseDeviceModel>(), Tray, DialogsOrNull(), log);
        Assign.DragProbeRequested += OnDragProbe;

        RailEntries = new ObservableCollection<ConsoleRailEntry>();
        Rows = new ObservableCollection<ServerRowViewModel>();
        MetricCells = new ObservableCollection<ServerMetricCell>();
        AssignCandidates = new ObservableCollection<ServerAssignCandidateViewModel>();
        UnitOptions = new ObservableCollection<ServerUnitOption>();
        CategoryOptions = new ObservableCollection<ServerCategoryOption>();

        BuildRail();
        Columns = ServerColumnCatalog.For(_service.IsUnitEra);
    }
    #endregion

    #region - Lifecycle -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken).ConfigureAwait(true);

        _devices.CollectionEntity.CollectionChanged += OnDevicesChanged;
        Assign.Completed += OnAssignCompleted;
        Detail.Guard.Blocked += OnNavigationBlocked;

        Columns = ServerColumnCatalog.For(_service.IsUnitEra);
        foreach (var name in new[] { nameof(Columns), nameof(IsUnitEra), nameof(IsAxisEra), nameof(LastChangeNote) })
            NotifyOfPropertyChange(name);

        RefreshCandidates();
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        _devices.CollectionEntity.CollectionChanged -= OnDevicesChanged;
        Assign.Completed -= OnAssignCompleted;
        Detail.Guard.Blocked -= OnNavigationBlocked;

        // 돌고 있던 상세 적재를 끊는다 — 닫힌 싱글턴에 늦은 응답이 들어차면 다음에 열 때 남의 값이 보인다.
        CancelDetailLoad();
        _generation++;

        Rows.Clear();
        MetricCells.Clear();
        AssignCandidates.Clear();
        Tray.Revert();
        _all = Array.Empty<ServerRowViewModel>();
        _selected = new List<ServerRowViewModel>();
        _detail = null;
        _intent = new ServerWriteIntent();
        _lastUndo = null;
        IsEditing = false;
        SearchText = string.Empty;
        StatusText = string.Empty;
        Detail.Reset();
        NotifyOfPropertyChange(nameof(CanUndoAssign));
        NotifyOfPropertyChange(nameof(HasDetail));

        await base.OnDeactivateAsync(close, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>머리 ✕(U-18) — 보고서 · 조치 문구 콘솔과 같은 닫기: 호스트 컨덕터가 받아 패널을 내린다.</summary>
    public Task Close() => TryCloseAsync();
    #endregion

    #region - Rail -
    public ObservableCollection<ConsoleRailEntry> RailEntries { get; }

    public ConsoleRailEntry? SelectedRail
    {
        get => RailEntries.FirstOrDefault(e => e.Key == _railKey);
        set
        {
            if (value is null || value.Key == _railKey) return;
            SelectRail(value.Key);
        }
    }

    /// <summary>레일에서 칸을 골랐다. 미적용 변경이 있으면 막고 false.</summary>
    public bool SelectRail(string key)
    {
        if (string.Equals(key, _railKey, StringComparison.Ordinal)) return true;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SwitchRail))
        {
            NotifyOfPropertyChange(nameof(SelectedRail));
            return false;
        }

        _railKey = ServerTypeCatalog.SpecOf(key) is null ? ServerTypeCatalog.AllKey : key;
        ClearSelection();
        ApplyFilter();

        foreach (var name in new[] { nameof(SelectedRail), nameof(IsServerList), nameof(IsSystemEvents), nameof(CanAdd) })
            NotifyOfPropertyChange(name);
        RefreshStatus();
        return true;
    }

    public string RailFooterText
    {
        get => _railFooterText;
        private set { _railFooterText = value; NotifyOfPropertyChange(); }
    }

    private void BuildRail()
    {
        RailEntries.Clear();
        foreach (var spec in ServerTypeCatalog.RailOrder)
            RailEntries.Add(new ConsoleRailEntry(spec.Key, spec.Label, new ConsoleIconToken(spec.Icon))
            {
                ShowCount = spec.ShowCount,
                HasSeparatorAbove = spec.HasSeparatorAbove,
            });
    }

    private void RefreshRailCounts()
    {
        var counts = ServerRailCounter.Count(_all);
        foreach (var count in counts)
        {
            var entry = RailEntries.FirstOrDefault(e => e.Key == count.Key);
            if (entry is null) continue;
            entry.Count = count.Total;
            entry.BadCount = count.Fault;
        }
        RailFooterText = ServerRailCounter.FooterText(counts);
    }
    #endregion

    #region - List -
    public ObservableCollection<ServerRowViewModel> Rows { get; }

    public IReadOnlyList<ServerColumnSpec> Columns { get; private set; }

    public bool IsServerList => ServerTypeCatalog.IsServerList(_railKey);
    public bool IsSystemEvents => !IsServerList;

    /// <summary>부대 편제(8.0 이상)를 쓸 수 있는가.</summary>
    public bool IsUnitEra => _service.IsUnitEra;

    /// <summary>축 계약(7.0 이상)인가 — 모드 절 · 해제 · 전 카테고리 배정의 게이트.</summary>
    public bool IsAxisEra => _service.IsAxisEra;

    /// <summary>"마지막 변화" 칸의 설명 — 6.3 에는 전이 시각이 없다는 사실을 화면이 말한다.</summary>
    public string LastChangeNote => IsAxisEra ? string.Empty : ServerStatusRules.NoTransitionClockNote;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value ?? string.Empty;
            NotifyOfPropertyChange();
            Detail.Guard.TryNavigate(ConsoleNavigation.Search);   // 검색은 막지 않는다 — 선택을 바꾸지 않는다
            ApplyFilter();
            RefreshStatus();
        }
    }

    public ObservableCollection<ServerUnitOption> UnitOptions { get; }

    /// <summary>
    /// 부대 필터. 미적용 변경이 있으면 <b>되돌린다</b> — 콤보만 바뀌고 목록은 그대로인 상태를 만들지 않는다.
    /// </summary>
    public ServerUnitOption? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            if (ReferenceEquals(_selectedUnit, value)) return;
            if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) { NotifyOfPropertyChange(); return; }

            _selectedUnit = value;
            NotifyOfPropertyChange();
            _ = ReloadAsync(CancellationToken.None);
        }
    }

    /// <summary>예하 부대까지 포함해 조회한다(부대 필터와 <b>함께만</b> 뜻이 있다).</summary>
    public bool IncludeDescendants
    {
        get => _includeDescendants;
        set
        {
            if (_includeDescendants == value) return;
            if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) { NotifyOfPropertyChange(); return; }

            _includeDescendants = value;
            NotifyOfPropertyChange();
            if (_selectedUnit is not null) _ = ReloadAsync(CancellationToken.None);
        }
    }

    public string ListStatusText
    {
        get => _listStatusText;
        private set { _listStatusText = value; NotifyOfPropertyChange(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>
    /// "시스템 이벤트" 칸의 안내. 그 칸과 상세 절은 지금 <b>내지 않는다</b>(서버 API 에 입구가 없다 — U-18 감사 D-7 7.1).
    /// 입구가 생겨 다시 켤 때 쓸 운영자 문장만 남긴다. 생존 신호 안내(REST · NATS 설계 메모)는 화면에서 뺐다(7.2).
    /// </summary>
    public string SystemEventsNote => "시스템 이벤트를 아직 제공하지 않습니다.";

    private void ApplyFilter()
    {
        var needle = _searchText.Trim();
        var filtered = _all
            .Where(row => ServerTypeCatalog.Matches(_railKey, row.Type))
            .Where(row => needle.Length == 0
                          || row.Name.Contains(needle, StringComparison.CurrentCultureIgnoreCase)
                          || row.AddressText.Contains(needle, StringComparison.OrdinalIgnoreCase)
                          || row.TypeText.Contains(needle, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        // Clear() + Add() 를 쓰지 않는다 — 묶인 목록의 선택과 스크롤이 통째로 날아간다.
        for (var i = Rows.Count - 1; i >= 0; i--)
            if (!filtered.Contains(Rows[i])) Rows.RemoveAt(i);

        for (var i = 0; i < filtered.Count; i++)
        {
            var index = Rows.IndexOf(filtered[i]);
            if (index < 0) Rows.Insert(i, filtered[i]);
            else if (index != i) Rows.Move(index, i);
        }
    }

    private void RefreshStatus()
    {
        if (IsSystemEvents) { ListStatusText = "시스템 이벤트"; return; }

        var shown = Rows.Count;
        var fault = Rows.Count(r => r.IsFault);
        var silent = Rows.Count(r => r.IsNotReported);
        ListStatusText = $"{shown}대 · 장애 {fault} · 보고 없음 {silent}";
    }
    #endregion

    #region - Load -
    public bool CanReload => !_isBusy;

    public void Reload() => _ = ReloadAsync(CancellationToken.None);

    public async Task ReloadAsync(CancellationToken token)
    {
        if (_isBusy) return;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;

        _isBusy = true;
        NotifyOfPropertyChange(nameof(CanReload));
        try { await LoadCoreAsync(token).ConfigureAwait(true); }
        finally
        {
            _isBusy = false;
            NotifyOfPropertyChange(nameof(CanReload));
        }
    }

    private async Task LoadCoreAsync(CancellationToken token)
    {
        try
        {
            var result = await _service.LoadAsync(
                IsUnitEra ? _selectedUnit?.Id : null, _includeDescendants, token).ConfigureAwait(true);

            var unitNames = result.Units.ToDictionary(u => u.Id, u => u.Name);
            _all = result.Servers
                .Select(view => new ServerRowViewModel(view, _service.Contract, _clock,
                    view.UnitId is { } id && unitNames.TryGetValue(id, out var name) ? name : null))
                .ToList();

            SyncOptions(UnitOptions, result.Units);
            SyncOptions(CategoryOptions, result.Categories);

            ClearSelection();
            ApplyFilter();
            RefreshRailCounts();
            RefreshStatus();
            NotifyOfPropertyChange(nameof(CanAdd));

            if (result.Message is not null) StatusText = result.Message;
        }
        catch (OperationCanceledException) { /* 화면을 닫는 중이다 */ }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 적재 실패 — {ex.Message}");
            StatusText = "서버 목록을 불러오지 못했습니다. 잠시 후 [갱신]을 누르세요.";
        }
    }

    private static void SyncOptions<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        for (var i = target.Count - 1; i >= 0; i--)
            if (!source.Contains(target[i])) target.RemoveAt(i);
        foreach (var item in source)
            if (!target.Contains(item)) target.Add(item);
    }
    #endregion

    #region - Selection · Detail -
    public ConsoleDetailPresenter Detail { get; }

    /// <summary>상세에 그릴 것이 있는가 — 없으면 <b>빈 편집 폼을 그리지 않는다</b>.</summary>
    public bool HasDetail => _detail is not null || Detail.IsCreating;

    /// <summary>뷰가 알린 선택 변경. 막혔으면 false — 뷰가 선택을 되돌린다.</summary>
    public bool OnRowsSelected(IList? selected)
    {
        var rows = selected?.Cast<object>().OfType<ServerRowViewModel>().ToList() ?? new List<ServerRowViewModel>();
        if (rows.SequenceEqual(_selected) && !Detail.IsCreating) return true;
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return false;

        _selected = rows;
        Detail.IsCreating = false;
        IsEditing = false;
        Detail.SelectedCount = rows.Count;
        Detail.SingleTitle = rows.Count == 1 ? rows[0].Name : string.Empty;
        Detail.SingleNumber = rows.Count == 1 ? rows[0].Id.ToString(CultureInfo.InvariantCulture) : string.Empty;

        // 커널의 ReadOnly 상태는 쓰지 않는다 — 그 배너가 "편집 권한이 없어" 라 여기서는 거짓말이 된다.
        Detail.IsReadOnly = false;

        _ = LoadDetailAsync(rows.Count == 1 ? rows[0] : null);
        foreach (var name in new[] { nameof(SelectedRow), nameof(CanBeginEdit), nameof(CanShowHistory), nameof(CanAssignSelection), nameof(HasDetail) })
            NotifyOfPropertyChange(name);
        return true;
    }

    public ServerRowViewModel? SelectedRow => _selected.Count == 1 ? _selected[0] : null;

    public IReadOnlyList<ServerRowViewModel> SelectedRows => _selected;

    /// <summary>
    /// 상세 적재. <b>세대 번호</b>와 <b>선택별 취소</b>로 늦은 응답을 버린다 — A 를 고르고 곧바로 B 를 고르면
    /// A 의 응답이 B 의 상세에 들어앉아 편집 기준선까지 오염된다.
    /// </summary>
    private async Task LoadDetailAsync(ServerRowViewModel? row)
    {
        CancelDetailLoad();
        var generation = ++_generation;
        var cts = _detailCts = new CancellationTokenSource();

        _detail = null;
        _intent = new ServerWriteIntent();
        MetricCells.Clear();
        OperationModeText = string.Empty;
        NotifyAllFields();

        if (row is null)
        {
            NotifyOfPropertyChange(nameof(IsMetricBandVisible));
            NotifyOfPropertyChange(nameof(HasDetail));
            return;
        }

        try
        {
            var view = await _service.GetAsync(row.Id, cts.Token).ConfigureAwait(true);
            if (generation != _generation) return;
            _detail = view ?? row.View;

            var metric = await _service.LatestMetricAsync(row.Id, cts.Token).ConfigureAwait(true);
            if (generation != _generation) return;
            foreach (var cell in ServerMetricBand.Band(metric)) MetricCells.Add(cell);

            if (!IsAxisEra)
            {
                var (proxy, _) = await _service.LegacyOperationModeAsync(row.Id, cts.Token).ConfigureAwait(true);
                if (generation != _generation) return;
                OperationModeText = proxy is not null
                    ? $"{ServerModeDisplay.OperationLabel(proxy.OperationMode)} · {ServerModeDisplay.WindyLabel(proxy.WindyMode)}"
                    : "—";
            }
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 상세 적재 실패 — {ex.Message}");
        }

        if (generation != _generation) return;
        NotifyAllFields();
        NotifyOfPropertyChange(nameof(IsMetricBandVisible));
        NotifyOfPropertyChange(nameof(HasDetail));
    }

    private void CancelDetailLoad()
    {
        var cts = _detailCts;
        _detailCts = null;
        if (cts is null) return;
        try { cts.Cancel(); } catch (ObjectDisposedException) { }
        cts.Dispose();
    }

    /// <summary>선택한 한 대가 있을 때만 지표 띠가 뜬다(스토리보드 L1345).</summary>
    public bool IsMetricBandVisible => MetricCells.Count > 0 && SelectedRow is not null;

    public ObservableCollection<ServerMetricCell> MetricCells { get; }

    #endregion

    #region - Detail fields -
    public string NameText
    {
        get => _intent.Name ?? _detail?.Name ?? string.Empty;
        set => SetText(v => _intent.Name = v, value, _detail?.Name, ServerRequestBuilder.NameKey, nameof(NameText));
    }

    public string IpText
    {
        get => _intent.IpAddress ?? _detail?.IpAddress ?? string.Empty;
        set => SetText(v => _intent.IpAddress = v, value, _detail?.IpAddress, ServerRequestBuilder.IpKey, nameof(IpText));
    }

    /// <summary>호스트명 — 비우면 <b>지운다</b>(축 계약). 6.3 에서는 검사에서 막는다.</summary>
    public string HostnameText
    {
        get => _intent.ClearHostname ? string.Empty : _intent.Hostname ?? _detail?.Hostname ?? string.Empty;
        set => SetClearable(
            text => { _intent.Hostname = text; _intent.ClearHostname = false; },
            () => { _intent.Hostname = null; _intent.ClearHostname = true; },
            value, _detail?.Hostname, ServerRequestBuilder.HostnameKey, nameof(HostnameText));
    }

    public string UserNameText
    {
        get => _intent.ClearUserName ? string.Empty : _intent.UserName ?? _detail?.UserName ?? string.Empty;
        set => SetClearable(
            text => { _intent.UserName = text; _intent.ClearUserName = false; },
            () => { _intent.UserName = null; _intent.ClearUserName = true; },
            value, _detail?.UserName, ServerRequestBuilder.UserNameKey, nameof(UserNameText));
    }

    public string PortText
    {
        get => (_intent.Port ?? _detail?.Port ?? 0) is var port && port == 0 && _detail is null ? string.Empty : (_intent.Port ?? _detail?.Port ?? 0).ToString(CultureInfo.InvariantCulture);
        set
        {
            var parsed = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ? port : (int?)null;
            _intent.Port = parsed;
            Detail.Tracker.Touch(ServerRequestBuilder.PortKey, _detail?.Port, parsed);
            NotifyOfPropertyChange();
            NotifyTouchFlags();
        }
    }

    /// <summary>
    /// 새 비밀번호. <b>읽으면 언제나 빈 글자</b>다 — 서버가 준 값을 화면에 되비추지 않고
    /// 로그 · 자동화 · 미리보기에도 남기지 않는다.
    /// </summary>
    public string PasswordText
    {
        get => string.Empty;
        set
        {
            _intent.NewPassword = string.IsNullOrEmpty(value) ? null : value;
            Detail.Tracker.Touch("user_password", null, _intent.NewPassword is null ? null : "(변경)", hasOriginal: false);
            NotifyOfPropertyChange(nameof(PasswordNote));
            NotifyTouchFlags();
        }
    }

    public string PasswordNote => _intent.NewPassword is null ? "비워 두면 바꾸지 않습니다" : "저장할 때 바뀝니다";

    /// <summary>6.3 에서 비우기가 막힌다는 사실을 칸 주석으로 먼저 말한다.</summary>
    public string ClearableNote => IsAxisEra ? "비우면 서버에서 지워집니다" : ServerRequestBuilder.CannotClear("이 값");

    public string CpuWarningText { get => Threshold(_intent.CpuWarning, "cpu", "warning"); set => SetThreshold(v => _intent.CpuWarning = v, value, "cpu", "warning", nameof(CpuWarningText)); }
    public string CpuCriticalText { get => Threshold(_intent.CpuCritical, "cpu", "critical"); set => SetThreshold(v => _intent.CpuCritical = v, value, "cpu", "critical", nameof(CpuCriticalText)); }
    public string RamWarningText { get => Threshold(_intent.RamWarning, "ram", "warning"); set => SetThreshold(v => _intent.RamWarning = v, value, "ram", "warning", nameof(RamWarningText)); }
    public string RamCriticalText { get => Threshold(_intent.RamCritical, "ram", "critical"); set => SetThreshold(v => _intent.RamCritical = v, value, "ram", "critical", nameof(RamCriticalText)); }
    public string DiskWarningText { get => Threshold(_intent.DiskWarning, "disk", "warning"); set => SetThreshold(v => _intent.DiskWarning = v, value, "disk", "warning", nameof(DiskWarningText)); }
    public string DiskCriticalText { get => Threshold(_intent.DiskCritical, "disk", "critical"); set => SetThreshold(v => _intent.DiskCritical = v, value, "disk", "critical", nameof(DiskCriticalText)); }
    public string NetworkWarningText { get => Threshold(_intent.NetworkWarningMbps, "network", "warning_mbps"); set => SetThreshold(v => _intent.NetworkWarningMbps = v, value, "network", "warning_mbps", nameof(NetworkWarningText)); }
    public string NetworkCriticalText { get => Threshold(_intent.NetworkCriticalMbps, "network", "critical_mbps"); set => SetThreshold(v => _intent.NetworkCriticalMbps = v, value, "network", "critical_mbps", nameof(NetworkCriticalText)); }

    /// <summary>운용 모드(7.0+ <c>server_config.modes</c>) — PROXY 서버만 갖는다.</summary>
    public bool HasModesSection => IsAxisEra && SelectedRow?.Type == EnumServerType.PROXY;

    public string OperationModeValue
    {
        get => _intent.OperationMode ?? ServerRequestBuilder.ReadMode(_detail?.Modes, "operation_mode") ?? string.Empty;
        set { _intent.OperationMode = string.IsNullOrWhiteSpace(value) ? null : value; Touch("modes.operation_mode", ServerRequestBuilder.ReadMode(_detail?.Modes, "operation_mode"), _intent.OperationMode, nameof(OperationModeValue)); }
    }

    public string WindyModeValue
    {
        get => _intent.WindyMode ?? ServerRequestBuilder.ReadMode(_detail?.Modes, "windy_mode") ?? string.Empty;
        set { _intent.WindyMode = string.IsNullOrWhiteSpace(value) ? null : value; Touch("modes.windy_mode", ServerRequestBuilder.ReadMode(_detail?.Modes, "windy_mode"), _intent.WindyMode, nameof(WindyModeValue)); }
    }

    /// <summary>운용 모드 보기 — 화면은 한국어(<see cref="ServerModeOption.Display"/>), 저장 값은 코드(<see cref="ServerModeOption.Code"/>).</summary>
    public IReadOnlyList<ServerModeOption> OperationModeOptions { get; } = ServerModeDisplay.OperationModes;
    public IReadOnlyList<ServerModeOption> WindyModeOptions { get; } = ServerModeDisplay.WindyModes;

    /// <summary>6.3 전용 — 프록시 설정 경로에서 읽은 모드 글자.</summary>
    public string OperationModeText
    {
        get => _operationModeText;
        private set { _operationModeText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>관측 절 — 읽기 전용이다.</summary>
    public string ObservedStatusText => SelectedRow?.StatusText ?? ServerStatusRules.NotReportedText;
    public string ObservedLastChangeText => SelectedRow?.LastChangeText ?? ServerStatusRules.NotReportedText;
    public string ObservedLastEditText => SelectedRow?.LastEditText ?? ServerStatusRules.NotReportedText;
    public bool IsStatusReceived => SelectedRow is { IsNotReported: false };
    public string StatusIsObservedNote => ServerWriteGuard.STATUS_IS_OBSERVED_NOTE;

    public string UnitSectionText => IsUnitEra
        ? SelectedRow?.UnitText ?? "—"
        : ServerWriteGuard.UNIT_NOT_IN_CONTRACT_NOTE;

    public string ValidationText
    {
        get
        {
            if (!HasDetail) return string.Empty;
            var errors = ServerRequestBuilder.Validate(_intent, _detail, _service.Contract);
            return errors.Count == 0 ? string.Empty : string.Join(" · ", errors.Select(e => e.Message));
        }
    }

    #region - 손댄 칸 표지 -
    public bool IsNameTouched => Detail.Tracker.IsTouched(ServerRequestBuilder.NameKey);
    public bool IsIpTouched => Detail.Tracker.IsTouched(ServerRequestBuilder.IpKey);
    public bool IsPortTouched => Detail.Tracker.IsTouched(ServerRequestBuilder.PortKey);
    public bool IsHostnameTouched => Detail.Tracker.IsTouched(ServerRequestBuilder.HostnameKey);
    public bool IsUserNameTouched => Detail.Tracker.IsTouched(ServerRequestBuilder.UserNameKey);
    public bool IsPasswordTouched => Detail.Tracker.IsTouched("user_password");
    public bool IsCpuTouched => Detail.Tracker.IsTouched("threshold.cpu.warning") || Detail.Tracker.IsTouched("threshold.cpu.critical");
    public bool IsRamTouched => Detail.Tracker.IsTouched("threshold.ram.warning") || Detail.Tracker.IsTouched("threshold.ram.critical");
    public bool IsDiskTouched => Detail.Tracker.IsTouched("threshold.disk.warning") || Detail.Tracker.IsTouched("threshold.disk.critical");
    public bool IsNetworkTouched => Detail.Tracker.IsTouched("threshold.network.warning_mbps") || Detail.Tracker.IsTouched("threshold.network.critical_mbps");
    public bool IsModesTouched => Detail.Tracker.IsTouched("modes.operation_mode") || Detail.Tracker.IsTouched("modes.windy_mode");
    #endregion

    private void SetText(System.Action<string?> assign, string value, string? original, string key, string propertyName)
    {
        assign(value);
        Touch(key, original ?? string.Empty, value ?? string.Empty, propertyName);
    }

    private void SetClearable(System.Action<string> assign, System.Action clear, string value, string? original, string key, string propertyName)
    {
        if (string.IsNullOrEmpty(value)) clear();
        else assign(value);

        Touch(key, original ?? string.Empty, value ?? string.Empty, propertyName);
    }

    private void Touch(string key, object? original, object? current, string propertyName)
    {
        Detail.Tracker.Touch(key, original, current);
        NotifyOfPropertyChange(propertyName);
        NotifyTouchFlags();
    }

    private string Threshold(double? draft, string group, string key)
    {
        var value = draft ?? ServerRequestBuilder.ReadThreshold(_detail?.Thresholds, group, key);
        return value?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void SetThreshold(System.Action<double?> assign, string value, string group, string key, string propertyName)
    {
        var parsed = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : (double?)null;
        assign(parsed);
        Touch($"threshold.{group}.{key}", ServerRequestBuilder.ReadThreshold(_detail?.Thresholds, group, key), parsed, propertyName);
    }

    private void NotifyTouchFlags()
    {
        foreach (var name in new[]
        {
            nameof(ValidationText), nameof(IsNameTouched), nameof(IsIpTouched), nameof(IsPortTouched),
            nameof(IsHostnameTouched), nameof(IsUserNameTouched), nameof(IsPasswordTouched),
            nameof(IsCpuTouched), nameof(IsRamTouched), nameof(IsDiskTouched), nameof(IsNetworkTouched), nameof(IsModesTouched),
        }) NotifyOfPropertyChange(name);
    }

    private void NotifyAllFields()
    {
        foreach (var name in new[]
        {
            nameof(NameText), nameof(IpText), nameof(PortText), nameof(HostnameText), nameof(UserNameText),
            nameof(PasswordNote), nameof(CpuWarningText), nameof(CpuCriticalText), nameof(RamWarningText), nameof(RamCriticalText),
            nameof(DiskWarningText), nameof(DiskCriticalText), nameof(NetworkWarningText), nameof(NetworkCriticalText),
            nameof(OperationModeValue), nameof(WindyModeValue), nameof(HasModesSection),
            nameof(ObservedStatusText), nameof(ObservedLastChangeText), nameof(ObservedLastEditText),
            nameof(IsStatusReceived), nameof(UnitSectionText), nameof(HasDetail), nameof(ClearableNote),
        }) NotifyOfPropertyChange(name);
        NotifyTouchFlags();
    }
    #endregion

    #region - Commands -
    public bool CanAdd => IsServerList && !_isBusy && CategoryOptions.Count > 0;

    public string AddBlockedReason => CategoryOptions.Count == 0
        ? "서버 분류를 불러오지 못해 등록할 수 없습니다. [갱신]을 누르세요."
        : "지금은 서버를 등록할 수 없습니다.";

    /// <summary>
    /// 서버 삭제는 내지 않는다 — 장비가 참조하고 있고 되돌릴 방법이 없다. 뷰가 툴바의 [삭제] 를 아예 접는다
    /// (늘 꺼진 버튼은 자리표시다 — U-18 감사 D-7 7.9). 사유는 접지 못하는 경로를 위해 남긴다.
    /// </summary>
    public bool CanDelete => false;
    public string DeleteBlockedReason => "이 화면에서는 서버를 삭제할 수 없습니다.";

    public ObservableCollection<ServerCategoryOption> CategoryOptions { get; }

    public ServerCategoryOption? SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value; NotifyOfPropertyChange(); }
    }

    public bool CanBeginEdit => SelectedRow is not null && !Detail.IsCreating && !IsEditing;

    /// <summary>상세는 읽기로 연다 — [설정 수정] 을 눌러야 칸이 열린다(스토리보드 L1351).</summary>
    public void BeginEdit()
    {
        if (!CanBeginEdit) return;
        IsEditing = true;
        NotifyOfPropertyChange(nameof(CanBeginEdit));
    }

    public bool IsEditing
    {
        get => _isEditing;
        private set { _isEditing = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanBeginEdit)); }
    }

    /// <summary>[추가] — 등록 폼에는 <b>상태 칸이 없다</b>(관측 필드를 쓰면 422).</summary>
    public void Add()
    {
        if (!CanAdd || !Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;

        CancelDetailLoad();
        _generation++;
        _selected = new List<ServerRowViewModel>();
        _detail = null;
        _intent = new ServerWriteIntent();
        MetricCells.Clear();
        SelectedCategory ??= CategoryOptions.FirstOrDefault();

        Detail.IsReadOnly = false;
        Detail.SelectedCount = 0;
        Detail.IsCreating = true;
        Detail.CreateBanner = "이름 · 주소 · 포트 · 분류를 입력하세요.";
        IsEditing = true;

        ClearGridSelectionRequested?.Invoke(this, EventArgs.Empty);
        NotifyAllFields();
        NotifyOfPropertyChange(nameof(IsMetricBandVisible));
    }

    public void Apply() => _ = ApplyAsync(CancellationToken.None);

    public async Task ApplyAsync(CancellationToken token)
    {
        if (!Detail.CanApply || _isBusy) return;

        _isBusy = true;
        NotifyOfPropertyChange(nameof(CanReload));
        try
        {
            if (Detail.IsCreating)
            {
                var (created, newId) = await _service.CreateAsync(SelectedCategory!, _intent, token).ConfigureAwait(true);
                StatusText = created.Message;
                if (!created.IsSuccess) return;

                Detail.IsCreating = false;
                IsEditing = false;
                Detail.Settle(ServerStatusRules.JustRegisteredNotice);
                await LoadCoreAsync(token).ConfigureAwait(true);
                SelectAfterReload(newId);
                return;
            }

            if (SelectedRow is not { } row) return;

            var saved = await _service.SaveAsync(row.Id, _intent, token).ConfigureAwait(true);
            StatusText = saved.Message;
            if (!saved.IsSuccess) return;

            IsEditing = false;
            Detail.Settle("설정을 저장했습니다.");
            await LoadCoreAsync(token).ConfigureAwait(true);
            SelectAfterReload(row.Id);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 적용 실패 — {ex.Message}");
            StatusText = "저장하지 못했습니다. 잠시 후 다시 시도하세요.";
        }
        finally
        {
            _isBusy = false;
            NotifyOfPropertyChange(nameof(CanReload));
        }
    }

    public void Revert()
    {
        _intent = new ServerWriteIntent();
        Detail.Tracker.Clear();

        if (Detail.IsCreating)
        {
            Detail.IsCreating = false;
            _detail = null;
        }

        IsEditing = false;
        Detail.IsReadOnly = false;
        Detail.Settle("되돌렸습니다.");
        NotifyAllFields();
    }

    public bool CanShowHistory => SelectedRow is not null && _dialogs is not null;

    /// <summary>[지표 이력] — 이력 화면은 <b>임계 배지를 그리지 않는다</b>(스토리보드 L1359).</summary>
    public async Task ShowHistoryAsync()
    {
        if (SelectedRow is not { } row || DialogsOrNull() is not { } dialogs) return;
        try { await dialogs.ShowMetricHistoryAsync(row.Id, row.Name).ConfigureAwait(true); }
        catch (Exception ex) { _log?.Error($"[ServerConsole] 지표 이력 실패 — {ex.Message}"); }
    }
    #endregion

    #region - Assign -
    public ServerAssignHandler Assign { get; }

    /// <summary>여러 대 배정은 여기 쌓였다가 [적용] 한 번에 나간다(콘솔 공통 규칙).</summary>
    public DraftTrayViewModel Tray { get; }

    public ObservableCollection<ServerAssignCandidateViewModel> AssignCandidates { get; }

    public bool HasAssignCandidates => AssignCandidates.Count > 0;

    public string AssignHint => IsAxisEra
        ? "장비를 서버 행에 끌어 놓거나, 행을 고르고 [배정]을 누르세요."
        : "현재 서버에서는 스피커만 배정할 수 있습니다. 서버 행을 고르고 [배정]을 누르세요.";

    public bool CanAssignSelection => SelectedRow is { AcceptsDevices: true } && AssignCandidates.Count > 0;

    /// <summary>끌기의 버튼 폴백 — 트레이에서 고른 장비를 지금 고른 서버 행에 배정한다.</summary>
    public async Task AssignSelectionAsync(IReadOnlyList<ServerAssignCandidateViewModel>? candidates)
    {
        if (SelectedRow is not { } row) { StatusText = "먼저 서버 행을 고르세요."; return; }
        var models = (candidates ?? Array.Empty<ServerAssignCandidateViewModel>()).Select(c => c.Model).ToList();
        if (models.Count == 0) { StatusText = "배정할 장비를 먼저 고르세요."; return; }

        await Assign.AssignAsync(row, models).ConfigureAwait(true);
    }

    public async Task ApplyTrayAsync()
    {
        if (!Tray.HasEntries) return;
        if (DialogsOrNull() is { } dialogs)
        {
            var accepted = await dialogs.ConfirmAsync("서버 배정", ServerDropRules.TrayConfirmText(Tray.Count)).ConfigureAwait(true);
            if (!accepted) { StatusText = ServerDropRules.CancelledText; return; }
        }
        await Assign.ApplyTrayAsync().ConfigureAwait(true);
    }

    public void RevertTray() => Assign.RevertTray();

    public bool CanUndoAssign => _lastUndo is not null;

    public async Task UndoAssignAsync()
    {
        if (_lastUndo is not { } undo) return;
        await Assign.UndoAsync(undo).ConfigureAwait(true);
    }

    private void OnAssignCompleted(ServerAssignResult result)
    {
        // 완료는 작업 스레드에서 올 수 있다 — 화면을 만지는 일은 UI 스레드로 옮긴다.
        Execute.OnUIThread(() =>
        {
            StatusText = result.Line;
            _lastUndo = result.Undo;
            NotifyOfPropertyChange(nameof(CanUndoAssign));
            RefreshCandidates();
        });
    }

    /// <summary>끄는 동안 행마다 "왜 못 받는지" 를 채운다 — 툴팁과 상태 줄이 같은 문장을 쓴다.</summary>
    private void OnDragProbe(IReadOnlyList<IBaseDeviceModel> dragged)
    {
        foreach (var row in Rows)
        {
            var plan = ServerDropRules.Plan(row.Id, row.Type, dragged, _service.Contract);
            row.DropBlockReason = plan.CanSend ? null : plan.BlockReason;
        }
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Execute.OnUIThread(RefreshCandidates);

    private void RefreshCandidates()
    {
        var wanted = _devices.OfType<IBaseDeviceModel>()
            .Where(d => d is not null && d.Id > 0)
            .Where(d => ServerDropRules.AllowedServerTypes.ContainsKey(DeviceAxesMapper.CategoryOf(d)))
            .Where(d => IsAxisEra || ServerDropRules.IsSupportedOnLegacy(DeviceAxesMapper.CategoryOf(d)))
            .OrderBy(d => ServerDropRules.CategoryLabel(DeviceAxesMapper.CategoryOf(d)), StringComparer.CurrentCulture)
            .ThenBy(d => d.DeviceName, StringComparer.CurrentCulture)
            .Select(d => new ServerAssignCandidateViewModel(
                d,
                ServerDropRules.CategoryLabel(DeviceAxesMapper.CategoryOf(d)),
                ServerNameOf(d)))
            .ToList();

        for (var i = AssignCandidates.Count - 1; i >= 0; i--)
            if (wanted.All(w => w.Id != AssignCandidates[i].Id)) AssignCandidates.RemoveAt(i);

        for (var i = 0; i < wanted.Count; i++)
        {
            var at = -1;
            for (var j = 0; j < AssignCandidates.Count; j++)
                if (AssignCandidates[j].Id == wanted[i].Id) { at = j; break; }

            if (at < 0) { AssignCandidates.Insert(Math.Min(i, AssignCandidates.Count), wanted[i]); continue; }
            if (at != i) AssignCandidates.Move(at, i);
            if (AssignCandidates[i].ServerText != wanted[i].ServerText) AssignCandidates[i] = wanted[i];
        }

        NotifyOfPropertyChange(nameof(HasAssignCandidates));
        NotifyOfPropertyChange(nameof(CanAssignSelection));
    }

    private string ServerNameOf(IBaseDeviceModel device)
    {
        var serverId = ServerDropRules.ServerIdOf(device);
        if (serverId is null) return "서버 없음";
        var row = _all.FirstOrDefault(r => r.Id == serverId.Value);
        return row?.Name ?? $"서버 #{serverId}";
    }
    #endregion

    #region - View plumbing -
    public event EventHandler<IReadOnlyList<object>>? SelectionRestoreRequested;

    public event EventHandler? ClearGridSelectionRequested;

    private void OnNavigationBlocked(object? sender, ConsoleNavigation navigation)
    {
        if (navigation == ConsoleNavigation.SelectRow)
            SelectionRestoreRequested?.Invoke(this, _selected.Cast<object>().ToList());
    }

    private void ClearSelection()
    {
        CancelDetailLoad();
        _generation++;
        _selected = new List<ServerRowViewModel>();
        _detail = null;
        _intent = new ServerWriteIntent();
        MetricCells.Clear();
        IsEditing = false;
        Detail.IsCreating = false;
        Detail.SelectedCount = 0;
        Detail.IsReadOnly = false;
        Detail.Tracker.Clear();
        NotifyAllFields();
        foreach (var name in new[] { nameof(SelectedRow), nameof(IsMetricBandVisible), nameof(CanBeginEdit), nameof(CanShowHistory), nameof(CanAssignSelection), nameof(HasDetail) })
            NotifyOfPropertyChange(name);
    }

    private void SelectAfterReload(int id)
    {
        var row = Rows.FirstOrDefault(r => r.Id == id);
        if (row is null) return;
        SelectionRestoreRequested?.Invoke(this, new object[] { row });
    }

    private IServerConsoleDialogs? DialogsOrNull()
    {
        try { return _dialogs?.Value; }
        catch (Exception ex) { _log?.Warning($"[ServerConsole] 창 입구를 만들지 못했습니다: {ex.Message}"); return null; }
    }
    #endregion

    #region - Attributes -
    private readonly IEventAggregator _events;
    private readonly ILogService _log;
    private readonly IServerConsoleService _service;
    private readonly DeviceProvider _devices;
    private readonly IClock _clock;
    private readonly Lazy<IServerConsoleDialogs>? _dialogs;

    private IReadOnlyList<ServerRowViewModel> _all = Array.Empty<ServerRowViewModel>();
    private List<ServerRowViewModel> _selected = new();
    private ServerAxisView? _detail;
    private ServerWriteIntent _intent = new();
    private ServerAssignUndo? _lastUndo;
    private ServerUnitOption? _selectedUnit;
    private ServerCategoryOption? _selectedCategory;
    private CancellationTokenSource? _detailCts;
    private int _generation;

    private string _railKey = ServerTypeCatalog.AllKey;
    private string _railFooterText = string.Empty;
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    private string _listStatusText = string.Empty;
    private string _operationModeText = string.Empty;
    private bool _includeDescendants;
    private bool _isEditing;
    private bool _isBusy;
    #endregion
}
