using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
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
    public ServerAssignCandidateViewModel(ISpeakerDeviceModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Name = string.IsNullOrWhiteSpace(model.DeviceName) ? $"스피커 {model.Id}" : model.DeviceName!;
        ServerText = model.Server is { Id: > 0 } server && !string.IsNullOrWhiteSpace(server.Name) ? server.Name : "서버 없음";
    }

    public ISpeakerDeviceModel Model { get; }
    public int Id => Model.Id;
    public string Name { get; }
    public string ServerText { get; }
    public override string ToString() => Name;
}

/// <summary>
/// 서버 모니터 — 레일(유형별 + 시스템 이벤트) · 목록 + 지표 띠 · 상세 340.
/// </summary>
/// <remarks>
/// <para>정본: <c>window-layout-system-storyboard.html</c> L1330-1365 · 드래그 와이어프레임 L368-372.</para>
/// <para><b>이 콘솔은 상태를 쓰지 않는다.</b> 상태는 관측 값이고 보고 입구는 서버 매니저의 것이다 —
/// 우리 쓰기는 ① 서버 설정 <c>PATCH</c> ② 등록 <c>POST</c> ③ 스피커 배정 <c>PATCH</c> 셋뿐이다.</para>
/// <para>싱글턴이다 — 닫을 때 선택 · 미적용 변경 · 구독을 전부 내려놓는다. 주기 타이머를 두지 않는다
/// (뷰보다 오래 사는 타이머는 싱글턴에 붙박이고, 경과 시간은 애초에 생존 판정이 아니다).</para>
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
        Assign = new ServerAssignHandler(_service, () => _devices.OfType<IBaseDeviceModel>(), DialogsOrNull(), log);

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
        NotifyOfPropertyChange(nameof(Columns));
        NotifyOfPropertyChange(nameof(IsUnitEra));

        RefreshCandidates();
        await ReloadAsync(cancellationToken).ConfigureAwait(true);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        _devices.CollectionEntity.CollectionChanged -= OnDevicesChanged;
        Assign.Completed -= OnAssignCompleted;
        Detail.Guard.Blocked -= OnNavigationBlocked;

        Rows.Clear();
        MetricCells.Clear();
        AssignCandidates.Clear();
        _all = Array.Empty<ServerRowViewModel>();
        _selected = new List<ServerRowViewModel>();
        _detailDto = null;
        _draft = new ServerEditDraft();
        _lastUndo = null;
        IsEditing = false;
        SearchText = string.Empty;
        StatusText = string.Empty;
        Detail.Reset();
        NotifyOfPropertyChange(nameof(CanUndoAssign));

        await base.OnDeactivateAsync(close, cancellationToken).ConfigureAwait(true);
    }
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

        NotifyOfPropertyChange(nameof(SelectedRail));
        NotifyOfPropertyChange(nameof(IsServerList));
        NotifyOfPropertyChange(nameof(IsSystemEvents));
        NotifyOfPropertyChange(nameof(CanAdd));
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

    /// <summary>지금 칸이 서버 목록인가 — "시스템 이벤트" 는 목록이 아니다.</summary>
    public bool IsServerList => ServerTypeCatalog.IsServerList(_railKey);
    public bool IsSystemEvents => !IsServerList;

    /// <summary>부대 편제(8.0 이상)를 쓸 수 있는가 — 부대 필터 · "부대" 열의 게이트.</summary>
    public bool IsUnitEra => _service.IsUnitEra;

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

    public ServerUnitOption? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            if (ReferenceEquals(_selectedUnit, value)) return;
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

    /// <summary>"시스템 이벤트" 칸의 안내 — 이 라이브러리 API 면에는 시스템 이벤트 입구가 없다.</summary>
    public string SystemEventsNote =>
        "시스템 이벤트 입구가 이 판의 서버 API 에 없습니다 — 받은 것이 없어 목록을 그리지 않습니다. "
        + "서버가 경로를 내면 이 칸과 상세의 '최근 시스템 이벤트' 가 같은 자료를 씁니다.";

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

    /// <summary>실제 적재 — 바쁨 판정을 하지 않는다(적용 뒤 재조회처럼 이미 바쁜 자리에서도 부른다).</summary>
    private async Task LoadCoreAsync(CancellationToken token)
    {
        try
        {
            var result = await _service.LoadAsync(
                IsUnitEra ? _selectedUnit?.Id : null,
                _includeDescendants,
                token).ConfigureAwait(true);

            _all = result.Servers.Select(entry => new ServerRowViewModel(entry, _clock)).ToList();

            SyncOptions(UnitOptions, result.Units);
            SyncOptions(CategoryOptions, result.Categories);

            ClearSelection();
            ApplyFilter();
            RefreshRailCounts();
            RefreshStatus();
            NotifyOfPropertyChange(nameof(CanAdd));     // 분류가 들어와야 [추가] 가 켜진다

            StatusText = result.Message
                ?? (result.IsTruncated ? "서버가 총계를 주지 않아 목록이 끊겼을 수 있습니다 — 첫 페이지만 보입니다" : StatusText);
        }
        catch (OperationCanceledException) { /* 화면을 닫는 중이다 */ }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 적재 실패 — {ex.Message}");
            StatusText = "서버 목록을 받지 못했습니다";
        }
    }

    private static void SyncOptions<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        // 묶인 목록은 비우고 채우지 않는다 — 콤보의 선택이 사라진다.
        for (var i = target.Count - 1; i >= 0; i--)
            if (!source.Contains(target[i])) target.RemoveAt(i);
        foreach (var item in source)
            if (!target.Contains(item)) target.Add(item);
    }
    #endregion

    #region - Selection · Detail -
    public ConsoleDetailPresenter Detail { get; }

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

        // 커널의 ReadOnly 상태는 쓰지 않는다 — 그 배너 문구가 "편집 권한이 없어" 라 여기서는 거짓말이 된다.
        // 칸을 잠그는 것은 IsEditing 이고, 상세 상태는 Single/Multiple 그대로 둔다.
        Detail.IsReadOnly = false;

        _ = LoadDetailAsync(rows.Count == 1 ? rows[0] : null);
        NotifyOfPropertyChange(nameof(SelectedRow));
        NotifyOfPropertyChange(nameof(CanBeginEdit));
        NotifyOfPropertyChange(nameof(CanShowHistory));
        NotifyOfPropertyChange(nameof(CanAssignSelection));
        return true;
    }

    public ServerRowViewModel? SelectedRow => _selected.Count == 1 ? _selected[0] : null;

    public IReadOnlyList<ServerRowViewModel> SelectedRows => _selected;

    private async Task LoadDetailAsync(ServerRowViewModel? row)
    {
        _detailDto = null;
        _draft = new ServerEditDraft();
        MetricCells.Clear();
        OperationModeText = string.Empty;
        NotifyAllFields();

        if (row is null) { NotifyOfPropertyChange(nameof(IsMetricBandVisible)); return; }

        var dto = await _service.GetAsync(row.Id, CancellationToken.None).ConfigureAwait(true);
        _detailDto = dto ?? row.Dto;

        var metric = await _service.LatestMetricAsync(row.Id, CancellationToken.None).ConfigureAwait(true);
        foreach (var cell in ServerMetricBand.Band(metric)) MetricCells.Add(cell);

        var (proxy, note) = await _service.OperationModeAsync(row.Id, CancellationToken.None).ConfigureAwait(true);
        OperationModeText = proxy is not null
            ? $"{proxy.OperationMode} · {proxy.WindyMode}"
            : note ?? string.Empty;

        NotifyAllFields();
        NotifyOfPropertyChange(nameof(IsMetricBandVisible));
    }

    /// <summary>선택한 한 대가 있을 때만 지표 띠가 뜬다(스토리보드 L1345).</summary>
    public bool IsMetricBandVisible => MetricCells.Count > 0 && SelectedRow is not null;

    public ObservableCollection<ServerMetricCell> MetricCells { get; }

    /// <summary>지표가 하나도 없을 때 띠 자리에 적는 한 줄.</summary>
    public string MetricNote => "서버가 보낸 계측만 그립니다 — 임계 배지는 서버가 판정해 보낸 것만 뜹니다.";
    #endregion

    #region - Detail fields -
    public string NameText
    {
        get => Field(_draft.Name, _detailDto?.Name);
        set => SetField(v => _draft.Name = v, value, _detailDto?.Name, ServerRequestBuilder.NameKey, nameof(NameText));
    }

    public string IpText
    {
        get => Field(_draft.IpAddress, _detailDto?.IpAddress);
        set => SetField(v => _draft.IpAddress = v, value, _detailDto?.IpAddress, ServerRequestBuilder.IpKey, nameof(IpText));
    }

    public string HostnameText
    {
        get => Field(_draft.Hostname, _detailDto?.Hostname);
        set => SetField(v => _draft.Hostname = v, value, _detailDto?.Hostname, "hostname", nameof(HostnameText));
    }

    public string UserNameText
    {
        get => Field(_draft.UserName, _detailDto?.UserName);
        set => SetField(v => _draft.UserName = v, value, _detailDto?.UserName, "user_name", nameof(UserNameText));
    }

    public string PortText
    {
        get => (_draft.Port ?? _detailDto?.Port ?? 0).ToString(CultureInfo.InvariantCulture);
        set
        {
            var parsed = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ? port : (int?)null;
            _draft.Port = parsed;
            Detail.Tracker.Touch(ServerRequestBuilder.PortKey, _detailDto?.Port, parsed);
            NotifyOfPropertyChange();
            NotifyTouchFlags();
        }
    }

    /// <summary>
    /// 새 비밀번호. <b>읽으면 언제나 빈 글자</b>다 — 서버가 준 값을 화면에 되비추지 않고 로그 · 자동화 · 미리보기에도 남기지 않는다.
    /// </summary>
    public string PasswordText
    {
        get => string.Empty;
        set
        {
            _draft.NewPassword = string.IsNullOrEmpty(value) ? null : value;
            Detail.Tracker.Touch("user_password", null, _draft.NewPassword is null ? null : "(변경)", hasOriginal: false);
            NotifyOfPropertyChange(nameof(PasswordNote));
            NotifyTouchFlags();
        }
    }

    public string PasswordNote => _draft.NewPassword is null ? "비워 두면 바꾸지 않습니다" : "저장할 때 바뀝니다";

    public string CpuWarningText { get => Threshold(_draft.CpuWarning, "cpu", "warning"); set => SetThreshold(v => _draft.CpuWarning = v, value, "cpu", "warning", nameof(CpuWarningText)); }
    public string CpuCriticalText { get => Threshold(_draft.CpuCritical, "cpu", "critical"); set => SetThreshold(v => _draft.CpuCritical = v, value, "cpu", "critical", nameof(CpuCriticalText)); }
    public string RamWarningText { get => Threshold(_draft.RamWarning, "ram", "warning"); set => SetThreshold(v => _draft.RamWarning = v, value, "ram", "warning", nameof(RamWarningText)); }
    public string RamCriticalText { get => Threshold(_draft.RamCritical, "ram", "critical"); set => SetThreshold(v => _draft.RamCritical = v, value, "ram", "critical", nameof(RamCriticalText)); }
    public string DiskWarningText { get => Threshold(_draft.DiskWarning, "disk", "warning"); set => SetThreshold(v => _draft.DiskWarning = v, value, "disk", "warning", nameof(DiskWarningText)); }
    public string DiskCriticalText { get => Threshold(_draft.DiskCritical, "disk", "critical"); set => SetThreshold(v => _draft.DiskCritical = v, value, "disk", "critical", nameof(DiskCriticalText)); }
    public string NetworkWarningText { get => Threshold(_draft.NetworkWarningMbps, "network", "warning_mbps"); set => SetThreshold(v => _draft.NetworkWarningMbps = v, value, "network", "warning_mbps", nameof(NetworkWarningText)); }
    public string NetworkCriticalText { get => Threshold(_draft.NetworkCriticalMbps, "network", "critical_mbps"); set => SetThreshold(v => _draft.NetworkCriticalMbps = v, value, "network", "critical_mbps", nameof(NetworkCriticalText)); }

    /// <summary>손댄 칸 표지 — 칸 왼쪽에 경고색 줄이 선다(색이 아니라 <b>형태</b>).</summary>
    public bool IsNameTouched => Detail.Tracker.IsTouched(ServerRequestBuilder.NameKey);
    public bool IsIpTouched => Detail.Tracker.IsTouched(ServerRequestBuilder.IpKey);
    public bool IsPortTouched => Detail.Tracker.IsTouched(ServerRequestBuilder.PortKey);
    public bool IsHostnameTouched => Detail.Tracker.IsTouched("hostname");
    public bool IsUserNameTouched => Detail.Tracker.IsTouched("user_name");
    public bool IsPasswordTouched => Detail.Tracker.IsTouched("user_password");

    /// <summary>운용 모드 — 6.3 이면 프록시 설정, 그 위면 "server_config 로 이관됐다" 는 안내.</summary>
    public string OperationModeText
    {
        get => _operationModeText;
        private set { _operationModeText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>관측 절 — 읽기 전용이다. 보고가 없으면 칸이 아니라 "미수신 상자" 를 그린다.</summary>
    public string ObservedStatusText => SelectedRow?.StatusText ?? ServerStatusRules.NotReportedText;
    public string ObservedLastChangeText => SelectedRow?.LastChangeText ?? ServerStatusRules.NotReportedText;
    public bool IsStatusReceived => SelectedRow is { IsNotReported: false };
    public string StatusIsObservedNote => ServerWriteGuard.STATUS_IS_OBSERVED_NOTE;

    public string UnitSectionText => IsUnitEra
        ? SelectedRow?.UnitText ?? "—"
        : ServerWriteGuard.UNIT_NOT_IN_CONTRACT_NOTE;

    public string ValidationText
    {
        get
        {
            if (_detailDto is null) return string.Empty;
            var errors = ServerRequestBuilder.Validate(_draft, _detailDto);
            return errors.Count == 0 ? string.Empty : string.Join(" · ", errors.Select(e => e.Message));
        }
    }

    private string Field(string? draft, string? original) => draft ?? original ?? string.Empty;

    private void SetField(Action<string?> assign, string value, string? original, string key, string propertyName)
    {
        assign(value);
        Detail.Tracker.Touch(key, original ?? string.Empty, value ?? string.Empty);
        NotifyOfPropertyChange(propertyName);
        NotifyTouchFlags();
    }

    private string Threshold(double? draft, string group, string key)
    {
        var value = draft ?? ServerRequestBuilder.ReadThreshold(_detailDto?.ThresholdConfig, group, key);
        return value?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void SetThreshold(Action<double?> assign, string value, string group, string key, string propertyName)
    {
        var parsed = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : (double?)null;
        assign(parsed);
        Detail.Tracker.Touch($"threshold.{group}.{key}", ServerRequestBuilder.ReadThreshold(_detailDto?.ThresholdConfig, group, key), parsed);
        NotifyOfPropertyChange(propertyName);
        NotifyTouchFlags();
    }

    private void NotifyTouchFlags()
    {
        NotifyOfPropertyChange(nameof(ValidationText));
        NotifyOfPropertyChange(nameof(IsNameTouched));
        NotifyOfPropertyChange(nameof(IsIpTouched));
        NotifyOfPropertyChange(nameof(IsPortTouched));
        NotifyOfPropertyChange(nameof(IsHostnameTouched));
        NotifyOfPropertyChange(nameof(IsUserNameTouched));
        NotifyOfPropertyChange(nameof(IsPasswordTouched));
    }

    private void NotifyAllFields()
    {
        foreach (var name in new[]
        {
            nameof(NameText), nameof(IpText), nameof(PortText), nameof(HostnameText), nameof(UserNameText),
            nameof(PasswordNote), nameof(CpuWarningText), nameof(CpuCriticalText), nameof(RamWarningText), nameof(RamCriticalText),
            nameof(DiskWarningText), nameof(DiskCriticalText), nameof(NetworkWarningText), nameof(NetworkCriticalText),
            nameof(ObservedStatusText), nameof(ObservedLastChangeText), nameof(IsStatusReceived), nameof(UnitSectionText),
            nameof(ValidationText), nameof(IsNameTouched), nameof(IsIpTouched), nameof(IsPortTouched),
            nameof(IsHostnameTouched), nameof(IsUserNameTouched), nameof(IsPasswordTouched),
        }) NotifyOfPropertyChange(name);
    }
    #endregion

    #region - Commands -
    public bool CanAdd => IsServerList && !_isBusy && CategoryOptions.Count > 0;

    public string AddBlockedReason => CategoryOptions.Count == 0
        ? "서버 분류를 받지 못해 등록할 수 없습니다"
        : "등록할 수 없습니다";

    /// <summary>서버 삭제는 내지 않는다 — 장비가 참조하고 있고 되돌릴 방법이 없다.</summary>
    public bool CanDelete => false;
    public string DeleteBlockedReason => "서버 삭제는 이 화면에서 제공하지 않습니다 — 장비의 서버 참조가 끊깁니다";

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
        Detail.IsReadOnly = false;
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

        _selected = new List<ServerRowViewModel>();
        _detailDto = new ServerDto();
        _draft = new ServerEditDraft();
        MetricCells.Clear();
        SelectedCategory ??= CategoryOptions.FirstOrDefault();

        Detail.IsReadOnly = false;
        Detail.SelectedCount = 0;
        Detail.IsCreating = true;
        Detail.CreateBanner = "이름 · 주소 · 포트 · 분류를 채우면 등록할 수 있습니다. 상태는 서버가 보고합니다 — 등록 폼에 없습니다.";
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
                var (created, newId) = await _service.CreateAsync(SelectedCategory?.Id ?? 0, _draft, token).ConfigureAwait(true);
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

            var saved = await _service.SaveAsync(row.Id, _draft, token).ConfigureAwait(true);
            StatusText = saved.Message;
            if (!saved.IsSuccess) return;

            IsEditing = false;
            Detail.Settle("설정을 저장했습니다");
            await LoadCoreAsync(token).ConfigureAwait(true);
            SelectAfterReload(row.Id);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _log?.Error($"[ServerConsole] 적용 실패 — {ex.Message}");
            StatusText = "적용하지 못했습니다";
        }
        finally
        {
            _isBusy = false;
            NotifyOfPropertyChange(nameof(CanReload));
        }
    }

    public void Revert()
    {
        _draft = new ServerEditDraft();
        Detail.Tracker.Clear();

        if (Detail.IsCreating)
        {
            Detail.IsCreating = false;
            _detailDto = null;
        }

        IsEditing = false;
        Detail.IsReadOnly = false;          // 커널 ReadOnly 배너는 "권한 없음" 이라 여기서는 거짓말이 된다
        Detail.Settle("되돌렸습니다");
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

    public ObservableCollection<ServerAssignCandidateViewModel> AssignCandidates { get; }

    /// <summary>배정 트레이를 낼 것인가 — 끌 수 있는 장비가 하나라도 있을 때.</summary>
    public bool HasAssignCandidates => AssignCandidates.Count > 0;

    public string AssignHint => "스피커를 끌어 목록의 스피커 서버 행에 놓습니다 — 다른 유형의 행은 놓을 수 없습니다";

    public bool CanAssignSelection => SelectedRow is { AcceptsDevices: true } && AssignCandidates.Count > 0;

    /// <summary>끌기의 버튼 폴백 — 트레이에서 고른 장비를 지금 고른 서버 행에 배정한다.</summary>
    public async Task AssignSelectionAsync(IReadOnlyList<ServerAssignCandidateViewModel>? candidates)
    {
        if (SelectedRow is not { } row) { StatusText = "먼저 서버 행을 고르십시오"; return; }
        var models = (candidates ?? Array.Empty<ServerAssignCandidateViewModel>()).Select(c => (IBaseDeviceModel)c.Model).ToList();
        if (models.Count == 0) { StatusText = "배정할 장비를 먼저 고르십시오"; return; }

        await Assign.AssignAsync(row, models).ConfigureAwait(true);
    }

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

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Execute.OnUIThread(RefreshCandidates);

    private void RefreshCandidates()
    {
        var wanted = _devices.OfType<ISpeakerDeviceModel>()
            .Where(s => s is not null && s.Id > 0)
            .OrderBy(s => s.DeviceName, StringComparer.CurrentCulture)
            .Select(s => new ServerAssignCandidateViewModel(s))
            .ToList();

        // 묶인 목록은 비우고 채우지 않는다 — 끌던 선택과 스크롤이 날아간다. 자리마다 맞춰 넣고 뺀다.
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
    #endregion

    #region - View plumbing -
    /// <summary>뷰가 그리드 선택을 되돌리게 한다.</summary>
    public event EventHandler<IReadOnlyList<object>>? SelectionRestoreRequested;

    public event EventHandler? ClearGridSelectionRequested;

    private void OnNavigationBlocked(object? sender, ConsoleNavigation navigation)
    {
        if (navigation == ConsoleNavigation.SelectRow)
            SelectionRestoreRequested?.Invoke(this, _selected.Cast<object>().ToList());
    }

    private void ClearSelection()
    {
        _selected = new List<ServerRowViewModel>();
        _detailDto = null;
        _draft = new ServerEditDraft();
        MetricCells.Clear();
        IsEditing = false;
        Detail.IsCreating = false;
        Detail.SelectedCount = 0;
        Detail.IsReadOnly = false;
        Detail.Tracker.Clear();
        NotifyAllFields();
        NotifyOfPropertyChange(nameof(SelectedRow));
        NotifyOfPropertyChange(nameof(IsMetricBandVisible));
        NotifyOfPropertyChange(nameof(CanBeginEdit));
        NotifyOfPropertyChange(nameof(CanShowHistory));
        NotifyOfPropertyChange(nameof(CanAssignSelection));
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
    private ServerDto? _detailDto;
    private ServerEditDraft _draft = new();
    private ServerAssignUndo? _lastUndo;
    private ServerUnitOption? _selectedUnit;
    private ServerCategoryOption? _selectedCategory;

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
