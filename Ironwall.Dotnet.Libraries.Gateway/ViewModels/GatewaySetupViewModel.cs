using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Libraries.Gateway.Services;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.GatewayEvents;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Gateway.ViewModels;
/****************************************************************************
   Purpose      : Gateway 설정 ViewModel (DataGrid 패널)
   Created By   : GHLee
   Created On   : 10/29/2025 12:16:00 PM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class GatewaySetupViewModel : BaseDataGridMultiPanelViewModel<GatewayEventViewModel>
                                    , IHandle<CallDelete3rdEventProcessMessageModel>
{
    #region - Ctors -
    public GatewaySetupViewModel(
        IEventAggregator eventAggregator,
        ILogService log,
        IGatewayDbService dbService,
        GatewayEventProvider eventProvider)
        : base(eventAggregator, log)
    {
        _dbService = dbService;
        _eventProvider = eventProvider;
    }
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
    #endregion

    #region - Abstract Method Implementations -
    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        try
        {
            var newEvent = new GatewayEventModel
            {
                Id = 0,
                EventName = $"sensor.event.{DateTime.Now:HHmmss}",
                Group = 1,
                IsEnable = true,
                Description = "새 이벤트"
            };

            var vm = new GatewayEventViewModel(newEvent)
            {
                Index = ViewModelProvider.Count + 1
            };

            ViewModelProvider.Add(vm);
            _log?.Info($"이벤트 추가 (임시): {newEvent.EventName}");
        }
        catch (Exception ex)
        {
            _log?.Error($"OnClickInsertButton Error: {ex}");
        }
    }

    public override async void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        if (SelectedItemCount == 0) return;
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "선택한 이벤트를 정말로 삭제하시겠습니까?",
            MessageModel = new CallDelete3rdEventProcessMessageModel()
        });
    }

    public override async void OnClickSaveButton(object sender, RoutedEventArgs e)
    {
        if (!await _processGate.WaitAsync(0))       // 0 → “비동기로 테스트-후-입장”
            return;

        try
        {
            SaveButtonEnable = false;

            if (_pCancellationTokenSource != null && !_pCancellationTokenSource!.IsCancellationRequested)
            {
                _pCancellationTokenSource.Cancel();
                _pCancellationTokenSource.Dispose();
            }
            _pCancellationTokenSource = new CancellationTokenSource();

            var token = _pCancellationTokenSource.Token;
            var currentList = _eventProvider;

            var dbList = await _dbService.FetchGatewayEventsAsync(token);

            var insertList = currentList.Where(m => m.Id <= 0).ToList();
            var updateList = currentList
                .Where(m => m.Id > 0)
                .Join(dbList, m => m.Id, d => d.Id,
                    (m, d) => new { updated = m, original = d })
                .Where(p => !GatewayEventEquals(p.updated, p.original))
                .Select(p => p.updated)
                .ToList();

            foreach (var model in updateList)
                await _dbService.UpdateGatewayEventAsync(model, token);

            foreach (var model in insertList)
                await _dbService.InsertGatewayEventAsync(model, token);

            await DataInitialize().ConfigureAwait(false);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            SaveButtonEnable = true;
            _processGate.Release();                // 뮤텍스 해제
        }

        //try
        //{
        //    await _processGate.WaitAsync();

        //    IsButtonEnable = false;
        //    SaveButtonEnable = false;

        //    var token = _pCancellationTokenSource?.Token ?? CancellationToken.None;

        //    var currentList = ViewModelProvider.Select(vm => vm.Model).Cast<IGatewayEventModel>().ToList();
        //    var dbList = (await _dbService.FetchGatewayEventsAsync(token))?.ToList() ?? new List<IGatewayEventModel>();

        //    var insertList = currentList.Where(m => m.Id <= 0).ToList();
        //    var updateList = currentList
        //        .Where(m => m.Id > 0)
        //        .Join(dbList, m => m.Id, d => d.Id, (m, d) => new { updated = m, original = d })
        //        .Where(p => !GatewayEventEquals(p.updated, p.original))
        //        .Select(p => p.updated)
        //        .ToList();

        //    foreach (var model in insertList)
        //    {
        //        var created = await _dbService.InsertGatewayEventAsync(model, token);
        //        if (created != null)
        //        {
        //            var vm = ViewModelProvider.FirstOrDefault(v => v.Model == model);
        //            if (vm != null)
        //            {
        //                vm.Id = created.Id;
        //            }

        //            if (!_eventProvider.Any(p => p.Id == created.Id))
        //                _eventProvider.Add(created);
        //        }
        //    }

        //    foreach (var model in updateList)
        //    {
        //        var updated = await _dbService.UpdateGatewayEventAsync(model, token);
        //        if (updated != null)
        //        {
        //            var providerItem = _eventProvider.FirstOrDefault(p => p.Id == updated.Id);
        //            if (providerItem != null)
        //            {
        //                providerItem.EventName = updated.EventName;
        //                providerItem.Group = updated.Group;
        //                providerItem.IsEnable = updated.IsEnable;
        //                providerItem.Description = updated.Description;
        //            }
        //        }
        //    }

        //    _log?.Info($"저장 완료 - Insert: {insertList.Count}건, Update: {updateList.Count}건");
        //}
        //catch (Exception ex)
        //{
        //    _log?.Error($"OnClickSaveButton Error: {ex}");
        //}
        //finally
        //{
        //    IsButtonEnable = true;
        //    SaveButtonEnable = true;
        //    _processGate.Release();
        //}
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
            _processGate.Release();                // 뮤텍스 해제
        }
    }

    public override void OnSelectionChanged(IList<GatewayEventViewModel> rows)
    {
        SelectedItems = rows;
        rows.ToList().ForEach(entity => entity.IsSelected = true);
        NotifyOfPropertyChange(() => SelectedItems);
        NotifyOfPropertyChange(() => SelectedItemCount);
    }
    #endregion

    #region - IHandle -
    public async Task HandleAsync(CallDelete3rdEventProcessMessageModel message, CancellationToken cancellationToken)
    {
        // 1. 진행중 UI 표시
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

        // 2. 비동기 작업 (UI 스레드와 분리)
        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var ret = await _dbService.DeleteGatewayEventAsync(item.Model, cancellationToken);
            }
        }, cancellationToken);

        await DataInitialize().ConfigureAwait(false);

        // 4. 진행중 UI 닫기
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
    }
    #endregion
    
    #region - Private Methods -
    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                IsVisible = false;
                //await Task.Delay(300, cancellationToken);

                //DB Fetching
                await _dbService.FetchInstanceAsync(cancellationToken);

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                //ViewModelProvider Setting
                if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");


                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in _eventProvider.Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");
                        ViewModelProvider.Add(new GatewayEventViewModel(item) { Index = index + 1 });
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

    private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        try
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // New items added
                    if (e.NewItems == null) return;
                    foreach (IGatewayEventViewModel newItem in e.NewItems)
                    {
                        _eventProvider.Add(newItem.Model);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    // Items removed
                    if (e.OldItems == null) return;
                    foreach (IGatewayEventViewModel oldItem in e.OldItems)
                    {
                        _eventProvider.Remove(oldItem.Model);
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    // Some items replaced
                    if (e.OldItems == null) return;
                    foreach (IGatewayEventViewModel oldItem in e.OldItems)
                    {
                        _eventProvider.Remove(oldItem.Model);
                    }
                    if (e.NewItems == null) return;
                    foreach (IGatewayEventViewModel newItem in e.NewItems)
                    {
                        _eventProvider.Add(newItem.Model);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // The whole list is refreshed
                    ViewModelProvider.Clear();
                    foreach (var item in _eventProvider)
                    {
                        ViewModelProvider.Add(new GatewayEventViewModel(item));
                    }

                    break;
            }
        }
        catch (Exception)
        {

            throw;
        }
        
    }

    private void ReindexViewModels()
    {
        for (int i = 0; i < ViewModelProvider.Count; i++)
        {
            ViewModelProvider[i].Index = i + 1;
        }
    }

    private bool GatewayEventEquals(IGatewayEventModel a, IGatewayEventModel b)
    {
        return a.Id == b.Id &&
               a.EventName == b.EventName &&
               a.Group == b.Group &&
               a.IsEnable == b.IsEnable &&
               a.Description == b.Description;
    }

   
    #endregion

    #region - Attributes -
    private readonly IGatewayDbService _dbService;
    private readonly GatewayEventProvider _eventProvider;
    #endregion
}
