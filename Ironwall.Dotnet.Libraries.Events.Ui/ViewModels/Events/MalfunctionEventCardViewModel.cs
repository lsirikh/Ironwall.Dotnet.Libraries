using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/2/2025 10:42:04 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class MalfunctionEventCardViewModel : EventCardViewModel<IMalfunctionEventModel>
    {
        
        #region - Ctors -
        public MalfunctionEventCardViewModel(IMalfunctionEventModel model)
            : base(model)
        {
        }

        public MalfunctionEventCardViewModel(IEventAggregator ea, ILogService log, IMalfunctionEventModel model)
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
            IdUser = $"{account.Username}({account.EmployeeNumber})";
            Contents = content ?? "자동 조치보고";
            await _eventAggregator.PublishOnCurrentThreadAsync(new MalfunctionReportedMessageModel(this, Contents, IdUser));
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
        public EnumFaultType Reason => _model.Reason;
        public int FirstEnd => _model.FirstEnd;
        public int FirstStart => _model.FirstStart;
        public int SecondEnd => _model.SecondEnd;
        public int SecondStart => _model.SecondStart;
        #endregion
        #region - Attributes -
        #endregion
    }
}