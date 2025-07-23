using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:57:48 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ActionEventViewModel : BaseEventViewModel<IActionEventModel>, IActionEventViewModel
{
    #region - Ctors -
    public ActionEventViewModel(IActionEventModel model) : base(model)
    {
        _model = model;
    }
    public ActionEventViewModel(IActionEventModel model, IEventAggregator ea, ILogService log) : base(model, ea, log)
    {
        _model = model;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    public override void Dispose()
    {
        _model = new ActionEventModel();
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
    public IExEventModel? OriginEvent
    {
        get { return _model.OriginEvent; }
        set { SetModelProperty(value, _model.OriginEvent, v => _model.OriginEvent = v); }
    }

    public string? User
    {
        get { return _model!.User; }
        set { SetModelProperty(value, _model.User, v => _model.User = v); }
    }

    public string? Content
    {
        get { return _model!.Content; }
        set { SetModelProperty(value, _model.Content, v => _model.Content = v); }
    }
    #endregion
    #region - Attributes -
    #endregion
}