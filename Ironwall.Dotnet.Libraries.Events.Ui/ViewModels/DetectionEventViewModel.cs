using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:57:04 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DetectionEventViewModel : ExEventViewModel, IDetectionEventViewModel
{
    #region - Ctors -
    public DetectionEventViewModel(IDetectionEventModel model) : base(model)
    {
    }

    public DetectionEventViewModel(IDetectionEventModel model, IEventAggregator ea, ILogService log)
        : base(model, ea, log)
    {
    }
    #endregion
    #region - Implementation of Interface -
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
    public EnumDetectionType Result
    {
        get { return (_model as IDetectionEventModel)!.Result; }
        set 
        {
            SetModelProperty(value, (_model as IDetectionEventModel)!.Result, v => (_model as IDetectionEventModel)!.Result = v);
        }
    }

    #endregion
    #region - Attributes -
    #endregion
}