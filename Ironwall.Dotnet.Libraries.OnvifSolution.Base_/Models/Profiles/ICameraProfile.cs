using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioEncoderConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.MetadataConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoAnalyticConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoEncoderConfigs;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles
{
    public interface ICameraProfile
    {
        string Name { get; set; }
        string Token { get; set; }
        bool Fixed { get; set; }
        VideoSourceConfigModel VideoSourceConfig { get; set; }
        VideoEncoderConfigModel VideoEncoderConfig { get; set; }
        AudioSourceConfigModel AudioSourceConfig { get; set; }
        AudioEncoderConfigModel AudioEncoderConfig { get; set; }
        PTZConfigModel PTZConfig { get; set; }
        VideoAnalyticsConfigModel VideoAnalyticsConfig { get; set; }
        MetadataConfigModel MetadataConfig { get; set; }
        MediaUriModel MediaUri { get; set; }

    }
}
