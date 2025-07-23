using System.Collections.Generic;
using System.Xml;

namespace Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Components
{
    public interface IFilterTypeModel
    {
        List<XmlElement> Any { get; set; }
    }
}