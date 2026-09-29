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
        /// <summary>
        /// 카드 자동화 식별자 — <c>Events.Card.{Detection|Malfunction}.{eventId}</c>(WP-1 ⑫). 종류를 넣는 까닭: 탐지 · 장애 번호가 겹친다.
        /// 카드 뷰 뿌리가 바인딩으로 단다(x:Name 은 Caliburn 지시자라 쓰지 않는다).
        /// </summary>
        public string AutomationKey
            => $"Events.Card.{Ironwall.Dotnet.Libraries.Events.Ui.Helpers.EventCardKind.AutomationSegment(Ironwall.Dotnet.Libraries.Events.Ui.Helpers.EventCardKind.Of(this))}.{Model?.Id ?? 0}";
        /// <summary>카드의 조치보고 단추 자동화 식별자 — <c>{AutomationKey}.Report</c>.</summary>
        public string ReportAutomationKey => AutomationKey + ".Report";
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
