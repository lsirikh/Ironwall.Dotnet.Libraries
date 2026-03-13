using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using Action = System.Action;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/1/2025 7:13:26 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class EventCardListPanelViewModel: BaseEventPanelViewModel<EventCardBaseViewModel>
                                            , IHandle<DetectionReportedMessageModel>
                                            , IHandle<MalfunctionReportedMessageModel>
    {
        #region - Ctors -
        public EventCardListPanelViewModel(IEventAggregator ea
                                          , ILogService log
                                          , EventProviderService providerService
                                          , IAccountModel userModel
                                          , IEventApiService apiService
                                          , ISymbolEventManager symbolEventManager
                                          , IEventQueueManager eventQueueManager)
                                        : base(ea, log)
        {
            _providerService = providerService;
            _userModel = userModel;
            _apiService = apiService;
            _symbolEventManager = symbolEventManager;
            _eventQueueManager = eventQueueManager;
            _batchBuffer = new EventCardBatchBuffer<EventCardBaseViewModel>();
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
            _batchTimer = new Timer(FlushPendingCards, null, BATCH_INTERVAL_MS, BATCH_INTERVAL_MS);
            return base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
            _batchTimer?.Dispose();
            _batchTimer = null;

            // 잔여 큐 즉시 flush
            var remaining = _batchBuffer.DrainQueue();
            if (remaining.Count > 0)
            {
                DispatcherService.Invoke(() =>
                {
                    foreach (var card in remaining)
                        ViewModelProvider.Add(card);
                });
            }
            return base.OnDeactivateAsync(close, cancellationToken);
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            IsAnimationEnabled = ViewModelProvider.Count <= ANIMATION_THRESHOLD;
            UpdateAction?.Invoke();
        }

        private async void FlushPendingCards(object? state)
        {
            var batch = _batchBuffer.DrainQueue();
            if (batch.Count == 0) return;

            // 적응형 간격 조정
            var newInterval = _batchBuffer.CalculateInterval(batch.Count);
            _batchTimer?.Change(newInterval, newInterval);

            await DispatcherService.BeginInvoke(() =>
            {
                foreach (var card in batch)
                    ViewModelProvider.Add(card);
            }, DispatcherPriority.Background);
        }

        public async void OnClickButtonActionAll(object sender, RoutedEventArgs e)
        {
            await _eventAggregator.PublishOnCurrentThreadAsync(new OpenConfirmPopupMessageModel() { Title = "전체 조치보고", Explain = "전체 조치보고를 수행하시겠습니까?", MessageModel = new CallAllEventReportMessageModel() });


        }

        public void OnClickEventCard(object sender, RoutedEventArgs e)
        {
            //if (!((sender as ListBox).SelectedItem is EventCardViewModel preEventViewModel))
            //    return;

            //if (SetupModel.EventCardMapChange)
            //{
            //    await _eventAggregator.PublishOnCurrentThreadAsync(new OpenCanvasMessageModel() { MapNumber = preEventViewModel.Map });
            //}
        }


        public async void OnButtonAction(object sender, RoutedEventArgs e)
        {
            //if (!SetupModel.IsServer)
            //    return;


            //var source = e.OriginalSource as FrameworkElement;
            //var dataContext = source.DataContext as EventCardViewModel;
            ////_eventAggregator.PublishOnCurrentThreadAsync(new DiscardEventPreMessageModel { Value = dataContext });
            //await _eventAggregator.PublishOnCurrentThreadAsync(new OpenPreEventRemoveDialogMessageModel { Value = dataContext });


            var source = e.OriginalSource as FrameworkElement;
            var dataContext = source?.DataContext as EventCardBaseViewModel;
            if(dataContext?.GetType() == typeof(DetectionEventCardViewModel)) 
            {
                var report = IoC.Get<DetectionReportDialogViewModel>();
                var user = IoC.Get<IAccountModel>();
                report.UpdateData(dataContext, user);
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel() { EventType = "DETECTION" });
            }
            else if(dataContext?.GetType() == typeof(MalfunctionEventCardViewModel))
            {
                var report = IoC.Get<MalfunctionReportDialogViewModel>();
                var user = IoC.Get<IAccountModel>();
                report.UpdateData(dataContext, user);
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel() { EventType = "MALFUNCTION" });
            }
            else
            {
                var report = IoC.Get<DetectionReportDialogViewModel>();
                var user = IoC.Get<IAccountModel>();
                report.UpdateData(dataContext, user);
                await _eventAggregator.PublishOnCurrentThreadAsync(new OpenEventReportDialogMessageModel() { EventType = "DETECTION" });
            }

        }

        public async Task ExecuteBatchReportAsync()
        {
            var cards = ViewModelProvider.ToList();
            foreach (var card in cards)
            {
                var eventModel = card.Model;
                var dto = new ActionEventCreateDto
                {
                    User = _userModel.Username,
                    Content = "일괄처리",
                    FromEventId = eventModel?.Id ?? 0
                };

                var response = await _apiService.CreateActionEventAsync(dto);
                if (!response.Success) break;

                ViewModelProvider.Remove(card);

                await _eventAggregator.PublishOnBackgroundThreadAsync(new SendActionRequestMessage
                {
                    EventId = eventModel?.Id ?? 0,
                    EventType = eventModel?.MessageType ?? EnumEventType.Intrusion,
                    ActionDetails = "일괄처리",
                    ActionUser = _userModel.Username,
                    ActionTime = DateTime.Now
                });
            }
        }

        public void OnButtonCameraPopup(object sender, RoutedEventArgs e)
        {
            //if (!SetupModel.IsServer)
            //    return;

            //lock (_locker)
            //{

            //    var source = e.OriginalSource as FrameworkElement;
            //    var dataContext = source.DataContext as EventCardViewModel;

            //    var domainService = IoC.Get<DomainService>();


            //    if (Cts.IsCancellationRequested)
            //        Cts = new CancellationTokenSource();
            //    else
            //    {
            //        Cts.Cancel();
            //        Cts = new CancellationTokenSource();
            //    }

            //    _ = domainService.CameraPopup(dataContext.IdController, dataContext.IdSensor, Cts.Token);
            //}
        }

        public async Task HandleAsync(DetectionReportedMessageModel message, CancellationToken cancellationToken)
        {
            try
            {
                if (message != null && message.ViewModel != null)
                {
                    var vm = message.ViewModel;
                    var model = vm.Model;
                    if (model == null) throw new NullReferenceException("DetectionEventModel을 찾을 수 없습니다.");

                    if (model.Device != null)
                    {
                        // Phase 14: 복합 키 - deviceType 추가
                        _symbolEventManager.ProcessEventReport(model.Device.Id, model.Device.DeviceType, model.Device.DeviceGroups);
                    }

                    DispatcherService.Invoke(() =>
                    {
                        ViewModelProvider.Remove(vm);
                        vm.Dispose();
                    });
                    // 서버가 Action 생성 시 action_reported=True를 자동으로 처리하므로 별도 Update 불필요
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
        }

        public async Task HandleAsync(MalfunctionReportedMessageModel message, CancellationToken cancellationToken)
        {
            try
            {
                if (message != null && message.ViewModel != null)
                {
                    var vm = message.ViewModel;
                    var model = vm.Model;
                    if (model == null) throw new NullReferenceException("MalfunctionEventModel을 찾을 수 없습니다.");

                    if (model.Device != null)
                    {
                        // Phase 14: 복합 키 - deviceType 추가
                        _symbolEventManager.ProcessEventReport(model.Device.Id, model.Device.DeviceType, model.Device.DeviceGroups);
                    }

                    DispatcherService.Invoke(() =>
                    {
                        ViewModelProvider.Remove(vm);
                        vm.Dispose();
                    });
                    // 서버가 Action 생성 시 action_reported=True를 자동으로 처리하므로 별도 Update 불필요
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
        }

        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public EventCardBaseViewModel SelectedEventCardViewModel
        {
            get { return _selectedEventCardViewModel; }
            set { _selectedEventCardViewModel = value; NotifyOfPropertyChange(() => SelectedEventCardViewModel); }
        }
        public event Action? UpdateAction;

        public bool IsAnimationEnabled
        {
            get { return _isAnimationEnabled; }
            set { _isAnimationEnabled = value; NotifyOfPropertyChange(() => IsAnimationEnabled); }
        }
        #endregion
        #region - Attributes -
        private const int ANIMATION_THRESHOLD = 20;
        private const int BATCH_INTERVAL_MS = 150;
        private EventProviderService _providerService;
        private IAccountModel _userModel;
        private IEventApiService _apiService;
        private ISymbolEventManager _symbolEventManager;
        private IEventQueueManager _eventQueueManager;
        private EventCardBatchBuffer<EventCardBaseViewModel> _batchBuffer;
        private Timer? _batchTimer;
        private EventCardBaseViewModel _selectedEventCardViewModel;
        private bool _isAnimationEnabled = true;
        #endregion
    }
}