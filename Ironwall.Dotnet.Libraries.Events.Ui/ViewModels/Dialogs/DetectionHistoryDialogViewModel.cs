using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Controls;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : 탐지 신호 이력 다이얼로그 (Detection_Signal_History FR-09/12/13/14/15)
                  센서 단위 기간 조회(≤500건) → 시간축 신호 차트 + 그리드 + 통계.
                  단일 인스턴스 — Initialize()로 장비 컨텍스트 교체, 구독/취소는 Activate 수명주기.
   Created By   : GHLee
   Created On   : 2026-07-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>이력 그리드 1행 — 원본 모델 보존(조치보고 연계용).</summary>
public class SignalHistoryItemViewModel : PropertyChangedBase
{
    public SignalHistoryItemViewModel(IDetectionEventModel model) => Model = model;

    public IDetectionEventModel Model { get; }
    public int EventId => Model.Id;
    public DateTime DateTime => Model.DateTime;
    public EnumDetectionType Result => Model.Result;
    public int? Signal => Model.Signal;
    public bool HasSignal => Signal is > 0;
    public string SignalText => Signal is > 0 ? Signal!.Value.ToString("N0") : "—";
    public string TimeText => DateTime.ToString("MM-dd HH:mm:ss");
    public bool IsActioned => Model.Status == EnumTrueFalse.True;
    public string ActionText => IsActioned ? "조치" : "미조치";
}

/// <summary>Result 타입 필터 칩 — 토글 시 소유 VM의 필터 재적용 콜백.</summary>
public class ResultChipViewModel : PropertyChangedBase
{
    private readonly System.Action _onToggled;
    private bool _isOn;

    public ResultChipViewModel(EnumDetectionType result, bool isOn, System.Action onToggled)
    {
        Result = result;
        _isOn = isOn;
        _onToggled = onToggled;
    }

    public EnumDetectionType Result { get; }
    public string Name => Result.ToString();

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value) return;
            _isOn = value;
            NotifyOfPropertyChange();
            _onToggled();
        }
    }
}

public class DetectionHistoryDialogViewModel : BasePanelViewModel
{
    #region - Ctors -
    public DetectionHistoryDialogViewModel(IEventAggregator eventAggregator
                                          , ILogService log
                                          , IEventApiService apiService
                                          , DeviceProvider deviceProvider)
                                          : base(eventAggregator, log)
    {
        _apiService = apiService;
        _deviceProvider = deviceProvider;

        RefreshCommand = new SimpleCommand(async () => await LoadAsync());
        ApplyCustomCommand = new SimpleCommand(async () => await LoadAsync());
        PointClickedCommand = new SimpleParamCommand(OnChartPointClickedAsync);
        ReportCommand = new SimpleParamCommand(ReportRowAsync);
    }
    #endregion

    #region - 컨텍스트 (Initialize) -
    public int DeviceId { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;
    public int? DeviceNumber { get; private set; }

    public string HeaderText => DeviceNumber is int n
        ? $"탐지 신호 이력 — {DeviceName} (No.{n})"
        : $"탐지 신호 이력 — {DeviceName}";

    /// <summary>오픈 메시지 컨텍스트 주입 — 이미 활성 상태면(다른 장비로 전환) 즉시 재조회.</summary>
    public void Initialize(OpenDetectionHistoryDialogMessageModel message)
    {
        DeviceId = message.DeviceId;
        DeviceName = string.IsNullOrWhiteSpace(message.DeviceName) ? $"장비 {message.DeviceId}" : message.DeviceName!;
        DeviceNumber = message.DeviceNumber;
        NotifyOfPropertyChange(nameof(HeaderText));

        if (IsActive)
            _ = LoadAsync();
    }
    #endregion

    #region - Lifecycle -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        await LoadAsync();
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        _loadCts?.Cancel();
        return base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - 기간 프리셋 (FR-12) -
    /// <summary>"1h" | "24h" | "7d" | "30d" | "custom" — 기본 24h.</summary>
    public string PeriodKey
    {
        get => _periodKey;
        private set
        {
            _periodKey = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsCustomPeriod));
        }
    }
    public bool IsCustomPeriod => PeriodKey == "custom";

    public DateTime CustomStart
    {
        get => _customStart;
        set { _customStart = value; NotifyOfPropertyChange(); }
    }
    public DateTime CustomEnd
    {
        get => _customEnd;
        set { _customEnd = value; NotifyOfPropertyChange(); }
    }

    /// <summary>기간 세그먼트 클릭 (cal:Message) — custom은 적용 버튼으로 조회.</summary>
    public Task SelectPeriod(string key)
    {
        PeriodKey = key;
        return key == "custom" ? Task.CompletedTask : LoadAsync();
    }

    private (DateTime Start, DateTime End) ResolveRange()
    {
        var now = DateTime.Now;
        return PeriodKey switch
        {
            "1h" => (now.AddHours(-1), now),
            "7d" => (now.AddDays(-7), now),
            "30d" => (now.AddDays(-30), now),
            "custom" => (CustomStart, CustomEnd),
            _ => (now.AddHours(-24), now),
        };
    }
    #endregion

    #region - 조회 엔진 (FR-12) -
    private const int PAGE_LIMIT = 100;
    private const int MAX_LOAD = 500;   // 과다 조회 상한 — 초과 시 경고 + 최신 500건만

    public bool IsBusy { get => _isBusy; private set { _isBusy = value; NotifyOfPropertyChange(); } }
    public bool HasLoadError { get => _hasLoadError; private set { _hasLoadError = value; NotifyOfPropertyChange(); } }
    public bool IsTruncated { get => _isTruncated; private set { _isTruncated = value; NotifyOfPropertyChange(); } }
    public string RangeText { get => _rangeText; private set { _rangeText = value; NotifyOfPropertyChange(); } }
    public string LastUpdatedText { get => _lastUpdatedText; private set { _lastUpdatedText = value; NotifyOfPropertyChange(); } }

    private async Task LoadAsync()
    {
        if (DeviceId <= 0) return;

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsBusy = true;
        HasLoadError = false;
        try
        {
            var (start, end) = ResolveRange();
            var startText = start.ToString("yyyy-MM-ddTHH:mm:ss");
            var endText = end.ToString("yyyy-MM-ddTHH:mm:ss");

            var models = new List<IDetectionEventModel>();
            int page = 1;
            bool truncated = false;
            while (true)
            {
                var response = await _apiService.GetDetectionEventsAsync(
                    startText, endText, sensor: DeviceId, page: page, limit: PAGE_LIMIT, token: ct);

                if (response is not { Success: true } || response.Data == null)
                {
                    if (page == 1)
                        throw new InvalidOperationException(response?.Message ?? "서버 응답이 없습니다.");
                    break;   // 후속 페이지 실패 — 확보한 만큼만 표시
                }

                models.AddRange(response.Data.Select(dto => dto.ToDetectionEventModel(_deviceProvider)));

                if (response.Data.Count < PAGE_LIMIT) break;          // 마지막 페이지
                if (models.Count >= MAX_LOAD) { truncated = true; break; }
                page++;
            }

            // 서버 정렬(id desc)=최신 우선 — 상한 초과분은 과거 데이터라 절단
            if (models.Count > MAX_LOAD)
                models = models.Take(MAX_LOAD).ToList();

            ct.ThrowIfCancellationRequested();

            _all.Clear();
            _all.AddRange(models
                .Select(m => new SignalHistoryItemViewModel(m))
                .OrderByDescending(i => i.DateTime));

            IsTruncated = truncated;
            RebuildChips();
            ApplyFilters();
            RangeText = $"{start:yyyy-MM-dd HH:mm} ~ {end:yyyy-MM-dd HH:mm} · {_all.Count}건"
                        + (truncated ? " (상한 500건 — 기간을 줄여주세요)" : string.Empty);
            LastUpdatedText = $"마지막 갱신 {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            // 재조회/닫기에 의한 취소 — 무시
        }
        catch (Exception ex)
        {
            _log?.Error($"[SIGNAL_HISTORY] 탐지 이력 조회 실패 (deviceId={DeviceId}): {ex.Message}");
            HasLoadError = true;
            _all.Clear();
            RebuildChips();
            ApplyFilters();
            RangeText = string.Empty;
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "탐지 이력 조회 실패",
                Explain = "서버에서 탐지 이력을 가져오지 못했습니다.\n네트워크/서버 상태 확인 후 새로고침으로 다시 시도해 주세요."
            });
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion

    #region - 필터/통계 (FR-14) -
    private readonly List<SignalHistoryItemViewModel> _all = new();

    public ObservableCollection<ResultChipViewModel> Chips { get; } = new();
    public ObservableCollection<SignalHistoryItemViewModel> FilteredItems { get; } = new();
    public IReadOnlyList<SignalChartPoint> ChartPoints
    {
        get => _chartPoints;
        private set { _chartPoints = value; NotifyOfPropertyChange(); }
    }

    /// <summary>미조치만 보기 토글.</summary>
    public bool OnlyUnactioned
    {
        get => _onlyUnactioned;
        set { _onlyUnactioned = value; NotifyOfPropertyChange(); ApplyFilters(); }
    }

    // 통계 스트립 (조회 구간 클라 측 집계 — signal null/0은 신호 통계에서 제외: RISK-01)
    public int TotalCount { get => _totalCount; private set { _totalCount = value; NotifyOfPropertyChange(); } }
    public int MaxSignal { get => _maxSignal; private set { _maxSignal = value; NotifyOfPropertyChange(); } }
    public string MaxSignalText => MaxSignal > 0 ? MaxSignal.ToString("N0") : "—";
    public string AvgSignalText { get => _avgSignalText; private set { _avgSignalText = value; NotifyOfPropertyChange(); } }
    public string TopResultText { get => _topResultText; private set { _topResultText = value; NotifyOfPropertyChange(); } }
    public int UnactionedCount { get => _unactionedCount; private set { _unactionedCount = value; NotifyOfPropertyChange(); } }

    /// <summary>조회 결과의 Result 분포로 칩 재구성 — 기존 on/off 보존, 신규 Result는 신호 전무(AI)면 기본 off.</summary>
    private void RebuildChips()
    {
        var previous = Chips.ToDictionary(c => c.Result, c => c.IsOn);
        Chips.Clear();
        foreach (var group in _all.GroupBy(i => i.Result).OrderBy(g => g.Key.ToString()))
        {
            bool defaultOn = group.Any(i => i.HasSignal);   // AI_DETECT(전부 signal 0) → 기본 off
            bool isOn = previous.TryGetValue(group.Key, out var prev) ? prev : defaultOn;
            Chips.Add(new ResultChipViewModel(group.Key, isOn, ApplyFilters));
        }
    }

    private void ApplyFilters()
    {
        var enabled = Chips.Where(c => c.IsOn).Select(c => c.Result).ToHashSet();

        var filtered = _all
            .Where(i => enabled.Contains(i.Result))
            .Where(i => !OnlyUnactioned || !i.IsActioned)
            .ToList();

        FilteredItems.Clear();
        foreach (var item in filtered)
            FilteredItems.Add(item);

        // 차트 — signal>0만, 시간 오름차순 (FR-13 / null-안전 규약)
        ChartPoints = filtered
            .Where(i => i.HasSignal)
            .OrderBy(i => i.DateTime)
            .Select(i => new SignalChartPoint(i.DateTime, i.Signal!.Value, i.IsActioned, i.Result.ToString(), i))
            .ToList();

        // 통계
        TotalCount = filtered.Count;
        var signals = filtered.Where(i => i.HasSignal).Select(i => i.Signal!.Value).ToList();
        MaxSignal = signals.Count > 0 ? signals.Max() : 0;
        AvgSignalText = signals.Count > 0 ? signals.Average().ToString("N0") : "—";
        var top = filtered.GroupBy(i => i.Result).OrderByDescending(g => g.Count()).FirstOrDefault();
        TopResultText = top != null ? $"{top.Key} ({top.Count()}건)" : "—";
        UnactionedCount = filtered.Count(i => !i.IsActioned);
        NotifyOfPropertyChange(nameof(MaxSignalText));
    }
    #endregion

    #region - 차트↔그리드 동기 (FR-13) -
    public SignalHistoryItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set { _selectedItem = value; NotifyOfPropertyChange(); }
    }

    private Task OnChartPointClickedAsync(object? payload)
    {
        if (payload is SignalHistoryItemViewModel item)
            SelectedItem = item;
        return Task.CompletedTask;
    }
    #endregion

    #region - 조치보고 연계 (FR-15, OQ-1=(b) 순차) -
    /// <summary>
    /// 미조치 행 우클릭 조치보고 — 기존 이력 패널 ReportRowAsync 패턴 미러.
    /// DialogShell(Conductor OneActive)이 본 다이얼로그를 조치보고 다이얼로그로 자동 교체한다(순차 전환).
    /// </summary>
    private async Task ReportRowAsync(object? param)
    {
        if (param is not SignalHistoryItemViewModel item) return;

        // FR-EN-10 미러 — 권한 게이트 (미해석 시 전체허용 폴백)
        if (!CanControlEvents())
        {
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "권한 없음",
                Explain = "조치보고 권한이 없습니다."
            });
            return;
        }

        if (item.IsActioned)
        {
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "조치보고 불가",
                Explain = "이미 조치보고된 이벤트입니다."
            });
            return;
        }

        // 행과 독립된 임시 카드 VM 재구성(SendAction/멱등가드는 카드 VM 보유)
        var card = new DetectionEventCardViewModel(_eventAggregator, _log, item.Model);
        IoC.Get<DetectionReportDialogViewModel>().UpdateData(card, IoC.Get<IAccountModel>());
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel { EventType = "DETECTION" });
    }

    private bool CanControlEvents()
    {
        try { return IoC.Get<IPermissionService>()?.CanControl("events") ?? true; }
        catch { return true; }   // IoC 미구성(테스트/오프라인) → 전체허용 폴백
    }
    #endregion

    #region - 닫기 -
    public Task CloseDialog()
        => _eventAggregator.PublishOnCurrentThreadAsync(new CloseDialogMessageModel());
    #endregion

    #region - Commands -
    public ICommand RefreshCommand { get; }
    public ICommand ApplyCustomCommand { get; }
    public ICommand PointClickedCommand { get; }
    public ICommand ReportCommand { get; }
    #endregion

    #region - Attributes -
    private readonly IEventApiService _apiService;
    private readonly DeviceProvider _deviceProvider;
    private CancellationTokenSource? _loadCts;

    private string _periodKey = "24h";
    private DateTime _customStart = DateTime.Now.AddDays(-1);
    private DateTime _customEnd = DateTime.Now;
    private bool _isBusy;
    private bool _hasLoadError;
    private bool _isTruncated;
    private string _rangeText = string.Empty;
    private string _lastUpdatedText = string.Empty;
    private bool _onlyUnactioned;
    private IReadOnlyList<SignalChartPoint> _chartPoints = Array.Empty<SignalChartPoint>();
    private SignalHistoryItemViewModel? _selectedItem;
    private int _totalCount;
    private int _maxSignal;
    private string _avgSignalText = "—";
    private string _topResultText = "—";
    private int _unactionedCount;
    #endregion
}
