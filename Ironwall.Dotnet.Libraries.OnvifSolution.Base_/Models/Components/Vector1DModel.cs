using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/22/2023 5:30:43 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class Vector1DModel : IVector1DModel
    {
        [JsonProperty("x", Order = 1)]
        public float X { get ; set ; }
        [JsonProperty("space", Order = 3)]
        public string Space { get ; set ; }
    }
}
