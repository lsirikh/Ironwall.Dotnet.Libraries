using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/27/2025 8:15:22 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SensorDeviceViewModel : DeviceViewModel, ISensorDeviceViewModel
{

    #region - Ctors -
    public SensorDeviceViewModel(ISensorDeviceModel model): base(model)
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
    public IControllerDeviceModel? Controller
    {
        get { return (_model as ISensorDeviceModel)!.Controller; }
        set
        {
            (_model as ISensorDeviceModel)!.Controller = value;
            NotifyOfPropertyChange(() => Controller);
            NotifyOfPropertyChange(() => ControllerDisplay);
        }
    }

    /// <summary>콘솔 목록 "제어기" 한 칸(device-console-v8 N02-B) — 소속 제어기가 없으면 "—".</summary>
    public string ControllerDisplay => Controller?.DeviceName ?? "—";
    #endregion
    #region - Attributes -
    #endregion
}