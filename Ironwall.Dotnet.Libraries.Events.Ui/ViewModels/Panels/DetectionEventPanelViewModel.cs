using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Db.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Specialized;
using System.Threading;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:48:06 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DetectionEventPanelViewModel : BaseDataGridMultiPanelViewModel<DetectionEventViewModel>
                                        , IHandle<CallDeleteDetectionEventProcessMessageModel>
{
    
    #region - Ctors -
    public DetectionEventPanelViewModel(IEventAggregator eventAggregator
                                        , ILogService log
                                        , IEventDbService eventDbService
                                        , DeviceProvider deviceProvider
                                        , EventProvider eventProvider) 
                                        : base(eventAggregator, log)
    {
        _dbService = eventDbService;
        _eventProvider = eventProvider;
        DeviceProvider = deviceProvider;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        
        await DataInitialize(cancellationToken).ConfigureAwait(false);
        await base.OnActivateAsync(cancellationToken);
        IsVisible = true;
        //UpdateAction?.Invoke(_startDate, _endDate);
    }

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
        _log.Info("OnDeactivateAsync in DetectionEventPanelViewModel");
        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public override async void OnClickDeleteButton(object sender, RoutedEventArgs e)
    {
        if (SelectedItemCount == 0) return;
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel
        {
            Explain = "선택한 이벤트를 정말로 삭제하시겠습니까? 해당이벤트의 조치보고도 함께 삭제 됩니다.",
            MessageModel = new CallDeleteDetectionEventProcessMessageModel()
        });
    }

    public override void OnClickInsertButton(object sender, RoutedEventArgs e)
    {
        var vm = new DetectionEventViewModel(new DetectionEventModel());
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
            //UpdateAction?.Invoke(_startDate, _endDate);
            _processGate.Release();                // 뮤텍스 해제
        }
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

            var dbList = await _dbService.FetchDetectionEventsAsync(startDate:StartDate, endDate:EndDate, token:token);

            var insertList = currentList
                            .Where(m => m.Id <= 0)
                            .ToList();
            var updateList = ViewModelProvider
                            .Where(vm => vm.IsEdited && vm.Model.Id > 0)
                            .Select(vm => (IDetectionEventModel)vm.Model)
                            .ToList();

            foreach (var model in updateList)
                await _dbService.UpdateDetectionEventAsync(model, token);

            foreach (var model in insertList.OfType<IDetectionEventModel>())
                await _dbService.InsertDetectionEventAsync(model, token);

            await DataInitialize().ConfigureAwait(false);
            await Task.Delay(2000, token);
        }
        catch (TaskCanceledException ex) { _log?.Warning(ex.Message); }
        catch (OperationCanceledException) { /* 무시 */ }
        catch (Exception ex) { _log?.Error(ex.Message); }
        finally
        {
            //UpdateAction?.Invoke(_startDate, _endDate);
            SaveButtonEnable = true;
            _processGate.Release();                // 뮤텍스 해제
        }
    }
    #endregion
    #region - Binding Methods -
    public override void OnSelectionChanged(IList<DetectionEventViewModel> rows)
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
                foreach (IDetectionEventViewModel newItem in e.NewItems)
                {
                    _eventProvider.Add((IDetectionEventModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                // Items removed
                if (e.OldItems == null) return;
                foreach (IDetectionEventViewModel oldItem in e.OldItems)
                {
                    _eventProvider.Remove((IDetectionEventModel)oldItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                // Some items replaced
                if (e.OldItems == null) return;
                foreach (IDetectionEventViewModel oldItem in e.OldItems)
                {
                    _eventProvider.Remove((IDetectionEventModel)oldItem.Model);
                }
                if (e.NewItems == null) return;
                foreach (IDetectionEventViewModel newItem in e.NewItems)
                {
                    _eventProvider.Add((IDetectionEventModel)newItem.Model);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                // The whole list is refreshed
                ViewModelProvider.Clear();
                foreach (var item in _eventProvider.OfType<IDetectionEventModel>())
                {
                    ViewModelProvider.Add(new DetectionEventViewModel(item));
                }

                break;
        }
    }

    private static bool EventEquals(IDetectionEventModel a, IDetectionEventModel b)
    {
        return a.EventGroup == b.EventGroup &&
               a?.Device?.Id == b?.Device?.Id &&
               a.MessageType == b.MessageType &&
               a.Status == b.Status && 
               a.Result == b.Result &&
               Math.Abs((a.DateTime - b.DateTime).TotalSeconds) < 1;
    }
    #endregion
    #region - Processes -
    public void SetDate(DateTime startDate, DateTime endDate)
    {
        _startDate = startDate;
        _endDate = endDate;
        EndDateDisplay = StartDate;
    }

    public bool CanClckSearch => true;
    public async void ClickSearch()
    {
        try
        {
            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();

            _cancellationTokenSource = new CancellationTokenSource();
            await DataInitialize(_cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            _log?.Error(ex.Message);
        }
        finally
        {
            IsVisible = true;
        }

    }
    public bool CanClickCancel => true;
    public void ClickCancel()
    {
        try
        {
            if (_cancellationTokenSource == null && _cancellationTokenSource.IsCancellationRequested)
                return;

            _cancellationTokenSource.Cancel();
        }
        catch (TaskCanceledException) { }
        catch(Exception ex)
        {
            _log?.Error(ex.Message);
        }
    }

    private Task DataInitialize(CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            try
            {
                IsVisible = false;

                //DB Fetching
                var events = await _dbService.FetchDetectionEventsAsync(startDate:StartDate, endDate:EndDate, token:cancellationToken);
                if (events == null) return;
                _eventProvider.Clear();
                foreach (var item in events)
                {
                    _eventProvider.Add(item);
                }

                if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");

                ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;

                //ViewModelProvider Setting
                if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");


                DispatcherService.Invoke(() =>
                {
                    ViewModelProvider.Clear();
                    foreach (var (item, index) in _eventProvider.OfType<IDetectionEventModel>().OrderBy(item => item.Id).Select((item, index) => (item, index)))
                    {
                        if (cancellationToken.IsCancellationRequested) new TaskCanceledException("Task was cancelled!");
                        ViewModelProvider.Add(new DetectionEventViewModel(item) { Index = index + 1 });
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
            finally
            {
                UpdateAction?.Invoke(_startDate, _endDate);
            }
        });
    }


    #endregion
    #region - IHanldes -
    public async Task HandleAsync(CallDeleteDetectionEventProcessMessageModel message, CancellationToken cancellationToken)
    {
        // 1. 진행중 UI 표시
        await _eventAggregator.PublishOnCurrentThreadAsync(new OpenProgressPopupMessageModel(), cancellationToken);

        // 2. 비동기 작업 (UI 스레드와 분리)
        await Task.Run(async () =>
        {
            foreach (var item in SelectedItems.ToList())
            {
                var ret = await _dbService.DeleteDetectionEventAsync((IDetectionEventModel)item.Model, cancellationToken);
            }
        }, cancellationToken);

        await DataInitialize().ConfigureAwait(false);
        UpdateAction?.Invoke(_startDate, _endDate);

        // 4. 진행중 UI 닫기
        await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel(), cancellationToken);
    }
    #endregion
    #region - Properties -
    public DateTime StartDate
    {
        get { return _startDate; }
        set
        {
            _startDate = value;
            NotifyOfPropertyChange(() => StartDate);
            EndDateDisplay = _startDate;
        }
    }

    public DateTime EndDate
    {
        get { return _endDate; }
        set
        {
            _endDate = value;
            NotifyOfPropertyChange(() => EndDate);
        }
    }

    public DateTime EndDateDisplay
    {
        get { return _endDateDisplay; }
        set
        {
            _endDateDisplay = value;
            NotifyOfPropertyChange(() => EndDateDisplay);
        }
    }
    public DeviceProvider DeviceProvider { get; }
    public delegate void SendDate(DateTime start, DateTime end);
    public event SendDate? UpdateAction;
    #endregion
    #region - Attributes -
    protected DateTime _startDate;
    protected DateTime _endDate;
    protected DateTime _endDateDisplay;
    private IEventDbService _dbService;
    private EventProvider _eventProvider;
    #endregion

}