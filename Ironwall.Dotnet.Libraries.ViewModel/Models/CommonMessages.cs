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
/// <summary>WindyMode NATS REQ 요청 트리거 — WindyPanelViewModel → NatsDomainService</summary>
public record SendWindyModeMessage(EnumWindyMode Mode);

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