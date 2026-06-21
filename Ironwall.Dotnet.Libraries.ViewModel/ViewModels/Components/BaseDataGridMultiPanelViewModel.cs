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
            DispatcherService.Invoke(() =>
            {
                ButtonAllDisable();
                ViewModelProvider.Clear();
                SelectedItems.Clear();
                IsVisible = false;
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
        CancellationToken ct = default)
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
        if (_deleteFailures.Count > 0)
        {
            await _eventAggregator.PublishOnUIThreadAsync(new OpenInfoPopupMessageModel
            {
                Title = "삭제 일부 실패",
                Explain = $"{_deleteFailures.Count}건 삭제에 실패했습니다. 서버에 남아있는 항목은 재조회 시 다시 표시됩니다.\n(Id: {string.Join(", ", _deleteFailures.Take(10))})"
            }, ct);
        }
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
    public ObservableCollection<T> ViewModelProvider { get; set; }

    #endregion
    #region - Attributes -
    private bool _isVisible;
    private bool _isButtonEnable;
    private bool _reloadButtonEnable;
    private bool _saveButtonEnable;
    protected readonly SemaphoreSlim _processGate = new(1, 1);
    protected readonly List<int> _deleteFailures = new();
    #endregion
}

/// <summary>
/// Temp-state Create/Update 루프용 경량 API 결과. 패널이 ApiResponse&lt;T&gt;를 이걸로 변환해 베이스에 전달.
/// </summary>
public readonly record struct ApiResultLite(bool Success, int StatusCode, string? Details);