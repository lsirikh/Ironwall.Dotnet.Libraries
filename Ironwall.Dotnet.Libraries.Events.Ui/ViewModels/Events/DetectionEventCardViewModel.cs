using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
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
        public override async Task SendAction(string? content, string? idUser)
        {
            var account = IoC.Get<IAccountModel>();
            IdUser = account.Name;
            Contents = content ?? "자동 조치보고";

            // UI 업데이트 (카드 제거, 상태 변경) - EventCardListPanelViewModel이 구독 중일 때 처리
            await _eventAggregator.PublishOnCurrentThreadAsync(new DetectionReportedMessageModel(this, Contents, IdUser));

            // NatsDomainService에 직접 발행 (EventCardListPanelViewModel 구독 여부 무관)
            await _eventAggregator.PublishOnBackgroundThreadAsync(new SendActionRequestMessage
            {
                EventId = Model.Id,
                EventType = EnumEventType.Intrusion,
                ActionDetails = Contents,
                ActionUser = IdUser,
                ActionTime = DateTime.Now
            });

            await base.SendAction(content, idUser);
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