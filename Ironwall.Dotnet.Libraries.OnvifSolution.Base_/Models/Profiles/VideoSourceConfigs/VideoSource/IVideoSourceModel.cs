using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource
{
    public interface IVideoSourceModel
    {
        string Token { get; set; }
        float FrameRate { get; set; }
        ResolutionModel Resolution { get; set; }
        ImagingOptionModel ImagingOption { get; set; }
    }
}
