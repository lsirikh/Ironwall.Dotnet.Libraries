using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:58:19 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ExEventViewModel : BaseEventViewModel<IExEventModel>, IExEventViewModel
{
    #region - Ctors -
    public ExEventViewModel(IExEventModel model) : base(model)
    {
        _model = model;
    }
    public ExEventViewModel(IExEventModel model, IEventAggregator ea, ILogService log) : base(model, ea, log)
    {
        _model = model;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    public override void Dispose()
    {
        _model = new ExEventModel();
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
    public string? EventGroup
    {
        get { return _model.EventGroup; }
        set
        {
            SetModelProperty(value, _model.EventGroup, v => _model.EventGroup = v);
        }
    }

    public IBaseDeviceModel? Device
    {
        get { return _model.Device; }
        set
        {
            SetModelProperty(value, _model.Device, v => _model.Device = v);

        }
    }

    public EnumTrueFalse Status
    {
        get { return _model.Status; }
        set
        {
            SetModelProperty(value, _model.Status, v => _model.Status = v);
        }
    }
    #endregion
    #region - Attributes -
    #endregion
}