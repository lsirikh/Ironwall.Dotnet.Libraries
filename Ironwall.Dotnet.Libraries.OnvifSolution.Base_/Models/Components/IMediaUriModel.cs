using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    public interface IMediaUriModel
    {
        string Uri { get; set; }
        bool InvalidAfterConnect { get; set; }
        bool InvalidAfterReboot { get; set; }
        string Timeout { get; set; }
    }
}
