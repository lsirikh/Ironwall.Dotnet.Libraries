using Ironwall.Dotnet.Framework.Models.Events;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IMalfunctionRequestModel: IBaseMessageModel
    {
        MalfunctionEventModel? Body { get; set; }
    }
}