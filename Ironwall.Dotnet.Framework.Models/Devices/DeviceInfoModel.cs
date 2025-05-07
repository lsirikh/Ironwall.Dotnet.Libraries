using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Framework.Models.Devices;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 5/16/2023 8:59:12 AM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public class DeviceInfoModel : IDeviceInfoModel
{
    #region - Ctors -
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public void Update(IDeviceDetailModel model)
    {
        Controller = model.Controller;
        Sensor = model.Sensor;
        Camera = model.Camera;
        UpdateTime = model.UpdateTime;
    }

    public void Clear()
    {
        Controller = 0;
        Sensor = 0;
        Camera = 0;
        UpdateTime = DateTime.MinValue;
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -

    [JsonProperty("controllers", Order = 1)]
    public int Controller { get; set; }
    [JsonProperty("sensors", Order = 2)]
    public int Sensor { get; set; }
    [JsonProperty("cameras", Order = 3)]
    public int Camera { get; set; }
    [JsonProperty("updatetime", Order = 3)]
    public DateTime UpdateTime { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}
