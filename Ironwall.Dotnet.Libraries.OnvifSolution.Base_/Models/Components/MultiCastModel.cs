using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/19/2023 3:36:18 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MultiCastModel : IMultiCastModel
    {
        [JsonProperty("ip_address", Order = 1)]
        public string IpAddress { get; set; }

        [JsonProperty("port", Order = 2)]
        public int Port { get; set; }

        [JsonProperty("ttl", Order = 3)]
        public int Ttl { get; set; }

        [JsonProperty("auto_start", Order = 4)]
        public bool AutoStart { get; set; }
    }
}
