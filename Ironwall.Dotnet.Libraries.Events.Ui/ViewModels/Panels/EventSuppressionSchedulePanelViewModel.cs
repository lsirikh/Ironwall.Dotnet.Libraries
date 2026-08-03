using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;  // SuppressionRules(폼 검증·중복 판정 순수 규칙)
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
        DeviceGroupProvider groupProvider)
        : base(eventAggregator, log)
    {
        _api = api;
        DeviceProvider = deviceProvider;
        DeviceGroupProvider = groupProvider;

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
    /// <summary>억제 스케줄 목록(DataGrid ItemsSource).</summary>
    public ObservableCollection<EventSuppressionScheduleItemViewModel> Schedules { get; } = new();
    #endregion

    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        var perm = ResolvePermissionService();
        if (perm != null) perm.PermissionsChanged += OnPermissionsChanged;
        // 조회 권한(events:view) 게이팅 — 권한 없으면 목록을 불러오지 않는다(서버 403 최종 권위, UI는 보조).
        if (!CanViewEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = "이벤트 조회 권한(events:view)이 없습니다." });
            return;
        }
        await LoadAllAsync(_cancellationTokenSource?.Token ?? cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        var perm = ResolvePermissionService();
        if (perm != null) perm.PermissionsChanged -= OnPermissionsChanged;
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
        && WindowEnd > WindowStart
        && IsWindowLengthValid
        && TargetType switch
        {
            "device" => SelectedDevices.Count > 0,
            "group" => SelectedGroups.Count > 0,
            _ => true,   // all
        };

    /// <summary>창 길이 상한 검증(서버는 상한이 없어 오타로 1년 억제도 생성됨 — 클라에서 방어).</summary>
    public bool IsWindowLengthValid => SuppressionRules.IsWindowLengthValid(WindowStart, WindowEnd, MAX_WINDOW_DAYS);

    /// <summary>취소(삭제) 권한 — 행 취소 버튼 게이팅 보조.</summary>
    public bool CanDelete => CanDelEvents();

    private void OnPermissionsChanged()
    {
        Execute.OnUIThread(() =>
        {
            NotifyOfPropertyChange(nameof(CanCreate));
            NotifyOfPropertyChange(nameof(CanDelete));
            NotifySelectionState();   // events:delete 회수 시 '선택 삭제' 버튼/전체선택 즉시 비활성 반영
        });
    }
    #endregion

    #region - Binding Methods -
    /// <summary>헤더 X 버튼(ModernPanelCloseButton, x:Name="Close") — 패널 닫기(ReportConsole 패턴).</summary>
    public Task Close() => TryCloseAsync();

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
        NotifyDuplicateWarning();   // 모드 전환 시 중복 경고 재평가
    }

    /// <summary>칩 제거 — 장비.</summary>
    public void RemoveDevice(IBaseDeviceModel device) { if (device != null) SelectedDevices.Remove(device); }
    /// <summary>칩 제거 — 그룹.</summary>
    public void RemoveGroup(IDeviceGroupModel group) { if (group != null) SelectedGroups.Remove(group); }

    /// <summary>억제 창 생성 — 클라 1차 검증 후 POST(대상 배열 + KST ISO8601). 성공 시 폼 리셋 + 재조회.</summary>
    public async Task ClickCreate()
    {
        if (!CanEditEvents())
        {
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "권한 없음", Explain = "이벤트 편집 권한(events:edit)이 없습니다." });
            return;
        }
        if (!CanCreate)
        {
            var why = !IsWindowLengthValid
                ? $"억제 기간이 너무 깁니다. 최대 {MAX_WINDOW_DAYS}일까지 지정할 수 있습니다."
                : "작업명·대상(≥1)·시간창(종료>시작)을 확인하세요.";
            await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "억제 창 생성", Explain = why });
            return;
        }
        try
        {
            var dto = new EventSuppressionScheduleRequestDto
            {
                Name = Name!.Trim(),
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                TargetType = TargetType,
                TargetDeviceIds = TargetType == "device" ? SelectedDevices.Select(d => d.Id).ToList() : new(),
                TargetGroupIds = TargetType == "group" ? SelectedGroups.Select(g => g.Id).ToList() : new(),
                TargetSide = TargetSide,
                EventScope = EventScope,
                WindowStart = KoreaTimeHelper.ToServerIso8601(WindowStart),
                WindowEnd = KoreaTimeHelper.ToServerIso8601(WindowEnd),
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
                    { Title = "억제 창 생성 완료", Explain = $"아래 대상이 억제됩니다. 대상이 맞는지 확인하세요.\n\n{echo}" });
            }
            else
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "억제 창 생성", Explain = $"생성 실패: {res.Error?.Message ?? res.Message}" });
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
            { Title = "권한 없음", Explain = "이벤트 삭제 권한(events:delete)이 없습니다." });
            return;
        }
        await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "억제 창 취소",
            Explain = $"'{item.Name}' 억제 창(#{item.Id})을 취소하시겠습니까?\n취소해도 이력은 보존(revoked_at)됩니다.",
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
                if (_activeCache.Count > 0)
                    await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                    {
                        Title = "억제 창 취소",
                        Explain = $"취소했지만 아직 진행 중인 억제 창이 {_activeCache.Count}건 남아 있습니다.\n"
                                + "해당 장비가 계속 억제될 수 있으니 목록에서 '진행중' 항목을 확인하세요."
                    });
            }
            else await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
            { Title = "억제 창 취소", Explain = $"취소 실패: {res.Error?.Message ?? res.Message}" });
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
            { Title = "권한 없음", Explain = "이벤트 삭제 권한(events:delete)이 없습니다." });
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
            { Title = "권한 없음", Explain = "이벤트 삭제 권한(events:delete)이 없습니다." });
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
                    var msg = $"{deleted}건 삭제.";
                    if (skipped > 0) msg += $"\n{skipped}건은 진행 중/예정이라 삭제할 수 없습니다. 먼저 취소하세요.";
                    if (notFound > 0) msg += $"\n{notFound}건은 이미 삭제된 항목이라 제외했습니다.";
                    await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                    { Title = "억제 스케줄 삭제", Explain = msg });
                }
            }
            else
            {
                // 404/405 = 서버에 /bulk-delete 미배포(구버전 서버) — 원인을 바로 알 수 있게 안내.
                var hint = res.StatusCode is 404 or 405
                    ? "\n\n※ 서버에 일괄삭제 기능이 아직 배포되지 않았습니다(서버 업데이트 필요)."
                    : string.Empty;
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "억제 스케줄 삭제", Explain = $"삭제 실패: {res.Error?.Message ?? res.Message}{hint}" });
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
        }
        catch (Exception ex) { _log?.Warning($"[Suppression] /active 갱신 실패(무시): {ex.Message}"); }
        Execute.OnUIThread(() =>
        {
            NotifyOfPropertyChange(nameof(ActiveCountText));
            NotifyOfPropertyChange(nameof(HasActiveSuppression));
            NotifyDuplicateWarning();
        });
    }

    private void NotifyDuplicateWarning()
    {
        NotifyOfPropertyChange(nameof(DuplicateWarningText));
        NotifyOfPropertyChange(nameof(HasDuplicateWarning));
    }

    /// <summary>(§6) 생성 응답의 대상 id를 장비/그룹 이름으로 되풀이 — 운영자 육안 확인용.</summary>
    private string BuildTargetEcho(EventSuppressionScheduleDto dto)
    {
        switch (dto.TargetType)
        {
            case "device":
                var dn = (dto.TargetDeviceIds ?? new()).Select(id =>
                    DeviceProvider?.CollectionEntity.FirstOrDefault(d => d.Id == id)?.DeviceName is string n && !string.IsNullOrEmpty(n)
                        ? $"· {n} (#{id})" : $"· #{id}");
                return $"[장비 {dto.TargetDeviceIds?.Count ?? 0}개]\n{string.Join("\n", dn)}";
            case "group":
                var gn = (dto.TargetGroupIds ?? new()).Select(id =>
                    DeviceGroupProvider?.CollectionEntity.FirstOrDefault(g => g.Id == id)?.Name is string n && !string.IsNullOrEmpty(n)
                        ? $"· {n} (#{id})" : $"· #{id}");
                return $"[그룹 {dto.TargetGroupIds?.Count ?? 0}개]\n{string.Join("\n", gn)}";
            default:
                return $"[전체 대상] side={dto.TargetSide}";
        }
    }

    private void ResetForm()
    {
        Name = string.Empty;
        Description = null;
        SelectedDevices.Clear();
        SelectedGroups.Clear();
        WindowStart = DateTime.Now;
        WindowEnd = DateTime.Now.AddHours(1);
        NotifyOfPropertyChange(nameof(CanCreate));
    }

    private async Task LoadAllAsync(CancellationToken ct = default)
    {
        _currentPage = 0; _totalPages = 1; _totalCount = 0;
        try
        {
            var res = await _api.GetSuppressionSchedulesAsync(
                page: 1, limit: PAGE_SIZE, status: FilterStatus, targetType: FilterTargetType, token: ct).ConfigureAwait(false);
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
                await _eventAggregator!.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel
                { Title = "억제 스케줄", Explain = $"목록 불러오기 실패: {res.Error?.Message ?? res.Message}" });
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
                page: _currentPage + 1, limit: PAGE_SIZE, status: FilterStatus, targetType: FilterTargetType, token: ct).ConfigureAwait(false);
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
    public string? Name { get => _name; set { _name = value; NotifyOfPropertyChange(nameof(Name)); NotifyOfPropertyChange(nameof(CanCreate)); } }

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
    /// <summary>기간 상한 경고 문구.</summary>
    public string WindowLengthWarningText => $"⚠ 억제 기간이 최대 {MAX_WINDOW_DAYS}일을 초과했습니다";

    private void NotifyWindowChanged()
    {
        NotifyOfPropertyChange(nameof(CanCreate));
        NotifyOfPropertyChange(nameof(IsWindowLengthValid));
        NotifyOfPropertyChange(nameof(HasWindowLengthWarning));
    }
    #endregion

    #region - Properties (Filter / List) -
    private string? _filterStatus;
    /// <summary>상태 필터(null=전체). 변경 시 재조회.</summary>
    public string? FilterStatus { get => _filterStatus; set { _filterStatus = string.IsNullOrEmpty(value) ? null : value; NotifyOfPropertyChange(nameof(FilterStatus)); _ = LoadAllAsync(); } }

    private string? _filterTargetType;
    /// <summary>대상유형 필터(null=전체). 변경 시 재조회.</summary>
    public string? FilterTargetType { get => _filterTargetType; set { _filterTargetType = string.IsNullOrEmpty(value) ? null : value; NotifyOfPropertyChange(nameof(FilterTargetType)); _ = LoadAllAsync(); } }

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
    public bool HasActiveSuppression => _activeCache.Count > 0;
    /// <summary>상단 활성 억제 요약 — 은폐 방지(정비 중임을 상시 인지).</summary>
    public string ActiveCountText => _activeCache.Count == 0
        ? string.Empty
        : $"⚠ 현재 억제 중 {_activeCache.Count}건 — 정비 창이 진행 중입니다";

    /// <summary>정리할 취소/종료 항목이 있는가('모두 정리' 버튼 활성).</summary>
    public bool CanCleanupAll => CanDelEvents() && Schedules.Any(s => s.IsDeletable);

    /// <summary>(§5-B) 지금 선택한 대상이 이미 활성 창에 덮여 있으면 경고 문구(없으면 빈 문자열).</summary>
    public string DuplicateWarningText
    {
        get
        {
            var dup = SuppressionRules.CountOverlappingActive(
                _activeCache, TargetType,
                SelectedDevices.Select(d => d.Id),
                SelectedGroups.Select(g => g.Id));
            return dup > 0
                ? $"⚠ 같은 대상에 진행 중인 억제 창이 이미 {dup}건 있습니다 (중복 생성 시 하나만 취소해도 억제가 계속됩니다)"
                : string.Empty;
        }
    }
    public bool HasDuplicateWarning => !string.IsNullOrEmpty(DuplicateWarningText);
    #endregion

    #region - Attributes -
    private readonly IEventSuppressionApiService _api;
    private const int PAGE_SIZE = 100;
    /// <summary>창 길이 상한(일) — 서버 무제한이라 클라 방어(§5-D).</summary>
    private const int MAX_WINDOW_DAYS = 30;
    /// <summary>GET /active 스냅샷 — 활성 인지(§7)·중복 경고(§5-B)·취소 후 잔존 확인.</summary>
    private IReadOnlyList<EventSuppressionScheduleDto> _activeCache = new List<EventSuppressionScheduleDto>();
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
