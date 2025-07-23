using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.PTZPresets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models
{
    public interface ICameraMediaModel
    {
        string Token { get; set; }
        List<CameraProfile> Profiles { get; set; }
        List<PTZPresetModel> PTZPresets { get; set; }
    }
}
