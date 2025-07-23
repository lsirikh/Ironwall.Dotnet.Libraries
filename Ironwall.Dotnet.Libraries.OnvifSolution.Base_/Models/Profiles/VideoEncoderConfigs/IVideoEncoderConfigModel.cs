using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoEncoderConfigs
{
    public interface IVideoEncoderConfigModel
    {
        string Name { get; set; }
        string Token { get; set; }
        int UseCount { get; set; }
        EnumVideoEncoding Encoding { get; set; }
        ResolutionModel Resolution { get; set; }
        string SessionTimeout { get; set; }
        float Quality { get; set; }
        int FrameRate { get; set; }
        int Bitrate { get; set; }
        int EncodingInterval { get; set; }
        MultiCastModel MultiCast { get; set; }
    }
}
