using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/2/2025 10:28:37 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class ConnectionEventViewModel : EventCardViewModel<IConnectionEventModel>
    {
        #region - Ctors -
        public ConnectionEventViewModel(IConnectionEventModel model)
                                    : base(model)
        {
            
        }

        public ConnectionEventViewModel(IEventAggregator ea, ILogService log, IConnectionEventModel model)
                                    : base(ea, log, model)
        {
            
        }


        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        public override Task SendAction(string content, string idUser)
        {

            return base.SendAction(content, idUser);
        }
        protected override Task CloseDialog() => Task.CompletedTask;
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        #endregion
    }
}