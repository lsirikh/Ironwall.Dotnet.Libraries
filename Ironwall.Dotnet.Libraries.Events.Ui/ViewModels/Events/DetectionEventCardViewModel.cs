using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/2/2025 10:41:49 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class DetectionEventCardViewModel: EventCardViewModel<IDetectionEventModel>
    {
        #region - Ctors -
        public DetectionEventCardViewModel(IDetectionEventModel model)
            : base(model)
        {
            InitializeSignalBar();
        }

        public DetectionEventCardViewModel(IEventAggregator ea, ILogService log, IDetectionEventModel model)
            : base(ea, log, model)
        {
            InitializeSignalBar();
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        public override async Task<bool> SendAction(string? content, string? idUser)
        {
            var account = IoC.Get<IAccountModel>();
            IdUser = account.Name;
            Contents = content ?? "자동 조치보고";

            // (Phase3) 조치보고 멱등 — 동일 EventId가 자동/자동복구/배치 경로에서 진행 중이면 수동 보고 스킵(서버/NATS 중복 차단).
            var guard = IoC.Get<IActionReportGuard>();
            if (!guard.TryEnter(Model.Id))
            {
                _log?.Info($"[ACTION_REPORT] Detection Event({Model.Id}) 조치보고 진행 중 — 수동 중복 스킵");
                return true;   // 다른 경로가 보고 중 → 이벤트는 보고됨(다이얼로그 닫기 허용)
            }
            try
            {
                var apiService = IoC.Get<IEventApiService>();
                var dto = new ActionEventCreateDto
                {
                    User = IdUser ?? string.Empty,
                    Content = Contents,
                    FromEventId = Model.Id
                };
                var response = await apiService.CreateActionEventAsync(dto);
                if (!response.Success)
                {
                    _log?.Error($"[ACTION_REPORT] Detection INSERT 실패: {response.Message}");
                    return false;   // (EA3) 실패 신호 → 다이얼로그 유지
                }

                await _eventAggregator.PublishOnCurrentThreadAsync(new DetectionReportedMessageModel(this, Contents, IdUser));
                await _eventAggregator.PublishOnBackgroundThreadAsync(new SendActionRequestMessage
                {
                    EventId = Model.Id,
                    EventType = EnumEventType.Intrusion,
                    ActionDetails = Contents,
                    ActionUser = IdUser,
                    ActionTime = DateTime.Now,
                    OriginEvent = Model,                 // NATS from_event(device) 원천
                    ActionId = response.Data?.Id ?? 0    // 생성된 Action DB ID
                });

                return await base.SendAction(content, idUser);
            }
            finally { guard.Exit(Model.Id); }
        }

        protected override Task CloseDialog()
        {
            Dispose();
            return Task.CompletedTask;
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public EnumDetectionType Result => (Model as IDetectionEventModel)!.Result;
        #endregion
        #region - FR-06 신호 표시 -
        private const double SIGNAL_BAR_TOTAL_WIDTH = 80d;

        // 세션 관측 최대 — 카드 미니바 상대 기준. 카드 생성은 UI 스레드 경유(DispatcherService.Invoke)라 단순 필드로 충분.
        // (code-review P1-4) 주의: 프로세스 생애 단조 증가라 이력 다이얼로그(조회 목록 max 기준)와 바 기준이 다르다 —
        // 실시간 카드는 이력이 없는 시점의 보조 시각화라 의도된 트레이드오프(PRD B3). 숫자 값이 정보의 정본.
        private static int _sessionMaxSignal;

        /// <summary>탐지 신호 크기(detail.signal). null=미제공, 0=AI_DETECT.</summary>
        public int? Signal => (Model as IDetectionEventModel)?.Signal;

        /// <summary>신호 줄 표시 여부 — null/0이면 앞면 신호 줄 Collapsed.</summary>
        public bool HasSignal => Signal is > 0;

        /// <summary>표시 문자열 — 천 단위 구분, null/0은 "—"(뒷면 상세용).</summary>
        public string SignalText => Signal is > 0 ? Signal!.Value.ToString("N0") : "—";

        /// <summary>바 폭(px) — 세션 관측 최대 기준 상대(첫 관측=100%), 카드 생성 시점 고정.</summary>
        public double SignalBarWidth { get; private set; }

        private void InitializeSignalBar()
        {
            if (Signal is not int s || s <= 0) { SignalBarWidth = 0d; return; }
            if (s > _sessionMaxSignal) _sessionMaxSignal = s;
            SignalBarWidth = (double)s / _sessionMaxSignal * SIGNAL_BAR_TOTAL_WIDTH;
        }
        #endregion
        #region - Attributes -
        #endregion
    }
}