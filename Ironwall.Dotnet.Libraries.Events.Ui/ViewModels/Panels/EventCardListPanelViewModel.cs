using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Db.Services;
using Ironwall.Dotnet.Libraries.Events.Modules;
using Ironwall.Dotnet.Libraries.Events.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Specialized;
using System.Reflection.Metadata;
using System.Windows;
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
                                            , IHandle<CallAllEventReportMessageModel>
    {
        #region - Ctors -
        public EventCardListPanelViewModel(IEventAggregator ea
                                          , ILogService log
                                          , IEventDbService dbService
                                          , IAccountModel userModel)
                                        : base(ea, log)
        {
            _dbService = dbService;
            _userModel = userModel;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            ViewModelProvider.CollectionChanged += CollectionEntity_CollectionChanged;
            return base.OnActivateAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            ViewModelProvider.CollectionChanged -= CollectionEntity_CollectionChanged;
            return base.OnDeactivateAsync(close, cancellationToken);
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        private void CollectionEntity_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateAction?.Invoke();
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
                    var eventModel = vm.Model;
                    if (eventModel == null) throw new NullReferenceException("DetectionEventModel을 찾을 수 없습니다.");
                    
                                        
                    var action = new ActionEventModel()
                    {
                        Content = message.Content,
                        User = message.User,
                        OriginEvent = eventModel,
                    };

                    DispatcherService.Invoke(() => 
                    {
                        ViewModelProvider.Remove(vm);
                        vm.Dispose();
                    });

                    await _dbService.InsertActionEventAsync(action, cancellationToken);
                    eventModel.Status = Enums.EnumTrueFalse.True;
                    await _dbService.UpdateDetectionEventAsync((IDetectionEventModel)eventModel!, cancellationToken);
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
                    var eventModel = vm.Model;
                    if (eventModel == null) throw new NullReferenceException("MalfunctionEventModel을 찾을 수 없습니다.");


                    var action = new ActionEventModel()
                    {
                        Content = message.Content,
                        User = message.User,
                        OriginEvent = eventModel,
                    };

                    DispatcherService.Invoke(() =>
                    {
                        ViewModelProvider.Remove(vm);
                        vm.Dispose();
                    });

                    await _dbService.InsertActionEventAsync(action, cancellationToken);
                    eventModel.Status = Enums.EnumTrueFalse.True;
                    await _dbService.UpdateMalfunctionEventAsync((IMalfunctionEventModel)eventModel!, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
        }

        public async Task HandleAsync(CallAllEventReportMessageModel message, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var item in ViewModelProvider.ToList())
                {
                    ActionEventModel action;

                    if (item is DetectionEventCardViewModel dEventCardViewModel)
                    {
                        var vm = dEventCardViewModel.Model;
                        action = new ActionEventModel()
                        {
                            Content = "자동 조치보고",
                            User = $"{_userModel.Username}({_userModel.EmployeeNumber})",
                            OriginEvent = vm,
                        };
                        vm.Status = Enums.EnumTrueFalse.True;
                        await _dbService.UpdateDetectionEventAsync((IDetectionEventModel)vm!, cancellationToken);
                    }
                    else if(item is MalfunctionEventCardViewModel mEventCardViewModel)
                    {
                        var vm = mEventCardViewModel.Model;
                        action = new ActionEventModel()
                        {
                            Content = "자동 조치보고",
                            User = $"{_userModel.Username}({_userModel.EmployeeNumber})",
                            OriginEvent = vm,
                        };
                        vm.Status = Enums.EnumTrueFalse.True;
                        await _dbService.UpdateMalfunctionEventAsync((IMalfunctionEventModel)vm!, cancellationToken);
                    }
                    else
                    {
                        continue;
                    }

                    DispatcherService.Invoke(() =>
                    {
                        ViewModelProvider.Remove(item);
                        item.Dispose();
                    });

                    await _dbService.InsertActionEventAsync(action, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex.Message);
            }
            finally
            {
                await _eventAggregator.PublishOnCurrentThreadAsync(new ClosePopupMessageModel());
            }
        }

        //private async void SendAction(EventCardViewModel viewModel)
        //{
        //    BrkAction brkAction = new BrkAction
        //    {
        //        IdCommand = (int)EnumEventType.Action,
        //        IdGroup = viewModel.IdGroup,
        //        IdController = viewModel.IdController,
        //        IdSensor = viewModel.IdSensor,
        //        TypeDevice = (int)viewModel.TypeDevice,
        //        TypeMessage = (int)EnumEventType.Action,
        //        Content = EnumLanguageHelper.GetAutoActionType(SetupModel.Language),
        //    };
        //    var json = JsonConvert.SerializeObject(brkAction);
        //    json = $"[{json}]";
        //    if (SetupModel.IsServer)
        //    {
        //        var channel = RedisChannel.Literal(SetupModel.NameChannel2);
        //        await Subscriber.PublishAsync(channel, json);
        //    }

        //}
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
        #endregion
        #region - Attributes -
        private IEventDbService _dbService;
        private IAccountModel _userModel;
        private EventCardBaseViewModel _selectedEventCardViewModel;
        #endregion
    }
}