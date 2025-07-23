using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 1:45:20 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DetectionEventModel : ExEventModel, IDetectionEventModel
{
    #region - Ctors -
    public DetectionEventModel()
    {
    }

    public DetectionEventModel(IExEventModel model): base(model)
    {
        
    }

    public DetectionEventModel(IDetectionEventModel model) : base(model)
    {
        Result = model.Result;
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
    [JsonProperty("result", Order = 6)]
    public EnumDetectionType Result { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}