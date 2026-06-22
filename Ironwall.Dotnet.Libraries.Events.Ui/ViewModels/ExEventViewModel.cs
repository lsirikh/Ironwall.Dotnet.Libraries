using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

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
            if (!IsDraft) return;   // device_id 불변(서버 E-05/06): 기존 행 장비 변경 차단 — SelectionView 일괄적용 등 프로그래matic 경로 포함
            SetModelProperty(value, _model.Device, v => _model.Device = v);

        }
    }

    public EnumTrueFalse Status
    {
        get { return _model.Status; }
        set
        {
            if (!IsDraft) return;   // action_reported 시스템 자동관리(서버 E-03/04/08): 사용자 변경 차단 — 전 경로
            SetModelProperty(value, _model.Status, v => _model.Status = v);
        }
    }

    public bool IsActionReported => Status == EnumTrueFalse.True;

    public int? ControllerId => (Device as ISensorDeviceModel)?.Controller?.Id;
    public int? ControllerDeviceNumber => (Device as ISensorDeviceModel)?.Controller?.DeviceNumber;
    public string? DeviceTypeName => Device?.DeviceType switch
    {
        EnumDeviceType.Controller => "제어기",
        EnumDeviceType.IpCamera => "카메라",
        EnumDeviceType.IpSpeaker => "스피커",
        EnumDeviceType.Enclosure => "함체",
        EnumDeviceType.Lamp => "경고등",
        not null => "센서",
        null => null
    };
    #endregion
    #region - Attributes -
    #endregion
}