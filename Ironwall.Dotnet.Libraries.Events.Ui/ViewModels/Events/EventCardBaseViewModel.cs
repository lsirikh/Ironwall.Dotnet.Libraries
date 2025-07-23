using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/3/2025 5:30:26 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public abstract class EventCardBaseViewModel: BasePanelViewModel, IDisposable
    {
        #region - Ctors -
        protected EventCardBaseViewModel()
        {
            InitializeProcess();
        }
        
        protected EventCardBaseViewModel(IEventAggregator ea, ILogService log) 
            : base(ea, log)
        {
            InitializeProcess();
        }
        
        ~EventCardBaseViewModel()
        {
            UninitializeProcess();
        }
        #endregion
        #region - Implementation of Interface -
        public void Dispose()
        {
            UninitializeProcess();
            GC.Collect();
        }
        #endregion
        #region - Overrides -
        public abstract Task TaskFinal();
        #endregion
        #region - Binding Methods -
        private void InitializeProcess()
        {
            _setupModel = IoC.Get<EventSetupModel>();
            var interval = _setupModel.TimeDiscardSec;
            _timer = new System.Timers.Timer(TimeSpan.FromSeconds(interval));
            _timer!.Elapsed += Timer_tick;
            if (_setupModel.IsAutoEventDiscard)
                _timer.Start();
        }

        private void UninitializeProcess()
        {
            _timer!.Elapsed -= Timer_tick;
            _timer?.Stop();
            _timer?.Dispose();
        }

        private async void Timer_tick(object? sender, System.Timers.ElapsedEventArgs e) => await TaskFinal(); 
        //{
        //    _log?.Info($"자동 조치보고 만료시간!!");
        //    await TaskFinal(); 
        //}
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public abstract IExEventModel Model { get; }
        public int Id { get; set; }
        public DateTime DateTime => Model.DateTime;
        public int TimeDiscardSec { get; set; }
        #endregion
        #region - Attributes -
        public CancellationTokenSource? Cts;
        private EventSetupModel? _setupModel;
        private System.Timers.Timer? _timer;
        public event EventHandler? EventHandlerTimer;
        #endregion
    }
}