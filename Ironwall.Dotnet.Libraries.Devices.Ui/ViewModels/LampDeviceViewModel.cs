using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public class LampDeviceViewModel : DeviceViewModel, ILampDeviceViewModel
{
    #region - Ctors -
    public LampDeviceViewModel(ILampDeviceModel model)
        : base(model)
    {
    }
    #endregion
    #region - Properties -
    public string IpAddress
    {
        get { return (_model as ILampDeviceModel)!.IpAddress; }
        set
        {
            (_model as ILampDeviceModel)!.IpAddress = value;
            NotifyOfPropertyChange(() => IpAddress);
        }
    }

    public int IpPort
    {
        get { return (_model as ILampDeviceModel)!.IpPort; }
        set
        {
            (_model as ILampDeviceModel)!.IpPort = value;
            NotifyOfPropertyChange(() => IpPort);
        }
    }

    public string? UserName
    {
        get { return (_model as ILampDeviceModel)!.UserName; }
        set
        {
            (_model as ILampDeviceModel)!.UserName = value;
            NotifyOfPropertyChange(() => UserName);
        }
    }

    public string? UserPassword
    {
        get { return (_model as ILampDeviceModel)!.UserPassword; }
        set
        {
            (_model as ILampDeviceModel)!.UserPassword = value;
            NotifyOfPropertyChange(() => UserPassword);
        }
    }

    public string? Description
    {
        get { return (_model as ILampDeviceModel)!.Description; }
        set
        {
            (_model as ILampDeviceModel)!.Description = value;
            NotifyOfPropertyChange(() => Description);
        }
    }
    #endregion
}
