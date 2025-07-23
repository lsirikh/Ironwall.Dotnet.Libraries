using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 1:19:17 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*--------------------------- DeviceEntity ---------------------------*/
public class DeviceEntityDto : IDeviceEntityDto
{
    [JsonProperty("token", Order = 1)]
    public string Token { get; init; } = default!;
}