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
        /// <summary>역사적 입구 — 다이얼로그가 부른다. 진짜 결말은 <see cref="SendActionDetailed"/> 가 안다.</summary>
        public override async Task<bool> SendAction(string? content, string? idUser)
            => (await SendActionDetailed(content, idUser).ConfigureAwait(true)).CanCloseDialog;

        /// <summary>
        /// 조치보고 전송의 정본 경로. 멱등 가드가 막은 경우를 <b>생성과 구분해</b> 돌려준다(N-07 R1).
        /// </summary>
        public override async Task<ActionSendResult> SendActionDetailed(string? content, string? idUser, CancellationToken token = default)
        {
            var account = IoC.Get<IAccountModel>();
            IdUser = account.Name;
            Contents = content ?? "자동 조치보고";

            // (Phase3) 조치보고 멱등 — 동일 이벤트가 자동/자동복구/배치 경로에서 진행 중이면 수동 보고 스킵.
            // (N-07 R1) 자물쇠는 종류 + Id 로 건다 — 탐지 3번과 장애 3번이 서로를 막지 않도록.
            var guard = IoC.Get<IActionReportGuard>();
            var keyed = guard as IKeyedActionReportGuard;
            var entered = keyed is not null ? keyed.TryEnter(ActionReportKind.Malfunction, Model.Id) : guard.TryEnter(Model.Id);
            if (!entered)
            {
                _log?.Info($"[ACTION_REPORT] Malfunction Event({Model.Id}) 조치보고 진행 중 — 수동 중복 스킵");
                // 만들지 않았다 — "적용" 이 아니라 "건너뜀" 이다.
                return ActionSendResult.GuardSkipped("다른 경로가 같은 이벤트를 보고 중입니다 — 보내지 않았습니다");
            }
            try
            {
                if (token.IsCancellationRequested)
                    return new ActionSendResult(ActionSendOutcome.Cancelled, "중단됐습니다 — 보냈는지는 이 자리에서 알 수 없습니다");

                var apiService = IoC.Get<IEventApiService>();
                // ⚠ ActionEventCreateDto 에는 원본 종류 식별자가 없다 {User, Content, FromEventId} —
                //   서버는 from_event_id 로 종류를 찾는다. 종류를 보는 것은 클라이언트 자물쇠뿐이다(와이어 불변).
                var dto = new ActionEventCreateDto
                {
                    User = IdUser ?? string.Empty,
                    Content = Contents,
                    FromEventId = Model.Id
                };
                var response = await apiService.CreateActionEventAsync(dto, token);
                if (!response.Success)
                {
                    _log?.Error($"[ACTION_REPORT] Malfunction INSERT 실패: {response.Message}");
                    return ActionSendResult.Failed(string.IsNullOrWhiteSpace(response.Message) ? "서버가 거절했습니다" : response.Message!);
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

                await base.SendAction(content, idUser);  // 로그 + CloseDialog (지금 동작 보존)
                return ActionSendResult.Created(response.Data?.Id ?? 0);
            }
            catch (OperationCanceledException)
            {
                return new ActionSendResult(ActionSendOutcome.Cancelled, "중단됐습니다 — 보냈는지는 이 자리에서 알 수 없습니다");
            }
            finally
            {
                if (keyed is not null) keyed.Exit(ActionReportKind.Malfunction, Model.Id); else guard.Exit(Model.Id);
            }
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