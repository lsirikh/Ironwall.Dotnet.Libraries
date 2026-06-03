using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Threading;

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
        }

        protected EventCardBaseViewModel(IEventAggregator ea, ILogService log)
            : base(ea, log)
        {
        }
        #endregion
        #region - Implementation of Interface -
        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _eventAggregator?.Unsubscribe(this);
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
                _cancellationTokenSource.Cancel();
            _cancellationTokenSource?.Dispose();
            Cts?.Cancel();
            Cts?.Dispose();
            Cts = null;
            GC.SuppressFinalize(this);
        }
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
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
        /// <summary>EventQueueManager의 entryId — Dequeue 호출 시 사용</summary>
        public string? EntryId { get; set; }
        /// <summary>레거시 호환용 — Path A 타이머 제거 후 더 이상 사용되지 않음</summary>
        public CancellationTokenSource? Cts { get; set; }
        public bool IsFlipped
        {
            get => _isFlipped;
            set { _isFlipped = value; NotifyOfPropertyChange(() => IsFlipped); }
        }
        #endregion
        #region - Attributes -
        private bool _disposed;
        private bool _isFlipped;
        /// <summary>HandleAutoReport 재진입 방지 — 0=idle, 1=in-flight (Interlocked 전용)</summary>
        public int _actionInProgress;
        #endregion
    }
}
