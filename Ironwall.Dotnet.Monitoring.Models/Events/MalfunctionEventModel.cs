using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 1:45:36 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class MalfunctionEventModel : ExEventModel, IMalfunctionEventModel
{
    #region - Ctors -
    public MalfunctionEventModel()
    {
        
    }

    public MalfunctionEventModel(IExEventModel model) : base(model)
    {
        
    }

    public MalfunctionEventModel(IMalfunctionEventModel model) : base(model)
    {
        Reason = model.Reason;
        FirstStart = model.FirstStart;
        FirstEnd = model.FirstEnd;
        SecondStart = model.SecondStart;
        SecondEnd = model.SecondEnd;
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
    public EnumFaultType Reason { get; set; }
    public int FirstStart { get; set; }
    public int FirstEnd { get; set; }
    public int SecondStart { get; set; }
    public int SecondEnd { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}