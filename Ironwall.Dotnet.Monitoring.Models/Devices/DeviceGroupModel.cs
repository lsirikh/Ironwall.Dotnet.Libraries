namespace Ironwall.Dotnet.Monitoring.Models.Devices;

public class DeviceGroupModel : IDeviceGroupModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DeviceCount { get; set; }
}
