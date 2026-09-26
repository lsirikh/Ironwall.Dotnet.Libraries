using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;  // SuppressionRules(폼 검증·중복 판정 순수 규칙)
using Ironwall.Dotnet.Libraries.Events.Ui.Models;   // SimpleCommand
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 이벤트 억제(정비 창) 스케줄 관리 패널 — 생성/목록/무한스크롤/취소(soft-cancel).
                  대상=장비 복수 / 그룹 복수 / 전체(혼합 없음). GrantManagement 선례 패턴.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class EventSuppressionSchedulePanelViewModel : BasePanelViewModel,
    IHandle<CallCancelSuppressionMessageModel>,
    IHandle<CallBulkDeleteSuppressionMessageModel>
{
    #region - Ctors -
    public EventSuppressionSchedulePanelViewModel(
        IEventAggregator eventAggregator,
        ILogService log,
        IEventSuppressionApiService api,
        DeviceProvider deviceProvider,
        DeviceGroupProvider groupProvider,
        ISuppressionActiveMonitor? monitor = null)
        : base(eventAggregator, log)
    {
        _api = api;
        DeviceProvider = deviceProvider;
        DeviceGroupProvider = groupProvider;
        // 활성 억제 단일 출처. 미주입(테스트·구버전 배선)이면 폴백 캐시로 동작한다.
        _monitor = monitor;

        _windowStart = DateTime.Now;
        _windowEnd = DateTime.Now.AddHours(1);

        SelectedDevices.CollectionChanged += OnTargetSelectionChanged;
        SelectedGroups.CollectionChanged += OnTargetSelectionChanged;

        LoadMoreCommand = new SimpleCommand(async () => await LoadNextPageAsync(_cancellationTokenSource?.Token ?? CancellationToken.None));
    }
    #endregion

    #region - Providers / Collections -
    /// <summary>장비 선택 소스(ComboBox ItemsSource).</summary>
    public DeviceProvider DeviceProvider { get; }
    /// <summary>그룹 선택 소스(ComboBox ItemsSource).</summary>
    public DeviceGroupProvider DeviceGroupProvider { get; }

    /// <summary>선택된 대상 장비(칩, 복수).</summary>
    public ObservableCollection<IBaseDeviceModel> SelectedDevices { get; } = new();
    /// <summary>선택된 대상 그룹(칩, 복수).</summary>
    public ObservableCollection<IDeviceGroupModel> SelectedGroups { get; } = new();

    /// <summary>칩 트레이 헤더 — 선택 개수. 트레이는 고정 높이라 개수는 숫자로만 보여준다.</summary>
    public string SelectedDeviceCountText => $"선택 {SelectedDevices.Count}개";
    /// <summary>칩 트레이 헤더 — 선택 개수(그룹).</summary>
    public string SelectedGroupCountText => $"선택 {SelectedGroups.Count}개";
    /// <summary>빈 트레이 안내 문구 노출 판정(장비).</summary>
    public bool HasSelectedDevices => SelectedDevices.Count > 0;
    /// <summary>빈 트레이 안내 문구 노출 판정(그룹).</summary>
    public bool HasSelectedGroups => SelectedGroups.Count > 0;
    /// <summary>억제 스케줄 목록(DataGrid ItemsSource).</summary>
    public ObservableCollection<EventSuppressionScheduleItemViewModel> Schedules { get; } = new();
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        var perm = ResolvePermissionService();
        if (perm != null) perm.PermissionsChanged += OnPermissionsChanged;
        HookMonitor();
        // 조회 권한(events:view) 게이팅 — 권한 없으면 목록을 불러오지 않는다(서버 403 최종 권위, UI는 보조).
        if (!CanViewEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = SuppressionPermissionText.ViewDenied });
            return;
        }
        await LoadAllAsync(_cancellationTokenSource?.Token ?? cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        var perm = ResolvePermissionService();
        if (perm != null) perm.PermissionsChanged -= OnPermissionsChanged;
        UnhookMonitor();
        await base.OnDeactivateAsync(close, cancellationToken);
    }
    #endregion

    #region - 권한 게이팅 (events 도메인) — DetectionEventPanel 패턴 -
    private IPermissionService? _permissionService;
    private bool _permissionResolved;
    private IPermissionService? ResolvePermissionService()
    {
        if (_permissionResolved) return _permissionService;
        try { _permissionService = IoC.Get<IPermissionService>(); _permissionResolved = _permissionService != null; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(EventSuppressionSchedulePanelViewModel)}] PermissionService 미해석(전체허용 폴백): {ex.Message}");
            _permissionService = null;
        }
        return _permissionService;
    }
    private bool CanViewEvents() => ResolvePermissionService()?.CanView("events") ?? true;
    private bool CanEditEvents() => ResolvePermissionService()?.CanEdit("events") ?? true;
    private bool CanDelEvents()  => ResolvePermissionService()?.CanDelete("events") ?? true;

    /// <summary>생성/저장 버튼 활성 — events:edit + 폼 유효(기간 상한 포함).</summary>
    public bool CanCreate =>
        CanEditEvents()
        && !string.IsNullOrWhiteSpace(Name)
        && (IsUnlimitedEffective || WindowEnd > WindowStart)
        && IsWindowLengthValid
        && WeeklyFormError is null
        // 3중 방어 마지막 — 어떤 경로로도 '단발 + window_end 없음' 이 나가지 못하게 한다.
        && !(IsOneShotMode && IsWindowEndUnlimited)
        && TargetType switch
        {
            "device" => SelectedDevices.Count > 0,
            "group" => SelectedGroups.Count > 0,
            _ => true,   // all
        };

    /// <summary>
    /// 생성 버튼 ToolTip — 비활성일 때 <b>왜 못 누르는지</b>를 알려준다.
    /// (비활성 버튼은 클릭이 안 되므로 눌러서 사유를 확인할 수 없다 → 호버로 알 수 있어야 한다.)
    /// </summary>
    public string CreateHintText
    {
        get
        {
            if (CanCreate) return "입력한 대상·시간으로 억제 스케줄을 만듭니다.";
            if (!CanEditEvents()) return SuppressionPermissionText.EditDenied;
            if (string.IsNullOrWhiteSpace(Name)) return "작업명을 입력하세요.";
            if (!IsWindowLengthValid) return WindowLengthWarningText.TrimStart('⚠', ' ') + ".";
            if (WeeklyFormError is { } we) return we + ".";
            if (!IsUnlimitedEffective && WindowEnd <= WindowStart) return "종료 시각이 시작 시각보다 뒤여야 합니다.";
            return TargetType == "group" ? "대상 그룹을 1개 이상 선택하세요." : "대상 장비를 1개 이상 선택하세요.";
        }
    }

    /// <summary>
    /// 폼 검증 오류 1줄 — <b>기간 상한 + 반복 검증 3종</b>을 하나로 모은다.
    /// <para>권한·작업명·대상 분기는 여기 넣지 않는다(그건 <see cref="CreateHintText"/> ToolTip 전용).</para>
    /// ⚠ 이 프로퍼티가 없으면 XAML 이 빈 바인딩에 붙어 <b>이미 출하된 기간 경고가 조용히 사라진다</b>.
    /// </summary>
    public string FormErrorText
    {
        get
        {
            if (!IsWindowLengthValid) return WindowLengthWarningText;
            if (WeeklyFormError is { } we) return "⚠ " + we;
            return string.Empty;
        }
    }

    /// <summary>폼 오류 줄 표시 조건.</summary>
    public bool HasFormError => !string.IsNullOrEmpty(FormErrorText);

    /// <summary>
    /// 주간 반복 폼 검증 결과(정상이면 null).
    /// <para>서버(API 6.3.4)가 422 로 막는 입력을 저장 전에 걸러 사용자가 422 를 보기 전에 고치게 한다.</para>
    /// </summary>
    public string? WeeklyFormError
    {
        get
        {
            if (!IsWeeklyMode) return null;
            var basic = SuppressionRules.ValidateWeeklyForm(
                DaysOfWeekMask, DailyStart?.TimeOfDay, DailyEnd?.TimeOfDay);
            if (basic is not null) return basic;

            // 유효기간 안에 회차가 하나도 없으면 서버가 422 다 —
            // 그런 창은 목록에 'active' 로 살아있는 것처럼 보이면서 영원히 발동하지 않는다.
            if (DailyStart is not { } s || DailyEnd is not { } e) return null;
            return SuppressionRules.DescribeUnreachable(
                DaysOfWeekMask, s.TimeOfDay, e.TimeOfDay,
                WindowStart, IsUnlimitedEffective ? null : WindowEnd);
        }
    }

    /// <summary>
    /// 창 길이 상한 검증. 단발 30일(순수 클라 방어) / 반복 366일(서버 게이트) / 무제한 검사 스킵.
    /// </summary>
    public bool IsWindowLengthValid => SuppressionRules.IsWindowLengthValidFor(
        WindowStart, WindowEnd, RecurrenceMode, IsUnlimitedEffective);

    /// <summary>현재 모드의 유효기간 상한(일) — 문구 생성용.</summary>
    public int EffectiveMaxWindowDays => SuppressionRules.MaxWindowDaysFor(RecurrenceMode);

    /// <summary>취소(삭제) 권한 — 행 취소 버튼 게이팅 보조.</summary>
    public bool CanDelete => CanDelEvents();

    private void OnPermissionsChanged()
    {
        Execute.OnUIThread(() =>
        {
            NotifyOfPropertyChange(nameof(CanCreate));
        NotifyOfPropertyChange(nameof(CreateHintText));
            NotifyOfPropertyChange(nameof(CanDelete));
            NotifySelectionState();   // events:delete 회수 시 '선택 삭제' 버튼/전체선택 즉시 비활성 반영
        });
    }
    #endregion

    #region - Binding Methods -
    /// <summary>헤더 X 버튼(ModernPanelCloseButton, x:Name="Close") — 패널 닫기(ReportConsole 패턴).</summary>
    public Task Close() => TryCloseAsync();

    /// <summary>Monitor 구독 — 반드시 <see cref="OnDeactivateAsync"/> 와 짝으로 건다(SingleInstance 누수 방지).</summary>
    private void HookMonitor()
    {
        if (_monitor is null || _monitorHooked) return;
        _monitor.ActiveChanged += OnMonitorActiveChanged;
        _monitorHooked = true;
        // 패널 진입 즉시 1회 — 30초 주기를 기다리면 배너가 늦게 뜬다.
        _monitor.RequestImmediatePoll("panel-open");
    }

    private void UnhookMonitor()
    {
        if (_monitor is null || !_monitorHooked) return;
        _monitor.ActiveChanged -= OnMonitorActiveChanged;
        _monitorHooked = false;
    }

    private void OnMonitorActiveChanged()
    {
        NotifyOfPropertyChange(nameof(HasActiveSuppression));
        NotifyOfPropertyChange(nameof(HasActiveBanner));
        NotifyOfPropertyChange(nameof(IsActiveStale));
        NotifyOfPropertyChange(nameof(ActiveStaleText));
        NotifyOfPropertyChange(nameof(ActiveCountText));
        NotifyOfPropertyChange(nameof(DuplicateWarningText));
        NotifyOfPropertyChange(nameof(HasDuplicateWarning));
    }

    public async Task OnClickReloadButton()
        => await LoadAllAsync(_cancellationTokenSource?.Token ?? CancellationToken.None);

    /// <summary>대상 유형 전환(장비/그룹/전체 배타).</summary>
    public void SetTargetType(string type)
    {
        if (_targetType == type) return;
        _targetType = type;
        NotifyOfPropertyChange(nameof(TargetType));
        NotifyOfPropertyChange(nameof(IsDeviceMode));
        NotifyOfPropertyChange(nameof(IsGroupMode));
        NotifyOfPropertyChange(nameof(IsAllMode));
        NotifyOfPropertyChange(nameof(CanCreate));
        NotifyOfPropertyChange(nameof(CreateHintText));
        NotifyDuplicateWarning();   // 모드 전환 시 중복 경고 재평가
    }

    /// <summary>칩 제거 — 장비.</summary>
    public void RemoveDevice(IBaseDeviceModel device) { if (device != null) SelectedDevices.Remove(device); }
    /// <summary>칩 제거 — 그룹.</summary>
    public void RemoveGroup(IDeviceGroupModel group) { if (group != null) SelectedGroups.Remove(group); }

    /// <summary>선택 장비 전체 해제 — 칩이 많을 때 하나씩 ✕ 누르는 것을 대체.</summary>
    public void ClearDevices() => SelectedDevices.Clear();
    /// <summary>선택 그룹 전체 해제.</summary>
    public void ClearGroups() => SelectedGroups.Clear();

    /// <summary>억제 창 생성 — 클라 1차 검증 후 POST(대상 배열 + KST ISO8601). 성공 시 폼 리셋 + 재조회.</summary>
    public async Task ClickCreate()
    {
        if (!CanEditEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = SuppressionPermissionText.EditDenied });
            return;
        }
        if (!CanCreate)
        {
            var why = !IsWindowLengthValid
                ? $"억제 기간이 너무 깁니다. 최대 {MAX_WINDOW_DAYS}일까지 지정할 수 있습니다."
                : "작업명, 대상(1개 이상), 시간(종료가 시작보다 뒤)을 확인하세요.";
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "억제 스케줄 만들기", Explain = why });
            return;
        }
        try
        {
            // 서버 8.0: unit_id 생략은 기본 부대 귀속 + 서버 로그 경고("다음 차수부터 필수"). 8.0 미만은 키가 없다 — SuppressionUnitStamp.
            var unitId = await SuppressionUnitStamp.ResolveForCreateAsync(
                SuppressionUnitStamp.ResolveFromIoC(), _log, nameof(EventSuppressionSchedulePanelViewModel)).ConfigureAwait(true);
            var dto = new EventSuppressionScheduleCreateDto
            {
                Name = Name!.Trim(),
                UnitId = unitId,
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                TargetType = TargetType,
                TargetDeviceIds = TargetType == "device" ? SelectedDevices.Select(d => d.Id).ToList() : new(),
                TargetGroupIds = TargetType == "group" ? SelectedGroups.Select(g => g.Id).ToList() : new(),
                TargetSide = TargetSide,
                EventScope = EventScope,
                WindowStart = KoreaTimeHelper.ToServerIso8601(WindowStart),
                // ⚠ 무제한은 키를 생략하면 422 — 명시적 null 이어야 한다(서버 model_fields_set 검사).
                //    NullValueHandling.Ignore 를 붙이지 않은 이유가 이것이다.
                WindowEnd = IsUnlimitedEffective ? null : KoreaTimeHelper.ToServerIso8601(WindowEnd),
                RecurrenceType = IsWeeklyMode ? "weekly" : "none",
                DaysOfWeek = IsWeeklyMode ? DaysOfWeekMask : null,
                // ⚠ 여기서 offset/Z 가 붙으면 즉시 422.
                //    DTO 가 string 인 이유 — ApiService 공통 설정(DateTimeZoneHandling.Local)이
                //    DateTime 을 만나면 "+09:00" 을 붙여 버린다.
                DailyStart = IsWeeklyMode ? SuppressionRules.FormatDailyTime(DailyStart) : null,
                DailyEnd = IsWeeklyMode ? SuppressionRules.FormatDailyTime(DailyEnd) : null,
            };
            var res = await _api.CreateSuppressionScheduleAsync(dto);
            if (res.Success)
            {
                // (§6) 대상 오지정 사고 방지 — 서버가 확정한 대상 id를 장비/그룹 '이름'으로 되풀이 표시.
                var echo = res.Data is null ? null : BuildTargetEcho(res.Data);
                ResetForm();
                await LoadAllAsync();
                if (!string.IsNullOrEmpty(echo))
                    await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                    { Title = "억제 스케줄 만들기 완료", Explain = echo! });
            }
            else
            {
                // 서버 원문은 로그로만 — 팝업에는 무엇이 안 됐고 어떻게 하면 되는지만 쓴다.
                _log?.Warning($"[Suppression] 생성 거절: {res.Error?.Message ?? res.Message}");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "억제 스케줄 만들기", Explain = "억제 스케줄을 만들지 못했습니다. 입력한 내용을 확인하고 다시 시도하세요." });
            }
        }
        catch (Exception ex) { _log?.Error($"[Suppression] 생성 실패: {ex.Message}"); }
    }

    /// <summary>행 취소 클릭 → Confirm(즉시 삭제 않음). Yes 시 CallCancelSuppressionMessageModel 발행.</summary>
    public async Task OnClickCancel(EventSuppressionScheduleItemViewModel item)
    {
        if (item is null) return;
        if (!CanDelEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = SuppressionPermissionText.DeleteDenied });
            return;
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = SuppressionConsoleViewModel.CancelConfirmTitle,
            Explain = SuppressionConsoleViewModel.CancelConfirmText(item.Name),
            MessageModel = new CallCancelSuppressionMessageModel { ScheduleId = item.Id }
        });
    }

    /// <summary>Confirm→Yes 후 실제 DELETE(soft-cancel) + 목록 갱신.</summary>
    public async Task HandleAsync(CallCancelSuppressionMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _api.CancelSuppressionScheduleAsync(message.ScheduleId);
            if (res.Success)
            {
                await LoadAllAsync();   // 목록 + /active 캐시 동시 갱신
                // (§5-B) 겹친 창 잔존 경고 — 하나를 취소해도 다른 활성 창이 계속 억제할 수 있다.
                // ⚠ 여기서는 모니터 스냅샷(최대 30초 묵음)이 아니라 **방금 서버가 확인해 준 값**을 읽는다.
                //    LoadAllAsync 안의 RefreshActiveAsync 가 await 로 _activeCache 를 갱신한 직후다.
                //    모니터를 읽으면 방금 취소한 창을 그대로 세어 "1건 남아 있습니다" 라고 거짓말한다
                //    — 목록엔 이미 '취소'로 보이므로 운용자는 찾을 수 없는 유령을 찾게 되고
                //      안전 경고 자체를 불신하게 된다.
                var residual = FreshActiveWindows.Count;
                if (residual > 0)
                    await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                    {
                        Title = SuppressionConsoleViewModel.CancelConfirmTitle,
                        Explain = $"취소했지만 아직 진행 중인 억제 스케줄이 {residual}건 남아 있습니다.\n"
                                + "해당 장비가 계속 억제될 수 있으니 목록에서 '진행중' 항목을 확인하세요."
                    });
            }
            else
            {
                _log?.Warning($"[Suppression] 취소 거절(#{message.ScheduleId}): {res.Error?.Message ?? res.Message}");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = SuppressionConsoleViewModel.CancelConfirmTitle, Explain = SuppressionConsoleViewModel.CancelFailedText });
            }
        }
        catch (Exception ex) { _log?.Error($"[Suppression] 취소 실패: {ex.Message}"); }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
        }
    }

    /// <summary>선택 삭제(체크된 취소/종료 행 일괄 하드삭제) → Confirm 후 CallBulkDeleteSuppressionMessageModel 발행.</summary>
    public async Task OnClickDeleteSelected()
    {
        if (!CanDelEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = SuppressionPermissionText.DeleteDenied });
            return;
        }
        var ids = Schedules.Where(s => s.IsSelected && s.IsDeletable).Select(s => s.Id).ToList();
        if (ids.Count == 0)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "억제 스케줄 삭제", Explain = "삭제할 취소/종료 항목을 체크하세요." });
            return;
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "억제 스케줄 삭제",
            Explain = $"선택한 {ids.Count}건을 목록에서 완전 삭제합니다.\n삭제 후에는 복구할 수 없습니다. 계속하시겠습니까?",
            MessageModel = new CallBulkDeleteSuppressionMessageModel { Ids = ids }
        });
    }

    /// <summary>취소·종료 항목 <b>모두 정리</b>(§8.2 권장 UI) — 현재 목록의 terminal 행 전체를 확인 후 일괄 하드삭제.</summary>
    public async Task OnClickCleanupAll()
    {
        if (!CanDelEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = SuppressionPermissionText.DeleteDenied });
            return;
        }
        var ids = Schedules.Where(s => s.IsDeletable).Select(s => s.Id).ToList();
        if (ids.Count == 0)
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "억제 스케줄 정리", Explain = "정리할 취소/종료 항목이 없습니다." });
            return;
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "취소·종료 항목 모두 정리",
            Explain = $"현재 목록의 취소/종료 항목 {ids.Count}건을 모두 삭제합니다.\n삭제 후에는 복구할 수 없습니다. 계속하시겠습니까?",
            MessageModel = new CallBulkDeleteSuppressionMessageModel { Ids = ids }
        });
    }

    /// <summary>Confirm→Yes 후 실제 일괄 하드삭제(POST /bulk-delete) + 목록 갱신. 활성/예정 skip 시 안내.</summary>
    public async Task HandleAsync(CallBulkDeleteSuppressionMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _api.BulkDeleteSuppressionSchedulesAsync(message.Ids);
            if (res.Success)
            {
                var deleted = res.Data?.DeletedIds?.Count ?? 0;
                var skipped = res.Data?.SkippedIds?.Count ?? 0;
                var notFound = res.Data?.NotFoundIds?.Count ?? 0;
                await LoadAllAsync();
                if (skipped > 0 || notFound > 0)
                {
                    var msg = $"{deleted}건을 삭제했습니다.";
                    if (skipped > 0) msg += $"\n{skipped}건은 진행 중/예정이라 삭제할 수 없습니다. 먼저 취소하세요.";
                    if (notFound > 0) msg += $"\n{notFound}건은 이미 삭제된 항목이라 제외했습니다.";
                    await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                    { Title = "억제 스케줄 삭제", Explain = msg });
                }
            }
            else
            {
                // 404/405 = 이 서버에 /bulk-delete 가 없다(구버전 서버). 서버 원문은 로그로만 남긴다.
                _log?.Warning($"[Suppression] 일괄 삭제 거절({res.StatusCode}): {res.Error?.Message ?? res.Message}");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "억제 스케줄 삭제",
                    Explain = SuppressionConsoleViewModel.BulkDeleteFailedText(res.StatusCode is 404 or 405),
                });
            }
        }
        catch (Exception ex) { _log?.Error($"[Suppression] 일괄 삭제 실패: {ex.Message}"); }
        finally
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
        }
    }

    /// <summary>행 체크박스 변경 콜백 — 선택수/버튼/전체선택 상태 갱신.</summary>
    private void OnItemSelectionChanged() => NotifySelectionState();

    private void NotifySelectionState()
    {
        NotifyOfPropertyChange(nameof(SelectedDeleteCount));
        NotifyOfPropertyChange(nameof(CanDeleteSelected));
        NotifyOfPropertyChange(nameof(DeleteSelectedText));
        NotifyOfPropertyChange(nameof(HasDeletableRows));
        NotifyOfPropertyChange(nameof(SelectAllDeletable));
        NotifyOfPropertyChange(nameof(CanCleanupAll));
    }
    #endregion

    #region - Processes -
    private void OnTargetSelectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyOfPropertyChange(nameof(CanCreate));
        NotifyOfPropertyChange(nameof(CreateHintText));
        // 칩 트레이 헤더(개수·비었음 안내·모두 지우기 활성) 갱신
        NotifyOfPropertyChange(nameof(SelectedDeviceCountText));
        NotifyOfPropertyChange(nameof(SelectedGroupCountText));
        NotifyOfPropertyChange(nameof(HasSelectedDevices));
        NotifyOfPropertyChange(nameof(HasSelectedGroups));
        NotifyDuplicateWarning();
    }

    /// <summary>
    /// (§7) 활성 억제 창 캐시 갱신 — 취소 후 잔존 확인(§5-B)·중복 사전 경고의 데이터 소스.
    /// 실패해도 목록 기능을 막지 않는다(경고성 보조 정보).
    /// </summary>
    private async Task RefreshActiveAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _api.GetActiveSuppressionSchedulesAsync(ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;
            _activeCache = res.Success && res.Data is not null ? res.Data : new List<EventSuppressionScheduleDto>();
            // 표시용 SSOT(모니터)도 따라오게 앞당긴다 — 안 그러면 배너가 최대 30초 옛 건수를 보인다.
            _monitor?.RequestImmediatePoll("suppression-refresh");
        }
        catch (Exception ex) { _log?.Warning($"[Suppression] /active 갱신 실패(무시): {ex.Message}"); }
        Execute.OnUIThread(() =>
        {
            NotifyOfPropertyChange(nameof(ActiveCountText));
            NotifyOfPropertyChange(nameof(HasActiveBanner));
            NotifyOfPropertyChange(nameof(IsActiveStale));
            NotifyOfPropertyChange(nameof(ActiveStaleText));
            NotifyOfPropertyChange(nameof(HasActiveSuppression));
            NotifyDuplicateWarning();
        });
    }

    private void NotifyDuplicateWarning()
    {
        NotifyOfPropertyChange(nameof(DuplicateWarningText));
        NotifyOfPropertyChange(nameof(HasDuplicateWarning));
    }

    /// <summary>완료 팝업에 이름을 그대로 나열할 최대 개수 — 초과분은 "외 N개"로 접는다.</summary>
    private const int ECHO_NAME_LIMIT = 3;

    /// <summary>
    /// (§6) 생성 응답의 대상 id를 장비/그룹 '이름'으로 되풀이 — 운영자 육안 확인용.
    /// <para>⚠ 한 줄에 하나씩 전량 나열하면 정보 팝업이 고정 높이라 <b>목록이 잘려 읽을 수 없다</b>
    /// (장비 6개만 돼도 위아래가 잘림). 그래서 <b>총 개수(안전상 핵심) + 앞 몇 개 이름 + "외 N개"</b>
    /// 한 문장으로 접는다. 전체 목록은 아래 스케줄 표의 '대상' 열에서 확인한다.</para>
    /// </summary>
    private string BuildTargetEcho(EventSuppressionScheduleDto dto)
    {
        switch (dto.TargetType)
        {
            case "device":
                var deviceIds = dto.TargetDeviceIds ?? new();
                return BuildEchoSentence("장비", deviceIds.Count, deviceIds.Take(ECHO_NAME_LIMIT).Select(id =>
                    DeviceProvider?.CollectionEntity.FirstOrDefault(d => d.Id == id)?.DeviceName is string n && !string.IsNullOrEmpty(n)
                        ? n : $"#{id}"));
            case "group":
                var groupIds = dto.TargetGroupIds ?? new();
                return BuildEchoSentence("그룹", groupIds.Count, groupIds.Take(ECHO_NAME_LIMIT).Select(id =>
                    DeviceGroupProvider?.CollectionEntity.FirstOrDefault(g => g.Id == id)?.Name is string n && !string.IsNullOrEmpty(n)
                        ? n : $"#{id}"));
            default:
                return $"전체 대상 · {SideLabel(dto.TargetSide)}에 억제 스케줄을 만들었습니다.";
        }
    }

    /// <summary>"정문, 주차장, 외곽_북측 외 3개 장비에 억제 창을 생성했습니다." 형태로 접는다.</summary>
    private static string BuildEchoSentence(string kindLabel, int total, IEnumerable<string> sampleNames)
    {
        var names = sampleNames.ToList();
        if (total == 0) return $"대상 {kindLabel}이(가) 없습니다.";

        var head = string.Join(", ", names);
        var rest = total - names.Count;
        var subject = rest > 0 ? $"{head} 외 {rest}개 {kindLabel}" : $"{head}({kindLabel} {total}개)";
        return $"{subject}에 억제 스케줄을 만들었습니다.\n대상이 맞는지 아래 목록에서 확인하세요.";
    }

    /// <summary>side 코드 → 표시 문구.</summary>
    private static string SideLabel(string? side) => side switch
    {
        "detection" => "감지",
        "surveillance" => "감시",
        _ => "감지+감시",
    };

    public void ResetForm()
    {
        Name = string.Empty;
        Description = null;
        SelectedDevices.Clear();
        SelectedGroups.Clear();
        // ⚠ 순서 고정 — IsWeeklyMode 를 먼저 되돌려야 그 setter 의 정리 로직이
        //    WindowEnd/무제한을 덮어쓰는 일이 없다.
        IsOneShotMode = true;
        _isWindowEndUnlimited = false;
        _daysOfWeekMask = SuppressionRules.DaysWeekdayPreset;
        _dailyStart = DateTime.Today.AddHours(8);
        _dailyEnd = DateTime.Today.AddHours(21);
        WindowStart = DateTime.Now;
        WindowEnd = DateTime.Now.AddHours(1);
        NotifyRecurrenceChanged();
        NotifyOfPropertyChange(nameof(CanCreate));
        NotifyOfPropertyChange(nameof(CreateHintText));
    }

    private async Task LoadAllAsync(CancellationToken ct = default)
    {
        _currentPage = 0; _totalPages = 1; _totalCount = 0;
        try
        {
            var res = await _api.GetSuppressionSchedulesAsync(
                page: 1, limit: PAGE_SIZE, status: ApiFilterStatus, targetType: ApiFilterTargetType, token: ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;
            if (res.Success && res.Data is not null)
            {
                _totalCount = res.Pagination?.Total ?? res.Total ?? res.Data.Count;
                _currentPage = res.Pagination?.Page ?? 1;
                _totalPages = _totalCount > 0 ? (int)Math.Ceiling(_totalCount / (double)PAGE_SIZE) : 1;
                Execute.OnUIThread(() =>
                {
                    Schedules.Clear();
                    foreach (var d in res.Data) Schedules.Add(new EventSuppressionScheduleItemViewModel(d, DeviceProvider, DeviceGroupProvider, OnItemSelectionChanged));
                    NotifyOfPropertyChange(nameof(LoadedCountText));
                    NotifyOfPropertyChange(nameof(HasMorePages));
                    NotifySelectionState();
                });
                await RefreshActiveAsync(ct).ConfigureAwait(false);   // (§5-B/§7) 활성 창 캐시 동기 갱신
            }
            else if (!res.Success)
            {
                _log?.Warning($"[Suppression] 목록 거절: {res.Error?.Message ?? res.Message}");
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "억제 스케줄", Explain = "목록을 불러오지 못했습니다. 갱신 버튼으로 다시 시도하세요." });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[Suppression] 전체 로드 실패: {ex.Message}"); }
    }

    private async Task LoadNextPageAsync(CancellationToken ct = default)
    {
        if (_isLoadingMore || !HasMorePages || ct.IsCancellationRequested) return;
        _isLoadingMore = true; IsLoadingMore = true;
        try
        {
            var res = await _api.GetSuppressionSchedulesAsync(
                page: _currentPage + 1, limit: PAGE_SIZE, status: ApiFilterStatus, targetType: ApiFilterTargetType, token: ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested || !res.Success || res.Data is null) return;
            _totalCount = res.Pagination?.Total ?? res.Total ?? _totalCount;
            _currentPage = res.Pagination?.Page ?? (_currentPage + 1);
            _totalPages = _totalCount > 0 ? (int)Math.Ceiling(_totalCount / (double)PAGE_SIZE) : _totalPages;
            Execute.OnUIThread(() =>
            {
                foreach (var d in res.Data) Schedules.Add(new EventSuppressionScheduleItemViewModel(d, DeviceProvider, DeviceGroupProvider, OnItemSelectionChanged));
                NotifyOfPropertyChange(nameof(LoadedCountText));
                NotifyOfPropertyChange(nameof(HasMorePages));
                NotifySelectionState();
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[Suppression] 다음 페이지 실패: {ex.Message}"); }
        finally { _isLoadingMore = false; IsLoadingMore = false; }
    }
    #endregion

    #region - Properties (Form) -
    private string? _name = string.Empty;
    public string? Name { get => _name; set { _name = value; NotifyOfPropertyChange(nameof(Name)); NotifyOfPropertyChange(nameof(CanCreate));
        NotifyOfPropertyChange(nameof(CreateHintText)); } }

    private string? _description;
    public string? Description { get => _description; set { _description = value; NotifyOfPropertyChange(nameof(Description)); } }

    private string _targetType = "device";
    /// <summary>대상 모드(배타): device / group / all.</summary>
    public string TargetType { get => _targetType; set => SetTargetType(value); }
    public bool IsDeviceMode { get => _targetType == "device"; set { if (value) SetTargetType("device"); } }
    public bool IsGroupMode  { get => _targetType == "group";  set { if (value) SetTargetType("group"); } }
    public bool IsAllMode    { get => _targetType == "all";    set { if (value) SetTargetType("all"); } }

    private IBaseDeviceModel? _deviceToAdd;
    /// <summary>장비 추가 ComboBox — 선택 시 칩에 추가하고 비운다.</summary>
    public IBaseDeviceModel? DeviceToAdd
    {
        get => _deviceToAdd;
        set
        {
            _deviceToAdd = value;
            if (value != null && !SelectedDevices.Any(d => d.Id == value.Id)) SelectedDevices.Add(value);
            _deviceToAdd = null;
            NotifyOfPropertyChange(nameof(DeviceToAdd));
        }
    }

    private IDeviceGroupModel? _groupToAdd;
    /// <summary>그룹 추가 ComboBox — 선택 시 칩에 추가하고 비운다.</summary>
    public IDeviceGroupModel? GroupToAdd
    {
        get => _groupToAdd;
        set
        {
            _groupToAdd = value;
            if (value != null && !SelectedGroups.Any(g => g.Id == value.Id)) SelectedGroups.Add(value);
            _groupToAdd = null;
            NotifyOfPropertyChange(nameof(GroupToAdd));
        }
    }

    private string _targetSide = "both";
    /// <summary>감지/감시 필터(group·all): detection / surveillance / both.</summary>
    public string TargetSide { get => _targetSide; set { _targetSide = value; NotifyOfPropertyChange(nameof(TargetSide)); } }

    private string _eventScope = "all";
    /// <summary>억제 범위: connection / detection / malfunction / all.</summary>
    public string EventScope { get => _eventScope; set { _eventScope = value; NotifyOfPropertyChange(nameof(EventScope)); } }

    private DateTime _windowStart;
    public DateTime WindowStart { get => _windowStart; set { _windowStart = value; NotifyOfPropertyChange(nameof(WindowStart)); NotifyWindowChanged(); } }

    private DateTime _windowEnd;
    public DateTime WindowEnd { get => _windowEnd; set { _windowEnd = value; NotifyOfPropertyChange(nameof(WindowEnd)); NotifyWindowChanged(); } }

    /// <summary>기간 상한 초과 경고 표시 조건(§5-D).</summary>
    public bool HasWindowLengthWarning => !IsWindowLengthValid;
    /// <summary>기간 상한 경고 문구(모드별).</summary>
    public string WindowLengthWarningText => IsWeeklyMode
        ? $"⚠ 유효기간이 {EffectiveMaxWindowDays}일을 넘었습니다. 더 길게 하려면 무제한을 쓰세요"
        : $"⚠ 억제 기간이 최대 {EffectiveMaxWindowDays}일을 초과했습니다";

    #region - 주간 반복 폼 상태 (API 6.3.3) -

    private bool _isWeeklyMode;
    /// <summary>단발 모드(기본). XAML 라디오 바인딩.</summary>
    public bool IsOneShotMode { get => !_isWeeklyMode; set { if (value) IsWeeklyMode = false; } }

    /// <summary>
    /// 주간 반복 모드. <b>모드 전환 시 상태를 정리한다.</b>
    /// <para>⚠ 정리가 없으면 "반복+무제한 → 단발 복귀" 에서 체크박스만 사라지고 플래그가 남아
    /// 종료일 없는 <b>단발 창</b>이 전송된다 → 서버 422, 그런데 원인이 화면에서 사라져 진단 불가.</para>
    /// </summary>
    public bool IsWeeklyMode
    {
        get => _isWeeklyMode;
        set
        {
            if (_isWeeklyMode == value) return;
            _isWeeklyMode = value;
            if (value)
            {
                // 단발 → 반복: 흔한 정비 패턴을 미리 채워 그대로 두면 끝나게 한다.
                if (!SuppressionRules.HasAnyDay(_daysOfWeekMask))
                    _daysOfWeekMask = SuppressionRules.DaysWeekdayPreset;
                _isWindowEndUnlimited = false;
                _windowEndBeforeWeekly = WindowEnd;

                // ⚠ 단발 기본 유효기간은 1시간이다 — 그대로 두면 어떤 요일도 그 안에 없어
                //    '영원히 발동하지 않는 창'이 되고 서버가 422 로 막는다(API 6.3.4).
                //    반복은 '기간' 개념이므로 최소 한 주는 덮도록 넓힌다(기본 30일, 서버 상한 366).
                if ((_windowEnd - _windowStart).TotalDays < DefaultWeeklySpanDays)
                    _windowEnd = _windowStart.AddDays(DefaultWeeklySpanDays);
            }
            else
            {
                // 반복 → 단발: 무제한을 강제 해제하고 '반복으로 들어가기 전' 종료일을 되살린다.
                _isWindowEndUnlimited = false;
                _windowEndBeforeUnlimited = null;
                if (_windowEndBeforeWeekly is { } back && back > WindowStart) WindowEnd = back;
                else if (WindowEnd <= WindowStart) WindowEnd = WindowStart.AddHours(1);
            }
            NotifyRecurrenceChanged();
        }
    }

    /// <summary>시간창 라벨 — 반복 모드에서는 '유효기간' 으로 의미가 바뀐다.</summary>
    public string WindowFieldLabelText => _isWeeklyMode
        ? "유효기간 (KST) *  —  시작 ~ 종료"
        : "억제 시간창 (KST) *  —  시작 ~ 종료";

    /// <summary>서버 recurrence_type 에 대응하는 모드.</summary>
    public SuppressionRecurrenceMode RecurrenceMode =>
        _isWeeklyMode ? SuppressionRecurrenceMode.Weekly : SuppressionRecurrenceMode.None;

    private int _daysOfWeekMask = SuppressionRules.DaysWeekdayPreset;
    /// <summary>요일 비트마스크(월1 … 일64). ⚠ 서버 원점은 <b>월=0</b>.</summary>
    public int DaysOfWeekMask
    {
        get => _daysOfWeekMask;
        set { if (_daysOfWeekMask == value) return; _daysOfWeekMask = value; NotifyRecurrenceChanged(); }
    }

    /// <summary>요일 토글 — 월.</summary>
    public bool IsMonChecked { get => Day(0); set => SetDay(0, value); }
    /// <summary>요일 토글 — 화.</summary>
    public bool IsTueChecked { get => Day(1); set => SetDay(1, value); }
    /// <summary>요일 토글 — 수.</summary>
    public bool IsWedChecked { get => Day(2); set => SetDay(2, value); }
    /// <summary>요일 토글 — 목.</summary>
    public bool IsThuChecked { get => Day(3); set => SetDay(3, value); }
    /// <summary>요일 토글 — 금.</summary>
    public bool IsFriChecked { get => Day(4); set => SetDay(4, value); }
    /// <summary>요일 토글 — 토.</summary>
    public bool IsSatChecked { get => Day(5); set => SetDay(5, value); }
    /// <summary>요일 토글 — 일.</summary>
    public bool IsSunChecked { get => Day(6); set => SetDay(6, value); }

    private bool Day(int i) => (_daysOfWeekMask & SuppressionRules.BitOf(i)) != 0;
    private void SetDay(int i, bool on)
    {
        var bit = SuppressionRules.BitOf(i);
        var next = on ? _daysOfWeekMask | bit : _daysOfWeekMask & ~bit;
        if (next == _daysOfWeekMask) return;
        _daysOfWeekMask = next;
        NotifyRecurrenceChanged();
    }

    /// <summary>빠른 선택 — 평일(31).</summary>
    public void PresetWeekday() => DaysOfWeekMask = SuppressionRules.DaysWeekdayPreset;
    /// <summary>빠른 선택 — 주말(96).</summary>
    public void PresetWeekend() => DaysOfWeekMask = SuppressionRules.DaysWeekendPreset;
    /// <summary>빠른 선택 — 매일(127).</summary>
    public void PresetEveryDay() => DaysOfWeekMask = SuppressionRules.DaysEveryDayPreset;
    /// <summary>빠른 선택 — 모두 해제(0).</summary>
    public void PresetClearDays() => DaysOfWeekMask = 0;

    private DateTime? _dailyStart = DateTime.Today.AddHours(8);
    /// <summary>일일 시작. ⚠ <b>시각만</b> 쓴다 — 날짜 부분은 전송 시 버린다(offset 금지).</summary>
    public DateTime? DailyStart
    {
        get => _dailyStart;
        set { _dailyStart = value; NotifyRecurrenceChanged(); }
    }

    private DateTime? _dailyEnd = DateTime.Today.AddHours(21);
    /// <summary>일일 종료. end 가 start 보다 이르면 자정 넘김(정상), 같으면 24시간 종일(차단).</summary>
    public DateTime? DailyEnd
    {
        get => _dailyEnd;
        set { _dailyEnd = value; NotifyRecurrenceChanged(); }
    }

    private bool _isWindowEndUnlimited;
    /// <summary>단발 → 반복 전환 직전의 종료일. 반복 모드에서 기간을 넓히므로 되돌릴 값이 필요하다.</summary>
    private DateTime? _windowEndBeforeWeekly;
    /// <summary>무제한 체크 직전의 종료일. 체크 해제 시 피커 값을 되살린다.
    /// <para>⚠ 위 필드와 <b>겸용하면 안 된다</b> — 무제한 토글이 반복 전환 백업을 덮어쓴다.</para></summary>
    private DateTime? _windowEndBeforeUnlimited;
    /// <summary>"기간 제한 없음" 체크. 원시 플래그 — 게이트는 <see cref="IsUnlimitedEffective"/> 를 쓴다.</summary>
    public bool IsWindowEndUnlimited
    {
        get => _isWindowEndUnlimited;
        set
        {
            if (_isWindowEndUnlimited == value) return;
            if (value) _windowEndBeforeUnlimited = WindowEnd;
            _isWindowEndUnlimited = value;
            if (!value && _windowEndBeforeUnlimited is { } back && back > WindowStart) WindowEnd = back;
            NotifyRecurrenceChanged();
        }
    }

    /// <summary>
    /// ⚠ <b>파생 플래그</b> — XAML 의 종료 피커/플레이스홀더 게이트는 반드시 이것을 쓴다.
    /// <para>원시 <see cref="IsWindowEndUnlimited"/> 를 쓰면 반복 게이트와 어긋나
    /// 모드 복귀 시 플래그만 남는 사고가 난다.</para>
    /// </summary>
    public bool IsUnlimitedEffective => _isWeeklyMode && _isWindowEndUnlimited;

    /// <summary>자정 넘김 안내 표시 조건.</summary>
    public bool HasOvernightNotice => _isWeeklyMode
        && _dailyStart is { } s && _dailyEnd is { } e
        && SuppressionRules.ClassifyDailyTime(s.TimeOfDay, e.TimeOfDay)
           == SuppressionRules.DailyTimeVerdict.Overnight;

    /// <summary>자정 넘김 안내 문구.</summary>
    public string OvernightNoticeText => _dailyEnd is { } e
        ? $"ⓘ 자정을 넘깁니다 — 다음날 {e:HH:mm}에 종료됩니다"
        : string.Empty;

    /// <summary>
    /// 전송될 내용을 그대로 렌더하는 <b>요약 미러</b>.
    /// <para>피커가 UIA 에 안 뜨므로 이 한 줄이 값 단언의 유일한 경로이자 운용자 확인 수단이다.</para>
    /// </summary>
    public string RecurrenceSummaryText
    {
        get
        {
            if (!_isWeeklyMode) return string.Empty;
            if (_dailyStart is not { } s || _dailyEnd is not { } e) return string.Empty;
            var rule = SuppressionRules.Summarize(_daysOfWeekMask, s.TimeOfDay, e.TimeOfDay);
            if (string.IsNullOrEmpty(rule)) return string.Empty;
            var span = IsUnlimitedEffective
                ? $"무제한 · {WindowStart:yyyy-MM-dd} 시작"
                : $"{WindowStart:yyyy-MM-dd} ~ {WindowEnd:MM-dd}";
            return $"→ {rule} · {span}";
        }
    }

    private void NotifyRecurrenceChanged()
    {
        NotifyOfPropertyChange(nameof(IsWeeklyMode));
        NotifyOfPropertyChange(nameof(IsOneShotMode));
        NotifyOfPropertyChange(nameof(RecurrenceMode));
        NotifyOfPropertyChange(nameof(WindowFieldLabelText));
        NotifyOfPropertyChange(nameof(DaysOfWeekMask));
        NotifyOfPropertyChange(nameof(IsMonChecked)); NotifyOfPropertyChange(nameof(IsTueChecked));
        NotifyOfPropertyChange(nameof(IsWedChecked)); NotifyOfPropertyChange(nameof(IsThuChecked));
        NotifyOfPropertyChange(nameof(IsFriChecked)); NotifyOfPropertyChange(nameof(IsSatChecked));
        NotifyOfPropertyChange(nameof(IsSunChecked));
        NotifyOfPropertyChange(nameof(DailyStart));
        NotifyOfPropertyChange(nameof(DailyEnd));
        NotifyOfPropertyChange(nameof(IsWindowEndUnlimited));
        NotifyOfPropertyChange(nameof(IsUnlimitedEffective));
        NotifyOfPropertyChange(nameof(HasOvernightNotice));
        NotifyOfPropertyChange(nameof(OvernightNoticeText));
        NotifyOfPropertyChange(nameof(RecurrenceSummaryText));
        NotifyOfPropertyChange(nameof(WeeklyFormError));
        NotifyOfPropertyChange(nameof(FormErrorText));
        NotifyOfPropertyChange(nameof(HasFormError));
        NotifyOfPropertyChange(nameof(IsWindowLengthValid));
        NotifyOfPropertyChange(nameof(HasWindowLengthWarning));
        NotifyOfPropertyChange(nameof(WindowLengthWarningText));
        NotifyOfPropertyChange(nameof(EffectiveMaxWindowDays));
        NotifyOfPropertyChange(nameof(CanCreate));
        NotifyOfPropertyChange(nameof(CreateHintText));
    }

    #endregion

    private void NotifyWindowChanged()
    {
        NotifyOfPropertyChange(nameof(CanCreate));
        NotifyOfPropertyChange(nameof(CreateHintText));
        NotifyOfPropertyChange(nameof(IsWindowLengthValid));
        NotifyOfPropertyChange(nameof(HasWindowLengthWarning));
        NotifyOfPropertyChange(nameof(WindowLengthWarningText));
        NotifyOfPropertyChange(nameof(FormErrorText));
        NotifyOfPropertyChange(nameof(HasFormError));
        NotifyOfPropertyChange(nameof(RecurrenceSummaryText));
    }
    #endregion

    #region - Properties (Filter / List) -
    // ⚠ 필터 값은 ComboBoxItem 의 Tag 와 1:1 이어야 한다. "전체" 항목의 Tag 는 빈 문자열이므로
    //    여기서 ""→null 로 정규화하면 SelectedValue 가 어떤 항목과도 매칭되지 않아
    //    ① 초기 표시가 빈칸이 되고 ② "전체"를 골라도 곧바로 선택이 풀린다.
    //    바인딩 값은 원문 그대로 두고, null 정규화는 API 호출 직전에만 한다(ApiFilter*).
    private string _filterStatus = string.Empty;
    /// <summary>상태 필터(""=전체). 변경 시 재조회.</summary>
    public string FilterStatus { get => _filterStatus; set { _filterStatus = value ?? string.Empty; NotifyOfPropertyChange(nameof(FilterStatus)); _ = LoadAllAsync(); } }

    private string _filterTargetType = string.Empty;
    /// <summary>대상유형 필터(""=전체). 변경 시 재조회.</summary>
    public string FilterTargetType { get => _filterTargetType; set { _filterTargetType = value ?? string.Empty; NotifyOfPropertyChange(nameof(FilterTargetType)); _ = LoadAllAsync(); } }

    /// <summary>서버 전달용 — 빈 문자열(전체)은 파라미터 미전송(null)으로 바꾼다.</summary>
    private string? ApiFilterStatus => string.IsNullOrEmpty(_filterStatus) ? null : _filterStatus;
    /// <summary>서버 전달용 — 빈 문자열(전체)은 파라미터 미전송(null)으로 바꾼다.</summary>
    private string? ApiFilterTargetType => string.IsNullOrEmpty(_filterTargetType) ? null : _filterTargetType;

    private bool _isLoadingMore;
    public bool IsLoadingMore { get => _isLoadingMore; set { _isLoadingMore = value; NotifyOfPropertyChange(nameof(IsLoadingMore)); } }

    public string LoadedCountText => $"{Schedules.Count} / {_totalCount}건";
    public bool HasMorePages => _currentPage < _totalPages;
    public ICommand LoadMoreCommand { get; }

    // ── 선택 삭제(취소/종료 행 일괄 하드삭제) ──
    /// <summary>체크된 삭제 대상(취소/종료) 개수.</summary>
    public int SelectedDeleteCount => Schedules.Count(s => s.IsSelected && s.IsDeletable);
    /// <summary>삭제 대상(취소/종료) 행이 하나라도 있는가 — 전체선택 체크박스 노출 조건.</summary>
    public bool HasDeletableRows => Schedules.Any(s => s.IsDeletable);
    /// <summary>선택 삭제 버튼 활성 — events:delete + 1건 이상 선택.</summary>
    public bool CanDeleteSelected => CanDelEvents() && SelectedDeleteCount > 0;
    /// <summary>선택 삭제 버튼 라벨.</summary>
    public string DeleteSelectedText => $"선택 삭제 ({SelectedDeleteCount})";
    /// <summary>취소/종료 행 전체선택 토글(헤더 체크박스 바인딩).</summary>
    public bool SelectAllDeletable
    {
        get { var d = Schedules.Where(s => s.IsDeletable).ToList(); return d.Count > 0 && d.All(s => s.IsSelected); }
        set { foreach (var s in Schedules.Where(x => x.IsDeletable)) s.IsSelected = value; NotifySelectionState(); }
    }

    // ── 활성 억제 인지(§7) / 중복 창 사전 경고(§5-B) ──
    /// <summary>현재 진행 중인 억제 창이 있는가(상단 경고 표시 조건).</summary>
    public bool HasActiveSuppression => ActiveWindows.Count > 0;

    /// <summary>활성 억제 목록(표시용 SSOT) — 단일 출처는 <see cref="ISuppressionActiveMonitor"/> 다.</summary>
    private IReadOnlyList<EventSuppressionScheduleDto> ActiveWindows
        => _monitor?.Active ?? _activeCache;

    /// <summary>
    /// <b>방금 서버가 확인해 준</b> 활성 목록(<see cref="RefreshActiveAsync"/> 가 await 로 갱신).
    /// <para>생성·취소·삭제 <b>직후</b>의 판정은 30초 폴링 스냅샷이 아니라 이 값을 써야 한다.</para>
    /// </summary>
    private IReadOnlyList<EventSuppressionScheduleDto> FreshActiveWindows => _activeCache;

    /// <summary>
    /// 배너 표시 조건. ⚠ <b>stale 일 때도 떠야 한다</b> — 폴링이 실패해 목록이 0건이면
    /// <see cref="HasActiveSuppression"/> 만으로는 배너가 통째로 사라져, 정작 알려야 할 순간에 침묵한다.
    /// </summary>
    public bool HasActiveBanner => HasActiveSuppression || IsActiveStale;

    /// <summary>폴링이 TTL(90초)을 넘겨 실패 중인가.</summary>
    public bool IsActiveStale => _monitor?.IsStale ?? false;

    /// <summary>stale 병기 문구 — 목록을 버리지 않고 "언제 기준인지"를 밝힌다.</summary>
    public string ActiveStaleText => IsActiveStale
        ? $"⚠ 갱신 실패 — {_monitor?.LastSuccessAgeText ?? "확인 안 됨"} 기준"
        : string.Empty;
    /// <summary>상단 활성 억제 요약 — 은폐 방지(정비 중임을 상시 인지).</summary>
    public string ActiveCountText => ActiveWindows.Count == 0
        ? string.Empty
        : $"⚠ 현재 억제 중인 스케줄 {ActiveWindows.Count}건"
          + (IsActiveStale ? $" · {ActiveStaleText}" : string.Empty);

    /// <summary>정리할 취소/종료 항목이 있는가('모두 정리' 버튼 활성).</summary>
    public bool CanCleanupAll => CanDelEvents() && Schedules.Any(s => s.IsDeletable);

    /// <summary>(§5-B) 지금 선택한 대상이 이미 활성 창에 덮여 있으면 경고 문구(없으면 빈 문자열).</summary>
    public string DuplicateWarningText
    {
        get
        {
            var dup = SuppressionRules.CountOverlappingActive(
                ActiveWindows, TargetType,
                SelectedDevices.Select(d => d.Id),
                SelectedGroups.Select(g => g.Id));
            return dup > 0
                ? $"⚠ 같은 대상에 진행 중인 억제 스케줄이 이미 {dup}건 있습니다 (중복으로 만들면 하나를 취소해도 억제가 계속됩니다)"
                : string.Empty;
        }
    }
    public bool HasDuplicateWarning => !string.IsNullOrEmpty(DuplicateWarningText);
    #endregion

    #region - Attributes -
    private readonly IEventSuppressionApiService _api;
    private const int PAGE_SIZE = 100;
    /// <summary>단발 → 반복 전환 시 기본 유효기간(일). 어떤 요일 조합이든 반드시 한 번은 포함된다.</summary>
    private const int DefaultWeeklySpanDays = 30;
    /// <summary>창 길이 상한(일) — 서버 무제한이라 클라 방어(§5-D).</summary>
    private const int MAX_WINDOW_DAYS = 30;
    /// <summary>GET /active 스냅샷 — 활성 인지(§7)·중복 경고(§5-B)·취소 후 잔존 확인.</summary>
    /// <summary>Monitor 미주입(테스트·구버전 배선) 시의 폴백 캐시. 정상 경로는 <c>_monitor.Active</c>.</summary>
    private IReadOnlyList<EventSuppressionScheduleDto> _activeCache = new List<EventSuppressionScheduleDto>();
    private readonly ISuppressionActiveMonitor? _monitor;
    private bool _monitorHooked;
    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    #endregion
}

/// <summary>억제 창 취소 확인 트리거 — Confirm 팝업 '확인' 시 발행 → HandleAsync가 실제 DELETE(soft-cancel).</summary>
public class CallCancelSuppressionMessageModel : IMessageModel
{
    public int ScheduleId { get; set; }
}

/// <summary>선택 삭제 확인 트리거 — Confirm '확인' 시 발행 → HandleAsync가 일괄 하드삭제(POST /bulk-delete).</summary>
public class CallBulkDeleteSuppressionMessageModel : IMessageModel
{
    public System.Collections.Generic.List<int> Ids { get; set; } = new();
}
