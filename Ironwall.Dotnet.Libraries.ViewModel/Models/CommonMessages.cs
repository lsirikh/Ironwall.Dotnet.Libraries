using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.ViewModel.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 6:28:41 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class CloseAllMessageModel;
public class OpenLoginPanelMessageModel
{
    /// <summary>로그인 게이팅(Login_Gated_GIS_Init) — 강제 로그인 모드(닫기 불가). 부팅/세션만료 시 true.</summary>
    public bool IsForced { get; set; }
}
public class OpenLogoutPanelMessageModel;
public class OpenWindyPanelMessageModel;
public class OpenSetupPanelMessageModel;
public class OpenMyPagePanelMessageModel;
public class OpenDevicePanelMessageModel;
public class OpenEventPanelMessageModel;
public class OpenReportPanelMessageModel;
public class OpenAccountManagerPanelMessageModel;
public class OpenVcaPanelMessageModel;
public class ClosePanelMessageModel;
public class CloseDialogMessageModel;
public class OpenRegisterDialogMessageModel;
public class OpenResetPasswordDialogMessageModel;
public class OpenDeleteAccountDialogMessageModel;
public class OpenEditAccountDialogMessageModel;
public class OpenPreEventRemoveAllDialogMessageModel;
public class OpenOnvifPropertyDialogMessageModel;
public class OpenCameraDetailDialogMessageModel
{
    public object? Dialog { get; set; }
}
public class OpenEnclosureThresholdDialogMessageModel
{
    public object? Dialog { get; set; }
}
public class OpenDeviceAssignDialogMessageModel
{
    public object? Dialog { get; set; }
    public Action? OnCompleted { get; set; }
}
public class OpenEventReportDialogMessageModel
{
    public string? EventType { get; set; }
}
/// <summary>
/// 탐지 신호 이력 다이얼로그 오픈 (Detection_Signal_History FR-08).
/// GMaps.Ui(심볼 우클릭)·Devices.Ui(센서 그리드 우클릭) 양쪽에서 발행 — 공용 어셈블리 배치(순환 참조 회피).
/// 메인솔루션 ConductorControl이 IHandle로 수신해 DialogShell에 다이얼로그를 띄운다.
/// </summary>
public class OpenDetectionHistoryDialogMessageModel
{
    /// <summary>대상 센서 장비 ID (GetDetectionEventsAsync sensor 필터 키).</summary>
    public int DeviceId { get; set; }
    /// <summary>헤더 표시용 장비명 (예: "[Multi] Sensor-A-1").</summary>
    public string? DeviceName { get; set; }
    /// <summary>헤더 표시용 장비 번호.</summary>
    public int? DeviceNumber { get; set; }
}
public class OpenPreEventRemoveDialogMessageModel;
public class OpenPreEventFaultDetailsDialogMessageModel;
public class OpenPostEventDetailsDialogMessageModel;
public class OpenPostEventFaultDetailsDialogMessageModel;
public class OpenDiscoveryDialogMessageModel;
public class OpenDeleteAccountAdminPopupMessageModel;
public class OpenAboutSetupPanelMessageModel;
public class ClosePopupMessageModel;
public class CloseAllWindowsMessageModel;
public class RefreshAccountsMessageModel;

public class CallEditAccountAdminProcessMessageModel : IMessageModel { }
public class CallDeleteAccountAdminProcessMessageModel : IMessageModel { }
public class CallResetPasswordAdminProcessMessageModel : IMessageModel { }
public class CallDeletePhotoAdminProcessMessageModel : IMessageModel { }
public class CallEditProcessMessageModel : IMessageModel { }
public class CallResetProcessMessageModel : IMessageModel { }
public class CallResetPasswordProcessMessageModel : IMessageModel { }
public class CallDeleteProcessMessageModel : IMessageModel { }
/// <summary>내정보(MyPage) 본인 프로필 사진 삭제 확인 트리거 — 확인 팝업 '확인' 시 발행. MyPagePanelViewModel이 IHandle로 수신해 서버 삭제. — MyPage_SelfPhoto_Delete_Fix</summary>
public class CallDeletePhotoProcessMessageModel : IMessageModel { }
/// <summary>통합웹 접속 실행 트리거 — 확인 팝업 '확인' 시 발행. LeftMenuSectionViewModel이 IHandle로 수신해 크롬 앱 모드로 웹 대시보드를 실행한다.</summary>
public class CallWebApiProcessMessageModel : IMessageModel { }
public class CallDeleteControllerDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteCameraDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteSensorDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteSpeakerDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteEnclosureDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteLampDeviceProcessMessageModel : IMessageModel { }
public class CallDeleteDeviceGroupProcessMessageModel : IMessageModel { }
public class CallRemoveDeviceFromGroupProcessMessageModel : IMessageModel { }
public class CallDeleteDetectionEventProcessMessageModel : IMessageModel { }
public class CallDeleteMalfunctionEventProcessMessageModel : IMessageModel { }
public class CallDeleteConnectionEventProcessMessageModel : IMessageModel { }
public class CallDeleteActionEventProcessMessageModel : IMessageModel { }
public class CallDelete3rdEventProcessMessageModel : IMessageModel { }
public class ExitProgramMessageModel : IMessageModel { }
public class OpenConfirmPopupMessageModel : CommonMessageModel { }
public class OpenInfoPopupMessageModel : CommonMessageModel { }
public class OpenProgressPopupMessageModel : IMessageModel { }
public class CallAllEventReportMessageModel : IMessageModel { }
public class CallDeleteMapRoiProcessMessageModel : IMessageModel { }
public class CallDeleteMapLayerProcessMessageModel : IMessageModel { }
public class CallCancelReportGenerationProcessMessageModel : IMessageModel { }
public class CallDeleteReportGenerationProcessMessageModel : IMessageModel { }
public class CallDeleteReportTemplateProcessMessageModel : IMessageModel { }
public sealed class ChangeModeWindyMessageModel : EventMessageModel<int>
{
}
/// <summary>WindyMode NATS REQ 요청 트리거 — WindyPanelViewModel → NatsDomainService.
/// PrevMode=클릭 직전 모드(라디오 낙관 갱신 전 값) — REQ 실패+서버 재조회까지 실패한 이중 장애 시
/// 로컬 롤백 폴백에 사용(GOP_Nats_Req_Failure_UX FR-2). null이면 폴백 생략(하위호환).</summary>
public record SendWindyModeMessage(EnumWindyMode Mode, EnumWindyMode? PrevMode = null);

/// <summary>디바이스 초기 로딩 완료 알림 — SymbolEventManager 일괄 동기화 트리거</summary>
public record AllDevicesLoadedMessage();

/// <summary>로그인 게이팅(Login_Gated_GIS_Init) — Device fetch 진행 단계 알림(커버 진행바). StepIndex 1..TotalSteps.</summary>
public record DeviceFetchProgressMessage(string Step, int StepIndex, int TotalSteps);

/// <summary>NatsSync 기반 단일 디바이스 Status 변경 알림</summary>
public record DeviceStatusChangedMessage(int DeviceId, EnumDeviceType DeviceType, EnumDeviceStatus Status);

/// <summary>웹서버 설정(IsWebServerEnabled) 변경 알림 — SETUP 웹설정 토글 시 발행. LeftMenu 통합웹 버튼 가시성 라이브 갱신용(FR-05).</summary>
public record WebServerEnabledChangedMessage(bool IsEnabled);

public class StatusMessageModel
{
    public StatusMessageModel()
    {
    }

    public StatusMessageModel(string log)
    {
        Log = log;
    }

    public string? Log { get; set; }
}