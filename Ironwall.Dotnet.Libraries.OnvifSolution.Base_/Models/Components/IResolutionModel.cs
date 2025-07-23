using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    public interface IResolutionModel
    {
        int Width { get; set; }
        int Height { get; set; }
    }
}
