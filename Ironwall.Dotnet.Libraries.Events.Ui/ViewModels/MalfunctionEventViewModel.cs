using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:57:33 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MalfunctionEventViewModel : ExEventViewModel, IMalfunctionEventViewModel
{
    #region - Ctors -
    public MalfunctionEventViewModel(IExEventModel model) : base(model)
    {
    }

    public MalfunctionEventViewModel(IExEventModel model, IEventAggregator ea, ILogService log) : base(model, ea, log)
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
    public EnumFaultType Reason
    {
        get { return (_model as IMalfunctionEventModel)!.Reason; }
        set { SetModelProperty(value, (_model as IMalfunctionEventModel)!.Reason, v => (_model as IMalfunctionEventModel)!.Reason = v); }
    }

    public int FirstStart
    {
        get { return (_model as IMalfunctionEventModel)!.FirstStart; }
        set { SetModelProperty(value, (_model as IMalfunctionEventModel)!.FirstStart, v => (_model as IMalfunctionEventModel)!.FirstStart = v); }
    }

    public int FirstEnd
    {
        get { return (_model as IMalfunctionEventModel)!.FirstEnd; }
        set { SetModelProperty(value, (_model as IMalfunctionEventModel)!.FirstEnd, v => (_model as IMalfunctionEventModel)!.FirstEnd = v); }
    }

    public int SecondStart
    {
        get { return (_model as IMalfunctionEventModel)!.SecondStart; }
        set { SetModelProperty(value, (_model as IMalfunctionEventModel)!.SecondStart, v => (_model as IMalfunctionEventModel)!.SecondStart = v); }
    }

    public int SecondEnd
    {
        get { return (_model as IMalfunctionEventModel)!.SecondEnd; }
        set { SetModelProperty(value, (_model as IMalfunctionEventModel)!.SecondEnd, v => (_model as IMalfunctionEventModel)!.SecondEnd = v); }
    }
    #endregion
    #region - Attributes -
    #endregion

}