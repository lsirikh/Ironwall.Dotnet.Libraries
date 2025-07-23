using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/17/2025 1:23:07 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/*--------------------------- PTZ-Node Ext-2 ---------------------------*/
public sealed class PTZNodeExt2Dto
{
    [JsonProperty("any", Order = 1)]
    public IReadOnlyList<string>? Any { get; init; }
}

/*--------------------------- PTZ-Node Ext ---------------------------*/
public sealed class PTZNodeExtDto
{
    [JsonProperty("any", Order = 1)] public IReadOnlyList<string>? Any { get; init; }
    [JsonProperty("preset_tour", Order = 2)] public PtzPresetTourSupportedDto? SupportedPresetTour { get; init; }
    [JsonProperty("ext2", Order = 3)] public PTZNodeExt2Dto? Extension { get; init; }
}

/*--------------------------- PTZ-Node ---------------------------*/
public sealed class PTZNodeDto : DeviceEntityDto
{
    [JsonProperty("name", Order = 2)] public string? Name { get; init; }
    [JsonProperty("spaces", Order = 3)] public PtzSpacesDto? Spaces { get; init; }
    [JsonProperty("max_presets", Order = 4)] public int MaximumNumberOfPresets { get; init; }
    [JsonProperty("home_supported", Order = 5)] public bool HomeSupported { get; init; }
    [JsonProperty("aux_cmds", Order = 6)] public IReadOnlyList<string>? AuxiliaryCommands { get; init; }
    [JsonProperty("ext", Order = 7)] public PTZNodeExtDto? Extension { get; init; }
    [JsonProperty("fixed_home_pos", Order = 8)] public bool? FixedHomePosition { get; init; }
    [JsonProperty("geo_move", Order = 9)] public bool? GeoMove { get; init; }
}