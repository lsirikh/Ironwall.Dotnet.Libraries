using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/27/2025 7:38:03 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DeviceViewModel : BaseDeviceViewModel<IBaseDeviceModel>, IDeviceViewModel
{
    #region - Ctors -
    public DeviceViewModel(IBaseDeviceModel model) : base(model)
    {
        _model = model;
    }
    public DeviceViewModel(IBaseDeviceModel model, IEventAggregator ea, ILogService log) : base(model, ea, log)
    {
        _model = model;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    public override void Dispose()
    {
        _model = new BaseDeviceModel();
        GC.Collect();
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    #endregion
    #region - Attributes -
    #endregion
}