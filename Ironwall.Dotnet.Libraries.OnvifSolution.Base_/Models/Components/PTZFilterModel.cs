using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 1/3/2024 11:08:19 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class PTZFilterModel : IPTZFilterModel
    {
        [JsonProperty("status", Order = 1)]
        public bool Status { get; set; }
        [JsonProperty("position", Order = 2)]
        public bool Position { get; set; }

    }
}
