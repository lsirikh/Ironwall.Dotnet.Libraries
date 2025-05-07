using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Devices;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 6/12/2023 9:43:51 AM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public class CameraPresetModel : BaseOptionModel, ICameraPresetModel
{
    #region - Ctors -
    public CameraPresetModel()
    {

    }

    public CameraPresetModel(int id) : this()
    {
        Id = id;
    }

    public CameraPresetModel(int id, int cameraId, string presetName, bool isHome, double pan, double tilt, int zoom, int delay) : base(id, cameraId)
    {
        PresetName = presetName;
        IsHome = isHome;
        Pan = pan;
        Tilt = tilt;
        Zoom = zoom;
        Delay = delay;
    }

    public CameraPresetModel(IPresetTableMapper presetTableMapper): base(presetTableMapper.Id, presetTableMapper.ReferenceId)
    {
        PresetName = presetTableMapper.PresetName;
        IsHome = presetTableMapper.IsHome;
        Pan = presetTableMapper.Pan;
        Tilt = presetTableMapper.Tilt;
        Zoom = presetTableMapper.Zoom;
        Delay = presetTableMapper.Delay;
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
    [JsonProperty("preset_name", Order = 3)]
    public string PresetName { get; set; } = default!;

    [JsonProperty("ishome", Order = 4)]
    public bool IsHome { get; set; }

    [JsonProperty("pan", Order = 5)]
    public double Pan { get; set; }

    [JsonProperty("tilt", Order = 6)]
    public double Tilt { get; set; }

    [JsonProperty("zoom", Order = 7)]
    public double Zoom { get; set; }

    [JsonProperty("delay", Order = 8)]
    public int Delay { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}
