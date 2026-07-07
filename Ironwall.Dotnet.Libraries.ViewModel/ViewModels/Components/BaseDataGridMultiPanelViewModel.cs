using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:13:45 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseDataGridMultiPanelViewModel<T> : BaseDataGridMultiViewModel<T>
                                                                where T : ISelectableBaseViewModel
{
    #region - Ctors -
    public BaseDataGridMultiPanelViewModel(IEventAggregator eventAggregator, ILogService log) : base(eventAggregator, log)
    {
        ViewModelProvider = new ObservableCollection<T>();
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        ButtonAllEnable();
        _pCancellationTokenSource = new CancellationTokenSource();
        return base.OnActivateAsync(cancellationToken);
    }
    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await Uninitialize();
        await base.OnDeactivateAsync(close, cancellationToken);
    }

    public abstract void OnClickInsertButton(object sender, RoutedEventArgs e);
    public abstract void OnClickDeleteButton(object sender, RoutedEventArgs e);
    public abstract void OnClickSaveButton(object sender, RoutedEventArgs e);
    public abstract void OnClickReloadButton(object sender, RoutedEventArgs e);

    protected virtual Task Uninitialize()
    {
        try
        {
            int draftCount = CountUnsavedDrafts();   // (PR-C) 미저장 Draft 수 — Clear로 폐기되기 전 캡처
            DispatcherService.Invoke(() =>
            {
                ButtonAllDisable();
                ViewModelProvider.Clear();
                SelectedItems.Clear();
                IsVisible = false;
            });

            // (PR-C) 화면 전환/종료로 미저장 Draft가 폐기됨을 비차단 통지(아키텍처상 차단형 확인불가 → 손실 시점 경고 + 행 시각마커로 사전 가시성).
            if (draftCount > 0)
                _ = _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
                {
                    Title = "미저장 항목 안내",
                    Explain = $"저장하지 않은 {draftCount}건이 화면 전환으로 사라졌습니다. 유지하려면 저장 후 이동하세요."
                });

            if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
                _pCancellationTokenSource?.Cancel();
            _pCancellationTokenSource?.Dispose();

        }
        catch (OperationCanceledException) { /* 비활성화 중 취소는 정상 */ }
        catch (Exception ex)
        {
            // 빈 catch 금지(rules) — 비활성화 중 Dispose/Cancel 실패를 침묵 처리하면 진단 불가.
            _log?.Error($"[{_className}] Uninitialize 예외: {ex.Message}");
        }
        return Task.CompletedTask;
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    protected void ButtonEnableControl(bool isButton, bool saveButton, bool reloadButton)
    {
        IsButtonEnable = isButton;
        SaveButtonEnable = saveButton;
        ReloadButtonEnable = reloadButton;
        Refresh();
    }
    protected void ButtonAllEnable()
    {
        _log?.Info($"{_className}({this.GetHashCode()})의 ButtonAllEnable Start!!");
        IsButtonEnable = true;
        SaveButtonEnable = true;
        ReloadButtonEnable = true;
        Refresh();
        _log?.Info($"{_className}({this.GetHashCode()})의 ButtonAllEnable End!!");
    }
    protected void ButtonAllDisable()
    {
        _log?.Info($"{_className}({this.GetHashCode()})의 ButtonAllEnable Start!!");
        IsButtonEnable = false;
        SaveButtonEnable = false;
        ReloadButtonEnable = false;
        _log?.Info($"{_className}({this.GetHashCode()})의 ButtonAllEnable End!!");
    }

    /// <summary>
    /// 공통 삭제 루프 (CRUD 라이프사이클 통일 — 가드 단일 출처).
    /// <para>Id≤0(Draft/미저장 임시행) → 로컬만 제거(API 미호출). Id&gt;0 → DELETE API 후 성공 시에만 로컬 제거(verify-after-success).</para>
    /// 실패한 id는 <see cref="_deleteFailures"/>에 누적되고 0건 초과 시 베이스가 InfoPopup으로 통지(중앙화).
    /// </summary>
    /// <param name="getId">행 VM → 모델 Id 추출 (예: r =&gt; r.Model.Id)</param>
    /// <param name="deleteApi">Id로 서버 1건 삭제 (성공 bool 반환)</param>
    /// <param name="removeLocal">로컬 provider에서 행 제거</param>
    protected async Task ExecuteDeleteAsync(
        IEnumerable<T> items,
        Func<T, int> getId,
        Func<int, CancellationToken, Task<bool>> deleteApi,
        Action<T> removeLocal,
        CancellationToken ct = default,
        bool suppressNotify = false)   // (INV-5) 봉투 경유 시 true — 통지를 ClosePopup 이후로 미룸(Progress 위 중첩 방지)
    {
        _deleteFailures.Clear();
        foreach (var row in items)
        {
            ct.ThrowIfCancellationRequested();
            int id = getId(row);
            if (id <= 0) { removeLocal(row); continue; }          // Draft = 로컬만 (API 미호출)
            bool ok;
            try { ok = await deleteApi(id, ct); }
            catch (OperationCanceledException) { throw; }          // 취소는 실패로 오분류하지 않음(재던지기)
            catch (Exception ex) { _log?.Error($"[{_className}] ExecuteDeleteAsync(id={id}) 예외: {ex.Message}"); ok = false; }
            if (ok) removeLocal(row);                              // verify-after-success
            else _deleteFailures.Add(id);
        }

        // 부분 삭제 실패 통지(중앙화) — 미통지 시 "삭제했는데 행이 조용히 복귀"가 사용자에게 노출됨
        // (INV-5) suppressNotify=true(봉투 경유)면 여기서 발행하지 않고 봉투가 ClosePopup 이후 단일 통지.
        if (!suppressNotify && _deleteFailures.Count > 0)
        {
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "삭제 일부 실패",
                Explain = $"{_deleteFailures.Count}건 삭제에 실패했습니다. 서버에 남아있는 항목은 재조회 시 다시 표시됩니다.\n(Id: {string.Join(", ", _deleteFailures.Take(10))})"
            }, ct);
        }
    }

    // ───────────────────────── CRUD 표준 오케스트레이션 봉투 (PRD DataGridPanel_CRUD_Standard_Convention) ─────────────────────────
    /// <summary>process CTS 재생성 — 조건 없이 항상 Dispose 후 new(NRE/ObjectDisposed 방지, INV-3).</summary>
    protected void RegenerateProcessCts()
    {
        try { _pCancellationTokenSource?.Dispose(); } catch { /* 이미 dispose됨 무시 */ }
        _pCancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// 모든 CRUD 오퍼레이션(Save/Reload/Delete)이 공유하는 오케스트레이션 봉투.
    /// 게이트 상호배타(INV-1/2) · linked+timeout CTS(INV-12) · Progress Close-in-finally(INV-4) ·
    /// 단일 통지-after-close(INV-5) · teardown 가드(INV-15). 호출 스레드: UI.
    /// </summary>
    protected async Task RunCrudOperationAsync(CrudOperationSpec spec)
    {
        if (_isTearingDown) return;                                             // INV-15
        if (!await _processGate.WaitAsync(0))                                   // INV-1 재진입 배타
        {
            _log?.Warning($"[{_className}] {spec.OperationName} 처리 중 — 중복 진입 차단");
            return;
        }
        spec.SetBusyFlag?.Invoke(true);
        var outcome = new OperationOutcome();
        try                                                                     // ── outer
        {
            RegenerateProcessCts();                                            // ★envelope가 process-CTS 수명 소유
            using var timeoutCts = new CancellationTokenSource(spec.Timeout);  // INV-12
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                _pCancellationTokenSource!.Token, spec.CallerToken, timeoutCts.Token); // INV-3 process 토큰 정본
            if (spec.ShowProgress)
                await _eventAggregator.PublishOnUIThreadAsync(new OpenProgressPopupMessageModel());   // INV-16 UI 마샬
            try                                                               // ── inner
            {
                outcome = await spec.Work(linked.Token) ?? outcome;
            }
            catch (OperationCanceledException) { _log?.Warning($"[{_className}] {spec.OperationName} 취소/타임아웃"); outcome.Canceled = true; }
            catch (Exception ex) { _log?.Error($"[{_className}] {spec.OperationName} 오류: {ex.Message}"); outcome.Error = ex; }
            finally
            {
                if (spec.ShowProgress)
                    await _eventAggregator.PublishOnUIThreadAsync(new ClosePopupMessageModel());       // ★INV-4 Close는 inner finally
            }
            if (spec.Notify != null) await spec.Notify(outcome);              // INV-5 Close 이후 단일 통지
        }
        finally                                                               // ── outer
        {
            spec.SetBusyFlag?.Invoke(false);
            _processGate.Release();                                           // ★INV-2 항상 해제
        }
    }

    /// <summary>Delete 특수화 — Progress 모달 + ExecuteDeleteAsync + ClosePopup 이후 단일 통지.</summary>
    protected Task RunDeleteOperationAsync(
        IReadOnlyList<T> snapshot,
        Func<T, int> getId,
        Func<int, CancellationToken, Task<bool>> deleteApi,
        Action<T> removeLocal,
        Func<CancellationToken, Task> fetchAndReinit,   // device=FetchAll+DataInitialize / event=DataInitialize
        CancellationToken callerToken)
        => RunCrudOperationAsync(new CrudOperationSpec
        {
            OperationName = "삭제",
            ShowProgress = true,
            CallerToken = callerToken,
            Work = async ct =>
            {
                await ExecuteDeleteAsync(snapshot, getId, deleteApi, removeLocal, ct, suppressNotify: true);
                await fetchAndReinit(ct);
                var failed = _deleteFailures.ToArray();
                return new OperationOutcome
                {
                    FailedIds = failed,
                    SuccessCount = Math.Max(0, snapshot.Count - failed.Length),
                };
            },
            Notify = NotifyDeleteResultAsync,
        });

    /// <summary>삭제 결과 통지(Close 이후 1회). 전건 성공=무통지(정책).</summary>
    protected async Task NotifyDeleteResultAsync(OperationOutcome o)
    {
        if (o.Canceled)
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            { Title = "삭제 시간 초과", Explain = "삭제가 취소되었거나 시간이 초과되었습니다. 목록을 새로고침해 확인하세요." });
        else if (o.Error != null)
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            { Title = "삭제 오류", Explain = "삭제 중 오류가 발생했습니다. 로그를 확인하세요." });
        else if (o.FailedIds.Count > 0)
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            { Title = "삭제 일부 실패", Explain = $"{o.FailedIds.Count}건 삭제에 실패했습니다. 서버에 남은 항목은 재조회 시 다시 표시됩니다.\n(Id: {string.Join(", ", o.FailedIds.Take(10))})" });
        // 전건 성공 = 무통지(정책 §11-8)
    }

    // ───────────────────────── 삭제 개수 상한 가드 (CRUD 표준) ─────────────────────────
    /// <summary>한 번에 삭제 허용하는 최대 선택 수 — 순차 삭제 시간(봉투 타임아웃)·오삭제·게이트 장기점유 방지. PRD DataGridPanel_CRUD_Standard_Convention.</summary>
    public const int MAX_DELETE_COUNT = 500;

    /// <summary>
    /// 삭제 선택 수가 <see cref="MAX_DELETE_COUNT"/>를 초과하면 안내 팝업 발행 후 true 반환(호출부는 즉시 return).
    /// 각 패널 OnClickDeleteButton에서 Confirm 발행 전에 호출. 호출 스레드: UI(INV-16).
    /// </summary>
    protected bool IsDeleteBatchExceeded(int count)
    {
        if (count <= MAX_DELETE_COUNT) return false;
        _log?.Warning($"[{_className}] 삭제 개수 상한 초과: {count} > {MAX_DELETE_COUNT} — 삭제 차단");
        _ = _eventAggregator?.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
        {
            Title = "삭제 개수 초과",
            Explain = $"한 번에 최대 {MAX_DELETE_COUNT}건까지 삭제할 수 있습니다.\n선택을 {MAX_DELETE_COUNT}건 이하로 줄여 다시 시도하세요. (현재 {count}건 선택)"
        });
        return true;
    }

    // ───────────────────────── Temp-state 통일 공통 루프 (PR-A) ─────────────────────────
    /// <summary>
    /// 루프A — Draft(Id≤0) 생성. <paramref name="isValidForCreate"/>=false면 보류(행 유지·서버 미호출, G7 FK 사전조건).
    /// <paramref name="createApi"/> 성공 시 <paramref name="onCommitted"/>(타입드 provider면 로컬 draft 제거 / 독립이면 noop,
    /// 후속 Clear+rebuild가 흡수). 실패는 sanitize 메시지로 <paramref name="failures"/>에 누적. 보류 건수 반환.
    /// </summary>
    protected async Task<int> ExecuteCreateAsync(
        IEnumerable<T> drafts,
        Func<T, bool> isValidForCreate,
        Func<T, CancellationToken, Task<ApiResultLite>> createApi,
        Action<T> onCommitted,
        Func<T, string> rowLabel,
        List<string> failures,
        CancellationToken ct = default)
    {
        int held = 0;
        foreach (var row in drafts)
        {
            ct.ThrowIfCancellationRequested();
            if (!isValidForCreate(row)) { held++; continue; }      // G7: 서버참조 FK/필수값 미충족 → 보류
            ApiResultLite res;
            try { res = await createApi(row, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log?.Error($"[{_className}] ExecuteCreateAsync 예외: {ex.Message}"); res = new ApiResultLite(false, 0, ex.Message); }
            if (res.Success) onCommitted(row);                     // 커밋 처리(타입드 Remove 등)
            else failures.Add($"{rowLabel(row)} — HTTP {res.StatusCode}: {SanitizeDetails(res.Details)}");
        }
        return held;
    }

    /// <summary>루프B — Id&gt;0 변경분 Update. 실패는 <paramref name="failures"/>에 누적.</summary>
    protected async Task ExecuteSaveUpdatesAsync(
        IEnumerable<T> rows,
        Func<T, CancellationToken, Task<ApiResultLite>> updateApi,
        Func<T, string> rowLabel,
        List<string> failures,
        CancellationToken ct = default)
    {
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            ApiResultLite res;
            try { res = await updateApi(row, ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { _log?.Error($"[{_className}] ExecuteSaveUpdatesAsync 예외: {ex.Message}"); res = new ApiResultLite(false, 0, ex.Message); }
            if (!res.Success)
                failures.Add($"{rowLabel(row)} — HTTP {res.StatusCode}: {SanitizeDetails(res.Details)}");
        }
    }

    /// <summary>저장 결과 통지 — 실패/보류 0건이면 무통지. 마스킹된 사유 포함.</summary>
    protected async Task NotifySaveResultAsync(string title, List<string> failures, int heldCount, CancellationToken ct = default)
    {
        if (failures.Count == 0 && heldCount == 0) return;
        var sb = new System.Text.StringBuilder();
        if (failures.Count > 0)
        {
            sb.AppendLine($"{failures.Count}건 저장에 실패했습니다:");
            sb.AppendLine(string.Join("\n", failures.Take(10)));
        }
        if (heldCount > 0)
            sb.AppendLine($"{heldCount}건은 필수값(제어기/IP 등) 미충족으로 보류했습니다. 값을 채운 뒤 다시 저장하세요.");
        await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
        {
            Title = title,
            Explain = sb.ToString().TrimEnd()
        }, ct);
    }

    /// <summary>(보안) 서버 422 응답 본문에서 민감 필드경로 노출 차단 — UI 표출 전 마스킹.</summary>
    protected static string SanitizeDetails(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "(상세 없음)";
        foreach (var k in new[] { "password", "passwd", "token", "secret", "credential" })
            if (raw.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)
                return "(민감정보 포함 응답 — 상세는 로그 확인)";
        return raw.Length > 400 ? raw.Substring(0, 400) + "…" : raw;
    }

    /// <summary>(Draft 격리 불변식) Id≤0 Draft는 공유/타입드 provider 투영 금지 — CollectionChanged.Add 가드용.</summary>
    protected static bool ShouldProjectToProvider(int id) => id > 0;

    /// <summary>(PR-C) 미저장 Draft(Id≤0) 행 수 — 패널별 override(T가 Id 미노출이라 베이스 일반화 불가). 종료/전환 시 손실 경고용.</summary>
    protected virtual int CountUnsavedDrafts() => 0;
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public bool IsVisible
    {
        get { return _isVisible; }
        set
        {
            _isVisible = value;
            NotifyOfPropertyChange(() => IsVisible);
        }
    }

    public bool IsButtonEnable
    {
        get { return _isButtonEnable; }
        set
        {
            _isButtonEnable = value;
            NotifyOfPropertyChange(() => IsButtonEnable);
        }
    }

    public bool ReloadButtonEnable
    {
        get { return _reloadButtonEnable; }
        set
        {
            _reloadButtonEnable = value;
            NotifyOfPropertyChange(() => ReloadButtonEnable);
        }
    }

    public bool SaveButtonEnable
    {
        get { return _saveButtonEnable; }
        set
        {
            _saveButtonEnable = value;
            NotifyOfPropertyChange(() => SaveButtonEnable);
        }
    }

    /// <summary>실제 저장 동작 진행 중에만 true. 저장버튼 스피너(ProgressCircle) 표시 전용 —
    /// 권한/활성 상태(SaveButtonEnable·IsButtonEnable)와 분리(권한 없는 계정 스피너 영구표시 버그 수정).</summary>
    public bool IsSaving
    {
        get { return _isSaving; }
        set
        {
            _isSaving = value;
            NotifyOfPropertyChange(() => IsSaving);
        }
    }
    public ObservableCollection<T> ViewModelProvider { get; set; }

    #endregion
    #region - Attributes -
    private bool _isVisible;
    private bool _isButtonEnable;
    private bool _reloadButtonEnable;
    private bool _saveButtonEnable;
    private bool _isSaving;
    protected readonly SemaphoreSlim _processGate = new(1, 1);
    protected readonly List<int> _deleteFailures = new();
    #endregion
}

/// <summary>
/// Temp-state Create/Update 루프용 경량 API 결과. 패널이 ApiResponse&lt;T&gt;를 이걸로 변환해 베이스에 전달.
/// </summary>
public readonly record struct ApiResultLite(bool Success, int StatusCode, string? Details);

/// <summary>CRUD 봉투 결과 — 성공/부분실패/보류/취소/오류를 모두 표현(침묵 무통지 방지).</summary>
public sealed record OperationOutcome
{
    public IReadOnlyList<int> FailedIds { get; init; } = Array.Empty<int>();      // Delete 실패 Id
    public IReadOnlyList<string> Failures { get; init; } = Array.Empty<string>(); // Save 실패(마스킹됨)
    public int HeldCount { get; init; }        // Save 보류
    public bool IncompleteFetch { get; init; } // Save/Reload 서버 스냅샷 불완전
    public int SuccessCount { get; init; }
    public bool Canceled { get; set; }         // 봉투가 설정(취소/타임아웃)
    public Exception? Error { get; set; }       // 봉투가 설정
}

/// <summary>CRUD 오케스트레이션 봉투 사양 — 오퍼레이션별 가변축을 파라미터/hook으로.</summary>
public sealed class CrudOperationSpec
{
    public required string OperationName { get; init; }
    public required Func<CancellationToken, Task<OperationOutcome>> Work { get; init; }
    public bool ShowProgress { get; init; }                          // Delete=true, Save/Reload=false
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public CancellationToken CallerToken { get; init; } = default;   // event delete의 EA 토큰
    public Func<OperationOutcome, Task>? Notify { get; init; }       // Close 이후 1회. null=무통지
    public Action<bool>? SetBusyFlag { get; init; }                 // IsSaving / ReloadButtonEnable
}