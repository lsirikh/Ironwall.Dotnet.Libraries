using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/1/2025 8:27:51 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public abstract class EventCardViewModel<T> : EventCardBaseViewModel where T : IExEventModel
    {
        #region - Ctors -
        protected EventCardViewModel(T model) 
        {
            _model = model;
        }

        protected EventCardViewModel(IEventAggregator ea, ILogService log, T model)
            : base(ea, log)
        {
            _model = model;
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected abstract Task CloseDialog();
        // (EA3) 반환값: 조치보고 서버 저장 성공 여부. 호출자(다이얼로그)는 false 면 닫지 않고 오류를 알린다.
        public async virtual Task<bool> SendAction(string? content, string? idUser)
        {
            _log?.Info($"CardViewModel SendAction : User({IdUser}), Content({Contents}), Device({Device?.DeviceName}), DateTime({DateTime})");
            await CloseDialog();
            return true;
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public string? IdUser { get; set; }
        public string? Contents { get; set; }
        /// <summary>
        /// 이 카드가 참조하는 장비. <b>null 가능</b> — 장비가 삭제(SYNC_DEVICE DELETED)되면
        /// 이벤트 모델의 Device 참조가 끊길 수 있다(FR-D4). EQM은 장비 삭제 시 해당 심볼 엔트리를
        /// 자동 정리하지만, 조치보고 전까지 카드 자체는 리스트에 잔류할 수 있으므로 파생 게터
        /// (ControllerId/DeviceTypeName/DeviceGroupsText 등)는 모두 null-safe(?. + 가드)로 유지한다.
        /// </summary>
        public IBaseDeviceModel? Device => _model.Device;
        public EnumTrueFalse Status => _model.Status;
        public EnumEventType MessageType => _model.MessageType;
        public override IExEventModel Model => _model;
        public int? ControllerId => (Device as ISensorDeviceModel)?.Controller?.Id;
        public int? ControllerDeviceNumber
        {
            get
            {
                var controller = (Device as ISensorDeviceModel)?.Controller;
                var num = controller?.DeviceNumber;
                if (num is > 0) return num;

                // 폴백: 장애 전문(BaseDeviceDto)은 컨트롤러 표시번호를 싣지 않고 controller_id(FK)만 준다.
                //   중첩 Controller nav가 미하이드레이션(번호=0)이면 controller_id로 DeviceProvider에서 재해석 —
                //   생성 경로(Provider 히트/폴백 스텁/미하이드레이션)와 무관하게 제어기 번호를 복구한다.
                var controllerId = controller?.Id ?? 0;
                if (controllerId <= 0) return null;
                try
                {
                    var ctrl = IoC.Get<DeviceProvider>()
                        .OfType<ControllerDeviceModel>()
                        .FirstOrDefault(c => c.Id == controllerId);
                    return ctrl?.DeviceNumber is > 0 ? ctrl.DeviceNumber : null;
                }
                catch { return null; }
            }
        }
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

        /// <summary>
        /// 이벤트 장비가 속한 그룹(구역) 이름 목록을 ", "로 결합.
        /// N:N — 다중 그룹이면 "1, 10", 단일 그룹이면 "1". DeviceGroupProvider로 Id→Name 조회(미발견 시 Id로 fallback).
        /// (장비 관리 패널 BaseDeviceViewModel.DeviceGroupsText 와 동일 패턴)
        /// </summary>
        public string DeviceGroupsText
        {
            get
            {
                var ids = Device?.DeviceGroups;
                if (ids == null || ids.Count == 0) return string.Empty;
                try
                {
                    var provider = IoC.Get<DeviceGroupProvider>();
                    return string.Join(", ", ids.Select(id =>
                        provider.OfType<DeviceGroupModel>().FirstOrDefault(g => g.Id == id)?.Name ?? id.ToString()));
                }
                catch { return string.Join(", ", ids); }
            }
        }
        #endregion
        #region - Attributes -
        protected readonly T _model;
        #endregion
    }
}