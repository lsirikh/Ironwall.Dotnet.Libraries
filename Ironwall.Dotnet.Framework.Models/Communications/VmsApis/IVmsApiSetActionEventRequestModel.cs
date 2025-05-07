

using Sensorway.Events.Base.Models;

namespace Ironwall.Dotnet.Framework.Models.Communications.VmsApis;

public interface IVmsApiSetActionEventRequestModel : IBaseMessageModel
{
    ActionEventModel? Body { get; set; }
}