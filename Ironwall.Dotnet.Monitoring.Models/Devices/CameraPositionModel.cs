using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/12/2025 4:32:53 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CameraPositionModel : BaseModel, ICameraPositionModel
{
    [JsonProperty("latitude", Order = 2)]
    public double Latitude { get; set; }

    [JsonProperty("longitude", Order = 3)]
    public double Longitude { get; set; }

    [JsonProperty("altitude", Order = 4)]
    public double Altitude { get; set; }

    [JsonProperty("heading", Order = 5)]
    public float Heading { get; set; }  // 방향 (0 ~ 360)

}