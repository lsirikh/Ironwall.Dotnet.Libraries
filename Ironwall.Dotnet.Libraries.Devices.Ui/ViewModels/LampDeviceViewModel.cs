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
            NotifyOfPropertyChange(() => AddressDisplay);
        }
    }

    public int IpPort
    {
        get { return (_model as ILampDeviceModel)!.IpPort; }
        set
        {
            (_model as ILampDeviceModel)!.IpPort = value;
            NotifyOfPropertyChange(() => IpPort);
            NotifyOfPropertyChange(() => AddressDisplay);
        }
    }

    /// <summary>
    /// 콘솔 목록 "IP:포트" 한 칸(device-console-v8 N02-B) — IP 가 비면 "—", 포트가 0(미설정)이면 IP만 보인다.
    /// </summary>
    public string AddressDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(IpAddress)) return "—";
            return IpPort == 0 ? IpAddress : $"{IpAddress}:{IpPort}";
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
