using Ironwall.Dotnet.Framework.Models.Maps;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Symbols
{
    public interface IMapFileSaveRequestModel : IUserSessionBaseRequestModel
    {
        List<MapModel>? Maps { get; set; }
    }
}