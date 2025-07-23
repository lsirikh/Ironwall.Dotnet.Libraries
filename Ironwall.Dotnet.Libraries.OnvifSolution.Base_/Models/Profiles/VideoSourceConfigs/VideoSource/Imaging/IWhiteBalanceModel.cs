using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoSourceConfigs.VideoSource.Imaging
{
    public interface IWhiteBalanceModel
    {
        EnumWhiteBalanceMode Mode { get; set; }
        float YrGain { get; set; }
        float YbGain { get; set; }
    }
}
