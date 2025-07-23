using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.AudioSourceConfigs.AudioSource
{
    internal interface IAudioSourceModel
    {
        string Token { get; set; }
        int Channels { get; set; }
    }
}
