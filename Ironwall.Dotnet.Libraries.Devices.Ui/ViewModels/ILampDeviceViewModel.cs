namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public interface ILampDeviceViewModel : IDeviceViewModel
{
    string IpAddress { get; set; }
    int IpPort { get; set; }
    string? UserName { get; set; }
    string? UserPassword { get; set; }
    string? Description { get; set; }
}
