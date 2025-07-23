using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.PtzConfigs
{
    public interface IPTZNode
    {
        string Name { get; set; }
        string Token { get; set; }
        bool HomeSupported { get; set; }
        int MaximumNumberOfPresets { get; set; }
        List<string> AuxiliaryCommands { get; set; }
        string SupportedPTZSpaces { get; set; }
    }
}
