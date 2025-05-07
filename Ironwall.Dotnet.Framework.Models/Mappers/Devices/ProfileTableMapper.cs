using Ironwall.Dotnet.Framework.Models.Devices;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/28/2023 5:58:20 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ProfileTableMapper : OptionMapperBase, IProfileTableMapper
    {

        #region - Ctors -
        public ProfileTableMapper()
        {

        }

        public ProfileTableMapper(ICameraProfileModel model) : base(model)
        {
            Profile = model.Profile;

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
        [JsonProperty("profile", Order = 3)]
        public string Profile { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
