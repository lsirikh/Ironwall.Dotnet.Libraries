using Ironwall.Dotnet.Framework.Models.Devices;

namespace Ironwall.Dotnet.Framework.Models.Events
{
    public interface IDetectionEventModel : IMetaEventModel
    {
        int Result { get; set; }
    }
}