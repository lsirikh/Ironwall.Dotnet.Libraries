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

    /// <summary>목록의 "설명" 열 — 비었으면 "—"(빈 칸을 값이 없는 것으로 읽게 한다).</summary>
    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "—" : Description!;

    public string? Description
    {
        get => _model.Description;
        set
        {
            _model.Description = value;
            NotifyOfPropertyChange(() => Description);
            NotifyOfPropertyChange(() => DescriptionDisplay);
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
