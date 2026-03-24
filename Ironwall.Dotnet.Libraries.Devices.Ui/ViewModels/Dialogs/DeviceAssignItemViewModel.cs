using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;

public class DeviceAssignItemViewModel : PropertyChangedBase
{
    public int Id { get; set; }
    public string? DeviceName { get; set; }
    public EnumDeviceType DeviceType { get; set; }
    public int DeviceNumber { get; set; }
    public EnumDeviceStatus Status { get; set; }
    public bool IsEnable { get; set; }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            _isChecked = value;
            NotifyOfPropertyChange(() => IsChecked);
        }
    }

    public bool IsAlreadyAssigned { get; set; }
}
