using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public class DeviceGroupViewModel : PropertyChangedBase, IDeviceGroupViewModel, ISelectableBaseViewModel
{
    #region - Ctors -
    public DeviceGroupViewModel(IDeviceGroupModel model)
    {
        _model = model;
    }
    #endregion
    #region - Properties -
    public int Id => _model.Id;

    public string Name
    {
        get => _model.Name;
        set
        {
            _model.Name = value;
            NotifyOfPropertyChange(() => Name);
        }
    }

    public string? Description
    {
        get => _model.Description;
        set
        {
            _model.Description = value;
            NotifyOfPropertyChange(() => Description);
        }
    }

    public int DeviceCount => _model.DeviceCount;

    public IDeviceGroupModel Model => _model;

    public int Index { get; set; }

    public bool IsSelected { get; set; }
    #endregion
    #region - Attributes -
    private readonly IDeviceGroupModel _model;
    #endregion
}
