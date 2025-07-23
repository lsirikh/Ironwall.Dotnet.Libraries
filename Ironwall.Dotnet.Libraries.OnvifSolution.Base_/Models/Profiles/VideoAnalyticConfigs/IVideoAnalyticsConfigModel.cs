using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles.VideoAnalyticConfigs
{
    public interface IVideoAnalyticsConfigModel
    {
        string Name { get; set; }
        string Token { get; set; }
        int UseCount { get; set; }
        List<string> Analytics { get; set; }
    }
}
