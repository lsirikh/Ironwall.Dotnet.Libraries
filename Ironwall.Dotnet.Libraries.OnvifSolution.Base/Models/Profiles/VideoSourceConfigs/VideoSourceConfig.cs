using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Commons;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/16/2025 2:53:58 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/

#region ── 5. 확장 블록 ─────────────────────────────────────────────
public sealed class VideoSourceConfigExt2Dto
{
    [JsonProperty("lenses", Order = 1)] public IReadOnlyList<LensDescriptionDto>? Lenses { get; init; }
    [JsonProperty("scene_orientation", Order = 2)] public SceneOrientationDto? SceneOrientation { get; init; }
    [JsonProperty("any", Order = 3)] public IReadOnlyList<string>? Any { get; init; }
}

public sealed class VideoSourceConfigExtDto
{
    [JsonProperty("rotate", Order = 1)] public RotateDto? Rotate { get; init; }
    [JsonProperty("extension", Order = 2)] public VideoSourceConfigExt2Dto? Extension2 { get; init; }
}
#endregion

#region ── 6. 최종 Video-Source-Config DTO ──────────────────────────
public sealed class VideoSourceConfigDto : ConfigEntityDto
{
    /*── VideoSourceConfiguration 고유 ──────────────────────────*/
    [JsonProperty("source_token", Order = 4)] public string SourceToken { get; init; } = default!;
    [JsonProperty("bounds", Order = 5)] public RectangleDto Bounds { get; init; } = default!;
    [JsonProperty("any", Order = 6)] public IReadOnlyList<string>? AnyElements { get; init; }
    [JsonProperty("view_mode", Order = 7)] public string? ViewMode { get; init; }

    /*── 확장(Extension) ────────────────────────────────────────*/
    [JsonProperty("extension", Order = 8)] public VideoSourceConfigExtDto? Extension { get; init; }
}
#endregion