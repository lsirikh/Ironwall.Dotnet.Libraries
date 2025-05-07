using Ironwall.Dotnet.Framework.Models.Mappers;
using Newtonsoft.Json;

using System.Xml.Linq;

namespace Ironwall.Dotnet.Framework.Models.Devices;

/****************************************************************************
    Purpose      :                                                           
    Created By   : GHLee                                                
    Created On   : 6/28/2023 2:46:18 PM                                                    
    Department   : SW Team                                                   
    Company      : Sensorway Co., Ltd.                                       
    Email        : lsirikh@naver.com                                         
 ****************************************************************************/

public class CameraMappingModel : BaseModel, ICameraMappingModel
{

    #region - Ctors -
    public CameraMappingModel()
    {
        Sensor = null;
        FirstPreset = null;
        SecondPreset = null;
    }

    public CameraMappingModel(string group, SensorDeviceModel? sensor, CameraPresetModel? preset1, CameraPresetModel? preset2)
    {
        MappingGroup = group;
        Sensor = sensor;
        FirstPreset = preset1;
        SecondPreset = preset2;
    }

    public CameraMappingModel(int id, string group, SensorDeviceModel? sensor, CameraPresetModel? preset1, CameraPresetModel? preset2) : base(id)
    {
        MappingGroup = group;
        Sensor = sensor;
        FirstPreset = preset1;
        SecondPreset = preset2;
    }

    public CameraMappingModel(IMappingTableMapper model, SensorDeviceModel? sensor, CameraPresetModel? preset1, CameraPresetModel? preset2) : base(model.Id)
    {
        MappingGroup = model.MappingGroup;
        Sensor = sensor;
        FirstPreset = preset1;
        SecondPreset = preset2;
    }

    public CameraMappingModel(ICameraMappingModel model)
    {
        MappingGroup = model.MappingGroup;
        Sensor = model.Sensor;
        FirstPreset = model.FirstPreset;
        SecondPreset = model.SecondPreset;
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
    [JsonProperty("mapping_group", Order = 2)]
    public string MappingGroup { get; set; } = default!;
    [JsonProperty("sensor", Order = 3)]
    public SensorDeviceModel? Sensor { get; set; } = null;
    [JsonProperty("first_preset", Order = 4)]
    public CameraPresetModel? FirstPreset { get; set; } = null;
    [JsonProperty("second_preset", Order = 5)]
    public CameraPresetModel? SecondPreset { get; set; } = null;
    #endregion
    #region - Attributes -
    #endregion
}
