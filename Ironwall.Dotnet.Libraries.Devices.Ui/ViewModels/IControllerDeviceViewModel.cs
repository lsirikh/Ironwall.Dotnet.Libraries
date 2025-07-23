namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public interface IControllerDeviceViewModel : IDeviceViewModel
{
    string IpAddress { get; set; }
    int Port { get; set; }
}