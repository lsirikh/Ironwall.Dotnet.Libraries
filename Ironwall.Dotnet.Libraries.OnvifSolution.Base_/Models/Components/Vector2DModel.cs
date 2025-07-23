using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/22/2023 5:31:55 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class Vector2DModel : Vector1DModel, IVector2DModel
    {
        [JsonProperty("y", Order = 2)]
        public float Y { get ; set ; }
    }
}
