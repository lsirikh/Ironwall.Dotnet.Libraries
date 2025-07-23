using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/21/2023 10:38:47 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MediaUriModel : IMediaUriModel
    {
        [JsonProperty("uri", Order = 1)]
        public string Uri { get; set; }

        [JsonProperty("invalid_after_connect", Order = 2)]
        public bool InvalidAfterConnect { get; set; }

        [JsonProperty("invalid_after_reboot", Order = 3)]
        public bool InvalidAfterReboot { get; set; }

        [JsonProperty("timeout", Order = 4)]
        public string Timeout { get; set; }
    }
}
