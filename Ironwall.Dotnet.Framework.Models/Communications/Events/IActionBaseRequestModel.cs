using Ironwall.Dotnet.Framework.Models.Events;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    public interface IActionBaseRequestModel<T> : IBaseEventMessageModel where T : MetaEventModel
    {
        T? Event { get; set; }
        string Content { get; set; }
        string User { get; set; }
    }
}