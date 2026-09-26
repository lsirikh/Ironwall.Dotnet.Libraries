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
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
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
                                     , IHandle<Ironwall.Dotnet.Libraries.Events.Ui.Models.DetectionReportedMessageModel>
                                     , IHandle<Ironwall.Dotnet.Libraries.Events.Ui.Models.MalfunctionReportedMessageModel>
                                     , IHandle<CallCloseEventConsoleMessageModel>
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
                                // N-13 mapping workbench — 늦게 푼다(아래 _mapping 주석 참조). 없어도 콘솔은 뜬다.
                                , Lazy<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.IMappingWorkbenchLauncher>? mappingLauncher = null
                                // ── N-08: 억제 스케줄 레일. 선택 주입이다 — 안 받으면 레일이 서지 않고 나머지는 그대로다.
                                //    (필수 인자로 바꾸면 이 뷰모델을 세우는 모든 곳 · 가짜가 한꺼번에 깨진다)
                                , IEventSuppressionApiService? suppressionApi = null
                                , DeviceProvider? deviceProvider = null
                                , DeviceGroupProvider? deviceGroupProvider = null
                                , IClock? clock = null
                                // 조치 트레이 문구 — 조치보고 문구 관리 목록(서버). 없으면 기본 문구로 간다.
                                , IActionReportPhraseSource? phraseSource = null
                                ) : base(eventAggregator, log)
    {
        _mappingFactory = mappingLauncher;
        _phrases = phraseSource ?? new ActionReportPhraseSource(ResolveTemplateApi, log);
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
                () => detectionEventPanelViewModel.LoadedCountText,
                () => detectionEventPanelViewModel.HasMorePages,
                detectionEventPanelViewModel.ClickCancel),

            [MalfunctionRailKey] = new EventConsoleSource<MalfunctionEventViewModel>(
                malfunctionEventPanelViewModel,
                h => malfunctionEventPanelViewModel.UpdateAction += (_, _) => h(),
                malfunctionEventPanelViewModel.SetDate,
                malfunctionEventPanelViewModel.ClickSearch,
                malfunctionEventPanelViewModel.InvalidateCache,
                () => malfunctionEventPanelViewModel.LoadedCountText,
                () => malfunctionEventPanelViewModel.HasMorePages,
                malfunctionEventPanelViewModel.ClickCancel),

            [ConnectionRailKey] = new EventConsoleSource<ConnectionEventViewModel>(
                connectionEventPanelViewModel,
                h => connectionEventPanelViewModel.UpdateAction += (_, _) => h(),
                connectionEventPanelViewModel.SetDate,
                connectionEventPanelViewModel.ClickSearch,
                connectionEventPanelViewModel.InvalidateCache,
                () => connectionEventPanelViewModel.LoadedCountText,
                () => connectionEventPanelViewModel.HasMorePages,
                connectionEventPanelViewModel.ClickCancel),

            [ActionRailKey] = new EventConsoleSource<ActionEventViewModel>(
                actionEventPanelViewModel,
                h => actionEventPanelViewModel.UpdateAction += (_, _) => h(),
                actionEventPanelViewModel.SetDate,
                actionEventPanelViewModel.ClickSearch,
                actionEventPanelViewModel.InvalidateCache,
                () => actionEventPanelViewModel.LoadedCountText,
                () => actionEventPanelViewModel.HasMorePages,
                actionEventPanelViewModel.ClickCancel),
        };

        Detail = new ConsoleDetailPresenter();
        // 조치 내역은 이미 있는 원본별 조회 API 를 열 때 한 번 부른다(정본 E-D4) — API 는 늦게 해석한다.
        DetailView = new EventDetailViewModel(Detail, new EventActionHistoryViewModel(ResolveEventApi, log));
        DetailView.ActionRequested += OnDetailAction;
        Overview = new EventOverviewViewModel();
        Tray = new ActionTrayViewModel(SendActionAsync);
        Tray.ApplyPhrases(_phrases.LastKnown);
        Tray.PropertyChanged += (_, _) =>
        {
            NotifyOfPropertyChange(nameof(IsTrayVisible));
            NotifyOfPropertyChange(nameof(IsTrayCollapsed));
            NotifyOfPropertyChange(nameof(TraySummaryText));
        };
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

        // ★ 기간 칩의 눌림 표시는 Period 의 setter 안에서만 맞춰졌다 — 처음 뜰 때는 아무도 그 setter 를
        //   지나가지 않아 넷 다 안 눌린 채로 떴다(부제는 "24시간" 인데 화면엔 고른 표시가 없었다).
        SyncPeriodChips();

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

        RefreshMappingEntry();      // N-13 mapping workbench — 판본·권한은 열릴 때마다 다시 본다
        _closeConfirmed = false;

        // 문구는 열 때마다 다시 읽는다 — 조치보고 문구 콘솔에서 방금 고친 목록이 바로 보이도록(창을 막지 않는다).
        _ = RefreshPhrasesAsync();

        await SwitchRailAsync(_railKey, force: true);
    }

    /// <summary>
    /// 창을 닫아도 되는가 — 보내지 않은 조치 트레이 · 적용하지 않은 상세 판정 · 억제 서랍 초안이 있으면
    /// <b>먼저 묻는다</b>(완성도 감사 E-2 #9). 확인 팝업은 다른 콘솔과 같은 길(<see cref="OpenConfirmPopupMessageModel"/>)이고,
    /// [확인] 이 오면 <see cref="CallCloseEventConsoleMessageModel"/> 으로 돌아와 버리고 닫는다.
    /// </summary>
    /// <remarks>
    /// ⚠ 호스트의 창 틀(EventPanelViewModel)이 이 판정을 물어야 실제로 막힌다 — 그 한 줄은 호스트 담당이다.
    /// 묻지 않는 호스트에서는 <see cref="OnDeactivateAsync"/> 가 닫힌 뒤 버린 것을 알린다(예전 동작).
    /// </remarks>
    public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        var pending = PendingWorkSummary();
        if (_closeConfirmed || pending is null) return true;

        await _eventAggregator!.PublishOnUIThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "이벤트 창 닫기",
            Explain = $"{pending}\n닫으면 이 내용은 사라집니다. 버리고 닫을까요?",
            MessageModel = new CallCloseEventConsoleMessageModel(),
        }, cancellationToken);
        return false;
    }

    /// <summary>닫으면 사라질 것 — 없으면 null. 확인 문장에 그대로 쓴다.</summary>
    public string? PendingWorkSummary()
    {
        var parts = new List<string>();
        if (Tray.HasEntries) parts.Add($"보내지 않은 조치보고 {Tray.Count}건");
        if (Detail.IsDirty) parts.Add("적용하지 않은 상세 변경");
        if (Suppression?.Drawer is { IsOpen: true, IsDirty: true }) parts.Add("저장하지 않은 억제 스케줄");
        return parts.Count == 0 ? null : string.Join(" · ", parts) + "이(가) 있습니다.";
    }

    /// <summary>닫기 확인에서 [확인] — 이번 닫기만 묻지 않고 통과시킨다(다음 열기에서 다시 초기화).</summary>
    public async Task HandleAsync(CallCloseEventConsoleMessageModel message, CancellationToken cancellationToken)
    {
        _closeConfirmed = true;
        await _eventAggregator!.PublishOnUIThreadAsync(new ClosePopupMessageModel(), cancellationToken);
        await _eventAggregator.PublishOnUIThreadAsync(new ClosePanelMessageModel(), cancellationToken);
    }

    private bool _closeConfirmed;

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
        // (R7) 담은 것을 소리 없이 버리지 않는다 — 닫기 확인을 거치지 않고 닫혔으면(호스트가 CanCloseAsync 를 묻지 않는 경로)
        //      몇 건을 버렸는지 뒤늦게라도 알린다. 확인을 받고 닫은 것이면 다시 말하지 않는다.
        if (Tray.HasEntries && !_closeConfirmed)
        {
            var discarded = Tray.Count;
            _log?.Warning($"[EventConsole] 조치 트레이에 남은 Draft {discarded}건을 창을 닫으며 버렸습니다(확인 없이 닫힘)");
            await _eventAggregator!.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "조치 트레이 안내",
                Explain = $"보내지 않은 조치보고 {discarded}건이 창을 닫으면서 사라졌습니다. 필요하면 다시 담아 보내세요."
            }, cancellationToken);
        }
        _closeConfirmed = false;
        Tray.Revert();
        Detail.Reset();
        DetailView.Load(EventDetailKind.Overview, Array.Empty<object>(), true, true, 0);
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
            // 관찰하지 않은 Task 는 예외를 숨긴다 — 끝날 때 로그로 남긴다(R12).
            SelectRailAsync(value.Key).ContinueWith(
                t => _log?.Error($"[EventConsole] 레일 전환 실패: {t.Exception?.GetBaseException().Message}"),
                TaskContinuationOptions.OnlyOnFaulted);
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

        // 억제 서랍의 미적용 변경도 같은 문을 지난다 — 여기서 묻지 않으면 레일을 옮기는 순간 초안이 말없이 사라진다
        // (정본 SB L2372 switchTab → L2665 dirtyBlock 이 탭 전환까지 막는다).
        if (IsSuppressionRail && !Suppression!.TryLeave())
        {
            StatusText = ConsoleDetailStateMachine.BlockedNotice;
            NotifyOfPropertyChange(nameof(SelectedRail));   // 레일 선택을 되돌린다
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

            _searchText = string.Empty;          // 레일을 바꾸면 거르기도 처음으로
            NotifyOfPropertyChange(nameof(SearchText));
            // 앞 레일의 한 줄(“적용 완료 …”)을 다음 레일로 끌고 가지 않는다(완성도 감사 E-2 #3).
            StatusText = string.Empty;
            RebuildChips();                      // 칩은 레일마다 다르다(정본 L2310-2313)

            // (D-26) AttachRows 가 그리드에 새 ItemsSource(ListCollectionView)를 물리면 WPF 가
            // CurrentItem(=첫 행)으로 그리드를 자동 동기화해 SelectionChanged 를 스스로 울린다 —
            // 그 경로로 SetSelection(rows=[첫 행])이 우리 모르게 먼저 불려 SelectedRows 가 1건으로
            // 차 버린다. 바로 다음 줄의 Detail 초기화는 상세 칸만 비우고 SelectedRows 는 못 건드려
            // "바닥 줄 선택 1건 vs 상세 선택 없음" 불일치가 남는다. 레일을 바꿨으면 선택도
            // 명시적으로 함께 비운다(그리드의 자동 동기화를 신뢰하지 않는다).
            SelectedRows = Array.Empty<object>();
            _current?.Select(SelectedRows);      // 자동 동기화가 켠 행의 IsSelected 도 함께 끈다
            DetailView.Load(CurrentKind, SelectedRows, CanEdit, CanReport, 0);
            NotifyOfPropertyChange(nameof(SelectedRows));
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
        // 억제 스케줄 — 배지는 등록된 스케줄 수다. 한 번도 불러오기 전에는 숨긴다("0" 은 거짓이다 — 실창 검토 #23).
        if (Suppression is not null)
            RailEntries.Add(new ConsoleRailEntry(SuppressionRailKey, "억제 스케줄", new EventConsoleIcon("ClockAlertOutline"))
            {
                ShowCount = Suppression.ScheduleTotal is not null,
                Count = Suppression.ScheduleTotal ?? 0,
            });
    }

    private void OnSuppressionCountsChanged()
    {
        var entry = RailEntries.FirstOrDefault(e => e.Key == SuppressionRailKey);
        if (entry is not null && Suppression is not null)
        {
            // 다른 레일처럼 목록 건수를 싣는다 — '억제중 0' 이 목록 55건 옆에 떠 "비었다" 로 읽혔다(실창 검토 #23).
            // 억제중 수는 상태 띠 · '억제중' 칩이 말한다. 아직 모르면 숫자를 숨긴다.
            entry.ShowCount = Suppression.ScheduleTotal is not null;
            entry.Count = Suppression.ScheduleTotal ?? 0;
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

        // (R10) 목록을 열기 전 폴백은 센서 + 카메라 둘 다다 — 탐지 목록은 두 출처를 한 줄로 섮는다.
        Apply(DetectionRailKey, detection, "sensor", "camera", "alert");   // 탐지 목록에는 사전 경보(Alert)도 들어 있다
        Apply(MalfunctionRailKey, malfunction, "mal");
        Apply(ConnectionRailKey, connection, "con");
        Apply(ActionRailKey, action, "act");

        // 미조치는 목록을 실제로 불러와야 세진다 — 서버 요약에는 그 숫자가 없다.
        // 아직 한 번도 안 열었으면 0 이 아니라 "—" 을 보인다(0 은 거짓이다).
        var counted = detection.Count > 0 || malfunction.Count > 0;
        OpenCountText = counted ? $"{EventRailCounter.OpenTotal(detection, malfunction)}건" : "—";
        FaultCountText = counted ? $"{EventRailCounter.FaultInProgress(malfunction)}건" : "—";
        // "—" 는 '빠진 값' 처럼 읽힌다(완성도 감사 E-2 #4) — 세기 전에는 줄 자체를 숨긴다.
        HasRailCounts = counted;
        OpenCount = EventRailCounter.OpenTotal(detection, malfunction);
        FaultCount = EventRailCounter.FaultInProgress(malfunction);

        void Apply(string key, RailBadge badge, params string[] summaryKeys)
        {
            var entry = RailEntries.FirstOrDefault(e => e.Key == key);
            if (entry is null) return;

            // 그 목록을 아직 한 번도 불러오지 않았으면 서버 요약의 건수를 보인다
            // — “0” 은 거짓이고, 미조치(▲n)는 목록을 열어야 알 수 있다.
            var count = badge.Count;
            if (count == 0 && summaryKeys.Length > 0)
            {
                var fromSummary = 0;
                foreach (var summaryKey in summaryKeys)
                    if (Overview.SummaryCounts.TryGetValue(summaryKey, out var n)) fromSummary += n;
                count = fromSummary;
            }

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
    public int FaultCount { get => _faultCount; private set { _faultCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasFaultInProgress)); } }

    /// <summary>
    /// 진행 중인 장애가 있는가 — 레일 아래 "장애 진행 N건" 을 경고색으로 칠할 조건. 0건을 빨갛게 칠하면
    /// 아무 일 없는데도 경보처럼 읽힌다(GIS 실창 육안 검토 #18).
    /// </summary>
    public bool HasFaultInProgress => EventRailCounter.IsFaultAlarm(_faultCount);

    /// <summary>미조치 · 장애 진행 수를 셀 수 있게 되었는가(탐지나 장애 목록을 한 번이라도 불러왔는가).</summary>
    public bool HasRailCounts
    {
        get => _hasRailCounts;
        private set { if (_hasRailCounts == value) return; _hasRailCounts = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(ShowRailFooter)); }
    }
    private bool _hasRailCounts;

    /// <summary>레일 아래 요약 줄을 보일 것인가 — 센 뒤에만, 그리고 억제 스케줄 레일에서는 보이지 않는다(그 레일과 무관한 숫자다).</summary>
    public bool ShowRailFooter => HasRailCounts && !IsSuppressionRail;
    #endregion

    #region - 목록 -
    /// <summary>
    /// 그리드가 묶는 것 — 거르기가 걸린 <see cref="ICollectionView"/> 다(R2 · R4).
    /// 원본 콜렉션을 Clear/Add 하지 <b>않는다</b> — 그러면 선택과 무한 스크롤이 망가진다.
    /// </summary>
    public ICollectionView? Rows => _view;
    private ICollectionView? _view;

    /// <summary>지금 레일의 필터 칩들(정본 L2310-2313). 조치 내역엔 칩이 없다.</summary>
    public IReadOnlyList<EventFilterChipOption> FilterChips { get; private set; } = Array.Empty<EventFilterChipOption>();

    /// <summary>
    /// 거르기 탭 줄을 보일 것인가 — <b>목록이 있는 레일에서만</b>. 개요 · 억제 스케줄에서는 아무것도 거르지 않는데
    /// 누를 수 있게 떠 있었고, 억제 서랍의 이름 칸과 겹쳤다(완성도 감사 E-2 #1 · #2).
    /// </summary>
    public bool HasFilterChips => FilterChips.Count > 0 && IsListVisible;

    /// <summary>고른 칩. 기본값은 전체.</summary>
    public string FilterChipKey
    {
        get => _chipKey;
        private set
        {
            if (_chipKey == value) return;
            _chipKey = value;
            foreach (var chip in FilterChips) chip.IsSelected = chip.Key == _chipKey;
            NotifyOfPropertyChange();
            RefreshFilter();
        }
    }

    /// <summary>칩을 눌렀다(뷰가 부른다).</summary>
    public void SelectFilterChip(string key) => FilterChipKey = key ?? EventListFilter.ChipAll;

    /// <summary>거르기가 걸려 있는가 — 상태 띄 문구가 이걸 보고 솔직해진다.</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(_searchText) || _chipKey != EventListFilter.ChipAll;

    private void RebuildChips()
    {
        // 목록이 없는 레일(개요 · 억제 스케줄)은 칩이 없다 — CurrentKind 가 Overview 라 ChipsFor 도 빈 목록을 준다.
        var chips = IsListVisible ? EventListFilter.ChipsFor(CurrentKind) : Array.Empty<EventFilterChip>();
        FilterChips = chips.Select(c => new EventFilterChipOption(c.Key, c.Label)).ToList();
        _chipKey = EventListFilter.ChipAll;
        foreach (var chip in FilterChips) chip.IsSelected = chip.Key == _chipKey;
        NotifyOfPropertyChange(nameof(FilterChips));
        NotifyOfPropertyChange(nameof(HasFilterChips));
        NotifyOfPropertyChange(nameof(FilterChipKey));
    }

    /// <summary>거르기를 다시 돌린다. 선택은 살려 둔다 — 여전히 맞는 행은 그대로 골라져 있다.</summary>
    private void RefreshFilter()
    {
        _view?.Refresh();
        NotifyOfPropertyChange(nameof(IsFiltered));
        RaiseListState();
    }

    /// <summary>ICollectionView 의 거름망 — 판정은 순수 함수가 한다.</summary>
    private bool PassesFilter(object row)
        => EventListFilter.Matches(EventRowFactsFactory.From(row), _searchText, _chipKey);

    public bool IsListVisible => _current is not null;
    public bool IsOverview => _current is null && !IsSuppressionRail;

    /// <summary>상세 칸이 있는 화면인가 — 개요는 고를 행이 없어 상세 340 을 접는다(커널 ConsoleShell.IsDetailAvailable, 실창 육안 검토 #19).</summary>
    public bool IsDetailAvailable => !IsOverview;

    /// <summary>억제 스케줄 레일인가 — 목록 · 상세 · 툴바가 통째로 바뀐다.</summary>
    public bool IsSuppressionRail => _railKey == SuppressionRailKey && Suppression is not null;

    /// <summary>억제 스케줄 콘솔(주입이 없으면 null — 레일도 서지 않는다).</summary>
    public SuppressionConsoleViewModel? Suppression { get; }

    /// <summary>툴바 [추가] 의 글자 — 억제 레일에서는 '새 스케줄'(정본 SB L2392).</summary>
    public string AddButtonText => IsSuppressionRail ? "새 스케줄" : "이벤트 추가";

    /// <summary>
    /// 툴바 [추가] 를 보일 것인가 — <b>억제 스케줄 레일에서만</b>.
    /// </summary>
    /// <remarks>
    /// 이벤트 목록의 [이벤트 추가] 는 새로운-순 무한 목록의 <b>맨 끝</b>에 빈 행을 붙이고, 상세가 장비 · 발생시각을
    /// 잠가 채울 방법이 없었다(완성도 감사 E-2 #10). 이벤트는 장비가 만드는 발생 기록이다 — 운영자 콘솔에서는 감춘다
    /// (늘 꺼진 버튼으로 두지 않는다). 기존 패널 경로(<c>OnClickInsertButton</c>)는 그대로 둔다.
    /// </remarks>
    public bool ShowAdd => IsSuppressionRail;

    /// <summary>툴바 [삭제] 를 보일 것인가 — 개요에는 지울 행이 없다(완성도 감사 E-3 #7).</summary>
    public bool ShowDelete => IsListVisible || IsSuppressionRail;

    /// <summary>검색 칸을 낼 것인가 — 억제 목록은 서버 검색이 없다(상태 칩으로 거른다).</summary>
    public bool ShowSearch => IsListVisible && !IsSuppressionRail;

    /// <summary>기간 칩을 낼 것인가 — 억제 스케줄은 부제에서도 기간을 뺀다(정본 SB L2396).</summary>
    public bool ShowPeriodChips => !IsSuppressionRail;

    /// <summary>
    /// 지금 레일의 종류 — 상세 · 칩 판정에 쓴다. 개요 · 억제 스케줄은 <see cref="EventDetailKind.Overview"/> 다
    /// (예전엔 탐지로 떨어져 개요 상세가 "탐지 행을 고르면…" 을 말하고 탐지 칩이 떴다 — 완성도 감사 E-3 #6 · E-2 #1).
    /// </summary>
    public EventDetailKind CurrentKind => _railKey switch
    {
        DetectionRailKey => EventDetailKind.Detection,
        MalfunctionRailKey => EventDetailKind.Malfunction,
        ConnectionRailKey => EventDetailKind.Connection,
        ActionRailKey => EventDetailKind.Action,
        _ => EventDetailKind.Overview,
    };

    public bool IsDetectionRail => _railKey == DetectionRailKey;
    public bool IsMalfunctionRail => _railKey == MalfunctionRailKey;
    public bool IsConnectionRail => _railKey == ConnectionRailKey;
    public bool IsActionRail => _railKey == ActionRailKey;

    /// <summary>탐지 · 장애에서만 핸들을 끌 수 있다 — 연결 · 조치는 조치보고 원본이 아니다.</summary>
    public bool CanDragToTray => _railKey is DetectionRailKey or MalfunctionRailKey && CanReport;

    /// <summary>
    /// 트레이를 보일 것인가 — <b>담은 것이 있으면 어느 레일에서든 보인다</b>(N-07 R7).
    /// 숨기면 Draft 가 사라졌다 다시 나타나는 것처럼 보이고, 끌기 중에 생기는 드롭존은 잡힐 수도 없다.
    /// </summary>
    public bool IsTrayVisible => CanDragToTray || Tray.HasEntries;

    /// <summary>끌 수 없는 레일에서는 줄이진 요약만 보인다.</summary>
    public bool IsTrayCollapsed => !CanDragToTray && Tray.HasEntries;

    public string TraySummaryText => $"조치 트레이에 {Tray.Count}건이 있습니다. 탐지 · 장애 내역에서 [조치 적용]을 누르세요.";

    public string ListStatusText
    {
        get
        {
            // N-08: 억제 레일은 자기 상태 줄을 쓴다(기준 시각을 함께 적는다).
            if (IsSuppressionRail) return Suppression!.StatusLineText;

            if (_current is null) return $"불러온 {Overview.Total}건";
            if (!IsFiltered) return $"불러온 {_current.LoadedCountText} · 선택 {SelectedRows.Count}건";

            // 거르기는 불러온 행 위에서만 돌아간다 — 그 사실을 감추지 않는다.
            var shown = _view?.Cast<object>().Count() ?? 0;
            return EventListFilter.StatusLine(shown, _current.RowCount, SelectedRows.Count, true, _current.HasMorePages);
        }
    }

    /// <summary>
    /// 목록이 비었는가. 빈 칸을 <b>정말 비워 두지 않는다</b> — 아무것도 없는 본문은
    /// "한 건도 없다"와 "못 불러왔다"를 구별해 주지 못한다(상태 띠 한 줄로는 모자란다).
    /// 불러오는 중에는 비었다고 말하지 않는다.
    /// </summary>
    public bool IsListEmpty
        => _current is not null && !_current.IsBusy && _view?.Cast<object>().Any() != true;

    /// <summary>빈 목록의 제목 — 거르기 때문인지 원래 없는지를 가른다.</summary>
    public string EmptyStateTitle => IsFiltered ? "조건에 맞는 이벤트가 없습니다" : "표시할 이벤트가 없습니다";

    /// <summary>
    /// 빈 목록에서 다음에 할 일. 한 줄로 끝나게 짧게 쓴다 —
    /// WPF 는 한글을 음절 단위로 끊어, 긴 문장은 마지막 줄에 음절 하나만 남긴다.
    /// </summary>
    public string EmptyStateHint => IsFiltered
        ? "검색어를 지우거나 [전체]를 누르세요"
        : "기간을 넓히거나 [새로 불러오기]를 누르세요";

    /// <summary>목록 상태 한 벌 — 건수 줄과 빈 칸 안내는 늘 같은 순간에 바뀐다.</summary>
    private void RaiseListState()
    {
        NotifyOfPropertyChange(nameof(ListStatusText));
        NotifyOfPropertyChange(nameof(IsListEmpty));
        NotifyOfPropertyChange(nameof(EmptyStateTitle));
        NotifyOfPropertyChange(nameof(EmptyStateHint));
    }

    public IReadOnlyList<object> SelectedRows { get; private set; } = Array.Empty<object>();

    /// <summary>
    /// 그리드가 선택을 알린다. <b>미적용 변경이 있으면 받지 않는다</b>(FR-55 · R6) —
    /// false 를 돌려주면 뷰가 그리드 선택을 <see cref="SelectedRows"/> 로 되돌린다.
    /// </summary>
    public bool SetSelection(IReadOnlyList<object> rows)
    {
        var next = rows ?? Array.Empty<object>();

        // 같은 선택을 다시 받는 것은 이동이 아니다(뷰가 되돌린 직후 올라오는 알림).
        if (!_isRevertingSelection && !SameSelection(next, SelectedRows)
            && !Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow))
            return false;

        SelectedRows = next;
        _current?.Select(SelectedRows);

        var actionCount = SelectedRows.Count == 1 && SelectedRows[0] is ExEventViewModel ex && ex.IsActionReported ? 1 : 0;
        DetailView.Load(CurrentKind, SelectedRows, CanEdit, CanReport, actionCount);

        NotifyOfPropertyChange(nameof(SelectedRows));
        RaiseListState();
        NotifyOfPropertyChange(nameof(CanQueueSelection));
        NotifyOfPropertyChange(nameof(QueueButtonText));
        NotifyOfPropertyChange(nameof(CanDelete));
        return true;
    }

    /// <summary>뷰가 선택을 되돌리는 동안은 가드를 다시 물지 않는다(무한 재귀).</summary>
    public IDisposable SuppressSelectionGuard()
    {
        _isRevertingSelection = true;
        return new RevertScope(() => _isRevertingSelection = false);
    }

    private static bool SameSelection(IReadOnlyList<object> a, IReadOnlyList<object> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!ReferenceEquals(a[i], b[i])) return false;
        return true;
    }

    private sealed class RevertScope : IDisposable
    {
        private readonly System.Action _onDispose;
        public RevertScope(System.Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }

    private void AttachRows(IEventConsoleSource source)
    {
        DetachRows();
        _attached = source;
        source.RowsChanged.CollectionChanged += OnRowsChanged;

        // ★ 기본 뷰(CollectionViewSource.GetDefaultView)를 쓰지 않는다.
        //   그것은 콜렉션마다 전역 캐시되고 <b>뗄 수가 없어</b>, 레일을 떠난 뒤에도
        //   패널의 ViewModelProvider 에 붙어 산다. 그 패널이 다음에 다른 스레드에서
        //   목록을 비우면 CollectionView 가 "발송자 스레드가 다르다" 로 터진다(NotSupportedException).
        //   자기 뷰를 만들고 떠날 때 DetachFromSourceCollection() 으로 말끔히 뗀다(장비 콘솔 선례).
        var view = new ListCollectionView((IList)source.Rows) { Filter = PassesFilter };
        // (D-26) 새 ListCollectionView 는 CurrentItem 이 첫 행이다 — 이 뷰를 공유하는 그리드가
        // Selector.IsSynchronizedWithCurrentItem(기본값 자동)로 그 CurrentItem 에 스스로 동기화해
        // 우리가 시키지 않은 SelectionChanged(1건 선택)를 낸다. 아무도 고르지 않은 채로 그리드를
        // 붙이는 것이므로 CurrentItem 을 미리 "없음"으로 돌려 자동 동기화가 아무것도 못 고르게 한다.
        view.MoveCurrentToPosition(-1);
        _view = view;

        NotifyOfPropertyChange(nameof(Rows));
        RaiseListState();
    }

    private void DetachRows()
    {
        if (_attached is not null)
        {
            _attached.RowsChanged.CollectionChanged -= OnRowsChanged;
            // (D-26) 뜨는 레일의 행 IsSelected 도 같이 끈다 — 안 그러면 그 레일로 되돌아오지 않는 한
            // 그 행은 영원히 "고른 것처럼" 보인다(모델의 IsSelected 는 그리드가 다시 안 그려도 남는다).
            _attached.Select(Array.Empty<object>());
        }

        // 안 떼면 버린 뷰가 패널의 목록에 매달려 남아, 그 패널을 다시 열 때
        // 다른 스레드의 변경을 받아 터진다.
        (_view as ListCollectionView)?.DetachFromSourceCollection();
        _view = null;
        _attached = null;
        SelectedRows = Array.Empty<object>();
        NotifyOfPropertyChange(nameof(Rows));
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 배지를 세면서 화면 글자를 바꾸므로 UI 스레드에서 돌아야 한다(R12).
        // 마샤러는 주입된다 — 전역 Dispatcher 를 직접 잡으면 헤드리스에서 동작이 갈려 간헐 실패한다.
        if (!_uiThread.IsOnUiThread)
        {
            _uiThread.Post(() => OnRowsChanged(sender, e));
            return;
        }

        RefreshRailCounts();
        RaiseListState();
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
        set
        {
            if (_searchText == (value ?? string.Empty)) return;
            _searchText = value ?? string.Empty;
            NotifyOfPropertyChange();
            // 검색은 선택을 바꾸지 않으므로 미적용 변경을 막지 않는다(커널 ConsoleNavigation.Search).
            RefreshFilter();
        }
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

    /// <summary>[새 스케줄] — 억제 스케줄 레일에서만 뜬다(<see cref="ShowAdd"/>). 이벤트 목록은 추가하지 않는다.</summary>
    public bool CanAdd => IsSuppressionRail && Suppression!.CanAdd;

    public string AddBlockedReason => IsSuppressionRail
        ? Suppression!.AddBlockedReason
        : "이벤트는 장비가 만드는 기록이라 여기서 추가하지 않습니다.";

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
        Suppression!.AddNew();
    }

    public void Delete()
    {
        if (!CanDelete) return;
        // 삭제도 이동이다 — 미적용 판정을 든 채로 행을 지우면 그 변경은 소리 없이 사라진다.
        if (!Detail.Guard.TryNavigate(ConsoleNavigation.SelectRow)) return;
        if (IsSuppressionRail) { _ = Suppression!.DeleteSelectedAsync(); return; }
        _current!.Delete();
    }

    /// <summary>지금 조회 중인가 — [중단] 을 보일지 가른다.</summary>
    public bool IsQueryRunning => _current?.IsBusy == true;

    /// <summary>
    /// 진행 중인 조회를 멈춘다. 옆 창에는 [취소] 버튼이 있었는데 새 툴바에 자리가 없어
    /// 잃었던 기능이다 — 바쁘 동안만 뜨는 버튼으로 되살렸다(R14).
    /// </summary>
    public void CancelQuery()
    {
        _current?.CancelQuery();
        StatusText = "조회를 멈췄습니다";
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
        var write = DetailView.WriteBack();

        if (write.Rejected.Count > 0)
        {
            // 값을 못 읽은 칸이 있다 — 그것을 "변경 없음" 으로 삼키지 않는다(R8).
            Detail.LastMessage = $"고치지 못한 칸이 있습니다 — {string.Join(" · ", write.Rejected)}";
            return;
        }

        if (write.Written == 0)
        {
            Detail.Settle("바뀐 칸이 없습니다");
            return;
        }

        if (_current?.Save() != true)
        {
            // 패널이 받아들이지 않았다 — 끝남이 오지 않으므로 여기서 끝낸다.
            DetailView.RollbackWriteBack();
            Detail.LastMessage = "저장을 시작하지 못했습니다. 권한이나 진행 중인 작업을 확인하세요.";
            return;
        }

        // 진짜 결과는 BusyEnded 가 알려 준다 — 시작만 보고 dirty 를 푸는 것은 낙관적 종결이다(R8).
        _pendingApply = new PendingApply(SelectedRows.Count, write.Written, SelectedRows.Count == 1 ? SelectedRows[0] : null);
        Detail.LastMessage = $"저장 중 — {write.Written}건";
    }

    /// <summary>[적용] 이 걸어 둔 일. 끝남이 오면 그때 정말 되었는지 보고 끝낸다.</summary>
    private sealed record PendingApply(int SelectedCount, int Written, object? Row);

    private PendingApply? _pendingApply;

    /// <summary>
    /// 저장이 끝난 뒤 — 행이 아직도 손대진 채라면 서버가 받지 않은 것이다
    /// (패널은 저장에 성공한 행만 IsEdited 를 끔다).
    /// </summary>
    internal void SettlePendingApply()
    {
        var pending = _pendingApply;
        if (pending is null) return;
        _pendingApply = null;

        // 행 뷰모델 계보가 제네릭이라 공통 기반으로 단언한다 — 네 종류 전부 BaseEventViewModel<T> 파생이다.
        if (IsRowStillEdited(pending.Row))
        {
            Detail.LastMessage = "서버가 저장하지 못했습니다. 고친 칸은 그대로 두었으니 다시 [적용]하거나 [되돌리기]로 되돌리세요.";
            DetailView.RollbackWriteBack();
            return;
        }

        Detail.Settle(ConsoleDetailStateMachine.AppliedMessage(pending.SelectedCount, pending.Written));
    }

    /// <summary>행이 아직 손대진 채로 남아 있는가. 네 행 뷰모델이 공통 기반(BaseEventViewModel&lt;T&gt;)을 쓰지만
    /// 제네릭 인자가 가지가서 공통 상위형으로 묶이지 않는다 — 패턴 매칭 대신 이름으로 읽는다.</summary>
    private static bool IsRowStillEdited(object? row)
        => row?.GetType().GetProperty(nameof(BaseEventViewModel<IBaseEventModel>.IsEdited))?.GetValue(row) is true;

    /// <summary>[되돌리기] — 서버 호출 0.</summary>
    public void Revert()
    {
        DetailView.RevertEdits();
        Detail.Settle("되돌렸습니다");
    }

    private void OnNavigationBlocked(object? sender, ConsoleNavigation navigation)
        => StatusText = ConsoleDetailStateMachine.BlockedNotice;

    /// <summary>
    /// 상세 동작 줄(정본 SB L2703-2731) — 새 전송 경로를 만들지 않는다. 이미 있는 길을 그대로 부른다:
    /// 조치보고 = 패널의 우클릭 경로(조치보고 창) · 트레이 담기 = 툴바와 같은 담기 함수 ·
    /// 탐지 신호 이력 = 호스트가 이미 받는 <see cref="OpenDetectionHistoryDialogMessageModel"/> ·
    /// 원본 열기 = 원본 레일로 옮겨 그 행을 고른다.
    /// </summary>
    private void OnDetailAction(EventDetailAction action)
    {
        try
        {
            switch (action)
            {
                case EventDetailAction.Report:
                    ReportSelected();
                    break;
                case EventDetailAction.QueueToTray:
                    QueueSelection();
                    break;
                case EventDetailAction.DetectionHistory:
                    _ = OpenDetectionHistoryAsync();
                    break;
                case EventDetailAction.OpenOrigin:
                    OpenOriginAsync().ContinueWith(
                        t => _log?.Error($"[EventConsole] 원본 열기 실패: {t.Exception?.GetBaseException().Message}"),
                        TaskContinuationOptions.OnlyOnFaulted);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventConsole] 상세 동작 실패({action}): {ex}");
            StatusText = "요청을 처리하지 못했습니다. 잠시 뒤 다시 시도하세요.";
        }
    }

    /// <summary>한 건 [조치보고] — 행 우클릭 '조치보고' 와 같은 명령(권한 · 저장 전 행 검사 포함).</summary>
    private void ReportSelected()
    {
        if (SelectedRows.Count != 1) return;
        var row = SelectedRows[0];
        var command = row switch
        {
            DetectionEventViewModel => DetectionPanelViewModel.ReportCommand,
            MalfunctionEventViewModel => MalfunctionPanelViewModel.ReportCommand,
            _ => null,
        };
        if (command?.CanExecute(row) == true) command.Execute(row);
    }

    /// <summary>[탐지 신호 이력] — 그 센서의 이력 창(호스트가 이미 처리하는 메시지). 행 우클릭과 같은 명령.</summary>
    private Task OpenDetectionHistoryAsync()
    {
        if (SelectedRows.Count == 1) RowHistoryCommand.Execute(SelectedRows[0]);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 뷰가 이 행을 골라 보이게 해 달라는 요청 — 뷰모델은 그리드를 모른다(선택은 그리드가 다시 알려 온다).
    /// </summary>
    public event Action<object>? RowFocusRequested;

    /// <summary>행 우클릭 '조치 내역 보기' — 그 행을 골라 상세의 조치 내역을 연다(서랍 모드면 상세가 밀려 나온다).</summary>
    public System.Windows.Input.ICommand RowActionsCommand => _rowActionsCommand ??= new Ironwall.Dotnet.Libraries.Events.Ui.Models.SimpleParamCommand(row =>
    {
        if (row is not null) FocusRow(row);
        return Task.CompletedTask;
    });
    private System.Windows.Input.ICommand? _rowActionsCommand;

    /// <summary>행 우클릭 '탐지 신호 이력' — 그 센서의 이력 창.</summary>
    public System.Windows.Input.ICommand RowHistoryCommand => _rowHistoryCommand ??= new Ironwall.Dotnet.Libraries.Events.Ui.Models.SimpleParamCommand(async row =>
    {
        if (row is not DetectionEventViewModel { Device: Ironwall.Dotnet.Monitoring.Models.Devices.ISensorDeviceModel sensor }) return;
        await _eventAggregator!.PublishOnUIThreadAsync(new OpenDetectionHistoryDialogMessageModel
        {
            DeviceId = sensor.Id,
            DeviceName = sensor.DeviceName,
            DeviceNumber = sensor.DeviceNumber,
        });
    });
    private System.Windows.Input.ICommand? _rowHistoryCommand;

    /// <summary>이 행을 고른다 — 뷰가 있으면 그리드에서(선택이 그리드를 거쳐 돌아온다), 없으면 직접.</summary>
    private void FocusRow(object row)
    {
        if (RowFocusRequested is null) SetSelection(new[] { row });
        else RowFocusRequested.Invoke(row);
    }

    /// <summary>
    /// [원본 열기] — 조치 행의 원본(탐지 · 장애)이 있는 레일로 옮기고 그 행을 고른다.
    /// 원본이 불러온 기간 밖이면 옮긴 뒤 그 사실을 말한다(없는 행을 고른 척하지 않는다).
    /// </summary>
    public async Task OpenOriginAsync()
    {
        if (SelectedRows.Count != 1 || SelectedRows[0] is not ActionEventViewModel { OriginEvent: { } origin }) return;

        var isDetection = origin is Ironwall.Dotnet.Monitoring.Models.Events.IDetectionEventModel;
        var rail = isDetection ? DetectionRailKey : MalfunctionRailKey;
        if (!await SelectRailAsync(rail)) return;

        object? row = isDetection
            ? DetectionPanelViewModel.ViewModelProvider.FirstOrDefault(r => r.Model?.Id == origin.Id)
            : MalfunctionPanelViewModel.ViewModelProvider.FirstOrDefault(r => r.Model?.Id == origin.Id);

        if (row is null)
        {
            StatusText = $"원본({(isDetection ? "탐지" : "장애")} · {origin.Id})이 지금 기간에 없습니다. 기간을 넓혀 보세요.";
            return;
        }

        FocusRow(row);
    }
    #endregion

    #region - 개요 -
    public EventOverviewViewModel Overview { get; }

    /// <summary>
    /// 통계가 도착했다 — 개요만 다시 그린다.
    /// </summary>
    /// <remarks>
    /// (R14) 예전엔 여기서 <c>EventInfoViewModel</c> · <c>CameraEventInfoViewModel</c> 을 활성화하고
    /// <c>DataInitializeFromStats</c> 를 돌렸지만, 새 콘솔은 그 둘을 <b>그리지 않는다</b>
    /// (<c>EventInfoView</c> · <c>CameraEventInfoView</c> 참조 0건) — 화면에 안 나오는 집계를 매번 돌리던 죽은 일이라 뜼어냈다.
    /// 그 두 뷰모델은 생성자에 그대로 남겨 둔다 — 호스트 DI 가 이 형을 그대로 해석하고,
    /// 카메라 KPI 를 개요에 다시 실을 때 쓴다.
    /// </remarks>
    private void OnDashboardUpdated(DateTime start, DateTime end)
    {
        // 🔴 이 신호는 차트 패널의 Task.Run 안(finally)에서 울린다 — 작업 스레드다.
        //    Overview.Load 는 조각 · 막대 컬렉션을 비우고 다시 채우는데, 그 컬렉션은 UI 스레드에서 만든
        //    CollectionView 에 묶여 있어 "발송자 스레드와 다른 스레드에서의 변경" 으로 터졌다
        //    (실창 log-2026-09-27.txt:779, 스레드 [11]). 목록 변경 신호(OnRowsChanged)와 같은 마샤러로 올린다.
        if (!_uiThread.IsOnUiThread)
        {
            _uiThread.Post(() => OnDashboardUpdated(start, end));
            return;
        }

        try
        {
            Overview.IsLoading = false;
            Overview.Load(DataChartPanelViewModel.LastDashboardDto, start, end);
            RefreshRailCounts();
            RaiseListState();
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
        // async void 는 예외가 그대로 터지면 앱을 내린다 — 여기서 감싼다(R12).
        try
        {
            SearchText = query;
            await SelectRailAsync(railKey == "det" ? DetectionRailKey : railKey);
        }
        catch (Exception ex)
        {
            _log?.Error($"[EventConsole] 내역으로 내려가지 못했습니다: {ex.Message}");
            StatusText = "내역으로 이동하지 못했습니다";
        }
    }
    #endregion

    #region - 조치 트레이 -
    public ActionTrayViewModel Tray { get; }
    public ActionTrayDropHandler TrayDrop { get; }

    /// <summary>드래그의 키보드 · 버튼 폴백 — 같은 담기 함수를 부른다(정본 DW L423).</summary>
    public bool CanQueueSelection => CanDragToTray && SelectedRows.Count > 0 && !Tray.IsApplying;

    /// <summary>
    /// 툴바 주 버튼 글자 — 이 버튼은 보고를 <b>보내지 않고</b> 트레이에 담기만 한다. "조치보고" 라고 쓰면
    /// 누른 순간 보고된 줄 안다(완성도 감사 E-2 #6). 보내는 것은 트레이의 [조치 적용] 이다.
    /// </summary>
    public string QueueButtonText => SelectedRows.Count > 1
        ? $"{SelectedRows.Count}건 트레이에 담기"
        : "트레이에 담기";

    /// <remarks>담은 결과는 트레이 자기 상태 줄이 말한다 — 바닥 상태 띠에 같은 말을 한 번 더 찍지 않는다(E-6 #5).</remarks>
    public void QueueSelection()
    {
        if (!CanQueueSelection) return;
        TrayDrop.Queue(SelectedRows);
    }

    public async void ApplyTray()
    {
        try
        {
            await ApplyTrayAsync();
        }
        catch (Exception ex)
        {
            // 예외 원문은 로그로만 — 화면에는 무엇이 안 됐고 어떻게 하면 되는지만(E-2 #8).
            _log?.Error($"[EventConsole] 조치 트레이 적용 실패: {ex}");
            StatusText = "조치 적용에 실패했습니다. 잠시 뒤 다시 시도하세요.";
        }
    }

    /// <summary>
    /// [조치 적용] — 트레이를 보내고, 보낸 뒤의 사실을 화면에 다시 들인다(완성도 감사 E-5 #1).
    /// 목록 행은 카드가 모델 상태를 맞춰 두었으니 다시 그리기만 하고, 상세는 '상태' 와 조치 내역을 서버에 다시 묻는다.
    /// </summary>
    public async Task<DraftApplySummary> ApplyTrayAsync()
    {
        var summary = await Tray.ApplyAsync();
        RefreshRailCounts();
        RaiseListState();
        RefreshDetailAfterReport();
        return summary;
    }

    public void RevertTray() => Tray.Revert();

    public void CancelTray() => Tray.Cancel();

    /// <summary>드래그 담기가 끝났다 — 결과 한 줄은 트레이가 이미 보인다(여기서 되풀이하지 않는다).</summary>
    private void OnTrayCompleted(string line) => NotifyOfPropertyChange(nameof(CanQueueSelection));

    /// <summary>
    /// 조치보고가 방금 만들어졌다 — 고른 한 건이 그 대상이면 상세의 '상태' · 조치 내역을 다시 보인다.
    /// 목록을 통째로 다시 부르지 않는다(선택 · 스크롤 · 적용 전 편집을 지킨다).
    /// </summary>
    private void RefreshDetailAfterReport()
    {
        if (SelectedRows.Count != 1 || SelectedRows[0] is not ExEventViewModel ex) return;
        (ex as Caliburn.Micro.PropertyChangedBase)?.Refresh();
        DetailView.RefreshAfterReport(ex.IsActionReported ? 1 : 0);
    }

    /// <summary>행 우클릭 · 상세 [조치보고] 창 경로로 보고가 만들어졌다(카드가 알린다).</summary>
    public Task HandleAsync(Ironwall.Dotnet.Libraries.Events.Ui.Models.DetectionReportedMessageModel message, CancellationToken cancellationToken)
    {
        OnReported(message.ViewModel?.Model?.Id, ActionTrayDrop.KindDetection);
        return Task.CompletedTask;
    }

    /// <inheritdoc cref="HandleAsync(Ironwall.Dotnet.Libraries.Events.Ui.Models.DetectionReportedMessageModel, CancellationToken)"/>
    public Task HandleAsync(Ironwall.Dotnet.Libraries.Events.Ui.Models.MalfunctionReportedMessageModel message, CancellationToken cancellationToken)
    {
        OnReported(message.ViewModel?.Model?.Id, ActionTrayDrop.KindMalfunction);
        return Task.CompletedTask;
    }

    private void OnReported(int? eventId, string kind)
    {
        if (eventId is not { } id || id <= 0) return;
        // 트레이가 보내는 중이면 트레이가 끝날 때 한 번에 다시 그린다 — 줄마다 조치 내역을 다시 부르지 않는다.
        if (Tray.IsApplying) return;

        var candidate = new ActionTrayCandidate(id, kind, string.Empty, true);
        FindOriginRow(candidate)?.Refresh();
        if (SelectedRows.Count == 1 && SelectedRows[0] is ExEventViewModel ex && ex.Model?.Id == id
            && (kind == ActionTrayDrop.KindDetection ? ex is DetectionEventViewModel : ex is MalfunctionEventViewModel))
            RefreshDetailAfterReport();
        RefreshRailCounts();
        RaiseListState();
    }

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

        // 행 뷰모델과 독립된 임시 카드 뷰모델을 세운다 — 우클릭 조치보고가 쓰는 것과 똑같은 경로다.
        // (R1) bool 이 아니라 진짜 결말을 받는다 — 멱등 가드가 막은 건을 "적용" 으로 세면 거짓말이 된다.
        EventCardViewModel<IDetectionEventModel>? detectionCard = null;
        EventCardViewModel<IMalfunctionEventModel>? malfunctionCard = null;
        ActionSendResult result;
        if (candidate.Kind == ActionTrayDrop.KindDetection)
        {
            detectionCard = new DetectionEventCardViewModel(_eventAggregator, _log!, (IDetectionEventModel)model);
            result = await detectionCard.SendActionDetailed(content, user, token).ConfigureAwait(true);
        }
        else
        {
            malfunctionCard = new MalfunctionEventCardViewModel(_eventAggregator, _log!, (IMalfunctionEventModel)model);
            result = await malfunctionCard.SendActionDetailed(content, user, token).ConfigureAwait(true);
        }
        _ = detectionCard; _ = malfunctionCard;

        LastSendReason = result.Reason;
        // 만들어졌으면 그 행의 '조치 있음' 을 바로 다시 그린다 — 모델 상태는 카드가 맞춰 두었다(E7a).
        if (result.Outcome == ActionSendOutcome.Created) FindOriginRow(candidate)?.Refresh();
        return result.Outcome switch
        {
            ActionSendOutcome.Created => DraftOutcome.Applied,
            ActionSendOutcome.GuardSkipped => DraftOutcome.Skipped,      // 만들지 않았다
            ActionSendOutcome.Cancelled => throw new OperationCanceledException(result.Reason ?? "중단"),
            _ => DraftOutcome.Failed,
        };
    }

    /// <summary>마지막 전송의 까닭 한 줄(스킵 · 실패). 트레이가 줄마다 붙이기 어려워 상태 띄에 낸다.</summary>
    internal string? LastSendReason { get; private set; }

    /// <summary>이벤트 API — 컨테이너가 없는 자리(단위 테스트 · 미리보기)에서는 null 이고, 그러면 조치 내역을 부르지 않는다.</summary>
    private IEventApiService? ResolveEventApi()
    {
        try { return IoC.Get<IEventApiService>(); }
        catch (Exception ex)
        {
            _log?.Warning($"[EventConsole] IEventApiService 미해석 — 조치 내역을 부르지 않습니다: {ex.Message}");
            return null;
        }
    }

    /// <summary>조치보고 문구 API — 컨테이너에 없으면(시험 · 미리보기 · 구 호스트) null → 기본 문구.</summary>
    private IActionReportTemplateApiService? ResolveTemplateApi()
    {
        try { return IoC.Get<IActionReportTemplateApiService>(); }
        catch (Exception ex)
        {
            _log?.Info($"[EventConsole] 조치보고 문구 API 미해석 — 기본 문구를 씁니다: {ex.Message}");
            return null;
        }
    }

    private readonly IActionReportPhraseSource _phrases;

    /// <summary>
    /// 트레이 문구를 조치보고 문구 관리 목록(서버)으로 갈아 끼운다. 못 읽으면 기본 문구 그대로다.
    /// 작업 스레드에서 끝나도 화면 반영은 UI 스레드에서 한다.
    /// </summary>
    public async Task RefreshPhrasesAsync()
    {
        try
        {
            var set = await _phrases.LoadAsync().ConfigureAwait(false);
            if (_uiThread.IsOnUiThread) Tray.ApplyPhrases(set);
            else _uiThread.Post(() => Tray.ApplyPhrases(set));
        }
        catch (Exception ex)
        {
            _log?.Warning($"[EventConsole] 조치보고 문구를 갈아 끼우지 못했습니다 — 기본 문구를 씁니다: {ex.Message}");
        }
    }

    private IExEventModel? FindOriginModel(ActionTrayCandidate candidate)
    {
        if (candidate.Kind == ActionTrayDrop.KindDetection)
            return DetectionPanelViewModel.ViewModelProvider
                .FirstOrDefault(r => r.Model?.Id == candidate.EventId)?.Model as IExEventModel;

        return MalfunctionPanelViewModel.ViewModelProvider
            .FirstOrDefault(r => r.Model?.Id == candidate.EventId)?.Model as IExEventModel;
    }

    /// <summary>트레이 후보의 목록 행(다시 그리기용).</summary>
    private Caliburn.Micro.PropertyChangedBase? FindOriginRow(ActionTrayCandidate candidate)
        => candidate.Kind == ActionTrayDrop.KindDetection
            ? DetectionPanelViewModel.ViewModelProvider.FirstOrDefault(r => r.Model?.Id == candidate.EventId) as Caliburn.Micro.PropertyChangedBase
            : MalfunctionPanelViewModel.ViewModelProvider.FirstOrDefault(r => r.Model?.Id == candidate.EventId) as Caliburn.Micro.PropertyChangedBase;

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
        SettlePendingApply();                 // (R8) 진짜 결과는 여기서야 알 수 있다
        RefreshRailCounts();
        RaiseListState();
        NotifyOfPropertyChange(nameof(CanReload));
        NotifyOfPropertyChange(nameof(IsQueryRunning));
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanDelete));
    }

    private void RaiseShellState()
    {
        NotifyOfPropertyChange(nameof(Rows));
        NotifyOfPropertyChange(nameof(IsListVisible));
        NotifyOfPropertyChange(nameof(IsOverview));
        NotifyOfPropertyChange(nameof(IsDetailAvailable));
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
        NotifyOfPropertyChange(nameof(IsTrayVisible));
        NotifyOfPropertyChange(nameof(IsTrayCollapsed));
        NotifyOfPropertyChange(nameof(CanEdit));
        NotifyOfPropertyChange(nameof(CanReport));
        NotifyOfPropertyChange(nameof(CanQueueSelection));
        NotifyOfPropertyChange(nameof(QueueButtonText));
        RaiseListState();
        NotifyOfPropertyChange(nameof(Subtitle));
        NotifyOfPropertyChange(nameof(IsQueryRunning));
        NotifyOfPropertyChange(nameof(CanAdd));
        NotifyOfPropertyChange(nameof(CanDelete));
        NotifyOfPropertyChange(nameof(AddBlockedReason));
        NotifyOfPropertyChange(nameof(DeleteBlockedReason));
        NotifyOfPropertyChange(nameof(ShowAdd));
        NotifyOfPropertyChange(nameof(ShowDelete));
        NotifyOfPropertyChange(nameof(ShowRailFooter));
        NotifyOfPropertyChange(nameof(HasFilterChips));
    }

    /// <summary>시험이 결정론적으로 목록을 갈아 끼우는 이음매 — 제품 경로는 쓰지 않는다.</summary>
    /// <summary>
    /// UI 스레드 마샤러를 갈아 끼운다 — 시험은 <see cref="ImmediateUiThread"/> 로 스레드 전환을 없앤다.
    /// 제품 경로는 이것을 부르지 않는다(기본값 = <see cref="ApplicationUiThread"/>).
    /// </summary>
    internal void UseUiThread(IUiThread uiThread) => _uiThread = uiThread ?? ApplicationUiThread.Instance;

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
    private string _chipKey = EventListFilter.ChipAll;
    private bool _isRevertingSelection;
    private IUiThread _uiThread = ApplicationUiThread.Instance;
    private string _statusText = string.Empty;
    private DateTime _startDate;
    private DateTime _endDate;
    private DateTime _endDateDisplay;
    #endregion

    #region - N-13 mapping workbench -
    private readonly Lazy<Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.IMappingWorkbenchLauncher>? _mappingFactory;
    private bool _mappingFailed;

    /// <summary>
    /// 맵핑 워크벤치 입구 — <b>늦게 만든다</b>. 곧바로 주입받으면 입구의 의존 하나가 컨테이너에서 안 풀릴 때
    /// 이 뷰모델까지 못 만들어져 <b>이벤트 콘솔 전체가 안 열린다</b>. 늦게 풀고, 실패하면 입구만 감춘다.
    /// </summary>
    private Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.IMappingWorkbenchLauncher? _mapping
    {
        get
        {
            if (_mappingFactory is null || _mappingFailed) return null;
            try { return _mappingFactory.Value; }
            catch (Exception ex)
            {
                _mappingFailed = true;
                _log?.Error($"[EventConsole] 맵핑 워크벤치 입구를 만들지 못했다 — 입구를 감춘다: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 워크벤치 입구를 보일 것인가. 운영 6.3.2 서버에는 이 연동 계약이 없어 <b>감춘다</b>(비활성 아님).
    /// </summary>
    public bool CanOpenMappingWorkbench => _mapping?.IsAvailable ?? false;

    /// <summary>워크벤치 창을 연다.</summary>
    public Task OpenMappingWorkbenchAsync() => _mapping?.OpenAsync() ?? Task.CompletedTask;

    /// <summary>
    /// 입구를 다시 판정한다 — <see cref="CanOpenMappingWorkbench"/> 는 권한·판본에서 나온
    /// <b>파생값</b>이라 스스로 알리지 못한다. 권한이 바뀌면 누가 깨워 줘야 한다.
    /// </summary>
    public void RefreshMappingEntry() => NotifyOfPropertyChange(nameof(CanOpenMappingWorkbench));
    #endregion
}

/// <summary>
/// 이벤트 창 닫기 확인에서 [확인] — <b>이 콘솔 전용</b>(다른 창의 확인 메시지를 재사용하면 같은 확인 한 번에 두 창이 반응한다).
/// </summary>
public sealed class CallCloseEventConsoleMessageModel : Ironwall.Dotnet.Libraries.Base.Models.IMessageModel { }

/// <summary>기간 칩 한 칸 — 선택 표시를 스스로 든다(라디오 버튼이 직접 묶을 것을 가진다).</summary>
public sealed class EventPeriodOption : Caliburn.Micro.PropertyChangedBase
{
    private bool _isSelected;

    public EventPeriodOption(string name) => Name = name;

    public string Name { get; }

    public bool IsSelected { get => _isSelected; set { _isSelected = value; NotifyOfPropertyChange(); } }

    public override string ToString() => Name;

}

/// <summary>필터 칩 한 칸 — 선택 표시를 스스로 든다(정본 L2310-2313).</summary>
public sealed class EventFilterChipOption : Caliburn.Micro.PropertyChangedBase
{
    private bool _isSelected;

    public EventFilterChipOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }

    public bool IsSelected { get => _isSelected; set { _isSelected = value; NotifyOfPropertyChange(); } }

    public override string ToString() => Label;
}
