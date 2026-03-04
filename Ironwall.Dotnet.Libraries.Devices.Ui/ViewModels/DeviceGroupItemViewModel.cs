using Caliburn.Micro;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public class DeviceGroupItemViewModel : PropertyChangedBase
{
    public int GroupId { get; set; }

    public string GroupName
    {
        get => _groupName;
        set { _groupName = value; NotifyOfPropertyChange(); }
    }

    public bool? IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; NotifyOfPropertyChange(); }
    }

    public bool? OriginalState { get; set; }

    private string _groupName = string.Empty;
    private bool? _isChecked;
}
