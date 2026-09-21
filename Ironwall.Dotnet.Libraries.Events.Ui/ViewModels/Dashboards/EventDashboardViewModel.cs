using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Components;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dashboards;

/// <summary>
/// 이벤트 콘솔 — 레일(개요 · 탐지 · 장애 · 연결 · 조치) · 목록 · 상세 3단.
/// </summary>
/// <remarks>
/// <para>설계 정본: <c>docs/design/window-layout-system-storyboard.html</c> L988-1030(콘솔) · L1040-1047(창 매핑) ·
/// L2302-2612(레일 · 툴바 · 목록 · 개요) · L2688-2757(상세 여섯 상태) ·
/// <c>all-windows-drag-wireframe.html</c> L283 · L423 · L427(드래그 판정).</para>
/// <para>이 뷰모델은 <b>전송 경로를 갖지 않는다.</b> 조회 · 저장 · 삭제 · 재조회는 종류별 패널 뷰모델의 기존 경로를
/// <see cref="IEventConsoleSource"/> 로 감싸 그대로 부르고, 조치보고는 카드 뷰모델의 <c>SendAction</c> 을 그대로 쓴다
/// (멱등 가드 · NATS 발행이 함께 가야 하기 때문 — events-console PRD NFR-01 · FR-43).</para>
/// <para>싱글턴이다 — 창을 닫을 때 선택 · 미적용 변경 · 트레이 · 구독을 전부 내려놓는다.
/// 구독은 <c>OnActivateAsync</c> 에서만 건다(생성자에서 걸면 두 번째 열기부터 무음 사망).</para>
/// </remarks>
public class EventDashboardViewModel : BasePanelViewModel
{
    public const string ConsoleKey = "Events";

    public const string OverviewRailKey = "ov";
    public const string DetectionRailKey = "det";
    public const string MalfunctionRailKey = "mal";
    public const string ConnectionRailKey = "con";
    public const string ActionRailKey = "act";

    /// <summary>억제 스케줄 레일(정본 SB L1122 결정 E-D7 — 억제창을 이벤트 콘솔 레일로 옮긴다).</summary>
    public const string SuppressionRailKey = "sup";

    #region - Ctors -
    public EventDashboardViewModel(IEventAggregator eventAggregator
                                , ILogService log
                                , EventTabControlViewModel tabControlViewModel
                                , DetectionEventPanelViewModel detectionEventPanelViewModel
                                , MalfunctionEventPanelViewModel malfunctionEventPanelViewModel
                                , ConnectionEventPanelViewModel connectionEventPanelViewModel
                                , ActionEventPanelViewModel actionEventPanelViewModel
                                , EventInfoViewModel eventInfoViewModel
                                , CameraEventInfoViewModel cameraEventInfoViewModel
                                , DataChartPanelViewModel dataChartPanelViewModel
                                // ── N-08: 억제 스케줄 레일. 선택 주입이다 — 안 받으면 레일이 서지 않고 나머지는 그대로다.
                                //    (필수 인자로 바꾸면 이 뷰모델을 세우는 모든 곳 · 가짜가 한꺼번에 깨진다)
                                , IEventSuppressionApiService? suppressionApi = null
                                , DeviceProvider? deviceProvider = null
                                , DeviceGroupProvider? deviceGroupProvider = null
                                , IClock? clock = null
                                ) : base(eventAggregator, log)
    {
        TabControlViewModel = tabControlViewModel;
        DetectionPanelViewModel = detectionEventPanelViewModel;
        MalfunctionPanelViewModel = malfunctionEventPanelViewModel;
        ConnectionPanelViewModel = connectionEventPanelViewModel;
        ActionPanelViewModel = actionEventPanelViewModel;
        EventInfoViewModel = eventInfoViewModel;
        CameraEventInfoViewModel = cameraEventInfoViewModel;
        DataChartPanelViewModel = dataChartPanelViewModel;

        // 패널의 UpdateAction 은 기반 클래스가 아니라 패널마다 선언돼 있다 — 구독 방법을 람다로 넘긴다.
        _sources = new Dictionary<string, IEventConsoleSource>(StringComparer.Ordinal)
        {
            [DetectionRailKey] = new EventConsoleSource<DetectionEventViewModel>(
                detectionEventPanelViewModel,
                h => detectionEventPanelViewModel.UpdateAction += (_, _) => h(),
                detectionEventPanelViewModel.SetDate,
                detectionEventPanelViewModel.ClickSearch,
                detectionEventPanelViewModel.InvalidateCache,
                () => detectionEventPanelViewModel.LoadedCountText),

            [MalfunctionRailKey] = new EventConsoleSource<MalfunctionEventViewModel>(
                malfunctionEventPanelViewModel,
                h => malfunctionEventPanelViewModel.UpdateAction += (_, _) => h(),
                malfunctionEventPanelViewModel.SetDate,
                malfunctionEventPanelViewModel.ClickSearch,
                malfunctionEventPanelViewModel.InvalidateCache,
                () => malfunctionEventPanelViewModel.LoadedCountText),

            [ConnectionRailKey] = new EventConsoleSource<ConnectionEventViewModel>(
                connectionEventPanelViewModel,
                h => connectionEventPanelViewModel.UpdateAction += (_, _) => h(),
                connectionEventPanelViewModel.SetDate,
                connectionEventPanelViewModel.ClickSearch,
                connectionEventPanelViewModel.InvalidateCache,
                () => connectionEventPanelViewModel.LoadedCountText),

            [ActionRailKey] = new EventConsoleSource<ActionEventViewModel>(
                actionEventPanelViewModel,
                h => actionEventPanelViewModel.UpdateAction += (_, _) => h(),
                actionEventPanelViewModel.SetDate,
                actionEventPanelViewModel.ClickSearch,
                actionEventPanelViewModel.InvalidateCache,
                () => actionEventPanelViewModel.LoadedCountText),
        };

        Detail = new ConsoleDetailPresenter();
        DetailView = new EventDetailViewModel(Detail);
        Overview = new EventOverviewViewModel();
        Tray = new ActionTrayViewModel(SendActionAsync);
        TrayDrop = new ActionTrayDropHandler(Tray, () => CanReport);

        if (suppressionApi is not null)
        {
            Suppression = new SuppressionConsoleViewModel(
                eventAggregator, log, suppressionApi, deviceProvider, deviceGroupProvider, clock,
                // 권한 서비스를 두 번 해석하지 않는다 — 패널이 이미 계산해 둔 값을 그대로 쓴다.
                canEdit: () => detectionEventPanelViewModel.CanSaveEvent,
                canDelete: () => detectionEventPanelViewModel.CanDeleteEvent);
        }

        RailEntries = new ObservableCollection<ConsoleRailEntry>();
        BuildRail();

        _endDate = DateTime.Now;
        _startDate = _endDate.AddDays(-1);
    }
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        // 구독은 활성화에서만 — 싱글턴이라 생성자에서 걸면 두 번째 열기부터 조용히 죽는다.
        foreach (var source in _sources.Values) source.BusyEnded += OnSourceBusyEnded;
        DataChartPanelViewModel.UpdateAction += OnDashboardUpdated;
        Detail.Guard.Blocked += OnNavigationBlocked;
        Overview.RangeSelected += OnTrendRangeSelected;
        Overview.DrillRequested += OnDrillRequested;
        TrayDrop.Completed += OnTrayCompleted;
        if (Suppression is not null) Suppression.CountsChanged += OnSuppressionCountsChanged;

        EndDate = DateTime.Now;
        StartDate = EndDate.AddDays(-1);
        PushDates();

        if (DataChartPanelViewModel.IsActive)
            await DataChartPanelViewModel.DeactivateAsync(true);
        await TabControlViewModel.ActivateAsync();

        EventProvider = IoC.Get<EventProvider>();
        NotifyOfPropertyChange(nameof(EventProvider));

        await SwitchRailAsync(_railKey, force: true);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await base.OnDeactivateAsync(close, cancellationToken);

        foreach (var source in _sources.Values) source.BusyEnded -= OnSourceBusyEnded;
        DataChartPanelViewModel.UpdateAction -= OnDashboardUpdated;
        Detail.Guard.Blocked -= OnNavigationBlocked;
        Overview.RangeSelected -= OnTrendRangeSelected;
        Overview.DrillRequested -= OnDrillRequested;
        TrayDrop.Completed -= OnTrayCompleted;
        if (Suppression is not null)
        {
            Suppression.CountsChanged -= OnSuppressionCountsChanged;
            await Suppression.DeactivateAsync();     // 초안 · 구독을 내려놓는다(싱글턴)
        }
        DetachRows();

        // 싱글턴 — 다음에 열 때 옛 선택 · 미적용 변경 · Draft 가 남아 있으면 안 된다.
        Tray.Revert();
        Detail.Reset();
        DetailView.Load(EventDetailKind.Detection, Array.Empty<object>(), true, true, 0);
        SearchText = string.Empty;
        StatusText = string.Empty;

        // 먼저 닫고 나서 비운다 — 비우고 닫으면 활성 패널이 닫힘을 못 받아 다음에 열 때 구독이 겹친다(패널은 싱글턴).
        if (TabControlViewModel.ActiveItem is not null)
            await TabControlViewModel.DeactivateItemAsync(TabControlViewModel.ActiveItem, true);
        TabControlViewModel.Items.Clear();
        await TabControlViewModel.DeactivateAsync(true);
        await EventInfoViewModel.DeactivateAsync(true);
        await CameraEventInfoViewModel.DeactivateAsync(true);
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
            _ = SelectRailAsync(value.Key);
        }
    }

    /// <summary>레일에서 항목을 골랐다(뷰가 부른다). 막혔으면 false — 뷰는 선택을 <see cref="SelectedRail"/> 로 되돌린다.</summary>
    public async Task<bool> SelectRailAsync(string key)
    {
        if (string.Equals(key, _railKey, StringComparison.Ordinal)) return true;

        if (_isSwitching || !Detail.Guard.TryNavigate(ConsoleNavigation.SwitchRail))
        {
            NotifyOfPropertyChange(nameof(SelectedRail));
            return false;
        }

        await SwitchRailAsync(key, force: false);
        return true;
    }

    private async Task SwitchRailAsync(string key, bool force)
    {
        if (!RailEntries.Any(e => e.Key == key)) key = OverviewRailKey;
        if (!force && key == _railKey) return;
        if (_isSwitching) return;       // 두 전환이 await 사이에 끼어들면 목록은 C 인데 열은 B 인 화면이 된다

        _isSwitching = true;
        try
        {
            DetachRows();
            Detail.Reset();

            if (TabControlViewModel.ActiveItem is not null)
                await TabControlViewModel.DeactivateItemAsync(TabControlViewModel.ActiveItem, true);

            _railKey = key;
            _current = _sources.TryGetValue(key, out var source) ? source : null;

            // 억제 스케줄은 이벤트 목록이 아니다 — 기존 억제창이 쓰던 그 API 경로를 그대로 부른다.
            if (Suppression is not null && key != SuppressionRailKey) await Suppression.DeactivateAsync();

            if (key == SuppressionRailKey && Suppression is not null)
            {
                await Suppression.ActivateAsync();
            }
            else if (_current is not null)
            {
                // 패널의 활성화 수명주기는 그대로 — 활성화가 목록을 채우고 권한을 준비한다(TabControl 은 이것을 안 한다).
                await TabControlViewModel.ActivateItemAsync(_current.Panel);
                AttachRows(_current);
            }
            else
            {
                // 개요 — 통계는 기존 차트 패널이 부른다(새 전송 경로 없음).
                await TabControlViewModel.ActivateItemAsync(DataChartPanelViewModel);
                Overview.IsLoading = DataChartPanelViewModel.LastDashboardDto is null;
                Overview.Load(DataChartPanelViewModel.LastDashboardDto, StartDate, EndDate);
            }

            DetailView.Load(CurrentKind, Array.Empty<object>(), CanEdit, CanReport, 0);
            RefreshRailCounts();
            RaiseShellState();
        }
        finally
        {
            _isSwitching = false;
            NotifyOfPropertyChange(nameof(SelectedRail));
        }
    }

    private void BuildRail()
    {
        RailEntries.Clear();
        RailEntries.Add(new ConsoleRailEntry(OverviewRailKey, "개요", new EventConsoleIcon("ChartBar")) { ShowCount = false });
        RailEntries.Add(new ConsoleRailEntry(DetectionRailKey, "탐지", new EventConsoleIcon("MotionSensor")));
        RailEntries.Add(new ConsoleRailEntry(MalfunctionRailKey, "장애", new EventConsoleIcon("AlertOutline")));
        RailEntries.Add(new ConsoleRailEntry(ConnectionRailKey, "연결", new EventConsoleIcon("LanConnect")));
        RailEntries.Add(new ConsoleRailEntry(ActionRailKey, "조치", new EventConsoleIcon("ClipboardCheckOutline")));
        // 억제 스케줄 — 배지는 '지금 억제 중' 건수다(정본 SB L2361).
        if (Suppression is not null)
            RailEntries.Add(new ConsoleRailEntry(SuppressionRailKey, "억제 스케줄", new EventConsoleIcon("ClockAlertOutline")));
    }

    private void OnSuppressionCountsChanged()
    {
        var entry = RailEntries.FirstOrDefault(e => e.Key == SuppressionRailKey);
        if (entry is not null && Suppression is not null)
        {
            // 정본 SB L2361 은 '억제중' 건수 하나만 배지로 낸다 — 합계는 상태 띠에 있다.
            entry.Count = Suppression.SuppressingCount;
            entry.BadCount = 0;
        }
        NotifyOfPropertyChange(nameof(ListStatusText));
        // 툴바 [삭제] · [새 스케줄] · [갱신] 은 콘솔이 아니라 여기가 그린다 — 다시 읽게 한다.
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanReload));
    }

    private void RefreshRailCounts()
    {
        var detection = EventRailCounter.Reportable(DetectionPanelViewModel.ViewModelProvider.Select(r => r.IsActionReported));
        var malfunction = EventRailCounter.Reportable(MalfunctionPanelViewModel.ViewModelProvider.Select(r => r.IsActionReported));
        var connection = EventRailCounter.PlainCount(ConnectionPanelViewModel.ViewModelProvider.Count);
        var action = EventRailCounter.PlainCount(ActionPanelViewModel.ViewModelProvider.Count);

        Apply(DetectionRailKey, detection, "sensor");
        Apply(MalfunctionRailKey, malfunction, "mal");
        Apply(ConnectionRailKey, connection, "con");
        Apply(ActionRailKey, action, "act");

        // 미조치는 목록을 실제로 불러와야 세진다 — 서버 요약에는 그 숫자가 없다.
        // 아직 한 번도 안 열었으면 0 이 아니라 "—" 을 보인다(0 은 거짓이다).
        var counted = detection.Count > 0 || malfunction.Count > 0;
        OpenCountText = counted ? $"{EventRailCounter.OpenTotal(detection, malfunction)}건" : "—";
        FaultCountText = counted ? $"{EventRailCounter.FaultInProgress(malfunction)}건" : "—";
        OpenCount = EventRailCounter.OpenTotal(detection, malfunction);
        FaultCount = EventRailCounter.FaultInProgress(malfunction);

        void Apply(string key, RailBadge badge, string? summaryKey = null)
        {
            var entry = RailEntries.FirstOrDefault(e => e.Key == key);
            if (entry is null) return;

            // 그 목록을 아직 한 번도 불러오지 않았으면 서버 요약의 건수를 보인다
            // — “0” 은 거짓이고, 미조치(▲n)는 목록을 열어야 알 수 있다.
            var count = badge.Count;
            if (count == 0 && summaryKey is not null && Overview.SummaryCounts.TryGetValue(summaryKey, out var fromSummary))
                count = fromSummary;

            entry.Count = count;
            entry.BadCount = badge.BadCount;
        }
    }

    /// <summary>칩의 선택 표시를 기간과 맞춘다.</summary>
    private void SyncPeriodChips()
    {
        foreach (var option in PeriodOptions) option.IsSelected = option.Name == _period;
    }

    /// <summary>레일 아래 요약 — 목록을 열기 전엔 "—".</summary>
    public string OpenCountText { get => _openCountText; private set { _openCountText = value; NotifyOfPropertyChange(); } }
    public string FaultCountText { get => _faultCountText; private set { _faultCountText = value; NotifyOfPropertyChange(); } }

    public int OpenCount { get => _openCount; private set { _openCount = value; NotifyOfPropertyChange(); } }
    public int FaultCount { get => _faultCount; private set { _faultCount = value; NotifyOfPropertyChange(); } }
    #endregion

    #region - 목록 -
    public IEnumerable? Rows => _current?.Rows;
    public bool IsListVisible => _current is not null;
    public bool IsOverview => _current is null && !IsSuppressionRail;

    /// <summary>억제 스케줄 레일인가 — 목록 · 상세 · 툴바가 통째로 바뀐다.</summary>
    public bool IsSuppressionRail => _railKey == SuppressionRailKey && Suppression is not null;

    /// <summary>억제 스케줄 콘솔(주입이 없으면 null — 레일도 서지 않는다).</summary>
    public SuppressionConsoleViewModel? Suppression { get; }

    /// <summary>툴바 [추가] 의 글자 — 억제 레일에서는 '새 스케줄'(정본 SB L2392).</summary>
    public string AddButtonText => IsSuppressionRail ? "새 스케줄" : "이벤트 추가";

    /// <summary>검색 칸을 낼 것인가 — 억제 목록은 서버 검색이 없다(상태 칩으로 거른다).</summary>
    public bool ShowSearch => IsListVisible && !IsSuppressionRail;

    /// <summary>기간 칩을 낼 것인가 — 억제 스케줄은 부제에서도 기간을 뺀다(정본 SB L2396).</summary>
    public bool ShowPeriodChips => !IsSuppressionRail;

    /// <summary>지금 레일의 종류 — 상세 · 트레이 판정에 쓴다.</summary>
    public EventDetailKind CurrentKind => _railKey switch
    {
        MalfunctionRailKey => EventDetailKind.Malfunction,
        ConnectionRailKey => EventDetailKind.Connection,
        ActionRailKey => EventDetailKind.Action,
        _ => EventDetailKind.Detection,
    };

    public bool IsDetectionRail => _railKey == DetectionRailKey;
    public bool IsMalfunctionRail => _railKey == MalfunctionRailKey;
    public bool IsConnectionRail => _railKey == ConnectionRailKey;
    public bool IsActionRail => _railKey == ActionRailKey;

    /// <summary>탐지 · 장애에서만 핸들을 끌 수 있다 — 연결 · 조치는 조치보고 원본이 아니다.</summary>
    public bool CanDragToTray => _railKey is DetectionRailKey or MalfunctionRailKey && CanReport;

    public string ListStatusText => IsSuppressionRail
        ? Suppression!.StatusLineText
        : _current is null
        ? $"불러온 {Overview.Total}건"
        : $"불러온 {_current.LoadedCountText} · 선택 {SelectedRows.Count}건";

    public IReadOnlyList<object> SelectedRows { get; private set; } = Array.Empty<object>();

    /// <summary>그리드가 선택을 알린다.</summary>
    public void SetSelection(IReadOnlyList<object> rows)
    {
        SelectedRows = rows ?? Array.Empty<object>();
        _current?.Select(SelectedRows);

        var actionCount = SelectedRows.Count == 1 && SelectedRows[0] is ExEventViewModel ex && ex.IsActionReported ? 1 : 0;
        DetailView.Load(CurrentKind, SelectedRows, CanEdit, CanReport, actionCount);

        NotifyOfPropertyChange(nameof(SelectedRows));
        NotifyOfPropertyChange(nameof(ListStatusText));
        NotifyOfPropertyChange(nameof(CanQueueSelection));
        NotifyOfPropertyChange(nameof(QueueButtonText));
        NotifyOfPropertyChange(nameof(CanDelete));
    }

    private void AttachRows(IEventConsoleSource source)
    {
        DetachRows();
        _attached = source;
        source.RowsChanged.CollectionChanged += OnRowsChanged;
        NotifyOfPropertyChange(nameof(Rows));
        NotifyOfPropertyChange(nameof(ListStatusText));
    }

    private void DetachRows()
    {
        if (_attached is not null) _attached.RowsChanged.CollectionChanged -= OnRowsChanged;
        _attached = null;
        SelectedRows = Array.Empty<object>();
        NotifyOfPropertyChange(nameof(Rows));
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshRailCounts();
        NotifyOfPropertyChange(nameof(ListStatusText));
    }
    #endregion

    #region - 툴바 · 기간 -
    /// <summary>기간 칩 — 오늘 · 24시간 · 7일 · 직접(정본 L1009-1011). 드래그의 폴백이기도 하다.</summary>
    public IReadOnlyList<EventPeriodOption> PeriodOptions { get; } =
        new[] { "오늘", "24시간", "7일", "직접" }.Select(n => new EventPeriodOption(n)).ToList();

    public string Period
    {
        get => _period;
        set
        {
            if (_period == value) return;
            _period = value ?? "24시간";
            SyncPeriodChips();
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsCustomPeriod));
            NotifyOfPropertyChange(nameof(ShowCustomPeriod));
            ApplyPeriod();
        }
    }

    public bool IsCustomPeriod => _period == "직접";

    /// <summary>직접 지정 두 칸을 낼 것인가 — 억제 레일에서는 기간 자체가 없다.</summary>
    public bool ShowCustomPeriod => IsCustomPeriod && ShowPeriodChips;

    public DateTime StartDate
    {
        get => _startDate;
        set { _startDate = value; NotifyOfPropertyChange(); EndDateDisplay = value; }
    }

    public DateTime EndDate
    {
        get => _endDate;
        set { _endDate = value; NotifyOfPropertyChange(); }
    }

    public DateTime EndDateDisplay
    {
        get => _endDateDisplay;
        set { _endDateDisplay = value; NotifyOfPropertyChange(); }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    public string Subtitle => IsSuppressionRail
        ? "억제 스케줄"
        : _current is null
        ? $"개요 · {Period}"
        : $"{RailEntries.FirstOrDefault(e => e.Key == _railKey)?.Label ?? string.Empty} 내역 · {Period}";

    public bool CanReload => IsSuppressionRail ? Suppression!.CanReload : _current is null || !_current.IsBusy;

    /// <summary>기간 칩을 눌렀다 — 활성 탭 한 곳만 다시 부른다(나머지는 캐시만 버린다).</summary>
    private void ApplyPeriod()
    {
        var now = DateTime.Now;
        switch (_period)
        {
            case "오늘": StartDate = now.Date; EndDate = now; break;
            case "24시간": EndDate = now; StartDate = now.AddDays(-1); break;
            case "7일": EndDate = now; StartDate = now.AddDays(-7); break;
            default: return;        // 직접 — 두 칸을 사람이 정하고 [갱신] 을 누른다
        }
        Reload();
    }

    private void PushDates()
    {
        DataChartPanelViewModel.SetDate(StartDate, EndDate);
        foreach (var source in _sources.Values)
        {
            source.SetDate(StartDate, EndDate);
            source.InvalidateCache();
        }
    }

    /// <summary>수동 이벤트 추가 — 기존 패널의 [추가] 경로 그대로(권한 검사 포함).</summary>
    public bool CanAdd => IsSuppressionRail ? Suppression!.CanAdd : _current is not null && !_current.IsBusy && _railKey switch
    {
        DetectionRailKey => DetectionPanelViewModel.CanInsertEvent,
        MalfunctionRailKey => MalfunctionPanelViewModel.CanInsertEvent,
        ConnectionRailKey => ConnectionPanelViewModel.CanInsertEvent,
        ActionRailKey => ActionPanelViewModel.CanInsertEvent,
        _ => false,
    };

    public string AddBlockedReason => IsSuppressionRail
        ? Suppression!.AddBlockedReason
        : _current is null
        ? "개요에서는 이벤트를 추가하지 않습니다 — 내역을 먼저 고르세요."
        : "권한이 없습니다.";

    /// <summary>선택한 행 삭제 — 패널이 확인 팝업을 띄우고, 취소하면 아무 일도 없다.</summary>
    public bool CanDelete => IsSuppressionRail ? Suppression!.CanDeleteSelected : _current is not null && !_current.IsBusy && SelectedRows.Count > 0 && _railKey switch
    {
        DetectionRailKey => DetectionPanelViewModel.CanDeleteEvent,
        MalfunctionRailKey => MalfunctionPanelViewModel.CanDeleteEvent,
        ConnectionRailKey => ConnectionPanelViewModel.CanDeleteEvent,
        ActionRailKey => ActionPanelViewModel.CanDeleteEvent,
        _ => false,
    };

    public string DeleteBlockedReason => IsSuppressionRail
        ? "삭제할 취소 · 종료 행을 체크하세요."
        : SelectedRows.Count == 0
        ? "지울 행을 먼저 고르세요."
        : "권한이 없습니다.";

    public void Add()
    {
        if (!CanAdd || !Detail.Guard.TryNavigate(ConsoleNavigation.BeginCreate)) return;
        if (IsSuppressionRail) { Suppression!.AddNew(); return; }
        _current!.Insert();
        StatusText = "새 행을 더했습니다 — 저장하기 전까지는 서버에 가지 않습니다.";
    }

    public void Delete()
    {
        if (!CanDelete) return;
        if (IsSuppressionRail) { _ = Suppression!.DeleteSelectedAsync(); return; }
        _current!.Delete();
    }

    /// <summary>억제 목록의 [모두 정리] — 취소 · 종료 행 일괄 하드삭제(확인 팝업이 먼저 뜬다).</summary>
    public void CleanupSuppression() => _ = Suppression?.CleanupAllAsync();

    /// <summary>[갱신] — 기간을 밀어 넣고 지금 보고 있는 것 하나만 다시 부른다.</summary>
    public void Reload()
    {
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.Refresh)) return;

        if (IsSuppressionRail)
        {
            Suppression!.Reload();
            NotifyOfPropertyChange(nameof(Subtitle));
            NotifyOfPropertyChange(nameof(CanReload));
            return;
        }

        PushDates();
        if (_current is null) DataChartPanelViewModel.ClickSearch();
        else _current.Search();

        NotifyOfPropertyChange(nameof(Subtitle));
        NotifyOfPropertyChange(nameof(CanReload));
    }
    #endregion

    #region - 상세 -
    public ConsoleDetailPresenter Detail { get; }
    public EventDetailViewModel DetailView { get; }

    /// <summary>[적용] — 손댄 판정을 행에 쓰고 패널의 기존 저장 경로를 부른다.</summary>
    public void Apply()
    {
        var written = DetailView.WriteBack();
        if (written == 0)
        {
            Detail.Settle("바뀐 칸이 없습니다");
            return;
        }

        if (_current?.Save() == true) Detail.Settle(ConsoleDetailStateMachine.AppliedMessage(SelectedRows.Count, written));
        else Detail.Settle("저장을 시작하지 못했습니다 — 권한이나 진행 중인 작업을 확인하세요");
    }

    /// <summary>[되돌리기] — 서버 호출 0.</summary>
    public void Revert()
    {
        DetailView.RevertEdits();
        Detail.Settle("되돌렸습니다");
    }

    private void OnNavigationBlocked(object? sender, ConsoleNavigation navigation)
        => StatusText = ConsoleDetailStateMachine.BlockedNotice;
    #endregion

    #region - 개요 -
    public EventOverviewViewModel Overview { get; }

    private async void OnDashboardUpdated(DateTime start, DateTime end)
    {
        try
        {
            var dashboard = DataChartPanelViewModel.LastDashboardDto;
            Overview.IsLoading = false;
            Overview.Load(dashboard, start, end);
            NotifyOfPropertyChange(nameof(ListStatusText));

            if (dashboard is null) return;

            // 기존 요약 뷰모델(센서 · 카메라 KPI)도 같은 DTO 로 계속 채운다 — 값의 원천을 둘로 만들지 않는다.
            if (EventInfoViewModel.IsActive) await EventInfoViewModel.DeactivateAsync(true);
            await EventInfoViewModel.ActivateAsync();
            EventInfoViewModel.SetData(start, end, new[] { "DET", "MAL", "CON", "ACT" });
            await EventInfoViewModel.DataInitializeFromStats(dashboard.Summary, dashboard.ByDevice, EventInfoViewModel.CancelAndRestart());

            if (CameraEventInfoViewModel.IsActive) await CameraEventInfoViewModel.DeactivateAsync(true);
            await CameraEventInfoViewModel.ActivateAsync();
            CameraEventInfoViewModel.SetData(start, end);
            await CameraEventInfoViewModel.DataInitializeFromStats(dashboard.Summary, CameraEventInfoViewModel.CancelAndRestart());
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventConsole] 개요 갱신 실패: {ex.Message}");
        }
    }

    /// <summary>추이 차트에서 기간을 끌어 골랐다 — 통계 재조회 1회(정본 DW L427).</summary>
    private void OnTrendRangeSelected(TrendRange range)
    {
        if (!range.IsCommittable) return;

        _period = "직접";
        SyncPeriodChips();
        NotifyOfPropertyChange(nameof(Period));
        NotifyOfPropertyChange(nameof(IsCustomPeriod));
        StartDate = range.From;
        EndDate = range.To;
        StatusText = $"기간을 {range.Label()} 로 바꿨습니다";
        Reload();
    }

    /// <summary>막대 · 조각을 눌렀다 — 그 장비의 내역으로 내려간다.</summary>
    private async void OnDrillRequested(string railKey, string query)
    {
        SearchText = query;
        await SelectRailAsync(railKey == "det" ? DetectionRailKey : railKey);
    }
    #endregion

    #region - 조치 트레이 -
    public ActionTrayViewModel Tray { get; }
    public ActionTrayDropHandler TrayDrop { get; }

    /// <summary>드래그의 키보드 · 버튼 폴백 — 같은 담기 함수를 부른다(정본 DW L423).</summary>
    public bool CanQueueSelection => CanDragToTray && SelectedRows.Count > 0 && !Tray.IsApplying;

    public string QueueButtonText => SelectedRows.Count > 1
        ? $"{SelectedRows.Count}건 조치보고"
        : "조치보고";

    public void QueueSelection()
    {
        if (!CanQueueSelection) return;
        StatusText = TrayDrop.Queue(SelectedRows);
    }

    public async void ApplyTray()
    {
        try
        {
            var summary = await Tray.ApplyAsync();
            StatusText = summary.ToMessage();
            RefreshRailCounts();
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventConsole] 조치 트레이 적용 실패: {ex.Message}");
            StatusText = $"적용에 실패했습니다 — {ex.Message}";
        }
    }

    public void RevertTray() => StatusText = RevertTrayCore();

    private string RevertTrayCore()
    {
        Tray.Revert();
        return Tray.StatusLine;
    }

    public void CancelTray() => Tray.Cancel();

    private void OnTrayCompleted(string line) => StatusText = line;

    /// <summary>
    /// 조치 한 건을 실제로 보낸다 — <b>기존 경로 그대로</b>(임시 카드 뷰모델의 <c>SendAction</c>).
    /// 멱등 가드 · 보고 통지 · NATS 발행이 함께 가야 하므로 새 API 호출부를 만들지 않는다.
    /// </summary>
    private async Task<DraftOutcome> SendActionAsync(ActionTrayCandidate candidate, string content, CancellationToken token)
    {
        if (token.IsCancellationRequested) throw new OperationCanceledException(token);

        var user = ResolveUserName();
        var model = FindOriginModel(candidate);
        if (model is null) return DraftOutcome.Missing;      // 다른 곳에서 지워졌다

        // 행 뷰모델과 독립된 임시 카드 뷰모델을 세운다 - 우클릭 조치보고가 쓰는 것과 똑같은 경로다.
        var ok = candidate.Kind == ActionTrayDrop.KindDetection
            ? await new DetectionEventCardViewModel(_eventAggregator, _log!, (IDetectionEventModel)model)
                    .SendAction(content, user).ConfigureAwait(true)
            : await new MalfunctionEventCardViewModel(_eventAggregator, _log!, (IMalfunctionEventModel)model)
                    .SendAction(content, user).ConfigureAwait(true);
        return ok ? DraftOutcome.Applied : DraftOutcome.Failed;
    }

    private IExEventModel? FindOriginModel(ActionTrayCandidate candidate)
    {
        if (candidate.Kind == ActionTrayDrop.KindDetection)
            return DetectionPanelViewModel.ViewModelProvider
                .FirstOrDefault(r => r.Model?.Id == candidate.EventId)?.Model as IExEventModel;

        return MalfunctionPanelViewModel.ViewModelProvider
            .FirstOrDefault(r => r.Model?.Id == candidate.EventId)?.Model as IExEventModel;
    }

    private string ResolveUserName()
    {
        try
        {
            var account = IoC.Get<IAccountModel>();
            return $"{account?.Username}({account?.EmployeeNumber})";
        }
        catch (Exception ex)
        {
            _log?.Warning($"[EventConsole] 계정을 읽지 못했습니다: {ex.Message}");
            return "알 수 없음";
        }
    }
    #endregion

    #region - 권한 -
    /// <summary>판정을 고칠 수 있는가 — 패널이 이미 계산한 값을 그대로 쓴다(권한 서비스를 두 번 해석하지 않는다).</summary>
    public bool CanEdit => _railKey switch
    {
        MalfunctionRailKey => MalfunctionPanelViewModel.CanSaveEvent,
        ConnectionRailKey => false,             // 연결은 전부 발생 기록
        ActionRailKey => ActionPanelViewModel.CanSaveEvent,
        _ => DetectionPanelViewModel.CanSaveEvent,
    };

    /// <summary>조치보고를 할 수 있는가.</summary>
    public bool CanReport => _railKey switch
    {
        MalfunctionRailKey => MalfunctionPanelViewModel.CanReportRow,
        DetectionRailKey => DetectionPanelViewModel.CanReportRow,
        _ => false,
    };
    #endregion

    #region - Processes -
    private void OnSourceBusyEnded(object? sender, EventArgs e)
    {
        RefreshRailCounts();
        NotifyOfPropertyChange(nameof(ListStatusText));
        NotifyOfPropertyChange(nameof(CanReload));
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanDelete));
    }

    private void RaiseShellState()
    {
        NotifyOfPropertyChange(nameof(Rows));
        NotifyOfPropertyChange(nameof(IsListVisible));
        NotifyOfPropertyChange(nameof(IsOverview));
        NotifyOfPropertyChange(nameof(IsDetectionRail));
        NotifyOfPropertyChange(nameof(IsMalfunctionRail));
        NotifyOfPropertyChange(nameof(IsConnectionRail));
        NotifyOfPropertyChange(nameof(IsActionRail));
        NotifyOfPropertyChange(nameof(IsSuppressionRail));
        NotifyOfPropertyChange(nameof(AddButtonText));
        NotifyOfPropertyChange(nameof(ShowSearch));
        NotifyOfPropertyChange(nameof(ShowPeriodChips));
        NotifyOfPropertyChange(nameof(ShowCustomPeriod));
        NotifyOfPropertyChange(nameof(CurrentKind));
        NotifyOfPropertyChange(nameof(CanDragToTray));
        NotifyOfPropertyChange(nameof(CanEdit));
        NotifyOfPropertyChange(nameof(CanReport));
        NotifyOfPropertyChange(nameof(CanQueueSelection));
        NotifyOfPropertyChange(nameof(QueueButtonText));
        NotifyOfPropertyChange(nameof(ListStatusText));
        NotifyOfPropertyChange(nameof(Subtitle));
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(AddBlockedReason));
        NotifyOfPropertyChange(nameof(DeleteBlockedReason));
    }

    /// <summary>시험이 결정론적으로 목록을 갈아 끼우는 이음매 — 제품 경로는 쓰지 않는다.</summary>
    internal void UseSource(string railKey, IEventConsoleSource source)
    {
        _sources[railKey] = source;
        if (_railKey == railKey) { _current = source; AttachRows(source); }
    }
    #endregion

    #region - Properties -
    public EventTabControlViewModel TabControlViewModel { get; }
    public DetectionEventPanelViewModel DetectionPanelViewModel { get; }
    public MalfunctionEventPanelViewModel MalfunctionPanelViewModel { get; }
    public ConnectionEventPanelViewModel ConnectionPanelViewModel { get; }
    public ActionEventPanelViewModel ActionPanelViewModel { get; }
    public EventInfoViewModel EventInfoViewModel { get; }
    public CameraEventInfoViewModel CameraEventInfoViewModel { get; }
    public DataChartPanelViewModel DataChartPanelViewModel { get; }
    public EventProvider? EventProvider { get; private set; }
    #endregion

    #region - Attributes -
    private readonly Dictionary<string, IEventConsoleSource> _sources;
    private IEventConsoleSource? _current;
    private IEventConsoleSource? _attached;
    private string _railKey = OverviewRailKey;
    private bool _isSwitching;
    private int _openCount;
    private int _faultCount;
    private string _openCountText = "—";
    private string _faultCountText = "—";
    private string _period = "24시간";
    private string _searchText = string.Empty;
    private string _statusText = string.Empty;
    private DateTime _startDate;
    private DateTime _endDate;
    private DateTime _endDateDisplay;
    #endregion
}

/// <summary>기간 칩 한 칸 — 선택 표시를 스스로 든다(라디오 버튼이 직접 묶을 것을 가진다).</summary>
public sealed class EventPeriodOption : Caliburn.Micro.PropertyChangedBase
{
    private bool _isSelected;

    public EventPeriodOption(string name) => Name = name;

    public string Name { get; }

    public bool IsSelected { get => _isSelected; set { _isSelected = value; NotifyOfPropertyChange(); } }

    public override string ToString() => Name;
}
