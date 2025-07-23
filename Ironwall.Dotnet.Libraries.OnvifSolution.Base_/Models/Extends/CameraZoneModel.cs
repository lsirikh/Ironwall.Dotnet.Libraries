using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Defines;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Extends
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 2/23/2024 10:09:40 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class CameraZoneModel : BaseModel, ICameraZoneModel
    {

        #region - Ctors -
        public CameraZoneModel()
        {
        }
        public CameraZoneModel(string name, string description)
        {
            Name = name;
            Description = description;
        }
        public CameraZoneModel(int id, string name, string description) : base(id)
        {
            Name = name;
            Description = description;
        }
        public CameraZoneModel(ICameraZoneModel model) : base(model.Id)
        {
            Name = model.Name;
            Description = model.Description;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        /// <summary>
        /// Zone Name
        /// </summary>
        [JsonProperty("zone_name", Order = 2)]
        public string Name { get; set; }
        /// <summary>
        /// Zone Description
        /// </summary>
        [JsonProperty("zone_description", Order = 3)]
        public string Description { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
