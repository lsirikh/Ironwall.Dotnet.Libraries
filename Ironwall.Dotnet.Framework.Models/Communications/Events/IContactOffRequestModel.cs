using Ironwall.Dotnet.Framework.Models.Events;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IContactOffRequestModel : IBaseEventMessageModel
    {
        ContactEventModel? Body { get; set; }
    }
}