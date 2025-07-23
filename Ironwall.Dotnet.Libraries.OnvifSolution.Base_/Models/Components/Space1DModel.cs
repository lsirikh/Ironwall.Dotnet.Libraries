using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/22/2023 5:22:48 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class Space1DModel : ISpace1DModel
    {
        public Space1DModel()
        {
            XRange = new LevelModel();
        }
        [JsonProperty("x_range", Order = 1)]
        public LevelModel XRange { get; set; }
        [JsonProperty("uri", Order = 3)]
        public string Uri { get ; set ; }
    }
}
