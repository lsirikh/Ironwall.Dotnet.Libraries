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

    /// <summary>
    /// 표시용 장비 이름 — 장비가 지워졌으면 서버 스냅샷(<c>device_description</c>)을 "삭제된 장비 · …" 로 보인다(E8).
    /// 목록 열 · 드래그 고스트 · 상세 제목이 이 값을 쓴다.
    /// </summary>
    /// <remarks>지워진 장비는 짧게 "삭제된 장비 (이름)" — 서버 스냅샷 원문은 <see cref="DeviceSnapshotText"/>(툴팁)에 남는다.</remarks>
    public string? DeviceLabel => Helpers.EventDeviceSnapshot.ShortLabel(_model.Device, _model);

    /// <summary>장비 이름의 원문 — 지워진 장비면 "삭제된 장비 · {서버 스냅샷 원문}". 툴팁용.</summary>
    public string? DeviceSnapshotText => Helpers.EventDeviceSnapshot.Label(_model.Device, _model);

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

    /// <summary>장비가 (살아 있는) 센서인가 — '탐지 신호 이력' 은 센서 기준으로 조회한다.</summary>
    public bool IsSensorDevice => Device is ISensorDeviceModel { Id: > 0 };

    /// <summary>행 한 줄 요약 — 화면 읽기 프로그램이 형 이름 대신 읽는다("2026-09-27 00:57:10 · 북측 1").</summary>
    public string RowSummary => $"{DateTime:yyyy-MM-dd HH:mm:ss} · {DeviceLabel ?? "장비 없음"}";

    public int? ControllerId => (Device as ISensorDeviceModel)?.Controller?.Id;
    public int? ControllerDeviceNumber => (Device as ISensorDeviceModel)?.Controller?.DeviceNumber;
    public string? DeviceTypeName => Device?.DeviceType switch
    {
        EnumDeviceType.Controller => "제어기",
        EnumDeviceType.IpCamera => "카메라",
        EnumDeviceType.IpSpeaker => "스피커",
        EnumDeviceType.Enclosure => "함체",
        EnumDeviceType.Lamp => "경고등",
        EnumDeviceType.Gate => "통문",
        // 종류축 미복원(NONE) — '센서'로 단정하지 않는다(F-03).
        // 근거·껍데기 선택 규칙은 EventCardViewModel.DeviceTypeName 주석 및
        // DtoToModelHelper.CreateDeviceShell 참조.
        EnumDeviceType.NONE => Device is ISensorDeviceModel ? "센서" : "알 수 없음",
        not null => "센서",
        null => null
    };
    #endregion
    #region - Attributes -
    #endregion
}