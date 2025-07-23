using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioEncoderConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.MetadataConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.ProfileExtensions;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoAnalyticConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoEncoderConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles;
public interface ICameraProfileDto
{
    string? Name { get; set; }
    string? Token { get; set; }
    bool Fixed { get; set; }
    VideoSourceConfigDto? VideoSourceConfig { get; set; }
    VideoEncoderConfigDto? VideoEncoderConfig { get; set; }
    AudioEncoderConfigDto? AudioEncoderConfig { get; set; }
    AudioSourceConfigDto? AudioSourceConfig { get; set; }
    VideoAnalyticsConfigDto? VideoAnalyticsConfig { get; set; }
    PTZConfigDto? PTZConfig { get; set; }
    MetadataConfigDto? MetadataConfig { get; set; }
    ProfileExtensionDto? ProfileExtension { get; set; }
    MediaUriDto? MediaUri { get; set; }
}