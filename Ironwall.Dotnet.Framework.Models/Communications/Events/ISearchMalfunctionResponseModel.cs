using Ironwall.Dotnet.Framework.Models.Events;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface ISearchMalfunctionResponseModel : IResponseModel
    {
        List<MalfunctionEventModel>? Body { get; set; }
    }
}