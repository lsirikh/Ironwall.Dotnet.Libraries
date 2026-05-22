using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
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
        public override async Task SendAction(string? content, string? idUser)
        {
            var account = IoC.Get<IAccountModel>();
            IdUser = account.Name;
            Contents = content ?? "자동 조치보고";

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
                return;
            }

            await _eventAggregator.PublishOnCurrentThreadAsync(new DetectionReportedMessageModel(this, Contents, IdUser));
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