using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Converters;
using Ironwall.Dotnet.Libraries.Events.Ui.Controls;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.ObjectModel;
using System.Windows;
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

/// <summary>이력 그리드 1행 — 원본 모델 보존(조치보고 연계용).
/// SensorId/SeriesIndex(pidsgroup-rightclick FR-11/13): 그룹 모드에서 팬아웃 조회 키 기준으로 센서 귀속 —
/// 이벤트 Device 매칭 실패에도 안전하다. 기본값=단일 모드 하위호환.</summary>
public class SignalHistoryItemViewModel : PropertyChangedBase
{
    public SignalHistoryItemViewModel(IDetectionEventModel model, int sensorId = 0, string? sensorName = null, int seriesIndex = 0)
    {
        Model = model;
        SensorId = sensorId;
        _sensorName = sensorName;
        SeriesIndex = seriesIndex;
    }

    private readonly string? _sensorName;

    public IDetectionEventModel Model { get; }
    public int SensorId { get; }
    public int SeriesIndex { get; }
    /// <summary>그룹 모드 그리드 센서 컬럼 표시명 (FR-13).</summary>
    public string DeviceName => _sensorName ?? Model.Device?.DeviceName ?? "—";
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
    public string Name => EnumKoreanMap.To(Result);

    /// <summary>칩 표시용 축약명 — "_SENSOR" 접미사 제거(PIR/THERMAL/…). 풀네임은 ToolTip으로 제공(툴바 잘림 방지).</summary>
    public string ShortName => Name.EndsWith("_SENSOR", StringComparison.Ordinal) ? Name[..^"_SENSOR".Length] : Name;

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

/// <summary>그룹 모드 센서 필터 칩 (pidsgroup-rightclick FR-12) — ResultChipViewModel 패턴 미러.
/// SeriesIndex는 그룹 멤버 순서 고정(칩 on/off와 무관) — 시리즈 색이 엔티티를 따르는 규약.</summary>
public class SensorChipViewModel : PropertyChangedBase
{
    private readonly System.Action _onToggled;
    private bool _isOn;

    public SensorChipViewModel(int deviceId, string name, int seriesIndex, bool isOn, System.Action onToggled)
    {
        DeviceId = deviceId;
        Name = name;
        SeriesIndex = seriesIndex;
        _isOn = isOn;
        _onToggled = onToggled;
    }

    public int DeviceId { get; }
    public string Name { get; }
    public int SeriesIndex { get; }
    /// <summary>칩 색 견본 인덱스 — 8색 검증 팔레트 순환(차트 SeriesBrushFor와 동일 규칙, 색=센서 고정).</summary>
    public int SeriesColorIndex => SeriesIndex % 8;

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

    /// <summary>[전체] 마스터 토글 일괄 적용용 — 콜백(ApplyFilters) 없이 상태만 갱신(N회 재적용 폭주 방지).</summary>
    internal void SetSilently(bool isOn)
    {
        if (_isOn == isOn) return;
        _isOn = isOn;
        NotifyOfPropertyChange(nameof(IsOn));
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

    #region - 컨텍스트 (Initialize — 단일 센서 / 그룹 모드) -
    public int DeviceId { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;
    public int? DeviceNumber { get; private set; }

    /// <summary>그룹 모드(pidsgroup-rightclick FR-09) — true면 멤버 센서 팬아웃 조회 + 센서 칩/컬럼 노출.</summary>
    public bool IsGroupMode { get; private set; }
    public int GroupId { get; private set; }
    public string GroupName { get; private set; } = string.Empty;

    /// <summary>그룹 모드 한정 그리드 센서 컬럼 표시 (FR-13 — DataGridColumn은 시각트리 밖이라 proxy 바인딩).</summary>
    public Visibility SensorColumnVisibility => IsGroupMode ? Visibility.Visible : Visibility.Collapsed;

    public string HeaderText => IsGroupMode
        ? $"그룹 탐지 이력 — {GroupName} (센서 {_groupSensors.Count})"
        : DeviceNumber is int n
            ? $"탐지 신호 이력 — {DeviceName} (No.{n})"
            : $"탐지 신호 이력 — {DeviceName}";

    /// <summary>오픈 메시지 컨텍스트 주입 — 이미 활성 상태면(다른 장비로 전환) 즉시 재조회.</summary>
    public void Initialize(OpenDetectionHistoryDialogMessageModel message)
    {
        // 그룹→센서 전환뿐 아니라 단일→다른 단일도 완전 리셋 — Result 칩 이월·이전 센서 잔상 차단(검증 F1/E9, 그룹 경로와 정책 일치)
        var contextChanged = IsGroupMode || DeviceId != message.DeviceId;
        IsGroupMode = false;
        GroupId = 0;
        GroupName = string.Empty;
        _groupSensors.Clear();

        DeviceId = message.DeviceId;
        DeviceName = string.IsNullOrWhiteSpace(message.DeviceName) ? $"장비 {message.DeviceId}" : message.DeviceName!;
        DeviceNumber = message.DeviceNumber;
        if (contextChanged) ResetModeContext();
        NotifyOfPropertyChange(nameof(HeaderText));
        NotifyOfPropertyChange(nameof(IsGroupMode));
        NotifyOfPropertyChange(nameof(SensorColumnVisibility));

        if (IsActive)
        {
            _loadedByInitialize = true;   // (code-review P1-1) 직후 재활성이 겹쳐도 OnActivate 중복 조회 방지
            _ = LoadAsync();
        }
    }

    /// <summary>그룹 오픈 메시지 컨텍스트 주입(FR-09) — 멤버 센서는 조회 시점 현재 멤버십으로 재해석(AD-5).</summary>
    public void Initialize(OpenGroupDetectionHistoryDialogMessageModel message)
    {
        // 모드 전환뿐 아니라 그룹→다른 그룹 전환도 완전 리셋 — 이전 그룹 잔상·칩 on/off 이월 차단(버그헌트 확정 이슈)
        var contextChanged = !IsGroupMode || GroupId != message.GroupId;
        IsGroupMode = true;
        GroupId = message.GroupId;
        GroupName = string.IsNullOrWhiteSpace(message.GroupName) ? $"그룹 {message.GroupId}" : message.GroupName!;
        DeviceId = 0;
        DeviceName = string.Empty;
        DeviceNumber = null;
        ResolveGroupSensors();
        if (contextChanged) ResetModeContext();
        NotifyOfPropertyChange(nameof(HeaderText));
        NotifyOfPropertyChange(nameof(IsGroupMode));
        NotifyOfPropertyChange(nameof(SensorColumnVisibility));

        if (IsActive)
        {
            _loadedByInitialize = true;
            _ = LoadAsync();
        }
    }

    /// <summary>그룹 멤버 센서 해석 — DeviceProvider 역참조 필터(장비의 DeviceGroups.Contains). 순서=DeviceNumber → 시리즈 인덱스 고정.</summary>
    private void ResolveGroupSensors()
    {
        _groupSensors.Clear();
        _groupSensors.AddRange(_deviceProvider.OfType<ISensorDeviceModel>()
            .Where(d => d.DeviceGroups != null && d.DeviceGroups.Contains(GroupId))
            .OrderBy(d => d.DeviceNumber).ThenBy(d => d.Id)
            .Select(d => (d.Id, string.IsNullOrWhiteSpace(d.DeviceName) ? $"장비 {d.Id}" : d.DeviceName!)));
    }

    /// <summary>모드 전환 시 상호 컨텍스트 완전 리셋(PRD 5-B) — 칩/차트/통계/그리드 오염 방지.</summary>
    private void ResetModeContext()
    {
        _all.Clear();
        Chips.Clear();
        SensorChips.Clear();
        FilteredItems.Clear();
        ChartPoints = Array.Empty<SignalChartPoint>();
        SelectedItem = null;
        IsTruncated = false;
        HasLoadError = false;
        RangeText = string.Empty;
        LastUpdatedText = string.Empty;
        TotalCount = 0;
        MaxSignal = 0;
        AvgSignalText = "—";
        TopResultText = "—";
        UnactionedCount = 0;
        NotifyOfPropertyChange(nameof(MaxSignalText));
        NotifyOfPropertyChange(nameof(TopStatLabel));
    }
    #endregion

    #region - Lifecycle -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        if (_loadedByInitialize) { _loadedByInitialize = false; return; }   // Initialize가 이미 조회를 걸었음 — 이중 서버 왕복 방지
        await LoadAsync();
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();   // 닫힘~다음 오픈 사이 CTS 미해제 잔존 방지
        _loadCts = null;
        // stale 플래그 리셋(버그헌트 확정 이슈) — 활성 중 중복 Open으로 소비되지 못한 플래그가 남으면
        // 다음 재오픈의 OnActivate 조회가 1회 통째로 생략되어 이전 컨텍스트 데이터가 그대로 표시된다.
        _loadedByInitialize = false;
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
        // 조회 시점 멤버십 재해석(AD-5 계약 — 검증 NEW-2): 다이얼로그를 열어둔 채 그룹 편성이 바뀌어도
        // 새로고침/기간 변경 재조회가 최신 멤버로 팬아웃하고 헤더 "(센서 N)"도 동기화된다.
        if (IsGroupMode)
        {
            ResolveGroupSensors();
            NotifyOfPropertyChange(nameof(HeaderText));
        }
        if (IsGroupMode ? _groupSensors.Count == 0 : DeviceId <= 0) return;   // 빈 그룹은 진입 게이트가 1차, 여기는 2차 방어(FR-10)

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = new CancellationTokenSource();   // 로컬 참조 — finally의 IsBusy 세대 가드용(검증 C1/Q5)
        _loadCts = cts;
        var ct = cts.Token;

        IsBusy = true;
        HasLoadError = false;
        _failedSensorNames.Clear();
        try
        {
            var (start, end) = ResolveRange();
            // 차트 X축 전체 범위 = 조회 구간 (데이터가 일부 구간에만 있어도 축은 설정 기간 반영 — 런타임 피드백)
            ChartRangeStart = start;
            ChartRangeEnd = end;
            NotifyOfPropertyChange(nameof(ChartRangeStart));
            NotifyOfPropertyChange(nameof(ChartRangeEnd));
            var startText = KoreaTimeHelper.ToServerIso8601(start);
            var endText = KoreaTimeHelper.ToServerIso8601(end);

            List<SignalHistoryItemViewModel> items;
            bool truncated;

            if (!IsGroupMode)
            {
                var (models, sensorTruncated, partialPages) = await FetchSensorPagedAsync(startText, endText, DeviceId, ct);
                truncated = sensorTruncated;
                if (partialPages) _failedSensorNames.Add($"{DeviceName}(일부 페이지)");   // 부분 데이터 표식(Q2/E1)
                // 서버 정렬(created_at desc, id desc 타이브레이크)=최신 우선 — 상한 초과분은 과거 데이터라 절단
                if (models.Count > MAX_LOAD)
                    models = models.Take(MAX_LOAD).ToList();
                // EventId dedup — offset 페이지네이션 중 신규 이벤트 삽입으로 행이 밀리면 페이지 경계 중복 발생 가능(버그헌트 확정)
                items = models.DistinctBy(m => m.Id)
                    .Select(m => new SignalHistoryItemViewModel(m)).ToList();
            }
            else
            {
                // FR-10: 멤버 센서 팬아웃 — 병렬(Task.WhenAll) + 공용 CT 취소 전파.
                // 부분 실패 = 확보분 표시 + 실패 센서 경고 표기(기존 후속 페이지 실패 정책 미러). 전체 실패 = 에러 경로.
                var tasks = _groupSensors.Select(async (sensor, index) =>
                {
                    try
                    {
                        var (models, sensorTruncated, partialPages) = await FetchSensorPagedAsync(startText, endText, sensor.Id, ct);
                        return (Sensor: sensor, Index: index, Models: models, Truncated: sensorTruncated, Failed: false, Partial: partialPages);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _log?.Warning($"[SIGNAL_HISTORY] 그룹 팬아웃 센서 조회 실패 (sensorId={sensor.Id}): {ex.Message}");
                        return (Sensor: sensor, Index: index, Models: new List<IDetectionEventModel>(), Truncated: false, Failed: true, Partial: false);
                    }
                }).ToList();

                var results = await Task.WhenAll(tasks);
                ct.ThrowIfCancellationRequested();

                if (results.All(r => r.Failed))
                    throw new InvalidOperationException("그룹 멤버 센서 전체 조회 실패");
                _failedSensorNames.AddRange(results.Where(r => r.Failed).Select(r => r.Sensor.Name));
                _failedSensorNames.AddRange(results.Where(r => r.Partial).Select(r => $"{r.Sensor.Name}(일부 페이지)"));   // 부분 데이터 표식(Q2/E1)

                // 병합 순서(버그헌트 확정 규약): 센서별 후필터(Fetch 내부) → 병합 → EventId dedup → 최신순 → 총합 500 절단(G-1=(a)).
                // dedup은 서버 필터 회귀 시 N중복 방어 + offset 페이지 경계 중복 방어(정상 서버에서는 no-op).
                var merged = results
                    .SelectMany(r => r.Models.Select(m => new SignalHistoryItemViewModel(m, r.Sensor.Id, r.Sensor.Name, r.Index)))
                    .DistinctBy(i => i.Model.Id)
                    .OrderByDescending(i => i.DateTime)
                    .ToList();
                truncated = results.Any(r => r.Truncated) || merged.Count > MAX_LOAD;
                items = merged.Take(MAX_LOAD).ToList();
            }

            ct.ThrowIfCancellationRequested();

            _all.Clear();
            _all.AddRange(items.OrderByDescending(i => i.DateTime));

            IsTruncated = truncated;
            RebuildChips();
            RebuildSensorChips();
            ApplyFilters();
            var failNote = _failedSensorNames.Count > 0 ? $" · 조회 실패: {string.Join(", ", _failedSensorNames)}" : string.Empty;
            RangeText = $"{start:yyyy-MM-dd HH:mm} ~ {end:yyyy-MM-dd HH:mm} · {_all.Count}건"
                        + (truncated ? " (상한 500건 — 기간을 줄여주세요)" : string.Empty)
                        + failNote;
            LastUpdatedText = $"마지막 갱신 {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException)
        {
            // 재조회/닫기에 의한 취소 — 무시
        }
        catch (Exception ex)
        {
            _log?.Error($"[SIGNAL_HISTORY] 탐지 이력 조회 실패 ({(IsGroupMode ? $"groupId={GroupId}" : $"deviceId={DeviceId}")}): {ex.Message}");
            HasLoadError = true;
            _all.Clear();
            RebuildChips();
            RebuildSensorChips();
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
            // 세대 가드(검증 C1/Q5) — 취소된 구 로드의 지연 finally가 신 로드의 스피너를 조기 소등하지 않도록
            if (ReferenceEquals(_loadCts, cts))
                IsBusy = false;
        }
    }

    /// <summary>원시 페이지 순회 상한(5=MAX_LOAD/PAGE_LIMIT) — 서버 필터 회귀 시 후필터로 카운트가 안 차 무한 순회하는 폭주 방지(버그헌트 확정 부작용 방어).</summary>
    private const int MAX_PAGES = MAX_LOAD / PAGE_LIMIT;

    /// <summary>단일 센서 페이지 순회 조회 — 그룹 팬아웃이 센서별로 재사용(FR-10).
    /// 응답을 요청 센서로 후필터(DTO의 device_id/device.id 검증) — 서버 device_id 필터와 의미 동일(정상 서버=no-op 멱등),
    /// 서버가 필터를 무시하던 실버그(B2/B3)의 재발 방어 + 오귀속 차단.</summary>
    private async Task<(List<IDetectionEventModel> Models, bool Truncated, bool Partial)> FetchSensorPagedAsync(
        string startText, string endText, int sensorId, CancellationToken ct)
    {
        var models = new List<IDetectionEventModel>();
        int page = 1;
        bool truncated = false;
        bool partial = false;
        while (true)
        {
            var response = await _apiService.GetDetectionEventsAsync(
                startText, endText, sensor: sensorId, page: page, limit: PAGE_LIMIT, token: ct);

            if (response is not { Success: true } || response.Data == null)
            {
                if (page == 1)
                    throw new InvalidOperationException(response?.Message ?? "서버 응답이 없습니다.");
                partial = true;   // 후속 페이지 실패 — 확보분만 표시하되 부분 데이터임을 표식(검증 Q2/E1: 침묵 금지)
                break;
            }

            models.AddRange(response.Data
                .Where(dto => dto.DeviceId == sensorId || (dto.Device != null && dto.Device.Id == sensorId))
                .Select(dto => dto.ToDetectionEventModel(_deviceProvider)));

            if (response.Data.Count < PAGE_LIMIT) break;          // 마지막 페이지(원시 건수 기준)
            if (models.Count >= MAX_LOAD) { truncated = true; break; }
            if (page >= MAX_PAGES) { truncated = true; break; }   // 원시 페이지 캡 — 후필터 시대에도 왕복 수 불변 보장
            page++;
        }
        return (models, truncated, partial);
    }
    #endregion

    #region - 필터/통계 (FR-14) -
    private readonly List<SignalHistoryItemViewModel> _all = new();

    public ObservableCollection<ResultChipViewModel> Chips { get; } = new();
    /// <summary>그룹 모드 센서 필터 칩(FR-12) — 멤버 전원, 시리즈 인덱스 고정.</summary>
    public ObservableCollection<SensorChipViewModel> SensorChips { get; } = new();

    /// <summary>[전체] 마스터 토글 — set: 전 칩 일괄 on/off(1회 재적용) / get: 전부 on일 때만 true(개별 토글 시 자동 동기).</summary>
    public bool AllSensorsOn
    {
        get => _allSensorsOn;
        set
        {
            if (_allSensorsOn == value) return;
            _allSensorsOn = value;
            NotifyOfPropertyChange(nameof(AllSensorsOn));
            foreach (var chip in SensorChips)
                chip.SetSilently(value);
            ApplyFilters();
        }
    }

    /// <summary>개별 칩 토글/재구성 후 [전체] 표시 상태 재계산 — setter 경유 금지(일괄 적용 루프 방지).</summary>
    private void SyncAllSensorsOn()
    {
        var allOn = SensorChips.Count > 0 && SensorChips.All(c => c.IsOn);
        if (_allSensorsOn == allOn) return;
        _allSensorsOn = allOn;
        NotifyOfPropertyChange(nameof(AllSensorsOn));
    }
    public ObservableCollection<SignalHistoryItemViewModel> FilteredItems { get; } = new();
    public IReadOnlyList<SignalChartPoint> ChartPoints
    {
        get => _chartPoints;
        private set { _chartPoints = value; NotifyOfPropertyChange(); }
    }

    /// <summary>차트 X축 전체 범위(조회 구간) — SignalChartControl.RangeStart/End 바인딩.</summary>
    public DateTime? ChartRangeStart { get; private set; }
    public DateTime? ChartRangeEnd { get; private set; }

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
    /// <summary>4번째 통계 타일 라벨 — 단일="최다 결과" / 그룹="최다 발생 센서" (FR-13, 그룹 모드 한정 교체).</summary>
    public string TopStatLabel => IsGroupMode ? "최다 발생 센서" : "최다 결과";

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

    /// <summary>그룹 모드 센서 칩 재구성(FR-12) — 멤버 전원 노출(0건 센서 포함), 기존 on/off 보존, 기본 on.
    /// SeriesIndex=멤버 순서 고정(칩 필터로 시리즈가 줄어도 남은 시리즈 색 불변). 단일 모드는 칩 없음.</summary>
    private void RebuildSensorChips()
    {
        var previous = SensorChips.ToDictionary(c => c.DeviceId, c => c.IsOn);
        SensorChips.Clear();
        if (!IsGroupMode) return;
        for (int i = 0; i < _groupSensors.Count; i++)
        {
            var (id, name) = _groupSensors[i];
            bool isOn = !previous.TryGetValue(id, out var prev) || prev;
            SensorChips.Add(new SensorChipViewModel(id, name, i, isOn, ApplyFilters));
        }
    }

    private void ApplyFilters()
    {
        SyncAllSensorsOn();   // 개별 칩 토글 경로 포함 — [전체] 표시 상태 동기
        var enabled = Chips.Where(c => c.IsOn).Select(c => c.Result).ToHashSet();
        var enabledSensors = SensorChips.Where(c => c.IsOn).Select(c => c.DeviceId).ToHashSet();

        var filtered = _all
            .Where(i => enabled.Contains(i.Result))
            .Where(i => !OnlyUnactioned || !i.IsActioned)
            .Where(i => !IsGroupMode || enabledSensors.Contains(i.SensorId))   // FR-12 센서 칩 — 차트/그리드/통계 3면 일관
            .ToList();

        FilteredItems.Clear();
        foreach (var item in filtered)
            FilteredItems.Add(item);

        // 차트 — signal>0만, 시간 오름차순 (FR-13 / null-안전 규약). 그룹 모드는 시리즈 키+센서명 부여(FR-11)
        ChartPoints = filtered
            .Where(i => i.HasSignal)
            .OrderBy(i => i.DateTime)
            .Select(i => new SignalChartPoint(i.DateTime, i.Signal!.Value, i.IsActioned, EnumKoreanMap.To(i.Result), i,
                                              IsGroupMode ? i.SeriesIndex : 0,
                                              IsGroupMode ? i.DeviceName : null))
            .ToList();

        // 통계
        TotalCount = filtered.Count;
        var signals = filtered.Where(i => i.HasSignal).Select(i => i.Signal!.Value).ToList();
        MaxSignal = signals.Count > 0 ? signals.Max() : 0;
        AvgSignalText = signals.Count > 0 ? signals.Average().ToString("N0") : "—";
        if (IsGroupMode)
        {
            // FR-13: 최다 발생 센서 — 동률 타이브레이크=최근 발생 우선
            var topSensor = filtered.GroupBy(i => i.SensorId)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Max(i => i.DateTime))
                .FirstOrDefault();
            TopResultText = topSensor != null
                ? $"{topSensor.First().DeviceName} ({topSensor.Count()}건)"
                : "—";
        }
        else
        {
            var top = filtered.GroupBy(i => i.Result).OrderByDescending(g => g.Count()).FirstOrDefault();
            TopResultText = top != null ? $"{EnumKoreanMap.To(top.Key)} ({top.Count()}건)" : "—";
        }
        UnactionedCount = filtered.Count(i => !i.IsActioned);
        NotifyOfPropertyChange(nameof(MaxSignalText));
        NotifyOfPropertyChange(nameof(TopStatLabel));
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
                Explain = ActionReportRules.NO_PERMISSION_TEXT
            });
            return;
        }

        // 조치 완료 행도 재보고 허용(사용자 요청, 2026-08-06 — 기존 "이미 조치보고된 이벤트" 차단 제거).
        // 동시 진행 중복만 카드 VM의 IActionReportGuard가 차단(완료 후 재보고는 통과) — 센서/그룹 모드 공용 경로.
        if (item.IsActioned)
            _log?.Info($"[SIGNAL_HISTORY] 조치 완료 이벤트 재보고 진입 (eventId={item.EventId})");

        // 행과 독립된 임시 카드 VM 재구성(SendAction/멱등가드는 카드 VM 보유)
        var card = new DetectionEventCardViewModel(_eventAggregator, _log, item.Model);
        IoC.Get<DetectionReportDialogViewModel>().UpdateData(card, IoC.Get<IAccountModel>());
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel { EventType = "DETECTION" });
    }

    private bool CanControlEvents()
    {
        // 조치보고 = 서버 events:edit — ActionReportRules 참조(종전 control 은 운영자를 403 으로 보냈다).
        try { return ActionReportRules.CanReport(IoC.Get<IPermissionService>()); }
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

    /// <summary>그룹 모드 멤버 센서 (Id, 표시명) — Initialize(그룹) 시점 현재 멤버십으로 재해석(AD-5), 순서=시리즈 인덱스.</summary>
    private readonly List<(int Id, string Name)> _groupSensors = new();
    /// <summary>팬아웃 부분 실패 센서명 — 푸터 경고 표기(FR-10).</summary>
    private readonly List<string> _failedSensorNames = new();

    private bool _loadedByInitialize;   // (code-review P1-1) Initialize 즉시조회 ↔ OnActivate 조회 중복 가드
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
    private bool _allSensorsOn = true;   // [전체] 마스터 토글 백킹 — 칩 재구성/개별 토글 시 SyncAllSensorsOn으로 동기
    #endregion
}
