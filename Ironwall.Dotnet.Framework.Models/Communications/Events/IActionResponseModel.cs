using Ironwall.Dotnet.Framework.Models.Events;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IActionResponseModel : IResponseModel
    {
        ActionEventModel? Body { get; set; }
    }
}