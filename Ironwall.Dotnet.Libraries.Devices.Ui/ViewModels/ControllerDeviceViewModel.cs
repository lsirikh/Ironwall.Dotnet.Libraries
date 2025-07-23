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
        }
    }

    public int Port
    {
        get { return (_model as IControllerDeviceModel)!.Port; }
        set
        {
            (_model as IControllerDeviceModel)!.Port = value;
            NotifyOfPropertyChange(() => Port);
        }
    }
    #endregion
    #region - Attributes -
    #endregion
}