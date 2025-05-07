using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Devices;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 6/12/2023 9:29:47 AM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public class CameraProfileModel : BaseOptionModel, ICameraProfileModel
{
    #region - Ctors -
    public CameraProfileModel()
    {
    }

    public CameraProfileModel(int id, int cameraId, string profile) : base(id, cameraId)
    {
        Profile = profile;
    }

    public CameraProfileModel(ICameraProfileModel model) : base(model)
    {
        Profile = model.Profile;
    }

    public CameraProfileModel(IProfileTableMapper model) : base(model.Id, model.ReferenceId)
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
    public string Profile { get; set; } = default!;
    #endregion
    #region - Attributes -
    #endregion
}
