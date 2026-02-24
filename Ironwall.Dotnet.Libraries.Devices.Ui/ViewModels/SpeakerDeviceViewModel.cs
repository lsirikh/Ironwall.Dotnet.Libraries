using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public class SpeakerDeviceViewModel : DeviceViewModel, ISpeakerDeviceViewModel
{
    #region - Ctors -
    public SpeakerDeviceViewModel(ISpeakerDeviceModel model)
        : base(model)
    {
    }
    #endregion
    #region - Properties -
    public string SpeakerType
    {
        get { return (_model as ISpeakerDeviceModel)!.SpeakerType; }
        set
        {
            (_model as ISpeakerDeviceModel)!.SpeakerType = value;
            NotifyOfPropertyChange(() => SpeakerType);
        }
    }

    public string? Description
    {
        get { return (_model as ISpeakerDeviceModel)!.Description; }
        set
        {
            (_model as ISpeakerDeviceModel)!.Description = value;
            NotifyOfPropertyChange(() => Description);
        }
    }

    public IServerModel? Server
    {
        get { return (_model as ISpeakerDeviceModel)!.Server; }
        set
        {
            (_model as ISpeakerDeviceModel)!.Server = value;
            NotifyOfPropertyChange(() => Server);
        }
    }
    #endregion
}
