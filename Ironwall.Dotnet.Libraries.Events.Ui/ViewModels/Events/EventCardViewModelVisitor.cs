using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/2/2025 1:51:35 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class EventCardViewModelVisitor : EventCardVisitor
    {
        #region - Ctors -
        #endregion
        #region - Implementation of Interface -

        #endregion
        #region - Overrides -
        public override void Visit(DetectionEventCardViewModel viewModel)
        {
        }

        public override void Visit(MalfunctionEventCardViewModel viewModel)
        {
        }
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