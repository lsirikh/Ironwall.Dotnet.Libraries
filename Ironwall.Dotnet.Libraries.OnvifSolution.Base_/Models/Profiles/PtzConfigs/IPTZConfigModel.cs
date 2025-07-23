using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs
{
    public interface IPTZConfigModel
    {
        string Name { get; set; }
        string Token { get; set; }
        int UseCount { get; set; }
        Space2DModel PanTiltRange { get; set; }
        Space1DModel ZoomRange { get; set; }
        PTZSpeedModel DefaultPTZSpeed { get; set; }
        string Timeout { get; set; }
        string DefaultAbsolutePantTiltPositionSpace { get; set; }
        string DefaultAbsoluteZoomPositionSpace { get; set; }
        string DefaultRelativePanTiltTranslationSpace { get; set; }
        string DefaultRelativeZoomTranslationSpace { get; set; }
        string DefaultContinuousPanTiltVelocitySpace { get; set; }
        string DefaultContinuousZoomVelocitySpace { get; set; }
        PTZNodeModel PTZNode { get; set; }
    }
}
