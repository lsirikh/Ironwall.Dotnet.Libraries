using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/23/2025 5:03:24 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CameraPresetModel : BaseModel, ICameraPresetModel
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
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    [JsonProperty("preset", Order = 2)]
    public int Preset { get; set; }
    [JsonProperty("name", Order = 3)]
    public string? Name { get; set; }

    [JsonProperty("description", Order = 4)]
    public string? Description { get; set; }

    [JsonProperty("pitch", Order = 5)]
    public float Pitch { get; set; }    // Pitch

    [JsonProperty("tilt", Order = 6)]
    public float Tilt { get; set; }     // Tilt

    [JsonProperty("zoom", Order = 7)]
    public float Zoom { get; set; }     // Zoom

    [JsonProperty("delay", Order = 8)]
    public int Delay { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}