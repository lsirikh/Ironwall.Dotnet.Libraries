using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Modules;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/1/2025 8:13:10 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public abstract class EventCardTimerViewModel<T> : BaseViewModel<T> where T : IBaseEventModel
    {
        #region - Ctors -
        public EventCardTimerViewModel(T model) : base(model)
        {
        }

        public EventCardTimerViewModel(IEventAggregator eventAggregator, ILogService log, T model) 
                                : base(eventAggregator, log, model)
        {
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        public abstract Task TaskFinal(string content, string user);
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public string? Tag { get; set; }
        public string? TagFault { get; set; }
        public int TimeDiscardSec { get; set; }
        #endregion
        #region - Attributes -
        public CancellationTokenSource? _eventCts;
        public event EventHandler? EventHandlerTimer;
        #endregion

    }
}