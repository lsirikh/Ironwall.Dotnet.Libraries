using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 이벤트 콘솔의 '억제 스케줄' 레일 — T1 목록 + 780 서랍(E-D7).
                  억제는 서버 REST 게이트만이 집행한다. 이 화면은 보여 주고 만들 뿐이다.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>억제 스케줄 취소(soft-cancel) 확인 트리거 — <b>콘솔 전용</b>.</summary>
/// <remarks>
/// 옛 패널의 메시지 타입을 다시 쓰지 않는다 — 두 화면이 같이 살아 있으면 같은 확인 한 번에
/// DELETE 가 <b>두 번</b> 나간다(패널 · 콘솔이 둘 다 <c>IHandle</c> 이라서).
/// </remarks>
public sealed class CallCancelConsoleSuppressionMessageModel : IMessageModel
{
    public int ScheduleId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>억제 스케줄 일괄 하드삭제 확인 트리거 — <b>콘솔 전용</b>.</summary>
public sealed class CallDeleteConsoleSuppressionMessageModel : IMessageModel
{
    public List<int> Ids { get; set; } = new();
}

/// <summary>
/// 억제 스케줄 콘솔(정본 SB L1048 · L1122 E-D7 · L2295-2313 목록/필터 · L2734-2740 상세).
/// </summary>
/// <remarks>
/// <para>호출 스레드: UI. API 완료가 작업 스레드로 올 수 있어 컬렉션 · 상태 갱신은 <see cref="Execute.OnUIThread"/> 로 옮긴다.</para>
/// <para>수명: 이벤트 콘솔이 <see cref="ActivateAsync"/> / <see cref="DeactivateAsync"/> 를 불러 준다.
/// 콘솔은 싱글턴이라 구독은 활성화에서만 걸고, 떠날 때 초안을 반드시 버린다.</para>
/// </remarks>
public sealed class SuppressionConsoleViewModel : PropertyChangedBase,
    IHandle<CallCancelConsoleSuppressionMessageModel>,
    IHandle<CallDeleteConsoleSuppressionMessageModel>
{
    /// <summary>한 번에 받아 오는 행 수 — 옛 패널과 같다.</summary>
    public const int PageSize = 100;

    private readonly IEventAggregator _events;
    private readonly ILogService? _log;
    private readonly IEventSuppressionApiService _api;
    private readonly DeviceProvider? _devices;
    private readonly DeviceGroupProvider? _groups;
    private readonly IClock _clock;
    private readonly Func<bool> _canEdit;
    private readonly Func<bool> _canDelete;
    private readonly Func<IUnitScopeService?> _unitScope;

    private IReadOnlyList<EventSuppressionScheduleDto> _active = Array.Empty<EventSuppressionScheduleDto>();
    private string _filterKey = SuppressionStatusView.FilterAll;
    private string _statusText = string.Empty;
    private SuppressionConsoleRow? _selected;
    private bool _isBusy;
    private bool _isSubscribed;
    private int _editEpoch;
    private int _currentPage;
    private int _totalPages = 1;
    private int _totalCount;
    private int? _allTotalCount;

    public SuppressionConsoleViewModel(IEventAggregator events,
                                       ILogService? log,
                                       IEventSuppressionApiService api,
                                       DeviceProvider? devices,
                                       DeviceGroupProvider? groups,
                                       IClock? clock = null,
                                       Func<bool>? canEdit = null,
                                       Func<bool>? canDelete = null,
                                       Func<IUnitScopeService?>? unitScope = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _log = log;
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _devices = devices;
        _groups = groups;
        _clock = clock ?? new SystemClock();
        _canEdit = canEdit ?? (() => true);
        _canDelete = canDelete ?? (() => true);
        // 부대 해석기는 늦게 찾는다 — 장비 쓰기 관문(UnitScopeGate)과 같은 관용구. 미등록이면 null → unit_id 를 싣지 않는다.
        _unitScope = unitScope ?? SuppressionUnitStamp.ResolveFromIoC;

        Schedules = new ObservableCollection<SuppressionConsoleRow>();
        RowsView = CollectionViewSource.GetDefaultView(Schedules);
        RowsView.Filter = o => o is SuppressionConsoleRow row && SuppressionStatusView.Matches(_filterKey, row.Shape);

        Filters = new[]
        {
            new SuppressionFilterOption(SuppressionStatusView.FilterAll, "전체"),
            new SuppressionFilterOption(SuppressionStatusView.FilterSuppressing, "억제중"),
            new SuppressionFilterOption(SuppressionStatusView.FilterActive, "진행중"),
            new SuppressionFilterOption(SuppressionStatusView.FilterPending, "예정"),
            // 정리(일괄 하드삭제)의 대상은 취소 · 종료 행뿐이다 — 그 둘을 부를 길이 없으면
            // [모두 정리] 가 '화면에 실린 것' 만 덮는다(옛 억제창의 상태 콤보에는 있던 값이다).
            new SuppressionFilterOption(SuppressionStatusView.FilterTerminal, "종료·취소"),
        };
        SyncFilterChips();

        Drawer = new SuppressionDrawerViewModel(
            _clock, devices, groups, SaveAsync, _canEdit, () => _active,
            onError: (what, ex) => _log?.Error($"[SuppressionConsole] {what} 실패: {ex}"));
        Drawer.Saved += OnDrawerSaved;
    }

    #region - 수명 -

    /// <summary>콘솔이 이 레일로 들어왔다 — 구독을 걸고 목록을 부른다.</summary>
    public async Task ActivateAsync(CancellationToken token = default)
    {
        if (!_isSubscribed)
        {
            _events.SubscribeOnUIThread(this);
            _isSubscribed = true;
        }
        await LoadAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// 이 레일을 떠나도 되는가 — 서랍에 미적용 변경이 있으면 <b>안 된다</b>.
    /// 대시보드의 레일 전환이 이것을 먼저 묻는다.
    /// </summary>
    public bool TryLeave() => Drawer.TryLeave();

    /// <summary>
    /// 콘솔이 이 레일을 떠났다 — 구독을 풀고 <b>초안을 버린다</b>.
    /// <para>⚠ 호출부가 <see cref="TryLeave"/> 를 먼저 물었다는 전제다. 여기서는 되묻지 않는다 —
    /// 창을 닫는 경로(대시보드 <c>OnDeactivateAsync</c>)는 막을 수 없기 때문이다.</para>
    /// <para>싱글턴이라 초안이 남으면 다음에 열 때 남의 편집이 떠 있다.</para>
    /// </summary>
    public Task DeactivateAsync()
    {
        if (_isSubscribed)
        {
            _events.Unsubscribe(this);
            _isSubscribed = false;
        }
        _editEpoch++;               // 날아오던 [수정] 응답이 떠난 뒤에 서랍을 열지 못하게 한다
        Drawer.Close();
        Selected = null;
        StatusText = string.Empty;
        return Task.CompletedTask;
    }

    #endregion

    #region - 목록 -

    /// <summary>불러온 스케줄(전량). 화면에 보이는 것은 <see cref="RowsView"/> 가 거른다.</summary>
    public ObservableCollection<SuppressionConsoleRow> Schedules { get; }

    /// <summary>상태 칩으로 거른 보기. 정렬은 걸지 않는다 — 행 순서는 서버가 준 순서다.</summary>
    public ICollectionView RowsView { get; }

    /// <summary>상태 필터 칩 — 정본 SB L2313.</summary>
    public IReadOnlyList<SuppressionFilterOption> Filters { get; }

    /// <summary>지금 필터.</summary>
    public string FilterKey
    {
        get => _filterKey;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? SuppressionStatusView.FilterAll : value;
            if (_filterKey == next) return;
            _filterKey = next;
            SyncFilterChips();
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsEmpty));
            NotifyOfPropertyChange(nameof(EmptyText));
            _ = LoadAsync();
        }
    }

    /// <summary>고른 행.</summary>
    public SuppressionConsoleRow? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(HasSelection));
            NotifyOfPropertyChange(nameof(CanEditSelected));
            NotifyOfPropertyChange(nameof(CanCancelSelected));
            NotifyOfPropertyChange(nameof(DetailTitle));
            NotifyOfPropertyChange(nameof(DetailFooterText));
            NotifyOfPropertyChange(nameof(DetailKind));
            NotifyOfPropertyChange(nameof(DetailTargets));
            NotifyOfPropertyChange(nameof(IsDetailTargetsEmpty));
            NotifyOfPropertyChange(nameof(DetailTargetsEmptyText));
            NotifyOfPropertyChange(nameof(StatusLineText));
        }
    }

    public bool HasSelection => _selected is not null;

    /// <summary>보이는 행이 하나도 없는가 — 빈 목록 안내 조건.</summary>
    public bool IsEmpty => !IsBusy && !Schedules.Any(r => SuppressionStatusView.Matches(_filterKey, r.Shape));

    /// <summary>빈 목록 안내 — 왜 비었는지에 따라 말이 달라진다.</summary>
    public string EmptyText => _filterKey == SuppressionStatusView.FilterAll
        ? "등록된 억제 스케줄이 없습니다. [새 스케줄]로 만드세요."
        : "이 상태에 맞는 스케줄이 없습니다. 필터를 '전체'로 바꿔 보세요.";

    /// <summary>
    /// 상태 띠 — 불러온 수 · 전체 수 · 억제중 수 · <b>기준 시각</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ 행의 상태는 <b>불러온 순간의 스냅샷</b>이다. 억제 여부의 권위는 서버(<c>is_suppressing_now</c>)이고
    /// 화면에는 다시 계산할 근거가 없다(회차는 유효기간 경계에서 잘린다). 자동 갱신도 없다 —
    /// 그래서 <b>언제 기준인지를 적고</b> [갱신] 을 권한다. 숨기는 것보다 낫다.
    /// </remarks>
    public string StatusLineText
        => $"불러온 {Schedules.Count} / {_totalCount}건 · 억제중 {SuppressingCount}건"
         + (_loadedAt is { } at ? $" · {at:HH:mm} 기준([새로 불러오기]로 최신화)" : string.Empty)
         + (_selected is null ? string.Empty : $" · 선택 {_selected.Name}");

    /// <summary>목록을 마지막으로 받아 온 시각(시계에서 온 값).</summary>
    private DateTime? _loadedAt;

    /// <summary>
    /// 레일 배지 — 등록된 억제 스케줄 수('전체' 로 받은 서버 합계). 아직 한 번도 불러오지 않았으면 null(배지를 숨긴다).
    /// </summary>
    /// <remarks>
    /// 예전 배지는 '지금 억제중' 수(정본 SB L2361)라 목록이 55건인데 레일에 0 이 떠 "비었다" 로 읽혔다(실창 검토 #23).
    /// 다른 레일처럼 목록 건수를 싣는다 — 억제중 수는 상태 띠와 '억제중' 칩이 말한다.
    /// </remarks>
    public int? ScheduleTotal => _allTotalCount;

    /// <summary>지금 억제 중인 창의 수 — 상태 띠 · '억제중' 칩의 뜻.</summary>
    public int SuppressingCount => Schedules.Count(s => s.Shape == SuppressionStatusShape.Suppressing);

    /// <summary>예정 + 진행중 — 레일 배지 합계.</summary>
    public int ScheduledCount => Schedules.Count(s => s.Shape is SuppressionStatusShape.Scheduled or SuppressionStatusShape.InWindow);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(CanReload));
            NotifyOfPropertyChange(nameof(CanAdd));
            NotifyOfPropertyChange(nameof(IsEmpty));
            // 툴바 [갱신] · [새 스케줄] 은 대시보드가 그린다 — 여기서 알리지 않으면 부른 뒤에도 꺼진 채 남는다.
            CountsChanged?.Invoke();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(DetailFooterText)); }
    }

    /// <summary>목록을 처음부터 다시 부른다.</summary>
    public async Task LoadAsync(CancellationToken token = default)
    {
        IsBusy = true;
        try
        {
            var res = await _api.GetSuppressionSchedulesAsync(
                page: 1, limit: PageSize,
                status: SuppressionStatusView.ServerStatusFor(_filterKey),
                token: token).ConfigureAwait(false);

            if (token.IsCancellationRequested) return;

            if (!res.Success || res.Data is null)
            {
                // 서버 원문은 로그로만 — 화면에는 무엇이 안 됐고 어떻게 하면 되는지만 쓴다.
                _log?.Warning($"[SuppressionConsole] 목록 거절: {res.Error?.Message ?? res.Message}");
                Post(() => StatusText = LoadFailedText);
                return;
            }

            var rows = res.Data.Select(Row).ToList();
            var total = res.Pagination?.Total ?? res.Total ?? res.Data.Count;
            var page = res.Pagination?.Page ?? 1;

            var at = _clock.Now;
            Post(() =>
            {
                _loadedAt = at;
                _totalCount = total;
                // 레일 배지는 '전체' 칩으로 받은 합계만 쓴다 — 상태 칩은 서버에서 거르므로 그 합계는 부분 집합이다.
                if (_filterKey == SuppressionStatusView.FilterAll) _allTotalCount = total;
                _currentPage = page;
                _totalPages = total > 0 ? (int)Math.Ceiling(total / (double)PageSize) : 1;
                Replace(rows);
            });

            await RefreshActiveAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // 예외 본문은 로그로만 — 화면 문장에 서버 주소가 실리면 안 된다.
            _log?.Error($"[SuppressionConsole] 목록 실패: {ex}");
            Post(() => StatusText = LoadFailedText);
        }
        finally
        {
            Post(() => IsBusy = false);
        }
    }

    /// <summary>목록을 받지 못했을 때의 한 줄.</summary>
    public const string LoadFailedText = "목록을 불러오지 못했습니다. [새로 불러오기]로 다시 시도하세요.";

    /// <summary>다음 페이지(아래로 내렸을 때).</summary>
    public async Task LoadMoreAsync(CancellationToken token = default)
    {
        if (IsBusy || _currentPage >= _totalPages) return;
        IsBusy = true;
        try
        {
            var res = await _api.GetSuppressionSchedulesAsync(
                page: _currentPage + 1, limit: PageSize,
                status: SuppressionStatusView.ServerStatusFor(_filterKey),
                token: token).ConfigureAwait(false);

            if (token.IsCancellationRequested || !res.Success || res.Data is null) return;

            var rows = res.Data.Select(Row).ToList();
            var page = res.Pagination?.Page ?? (_currentPage + 1);
            Post(() =>
            {
                _currentPage = page;
                foreach (var row in rows) Schedules.Add(row);
                RaiseCounts();
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log?.Error($"[SuppressionConsole] 다음 페이지 실패: {ex.Message}"); }
        finally { Post(() => IsBusy = false); }
    }

    /// <summary>[갱신].</summary>
    public bool CanReload => !IsBusy;

    public void Reload() => _ = LoadAsync();

    private async Task RefreshActiveAsync(CancellationToken token)
    {
        try
        {
            var res = await _api.GetActiveSuppressionSchedulesAsync(token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;
            _active = res.Success && res.Data is not null ? res.Data : Array.Empty<EventSuppressionScheduleDto>();
        }
        catch (Exception ex)
        {
            // 경고성 보조 정보다 — 실패해도 목록 기능을 막지 않는다.
            _log?.Warning($"[SuppressionConsole] /active 갱신 실패(무시): {ex.Message}");
        }
    }

    private SuppressionConsoleRow Row(EventSuppressionScheduleDto dto)
        => new(dto, _devices, _groups, RaiseSelectionCounts);

    /// <summary>
    /// 목록을 갈아 끼운다 — <c>Clear()+Add()</c> 대신 <b>있는 것은 두고</b> 맞춘다.
    /// </summary>
    private void Replace(IReadOnlyList<SuppressionConsoleRow> rows)
    {
        var selectedId = _selected?.Id;

        for (var i = Schedules.Count - 1; i >= 0; i--)
            if (rows.All(r => r.Id != Schedules[i].Id)) Schedules.RemoveAt(i);

        for (var i = 0; i < rows.Count; i++)
        {
            var at = IndexOfId(rows[i].Id);
            if (at < 0) Schedules.Insert(i, rows[i]);
            else
            {
                // 값이 바뀌었을 수 있으니 새 투영으로 갈되, 자리는 지킨다.
                Schedules[at] = rows[i];
                if (at != i) Schedules.Move(at, i);
            }
        }

        while (Schedules.Count > rows.Count) Schedules.RemoveAt(Schedules.Count - 1);

        Selected = selectedId is { } id ? Schedules.FirstOrDefault(s => s.Id == id) : null;
        RowsView.Refresh();
        RaiseCounts();
    }

    private int IndexOfId(int id)
    {
        for (var i = 0; i < Schedules.Count; i++) if (Schedules[i].Id == id) return i;
        return -1;
    }

    private void SyncFilterChips()
    {
        foreach (var f in Filters) f.IsSelected = f.Key == _filterKey;
        RowsView?.Refresh();
    }

    private void RaiseSelectionCounts()
    {
        NotifyOfPropertyChange(nameof(SelectedDeleteCount));
        NotifyOfPropertyChange(nameof(CanDeleteSelected));
        NotifyOfPropertyChange(nameof(DeleteSelectedText));
        NotifyOfPropertyChange(nameof(SelectAllDeletable));
        // 툴바 [삭제] 는 콘솔 바깥(대시보드)이 그린다 — 여기서 알리지 않으면 체크해도 계속 꺼져 있다.
        CountsChanged?.Invoke();
    }

    private void RaiseCounts()
    {
        NotifyOfPropertyChange(nameof(StatusLineText));
        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(EmptyText));
        NotifyOfPropertyChange(nameof(SuppressingCount));
        NotifyOfPropertyChange(nameof(ScheduledCount));
        NotifyOfPropertyChange(nameof(HasDeletableRows));
        NotifyOfPropertyChange(nameof(CanCleanupAll));
        NotifyOfPropertyChange(nameof(CleanupText));
        RaiseSelectionCounts();
        CountsChanged?.Invoke();
    }

    /// <summary>레일 배지를 다시 그려야 한다.</summary>
    public event System.Action? CountsChanged;

    #endregion

    #region - 서랍 -

    /// <summary>780 서랍.</summary>
    public SuppressionDrawerViewModel Drawer { get; }

    /// <summary>[새 스케줄] — 정본 SB L2392.</summary>
    public bool CanAdd => _canEdit() && !IsBusy;

    public string AddBlockedReason => _canEdit() ? "목록을 불러오는 중입니다." : SuppressionPermissionText.EditDenied;

    public void AddNew()
    {
        if (!CanAdd) { StatusText = AddBlockedReason; return; }
        _editEpoch++;               // 날아오던 [수정] 응답이 이 새 초안을 덮지 못하게 한다
        // 미적용 변경이 있으면 서랍이 거절한다 — 초안을 말없이 버리지 않는다.
        if (!Drawer.OpenNew()) StatusText = Drawer.StatusLine;
    }

    /// <summary>[수정] — 받아 온 원본에서 초안을 채운다.</summary>
    /// <summary>
    /// [수정] — 취소 · 종료된 창은 고칠 수 없다(서버가 거절한다). <see cref="SuppressionConsoleRow.IsCancellable"/> 가 그 판정이다.
    /// </summary>
    public bool CanEditSelected => _canEdit() && _selected is { IsCancellable: true };

    public async Task EditSelectedAsync(CancellationToken token = default)
    {
        if (_selected is null) return;
        if (!_canEdit()) { StatusText = SuppressionPermissionText.EditDenied; return; }
        // 초안이 살아 있으면 열기 전에 막는다 — 응답을 기다린 뒤 막으면 그 사이 화면이 바뀐다.
        if (!Drawer.TryLeave()) { StatusText = Drawer.StatusLine; return; }

        var row = _selected;
        // ⚠ 골라 놓고 [수정], 다시 골라 [수정] 하면 앞선 응답이 뒤에 도착할 수 있다.
        //   그러면 목록은 B 를 가리키는데 서랍은 A 를 연다 — 그대로 저장하면 엉뚱한 스케줄을 고친다.
        var epoch = ++_editEpoch;

        // 가장 최근 원본으로 연다 — 다른 세션이 고쳤을 수 있다. 실패하면 목록이 준 원본으로 간다(읽기라서 안전).
        EventSuppressionScheduleDto baseline = row.Dto;
        try
        {
            var res = await _api.GetSuppressionScheduleByIdAsync(row.Id, token).ConfigureAwait(false);
            if (res.Success && res.Data is not null) baseline = res.Data;
        }
        catch (Exception ex) { _log?.Warning($"[SuppressionConsole] 단건 조회 실패(목록 값으로 엽니다): {ex}"); }

        Post(() =>
        {
            if (epoch != _editEpoch) return;        // 더 최신 [수정] 이 이미 떠났다 — 이 응답은 버린다
            Drawer.OpenEdit(baseline);
        });
    }

    private void OnDrawerSaved(EventSuppressionScheduleDto? saved)
    {
        StatusText = saved is null
            ? "저장했습니다."
            : SuppressionRequestBuilder.TargetEcho(saved, DeviceName, GroupName);
        _ = LoadAsync();
    }

    /// <summary>
    /// 저장 — <b>서버 호출 한 번</b>. 새 것이면 POST, 고친 것이면 PATCH.
    /// </summary>
    private async Task<SuppressionSaveOutcome> SaveAsync(SuppressionDraft draft, CancellationToken token)
    {
        if (draft.IsNew)
        {
            // 서버 8.0 은 unit_id 생략을 "기본 부대 귀속 + 서버 로그 경고" 로 받고 다음 차수부터 422 로 바꾼다
            //   (schemas/event_suppression.py:68-71). 이 클라이언트의 부대를 싣는다 — 8.0 미만은 키 자체가 없다(extra=forbid).
            //   수정(PATCH)은 unit_id 를 보내지 않는다 — RFC 7396 에서 미전송은 '그대로 둠'이라 원래 부대가 보존된다.
            var unitId = await SuppressionUnitStamp.ResolveForCreateAsync(_unitScope(), _log, "SuppressionConsole", token).ConfigureAwait(false);
            var res = await _api.CreateSuppressionScheduleAsync(SuppressionRequestBuilder.BuildCreate(draft, unitId), token)
                                .ConfigureAwait(false);
            if (res.Success) return new SuppressionSaveOutcome(true, "억제 스케줄을 만들었습니다.", res.Data);

            // 서버 원문(검증 메시지 · 상태 코드)은 로그로만 — 화면 문장은 고정이다.
            _log?.Warning($"[SuppressionConsole] 생성 거절: {res.Error?.Message ?? res.Message}");
            return new SuppressionSaveOutcome(false, CreateFailedText, null);
        }

        var patch = await _api.PatchSuppressionScheduleAsync(draft.Id!.Value,
                                                             SuppressionRequestBuilder.BuildUpdate(draft),
                                                             token).ConfigureAwait(false);
        if (patch.Success) return new SuppressionSaveOutcome(true, "억제 스케줄을 수정했습니다.", patch.Data);

        _log?.Warning($"[SuppressionConsole] 수정 거절(#{draft.Id}): {patch.Error?.Message ?? patch.Message}");
        return new SuppressionSaveOutcome(false, UpdateFailedText, null);
    }

    /// <summary>생성을 서버가 받지 않았을 때의 한 줄.</summary>
    public const string CreateFailedText = "억제 스케줄을 만들지 못했습니다. 입력한 내용을 확인하고 다시 저장하세요.";

    /// <summary>수정을 서버가 받지 않았을 때의 한 줄.</summary>
    public const string UpdateFailedText = "억제 스케줄을 수정하지 못했습니다. [새로 불러오기]로 최신 내용을 확인한 뒤 다시 저장하세요.";

    #endregion

    #region - 상세 (SB L2734-2740) -

    public string DetailKind => "억제 스케줄";

    /// <summary>상세 머리 제목 — 고른 것이 없으면 다른 콘솔과 같은 "선택한 항목 없음"(실창 검토 #24).</summary>
    public string DetailTitle => _selected?.Name ?? NoSelectionTitle;

    /// <summary>고른 것이 없을 때의 상세 제목 — 커널 상세(ConsoleDetailPresenter)와 같은 말.</summary>
    public const string NoSelectionTitle = "선택한 항목 없음";

    /// <summary>고른 것이 없고 알릴 것도 없을 때의 바닥 한 줄 — 커널 상세와 같은 말.</summary>
    public const string IdleFooterText = "선택 대기";

    /// <summary>
    /// 상세 바닥 막대의 한 줄 — 알릴 것(저장 · 취소 결과 등)이 있으면 그것, 없고 고른 것도 없으면 "선택 대기".
    /// 바닥이 통째로 비어 있으면 다른 콘솔과 달리 '고장난 칸' 처럼 보였다(실창 검토 #24).
    /// </summary>
    public string DetailFooterText => _statusText.Length > 0 ? _statusText
        : _selected is null ? IdleFooterText
        : string.Empty;

    /// <summary>대상 칩(읽기 전용).</summary>
    public IReadOnlyList<SuppressionTargetChip> DetailTargets
    {
        get
        {
            if (_selected is null) return Array.Empty<SuppressionTargetChip>();
            var dto = _selected.Dto;
            var chips = new List<SuppressionTargetChip>();
            foreach (var id in dto.TargetDeviceIds ?? new List<int>())
                chips.Add(new SuppressionTargetChip(SuppressionTargetKind.Device, id, DeviceName(id)));
            foreach (var id in dto.TargetGroupIds ?? new List<int>())
                chips.Add(new SuppressionTargetChip(SuppressionTargetKind.Group, id, GroupName(id)));
            return chips;
        }
    }

    /// <summary>고른 행에 대상 칩이 하나도 없는가 — 그러면 칩 자리에 안내 한 줄을 낸다.</summary>
    public bool IsDetailTargetsEmpty => _selected is not null && DetailTargets.Count == 0;

    /// <summary>칩 자리가 비었을 때의 안내 — 전체 대상 스케줄은 원래 칩이 없다.</summary>
    public string DetailTargetsEmptyText => _selected?.Dto.TargetType == SuppressionTargetDrop.ModeAll
        ? "모든 장비에 적용되는 스케줄입니다."
        : "담긴 대상이 없습니다.";

    // '진행중' 이 '지금 억제 중' 이 아니라는 안내(SB L2738)는 상세 '억제' 절 "?"(Events.Suppression.Detail)로 옮겼다(help-callout H-3).

    #endregion

    #region - 파괴적 동작 (확인 → 실행) -

    /// <summary>[취소 예약] — soft-cancel. 이력(revoked_at)은 남는다.</summary>
    public bool CanCancelSelected => _canDelete() && _selected is { IsCancellable: true };

    public async Task CancelSelectedAsync()
    {
        if (_selected is null) return;
        if (!_canDelete()) { StatusText = SuppressionPermissionText.DeleteDenied; return; }
        if (!_selected.IsCancellable) { StatusText = "이미 끝났거나 취소된 스케줄입니다."; return; }

        await _events.PublishOnUIThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = CancelConfirmTitle,
            Explain = CancelConfirmText(_selected.Name),
            MessageModel = new CallCancelConsoleSuppressionMessageModel { ScheduleId = _selected.Id, Name = _selected.Name },
        }).ConfigureAwait(false);
    }

    /// <summary>취소 확인 팝업 제목 — 콘솔과 옛 억제창이 같은 말을 쓴다.</summary>
    public const string CancelConfirmTitle = "억제 스케줄 취소";

    /// <summary>
    /// 취소 확인 본문. 내부 번호(#id)와 필드 이름(<c>revoked_at</c>)은 싣지 않는다 — 운영자가 알아볼 것은 이름이다.
    /// </summary>
    public static string CancelConfirmText(string? name)
        => $"'{name}' 억제 스케줄을 취소할까요?\n취소해도 기록은 남습니다.";

    /// <summary>체크된 취소/종료 행.</summary>
    public int SelectedDeleteCount => Schedules.Count(s => s.IsSelected && s.IsDeletable);

    public bool HasDeletableRows => Schedules.Any(s => s.IsDeletable);

    public bool CanDeleteSelected => _canDelete() && SelectedDeleteCount > 0;

    public string DeleteSelectedText => $"선택 삭제 ({SelectedDeleteCount})";

    /// <summary>취소/종료 행 전체 선택.</summary>
    public bool SelectAllDeletable
    {
        get { var d = Schedules.Where(s => s.IsDeletable).ToList(); return d.Count > 0 && d.All(s => s.IsSelected); }
        set { foreach (var s in Schedules.Where(x => x.IsDeletable)) s.IsSelected = value; RaiseSelectionCounts(); }
    }

    public async Task DeleteSelectedAsync()
    {
        if (!_canDelete()) { StatusText = SuppressionPermissionText.DeleteDenied; return; }

        var ids = Schedules.Where(s => s.IsSelected && s.IsDeletable).Select(s => s.Id).ToList();
        if (ids.Count == 0) { StatusText = "삭제할 취소/종료 항목을 체크하세요."; return; }

        await ConfirmDeleteAsync(ids, $"선택한 {ids.Count}건을 목록에서 완전 삭제합니다.").ConfigureAwait(false);
    }

    /// <summary>[취소·종료 모두 정리].</summary>
    public bool CanCleanupAll => _canDelete() && Schedules.Any(s => s.IsDeletable);

    /// <summary>
    /// [정리] 버튼 글자. "정리 (51)" 만으로는 무엇을 지우는지 몰랐다(감사 E-7 #11) — 대상을 앞에 붙인다.
    /// 칩이 여섯이라 툴바가 빡빡해 가운뎃점으로 짧게 쓴다.
    /// </summary>
    public string CleanupText => $"종료·취소 정리 ({Schedules.Count(s => s.IsDeletable)})";

    public async Task CleanupAllAsync()
    {
        if (!_canDelete()) { StatusText = SuppressionPermissionText.DeleteDenied; return; }

        var ids = Schedules.Where(s => s.IsDeletable).Select(s => s.Id).ToList();
        if (ids.Count == 0) { StatusText = "정리할 취소/종료 항목이 없습니다."; return; }

        await ConfirmDeleteAsync(ids, $"현재 목록의 취소/종료 항목 {ids.Count}건을 모두 삭제합니다.").ConfigureAwait(false);
    }

    private Task ConfirmDeleteAsync(List<int> ids, string what)
        => _events.PublishOnUIThreadAsync(new OpenConfirmPopupMessageModel
        {
            Title = "억제 스케줄 삭제",
            Explain = $"{what}\n삭제 후에는 복구할 수 없습니다. 계속하시겠습니까?",
            MessageModel = new CallDeleteConsoleSuppressionMessageModel { Ids = ids },
        });

    /// <summary>확인 → 실제 soft-cancel.</summary>
    public async Task HandleAsync(CallCancelConsoleSuppressionMessageModel message, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _api.CancelSuppressionScheduleAsync(message.ScheduleId, cancellationToken).ConfigureAwait(false);
            if (res.Success)
            {
                await LoadAsync(cancellationToken).ConfigureAwait(false);

                // 하나를 취소해도 다른 활성 창이 계속 억제할 수 있다 — 방금 서버가 확인해 준 값으로 센다.
                var residual = _active.Count;
                Post(() => StatusText = residual > 0
                    ? $"'{message.Name}' 을(를) 취소했습니다. 아직 진행 중인 억제 스케줄이 {residual}건 남아 있습니다."
                    : $"'{message.Name}' 을(를) 취소했습니다.");
            }
            else
            {
                _log?.Warning($"[SuppressionConsole] 취소 거절(#{message.ScheduleId}): {res.Error?.Message ?? res.Message}");
                Post(() => StatusText = CancelFailedText);
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"[SuppressionConsole] 취소 실패: {ex}");
            Post(() => StatusText = CancelFailedText);
        }
        finally
        {
            await _events.PublishOnUIThreadAsync(new ClosePopupMessageModel(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>확인 → 실제 일괄 하드삭제 + <b>삭제 검증</b>.</summary>
    public async Task HandleAsync(CallDeleteConsoleSuppressionMessageModel message, CancellationToken cancellationToken)
    {
        var before = _totalCount;
        try
        {
            // ⚠ 서버는 한 요청에 500개까지만 받는다(schemas/event_suppression.py:379-383) —
            //   넘기면 요청 전체가 422 라 한 건도 지워지지 않는다. 목록은 100씩 쌓이므로 실제로 닿는 수다.
            var batches = SuppressionDeletionCheck.Chunk(message.Ids);
            var deleted = new List<int>();
            var skipped = 0;
            var notFound = 0;

            foreach (var batch in batches)
            {
                var res = await _api.BulkDeleteSuppressionSchedulesAsync(batch, cancellationToken).ConfigureAwait(false);
                if (!res.Success)
                {
                    // 서버 원문은 로그로만. 404/405 = 이 서버에 /bulk-delete 가 없다 — 그 사실만 운영자 말로 알린다.
                    _log?.Warning($"[SuppressionConsole] 일괄 삭제 거절({res.StatusCode}): {res.Error?.Message ?? res.Message}");
                    var head = BulkDeleteFailedText(res.StatusCode is 404 or 405);
                    var partial = deleted.Count > 0 ? $" 앞선 {deleted.Count}건은 이미 삭제됐습니다." : string.Empty;
                    Post(() => StatusText = head + partial);
                    if (deleted.Count > 0) await LoadAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                deleted.AddRange(res.Data?.DeletedIds ?? new List<int>());
                skipped += res.Data?.SkippedIds?.Count ?? 0;
                notFound += res.Data?.NotFoundIds?.Count ?? 0;
            }

            await LoadAsync(cancellationToken).ConfigureAwait(false);

            // 행 수 델타가 아니라 '전체 −N + id 부재' 로 본다(재조회는 1페이지로 리셋된다).
            var verdict = SuppressionDeletionCheck.Verify(before, deleted, _totalCount, Schedules.Select(s => s.Id));
            var extra = (skipped > 0 ? $" · 진행중/예정 {skipped}건은 건너뛰었습니다" : string.Empty)
                      + (notFound > 0 ? $" · 이미 없던 {notFound}건은 제외했습니다" : string.Empty);
            Post(() => StatusText = verdict.Message + extra);
        }
        catch (Exception ex)
        {
            _log?.Error($"[SuppressionConsole] 일괄 삭제 실패: {ex}");
            Post(() => StatusText = BulkDeleteFailedText(unsupported: false));
        }
        finally
        {
            await _events.PublishOnUIThreadAsync(new ClosePopupMessageModel(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>취소를 서버가 받지 않았을 때의 한 줄.</summary>
    public const string CancelFailedText = "억제 스케줄을 취소하지 못했습니다. 잠시 뒤 다시 시도하세요.";

    /// <summary>일괄 삭제 실패 문장. <paramref name="unsupported"/> = 이 서버에 일괄 삭제 기능이 없다(404/405).</summary>
    public static string BulkDeleteFailedText(bool unsupported) => unsupported
        ? "삭제하지 못했습니다. 이 서버에서는 일괄 삭제를 지원하지 않습니다."
        : "삭제하지 못했습니다. 잠시 뒤 다시 시도하세요.";

    #endregion

    #region - Helpers -

    private string DeviceName(int id)
        => _devices?.CollectionEntity.FirstOrDefault(d => d.Id == id)?.DeviceName is { Length: > 0 } n ? n : $"#{id}";

    private string GroupName(int id)
        => _groups?.CollectionEntity.FirstOrDefault(g => g.Id == id)?.Name is { Length: > 0 } n ? n : $"#{id}";

    /// <summary>화면 상태는 UI 스레드에서만 만진다 — API 완료는 작업 스레드로 온다.</summary>
    private static void Post(System.Action action) => Execute.OnUIThread(action);

    #endregion
}

/// <summary>
/// 권한이 없을 때 운영자에게 보이는 문장 — 권한 키(<c>events:edit</c> 등)는 화면에 싣지 않는다.
/// 콘솔 · 서랍 · 트레이 · 옛 억제창이 같은 문장을 쓴다.
/// </summary>
public static class SuppressionPermissionText
{
    public const string ViewDenied = "이벤트 조회 권한이 없습니다.";
    public const string EditDenied = "이벤트 편집 권한이 없습니다.";
    public const string DeleteDenied = "이벤트 삭제 권한이 없습니다.";
}

/// <summary>상태 필터 칩 하나.</summary>
public sealed class SuppressionFilterOption : PropertyChangedBase
{
    public SuppressionFilterOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; NotifyOfPropertyChange(); }
    }
    private bool _isSelected;

    public override string ToString() => Label;
}
