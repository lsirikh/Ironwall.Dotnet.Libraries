using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/20/2025 10:15:51 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*---------------------------  Media-URI DTO  ---------------------------*/
public sealed class MediaUriDto
{
    [JsonProperty("uri", Order = 1)]
    public string Uri { get; init; } = default!;

    [JsonProperty("invalid_after_connect", Order = 2)]
    public bool InvalidAfterConnect { get; init; }

    [JsonProperty("invalid_after_reboot", Order = 3)]
    public bool InvalidAfterReboot { get; init; }

    [JsonProperty("timeout", Order = 4)]
    public string? Timeout { get; init; }
}