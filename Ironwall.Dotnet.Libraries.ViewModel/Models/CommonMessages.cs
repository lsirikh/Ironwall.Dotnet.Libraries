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
/// <summary>이벤트 억제(정비 창) 스케줄 관리 패널 오픈 — 좌측 메뉴/배너 클릭 시 발행. ConductorControl이 events:view 백스톱 후 PanelShell에 띄운다.</summary>
public class OpenEventSuppressionPanelMessageModel;
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
/// <summary>
/// 그룹 탐지 이력 다이얼로그 오픈 (pidsgroup-rightclick FR-08).
/// GMaps.Ui(구역 심볼 우클릭·등록 센서 정보 오버레이 푸터)·Devices.Ui(그룹 설정 탭 행 우클릭)에서 발행 —
/// 서버에 그룹 필터가 없어 다이얼로그가 멤버 센서 팬아웃(sensor 필터 N회)으로 조회한다(그룹 총합 500 절단, G-1=(a)).
/// 메인솔루션 ConductorControl이 IHandle로 수신해 기존 DetectionHistoryHostDialog에 그룹 모드로 띄운다.
/// </summary>
public class OpenGroupDetectionHistoryDialogMessageModel
{
    /// <summary>대상 장비그룹 ID (PidsGroup 심볼 LinkedDeviceGroup).</summary>
    public int GroupId { get; set; }
    /// <summary>헤더 표시용 그룹명.</summary>
    public string? GroupName { get; set; }
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
public class CallDeleteGateDeviceProcessMessageModel : IMessageModel { }
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
/// <summary>조치보고 문구 삭제 확인 트리거 — 확인 팝업 '확인' 시 발행. ActionReportTemplateConsoleViewModel이 IHandle로 수신해 서버 삭제.</summary>
public class CallDeleteActionReportTemplateProcessMessageModel : IMessageModel { }
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

/// <summary>
/// 장비 그룹 소속이 바뀌었다 — <see cref="GroupIds"/> 는 소속이 달라진 그룹들(넣은 그룹 · 뺀 그룹 모두).
/// </summary>
/// <remarks>
/// <para>소속은 공용 <c>DeviceProvider</c> 장비 모델의 <c>DeviceGroups</c> 를 제자리에서 고치는 평범한 목록이라
/// 바뀌어도 아무도 모른다. 지도의 구역선(PidsGroup) 이벤트 조회표는 부팅 · 전량 재조회 때만 만들어져,
/// 부팅 때 비어 있던 그룹에 콘솔로 장비를 넣어도 그 구역선은 탐지 · 장애 · 제어기 무통신 색을 끝내 받지 못했다.
/// 이 알림을 받은 지도는 <b>그 그룹들의 구역선만</b> 다시 등록(소속이 비면 해제)한다.</para>
/// <para>발행: 장비 콘솔 그룹 넣기 · 되돌리기, 장비 배정 창 저장, 옛 그룹 패널의 빼기, 호스트의 NATS
/// <c>SYNC_DEVICE</c>(소속이 달라졌을 때) · <c>SYNC_DEVICE_GROUP</c>. 수신 스레드는 정하지 않는다 — 받는 쪽이 마샬링한다.</para>
/// </remarks>
public sealed record DeviceGroupMembershipChangedMessage(System.Collections.Generic.IReadOnlyList<int> GroupIds)
{
    /// <summary>유효한(양수) 그룹만 겹침 없이 담는다. 남는 것이 없으면 null — 보낼 것이 없다.</summary>
    public static DeviceGroupMembershipChangedMessage? For(System.Collections.Generic.IEnumerable<int>? groupIds)
    {
        if (groupIds is null) return null;
        var ids = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Where(groupIds, id => id > 0)));
        return ids.Count == 0 ? null : new DeviceGroupMembershipChangedMessage(ids);
    }

    /// <summary>
    /// 한 장비의 소속 전후를 견줘 달라진 그룹(대칭차)을 담는다. 같으면 null.
    /// </summary>
    /// <remarks><paramref name="after"/> 가 null 이면 "소속 없음"이 아니라 "소속을 받지 못함"이다 — 캐시도 그 목록을
    /// 비우지 않으므로(<c>DeviceProviderService.UpdateDeviceProperties</c>) 바뀐 것이 없다고 본다.</remarks>
    public static DeviceGroupMembershipChangedMessage? FromDiff(System.Collections.Generic.IEnumerable<int>? before, System.Collections.Generic.IEnumerable<int>? after)
    {
        if (after is null) return null;
        var was = new System.Collections.Generic.HashSet<int>(before ?? System.Array.Empty<int>());
        var now = new System.Collections.Generic.HashSet<int>(after);
        was.SymmetricExceptWith(now);
        return For(was);
    }
}

/// <summary>
/// 부대 편제가 <b>다른 곳에서</b> 바뀌었다 — 서버 NATS <c>SYNC_UNIT</c>(<c>sensorway.global.all.sync.unit</c>)를 호스트가 옮긴다.
/// </summary>
/// <param name="Action">서버가 보낸 <c>body.action</c> 원문 — <c>CREATED</c> · <c>UPDATED</c> · <c>DELETED</c>(모르면 빈 문자열).</param>
/// <param name="ResourceId">서버가 보낸 <c>body.resource_id</c> 원문.</param>
/// <remarks>
/// <para><b>"다시 읽어라" 는 신호일 뿐이다.</b> 무엇이 바뀌었는지는 싣지 않는다 — 받는 쪽은 편제(<c>GET /api/units/graph</c>)를
/// <b>통째로</b> 다시 읽는다.</para>
/// <para>⚠ <see cref="ResourceId"/> 로 한 건만 읽는 최적화를 하지 않는다 — 로그 · 진단용이다. 부대 생성 · 수정 · 삭제에서는 부대 id 이고,
/// 인접 추가 · 제거에서는 판본에 따라 다르다: 8.0.3 부터는 쌍의 낮은 쪽 부대 id(+ 본문 <c>unit_ids:[low,high]</c>),
/// 그 전 판은 인접 <b>행</b> id 였다(서버 요청 R-3, 2026-09-28 실측 VER-11).</para>
/// <para>PUT 한 번이 여러 건을 <b>몰아서</b> 낼 수 있다 — 받는 쪽이 짧은 창(약 500 ms)으로 합친다
/// (<c>Ironwall.Dotnet.Libraries.Utils.Consoles.CoalescingTrigger</c>). 호스트는 같은 봉투 id 를 한 번만 옮긴다.</para>
/// <para>수신 스레드는 정하지 않는다 — 받는 쪽이 마샬링한다(<c>SubscribeOnUIThread</c> 등).
/// 받는 쪽: 부대 편제 콘솔(열려 있을 때) · 부대 관계도. 부대 캐시(<c>UnitNameDirectory</c> · <c>UnitScopeService</c>)는 호스트가 직접 무효화한다.</para>
/// </remarks>
public sealed record UnitTopologyChangedMessage(string Action, int ResourceId);

/// <summary>
/// 조치보고 문구 목록이 <b>다른 곳에서</b> 바뀌었다 — 서버 NATS <c>SYNC_ACTION_REPORT_TEMPLATE</c> 를 호스트가 옮긴다.
/// </summary>
/// <param name="Action">서버가 보낸 <c>body.action</c> 원문 — <c>CREATED</c> · <c>UPDATED</c> · <c>DELETED</c>.</param>
/// <param name="ResourceId">서버가 보낸 <c>body.resource_id</c> 원문. 순서 바꾸기(<c>/reorder</c>)는 <c>UPDATED</c> + <c>0</c> 한 건이다.</param>
/// <remarks>
/// <para>받는 쪽은 목록을 통째로 다시 읽는다(<c>GET /api/events/action-report-templates</c>).</para>
/// <para>전환기에는 서버가 같은 봉투를 전역 subject 와 부대 subject 로 <b>두 번</b> 보낸다 — 호스트가 봉투 id 로 한 번만 옮긴다.</para>
/// <para>받는 쪽: 조치보고 문구 콘솔 · 이벤트 콘솔 조치 트레이 · 조치보고 창(열려 있을 때). 적용하지 않은 편집은 덮지 않는다.</para>
/// </remarks>
public sealed record ActionReportTemplatesChangedMessage(string Action, int ResourceId);

/// <summary>
/// 이벤트 맵핑이 <b>다른 곳에서</b> 바뀌었다 — 서버 NATS <c>SYNC_EVENT_MAPPING</c>(<c>{action, resource_id}</c>, 브로커 §9.6)을 호스트가 옮긴다.
/// </summary>
/// <param name="Action">서버가 보낸 <c>body.action</c> 원문 — <c>CREATED</c> · <c>UPDATED</c> · <c>DELETED</c>.</param>
/// <param name="ResourceId">서버가 보낸 <c>body.resource_id</c>(맵핑 id) 원문 — 로그 · 진단용.</param>
/// <remarks>
/// <para>GIS 안에서 서버 맵핑을 들고 있는 곳은 <b>이벤트 맵핑 워크벤치 창뿐</b>이다(열 때 읽는다). RTSP 이벤트 호출 판단은
/// 서버 맵핑이 아니라 로컬 게이트웨이 표(<c>GatewayEventProvider</c>)를 쓴다 — 이 알림과 무관하다.</para>
/// <para>받는 쪽은 목록을 통째로 다시 읽고, 몰려오는 알림은 짧은 창(약 500 ms)으로 합친다. 적용하지 않은 편집은 덮지 않는다.</para>
/// </remarks>
public sealed record EventMappingsChangedMessage(string Action, int ResourceId);

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