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
        catch (Exception)
        {
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