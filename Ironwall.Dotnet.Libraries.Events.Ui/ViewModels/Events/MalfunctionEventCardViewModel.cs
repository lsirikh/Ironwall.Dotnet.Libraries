using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Comms;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/2/2025 10:42:04 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class MalfunctionEventCardViewModel : EventCardViewModel<IMalfunctionEventModel>
    {
        
        #region - Ctors -
        public MalfunctionEventCardViewModel(IMalfunctionEventModel model)
            : base(model)
        {
        }

        public MalfunctionEventCardViewModel(IEventAggregator ea, ILogService log, IMalfunctionEventModel model)
            : base(ea, log, model)
        {
        }
        #endregion
        #region - Implementation of Interface -
        #endregion 
        #region - Overrides -
        public override async Task<bool> SendAction(string? content, string? idUser)
        {
            var account = IoC.Get<IAccountModel>();
            IdUser = account.Name;
            Contents = content ?? "자동 조치보고";

            // (Phase3) 조치보고 멱등 — 동일 EventId가 자동/자동복구/배치 경로에서 진행 중이면 수동 보고 스킵(서버/NATS 중복 차단).
            var guard = IoC.Get<IActionReportGuard>();
            if (!guard.TryEnter(Model.Id))
            {
                _log?.Info($"[ACTION_REPORT] Malfunction Event({Model.Id}) 조치보고 진행 중 — 수동 중복 스킵");
                return true;   // 다른 경로가 보고 중 → 이벤트는 보고됨(다이얼로그 닫기 허용)
            }
            try
            {
                var apiService = IoC.Get<IEventApiService>();
                var dto = new ActionEventCreateDto
                {
                    User = IdUser ?? string.Empty,
                    Content = Contents,
                    FromEventId = Model.Id
                };
                var response = await apiService.CreateActionEventAsync(dto);
                if (!response.Success)
                {
                    _log?.Error($"[ACTION_REPORT] Malfunction INSERT 실패: {response.Message}");
                    return false;   // (EA3) 실패 신호 → 다이얼로그 유지
                }

                await _eventAggregator.PublishOnCurrentThreadAsync(new MalfunctionReportedMessageModel(this, Contents, IdUser));
                await _eventAggregator.PublishOnBackgroundThreadAsync(new SendActionRequestMessage
                {
                    EventId = Model.Id,
                    EventType = EnumEventType.Fault,
                    ActionDetails = Contents,
                    ActionUser = IdUser,
                    ActionTime = DateTime.Now,
                    OriginEvent = Model,                 // NATS from_event(device) 원천
                    ActionId = response.Data?.Id ?? 0    // 생성된 Action DB ID
                });

                return await base.SendAction(content, idUser);
            }
            finally { guard.Exit(Model.Id); }
        }

        protected override Task CloseDialog()
        {
            Dispose();
            return Task.CompletedTask;
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public EnumFaultType Reason => _model.Reason;
        public int FirstEnd => _model.FirstEnd;
        public int FirstStart => _model.FirstStart;
        public int SecondEnd => _model.SecondEnd;
        public int SecondStart => _model.SecondStart;

        /// <summary>
        /// 제어기 필드에 표시할 번호 — <b>장비 타입 기준(사유 무관)</b>.
        /// 센서 장비(ISensorDeviceModel): 연결된 제어기의 DeviceNumber(controller_id→DeviceProvider 해석).
        /// 제어기 장비(그 외): 장비 자신의 DeviceNumber.
        /// <para>사유(reason)로 분기하지 않는다 — reason은 장비 타입을 신뢰성 있게 나타내지 못한다.
        /// 실측: FAULT_CABLE_CUTTING이 제어기가 아니라 Fence 센서(device.type_device="Fence", controller_id=제어기FK)에 실려 옴 →
        /// reason 기준이면 센서 번호를 제어기 칸에 표시하고 센서 칸은 공란이 되는 오배정 발생.</para>
        /// </summary>
        public int? ControllerDisplay => Device is ISensorDeviceModel
            ? ControllerDeviceNumber
            : (Device?.DeviceNumber is null or 0 ? null : Device?.DeviceNumber);

        /// <summary>
        /// 센서 필드에 표시할 번호 — <b>장비 타입 기준(사유 무관)</b>.
        /// 센서 장비(ISensorDeviceModel): 장비 자신의 DeviceNumber. 제어기 장비(그 외): null(센서 없음).
        /// </summary>
        public int? SensorDisplay => Device is ISensorDeviceModel
            ? (Device?.DeviceNumber is null or 0 ? null : Device?.DeviceNumber)
            : null;
        #endregion
        #region - Attributes -
        #endregion
    }
}