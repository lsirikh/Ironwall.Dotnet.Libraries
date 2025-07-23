using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 12/22/2023 5:13:35 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class Space2DModel : Space1DModel, ISpace2DModel
    {
        public Space2DModel() : base()
        {
            YRange = new LevelModel();   
            
        }

        [JsonProperty("y_range", Order = 2)]
        public LevelModel YRange { get; set; }

    }
}
