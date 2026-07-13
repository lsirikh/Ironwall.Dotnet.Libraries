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
        }

        public DetectionEventCardViewModel(IEventAggregator ea, ILogService log, IDetectionEventModel model) 
            : base(ea, log, model)
        {
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
        #region - Attributes -
        #endregion
    }
}