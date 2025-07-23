using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/21/2023 5:07:23 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ResolutionModel : IResolutionModel
    {
        [JsonProperty("width", Order = 1)]
        public int Width { get ; set ; }
        [JsonProperty("height", Order = 2)]
        public int Height { get ; set ; }
    }
}
