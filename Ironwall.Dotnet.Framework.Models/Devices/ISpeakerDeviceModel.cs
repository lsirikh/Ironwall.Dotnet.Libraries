namespace Ironwall.Dotnet.Framework.Models.Devices;

public interface ISpeakerDeviceModel : IBaseDeviceModel
{
    string SpeakerType { get; set; }
    string? Description { get; set; }
}
