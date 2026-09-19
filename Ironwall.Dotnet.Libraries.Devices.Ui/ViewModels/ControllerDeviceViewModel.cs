using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/27/2025 8:15:10 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ControllerDeviceViewModel : DeviceViewModel, IControllerDeviceViewModel
{

    #region - Ctors -
    public ControllerDeviceViewModel(IControllerDeviceModel model)
        : base(model)
    {
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public string IpAddress
    {
        get { return (_model as IControllerDeviceModel)!.IpAddress; }
        set
        {
            (_model as IControllerDeviceModel)!.IpAddress = value;
            NotifyOfPropertyChange(() => IpAddress);
            NotifyOfPropertyChange(() => AddressDisplay);
        }
    }

    public int Port
    {
        get { return (_model as IControllerDeviceModel)!.Port; }
        set
        {
            (_model as IControllerDeviceModel)!.Port = value;
            NotifyOfPropertyChange(() => Port);
            NotifyOfPropertyChange(() => AddressDisplay);
        }
    }

    /// <summary>
    /// 콘솔 목록 "IP:포트" 한 칸(device-console-v8 N02-B) — IP 가 비면 "—", 포트가 0(미설정)이면
    /// 포트를 덧붙이지 않고 IP만 보인다(<c>:0</c> 은 아무 의미가 없는 값이라 그대로 보이면 오독을 부른다).
    /// </summary>
    public string AddressDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(IpAddress)) return "—";
            return Port == 0 ? IpAddress : $"{IpAddress}:{Port}";
        }
    }
    #endregion
    #region - Attributes -
    #endregion
}