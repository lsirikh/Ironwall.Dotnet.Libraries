using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/28/2025 2:06:53 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ControllerDevicePanelViewModel : BaseDataGridMultiPanelViewModel<ControllerDeviceViewModel>
                                            , IHandle<CallDeleteControllerDeviceProcessMessageModel>
{
    #region - Ctors -
    public ControllerDevicePanelViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , IDeviceApiService apiService
                                        , ControllerDeviceProvider deviceProvider
                                        ) : base(eventAggregator, log)
    {
        _apiService = apiService;
        _deviceProvider = deviceProvider;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);
        _pCancellationTokenSource = new CancellationTokenSource();
        await DataInitialize(_pCancellationTokenSource!.Token).ConfigureAwait(false);
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
        if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
        {
            _pCancellationTokenSource.Cancel();
            _pCancellationTokenSource.Dispose();
        }
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public override async void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        if (SelectedItemCount == 0) return;
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "선택한 제어기를 참조하는 센서도 함께 삭제 될 수 있습니다. 정말 삭제하시겠습니까?",
            MessageModel = new CallDeleteControllerDeviceProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new ControllerDeviceViewModel(new ControllerDeviceModel());
        ViewModelProvider.Add(vm);
    }

    public override async void OnClickReloadButton(object sender, RoutedEventArgs e)
    {
        if (!await _processGate.WaitAsync(0))       // 0 → “비동기로 테스트-후-입장”
            return;
        try
        {
            ReloadButtonEnable = false;
            if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();
            var token = _pCancellationTokenSource!.Token;

            await DataInitialize(token);
            await Task.Delay(2000, token);
        }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            ReloadButtonEnable = true;            // UI Enable
            UpdateAction?.Invoke();
            _processGate.Release();                // 뮤텍스 해제
        }
    }

    public override async void OnClickSaveButton(object sender, RoutedEventArgs e)
    {
        if (!await _processGate.WaitAsync(0))
            return;

        try
        {
            SaveButtonEnable = false;

            if (_pCancellationTokenSource != null && !_pCancellationTokenSource.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();

            var token = _pCancellationTokenSource.Token;

            // 현재 Provider의 목록
            var currentList = _deviceProvider.OfType<ControllerDeviceModel>().ToList();

            // 서버 목록 조회 (비교를 위해)
            var serverList = await FetchControllersAsync(token);

            // Insert 대상: ID가 없는 경우 (신규)
            var insertList = currentList.Where(m => m.Id <= 0).ToList();

            // Update 대상: ID가 있고 변경된 경우
            var updateList = currentList
                .Where(m => m.Id > 0)
                .Join(serverList, m => m.Id, d => d.Id,
                    (m, d) => new { updated = m, original = d })
                .Where(p => !DeviceEquals(p.updated, p.original))
                .Select(p => p.updated)
                .ToList();

            // Create 처리
            foreach (var model in insertList)
            {
                await CreateControllerAsync(model, token);
            }

            // Update 처리
            foreach (var model in updateList)
            {
                await UpdateControllerAsync(model, token);
            }

            // 재조회
            await DataInitialize(_pCancellationTokenSource.Token);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            UpdateAction?.Invoke();
            SaveButtonEnable = true;
            _processGate.Release();
        }
    }

    public override void OnSelectionChanged(IList<ControllerDeviceViewModel> rows)
    {
        SelectedItems = rows;
        rows.ToList().ForEach(entity => entity.IsSelected = true);
        NotifyOfPropertyChange(() => SelectedItems);
    }

    private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // New items added
                if (e.NewItems == null) return;
                foreach (IControllerDeviceViewModel newItem in e.NewItems)
                {
                    _deviceProvider.Add((IControllerDeviceModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // Items removed
                if (e.OldItems == null) return;
                foreach (IControllerDeviceViewModel oldItem in e.OldItems)
                {
                    _deviceProvider.Remove((IControllerDeviceModel)oldItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // Some items replaced
                if (e.OldItems == null) return;
                foreach (IControllerDeviceViewModel oldItem in e.OldItems)
                {
                    _deviceProvider.Remove((IControllerDeviceModel)oldItem.Model);
                }
                if (e.NewItems == null) return;
                foreach (IControllerDeviceViewModel newItem in e.NewItems)
                {
                    _deviceProvider.Add((IControllerDeviceModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // The whole list is refreshed
                ViewModelProvider.Clear();
                foreach (var item in _deviceProvider.OfType<IControllerDeviceModel>())
                {
                    ViewModelProvider.Add(new ControllerDeviceViewModel(item));
                }

                break;
        }
    }
    #endregion
    #region - Binding Methods -
    private static bool DeviceEquals(IControllerDeviceModel a, IControllerDeviceModel b)
    {
        return a.DeviceNumber == b.DeviceNumber &&
               a.DeviceGroup == b.DeviceGroup &&
               a.DeviceName == b.DeviceName &&
               a.DeviceType == b.DeviceType &&
               a.Version == b.Version &&
               a.Status == b.Status &&
               a.IpAddress == b.IpAddress &&
               a.Port == b.Port;
    }

    #endregion
    #region - Helper Methods -
    /// <summary>
    /// GOP API를 통해 Controller 목록 조회하고 Model로 변환
    /// </summary>
    private async Task<List<ControllerDeviceModel>> FetchControllersAsync(CancellationToken token = default)
    {
        try
        {
            var response = await _apiService.GetControllersAsync(
                page: 1,
                limit: 100,
                token: token);

            if (response.Success && response.Data != null)
            {
                return response.Data
                    .Select(dto => dto.ToControllerDeviceModel())
                    .ToList();
            }
            else
            {
                _log?.Error($"Failed to fetch controllers: {response.Error?.Message}");
                return new List<ControllerDeviceModel>();
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in FetchControllersAsync: {ex.Message}");
            return new List<ControllerDeviceModel>();
        }
    }

    /// <summary>
    /// GOP API를 통해 Controller 생성
    /// </summary>
    private async Task<bool> CreateControllerAsync(ControllerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToControllerDeviceDto();
            var response = await _apiService.CreateControllerAsync(dto, token);

            if (response.Success)
            {
                _log?.Info($"Controller created successfully: {response.Data?.Id}");
                return true;
            }
            else
            {
                _log?.Error($"Failed to create controller: {response.Error?.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in CreateControllerAsync: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// GOP API를 통해 Controller 업데이트
    /// </summary>
    private async Task<bool> UpdateControllerAsync(ControllerDeviceModel model, CancellationToken token = default)
    {
        try
        {
            var dto = model.ToControllerDeviceDto();
            var response = await _apiService.UpdateControllerAsync(model.Id, dto, token);

            if (response.Success)
            {
                _log?.Info($"Controller updated successfully: {model.Id}");
                return true;
            }
            else
            {
                _log?.Error($"Failed to update controller {model.Id}: {response.Error?.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.Error($"Exception in UpdateControllerAsync: {ex.Message}");
            return false;
        }
    }
    #endregion
    #region - Processes -
    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                IsVisible = false;

                // API Fetching (DBService → ApiService 마이그레이션)
                var models = await FetchControllersAsync(cancellationToken);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException("Task was cancelled!");

                // Provider 업데이트
                _deviceProvider.Clear();
                foreach (var model in models)
                {
                    _deviceProvider.Add(model);
                }

                // ViewModelProvider 업데이트
                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in models.Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested)
                            throw new TaskCanceledException("Task was cancelled!");

                        var viewModel = new ControllerDeviceViewModel(item) { Index = index + 1 };
                        ViewModelProvider.Add(viewModel);
                    }
                    NotifyOfPropertyChange(() => ViewModelProvider);
                });

                ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
                IsVisible = true;
            }
            catch (TaskCanceledException ex)
            {
                _log?.Warning($"Raised {nameof(TaskCanceledException)}({nameof(DataInitialize)}) : {ex.Message}");
            }
        });
    }


    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CallDeleteControllerDeviceProcessMessageModel message, CancellationToken cancellationToken)
    {
        // Progress Popup 표시
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new OpenProgressPopupMessageModel(),
            cancellationToken);

        // 삭제 처리 (UI 스레드와 분리)
        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var model = (IControllerDeviceModel)item.Model;
                var response = await _apiService.DeleteControllerAsync(model.Id, cancellationToken);

                if (!response.Success)
                {
                    _log?.Error($"Failed to delete controller {model.Id}: {response.Error?.Message}");
                }
            }
        }, cancellationToken);

        // 취소 토큰 재생성
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        // 재조회
        await DataInitialize(_cancellationTokenSource!.Token).ConfigureAwait(false);
        UpdateAction?.Invoke();

        // Progress Popup 닫기
        await _eventAggregator.PublishOnCurrentThreadAsync(
            new ClosePopupMessageModel(),
            cancellationToken);
    }
    #endregion
    #region - Properties -
    public event System.Action? UpdateAction;
    #endregion
    #region - Attributes -
    private readonly IDeviceApiService _apiService;
    private readonly ControllerDeviceProvider _deviceProvider;
    #endregion

}