using Ironwall.Dotnet.Framework.Models.Events;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IConnectionRequestModel : IBaseEventMessageModel
    {
        ConnectionEventModel Body { get; set; }
    }
}