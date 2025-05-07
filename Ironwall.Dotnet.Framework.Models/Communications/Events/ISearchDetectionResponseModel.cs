using Ironwall.Dotnet.Framework.Models.Events;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface ISearchDetectionResponseModel : IResponseModel
    {
        List<DetectionEventModel>? Body { get; set; }
    }
}