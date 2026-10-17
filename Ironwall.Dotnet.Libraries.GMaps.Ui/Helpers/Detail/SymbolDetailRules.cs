using System;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;

/****************************************************************************
   Purpose      : 심볼 상세 보기 창의 탭·액션 노출 규칙(순수) — PRD symbol-detail-and-door-control FR-18~21
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>상세 창 우측 탭.</summary>
public enum SymbolDetailTab
{
    /// <summary>번호·장비번호·장비명·유형·소속 제어기·장비그룹·위치·상태·버전.</summary>
    Basic,
    /// <summary>IP·포트·모드·카테고리·녹화 — IP 를 갖는 타입만.</summary>
    Comm,
    /// <summary>현재 상태·최근 변경 · 함체(도어·히터·팬) · 통문(게이트 상태).</summary>
    Status,
    /// <summary>방송 송출 상태·대상·마이크 안내 — 스피커 전용(FR-22/24).</summary>
    Broadcast,
    /// <summary>심볼 자체 정보 — 제목·위경도·회전·최소표시줌·레이어·잠금. <b>장비와 무관</b>.</summary>
    Symbol,
    /// <summary>부품 표 — 장비 콘솔 "부품 상태" 절과 같은 칸 · 같은 요약 줄(component-display-unify FR-06). 부품 축이 있는 장비만.</summary>
    Components,
}

/// <summary>상세 창 하단 액션 바 버튼(기존 컨텍스트 메뉴와 1:1).</summary>
public enum SymbolDetailAction
{
    /// <summary>{장비}페이지 — 웹서버 목록.</summary>
    DevicePage,
    /// <summary>{장비}상세 — 웹서버 상세.</summary>
    DeviceDetail,
    /// <summary>{장비}수정 — 웹서버 수정.</summary>
    DeviceEdit,
    /// <summary>탐지 이력(감지센서 전용).</summary>
    DetectionHistory,
    /// <summary>제어기 홈페이지(IP:Port).</summary>
    ControllerHome,
    /// <summary>카메라 홈페이지(IP:Port).</summary>
    CameraHome,
    /// <summary>특정 위치 확인(PTZ 카메라).</summary>
    AimLocation,
    /// <summary>음원 실행(스피커).</summary>
    SoundPlay,
    /// <summary>TTS 실행(스피커).</summary>
    Tts,
    /// <summary>방송 정지(스피커).</summary>
    BroadcastStop,
    /// <summary>마이크 누르고 말하기(스피커) — 서버 cmd 확정 전까지 비활성.</summary>
    MicPtt,
    /// <summary>문 열기(통문·함체).</summary>
    DoorOpen,
    /// <summary>문 닫기(통문·함체).</summary>
    DoorClose,
    /// <summary>지도에서 보기 — 항상 가능(심볼만 있으면 된다).</summary>
    ShowOnMap,
}

/// <summary>액션 하나의 노출·활성 판정 결과.</summary>
/// <param name="Action">액션 종류.</param>
/// <param name="IsEnabled">누를 수 있는가.</param>
/// <param name="DisabledReason">비활성 사유(활성이면 null) — 조용한 무동작 금지(FR-28).</param>
public readonly record struct SymbolDetailActionState(SymbolDetailAction Action, bool IsEnabled, string? DisabledReason);

/// <summary>상세 창 판정에 필요한 바깥 상태 묶음.</summary>
/// <param name="DeviceType">심볼의 장비 타입.</param>
/// <param name="HasDevice">심볼에 장비가 연결됐는가(<c>LinkedDeviceId &gt; 0</c>).</param>
/// <param name="WebServerEnabled">장비 웹서버 연동이 켜져 있는가.</param>
/// <param name="HasNetworkEndpoint">IP·포트를 아는가(제어기·카메라 홈페이지용).</param>
/// <param name="IsPtzCamera">PTZ 카메라인가.</param>
/// <param name="CanControlDevice">devices:control 권한.</param>
/// <param name="CanBroadcast">broadcast:control 권한.</param>
/// <param name="CanViewEvents">events:view 권한.</param>
/// <param name="MicCommandAvailable">마이크 NATS cmd 가 확정·배선됐는가(서버 회신 전 false).</param>
/// <param name="DoorState">문 상태 — 통문·함체에서만 의미. 대기·미수신이면 버튼이 잠긴다.</param>
public readonly record struct SymbolDetailContext(
    EnumDeviceType DeviceType,
    bool HasDevice,
    bool WebServerEnabled = false,
    bool HasNetworkEndpoint = false,
    bool IsPtzCamera = false,
    bool CanControlDevice = false,
    bool CanBroadcast = false,
    bool CanViewEvents = false,
    bool MicCommandAvailable = false,
    Door.DoorUiState DoorState = Door.DoorUiState.Unknown);

/// <summary>
/// 상세 창 규칙 — WPF 무의존 순수 판정. 뷰·뷰모델이 이 결과만 그린다.
///
/// <para><b>설계 원칙</b>: 액션의 노출·비활성 조건을 <b>기존 컨텍스트 메뉴와 동일하게</b> 유지한다(FR-20/21).
/// 두 벌의 규칙이 생기면 한쪽만 고쳐져 "메뉴에선 되는데 버튼은 안 되는" 상태가 만들어진다.</para>
/// </summary>
public static class SymbolDetailRules
{
    /// <summary>IP·포트를 갖는(통신 탭이 의미 있는) 타입.</summary>
    private static bool HasNetworkTab(EnumDeviceType t)
        => t is EnumDeviceType.Controller or EnumDeviceType.IpCamera or EnumDeviceType.IpSpeaker or EnumDeviceType.Lamp;

    /// <summary>개폐 형태를 갖는 타입 — 통문·함체.</summary>
    public static bool HasDoor(EnumDeviceType t)
        => t is EnumDeviceType.Gate or EnumDeviceType.Enclosure;

    /// <summary>감지센서 계열(탐지 이력 대상).</summary>
    public static bool IsSensor(EnumDeviceType t)
        => t is EnumDeviceType.Multi or EnumDeviceType.Fence or EnumDeviceType.Underground
            or EnumDeviceType.Contact or EnumDeviceType.PIR or EnumDeviceType.IoController
            or EnumDeviceType.Laser or EnumDeviceType.Cable or EnumDeviceType.SmartSensor
            or EnumDeviceType.SmartSensor2 or EnumDeviceType.SmartCompound or EnumDeviceType.Radar
            or EnumDeviceType.OpticalCable or EnumDeviceType.SmartMultisensor2;

    /// <summary>
    /// 이 타입에서 <b>보이는</b> 탭. 해당 없는 탭은 아예 숨긴다 — 빈 탭을 눌러보게 만들지 않는다(FR-18).
    /// 심볼 탭은 <b>항상</b> 보인다(장비와 무관하므로).
    /// </summary>
    public static IReadOnlyList<SymbolDetailTab> VisibleTabs(EnumDeviceType deviceType)
    {
        // ⚠ 유형별 탭(통신·방송·상태)은 **일단 내리기로 했다**(사용자 결정 2026-09-09).
        //   기준은 앱의 '센서 설정 패널' 컬럼 — 번호·장비번호·장비명·유형·제어기·위치·상태·활성화.
        //   현장에서 채워지지 않는 필드(카메라 제원·함체 임계·통문 결선)를 늘어놓느니 확실한 것만 보여준다.
        //   되살릴 때는 이 목록에 다시 넣기만 하면 된다 — 판정·필드 코드는 그대로 두었다.
        //   '최근 이벤트' 탭도 두지 않는다 — 요약을 흉내 내는 대신 액션 바 [탐지 이력] 으로 보낸다.
        var tabs = new List<SymbolDetailTab>
        {
            SymbolDetailTab.Basic,
            SymbolDetailTab.Symbol,
        };
        _ = deviceType;   // 타입별 분기는 현재 없음(위 결정) — 시그니처는 유지한다
        return tabs;
    }

    /// <summary>
    /// 부품 축 여부까지 본 탭 목록 — 부품 축이 있는 장비(7.0+)면 기본 · <b>부품</b> · 심볼, 없으면(6.3 · 미연결) 종전과 같다(무회귀).
    /// </summary>
    /// <param name="deviceType">장비 종류.</param>
    /// <param name="hasComponentAxes">연결 장비에 부품 축 묶음이 있는가(component-display-unify FR-06 · FR-08).</param>
    public static IReadOnlyList<SymbolDetailTab> VisibleTabs(EnumDeviceType deviceType, bool hasComponentAxes)
    {
        var tabs = VisibleTabs(deviceType).ToList();
        if (hasComponentAxes) tabs.Insert(tabs.IndexOf(SymbolDetailTab.Symbol), SymbolDetailTab.Components);
        return tabs;
    }

    /// <summary>
    /// 탭 활성 여부. <b>심볼 탭만은 장비 미연결이어도 활성</b>(FR-19) — 심볼 제목·좌표·레이어는
    /// 장비와 무관하고, 이걸 막으면 "이 심볼이 뭔지" 확인할 방법이 사라진다.
    /// </summary>
    public static bool IsTabEnabled(SymbolDetailTab tab, bool hasDevice)
        => tab == SymbolDetailTab.Symbol || hasDevice;

    /// <summary>미연결 상태에서 기본 선택될 탭 — 유일하게 볼 수 있는 심볼 탭.</summary>
    public static SymbolDetailTab DefaultTab(bool hasDevice)
        => hasDevice ? SymbolDetailTab.Basic : SymbolDetailTab.Symbol;

    /// <summary>이 타입에서 <b>보이는</b> 액션(순서 = 화면 배치 순서).</summary>
    public static IReadOnlyList<SymbolDetailAction> VisibleActions(EnumDeviceType deviceType)
    {
        var list = new List<SymbolDetailAction>
        {
            SymbolDetailAction.DevicePage,
            SymbolDetailAction.DeviceDetail,
            SymbolDetailAction.DeviceEdit,
        };
        if (IsSensor(deviceType)) list.Add(SymbolDetailAction.DetectionHistory);
        if (deviceType == EnumDeviceType.Controller) list.Add(SymbolDetailAction.ControllerHome);
        if (deviceType == EnumDeviceType.IpCamera)
        {
            list.Add(SymbolDetailAction.CameraHome);
            list.Add(SymbolDetailAction.AimLocation);
        }
        if (deviceType == EnumDeviceType.IpSpeaker)
        {
            list.Add(SymbolDetailAction.SoundPlay);
            list.Add(SymbolDetailAction.Tts);
            list.Add(SymbolDetailAction.BroadcastStop);
            list.Add(SymbolDetailAction.MicPtt);
        }
        if (HasDoor(deviceType))
        {
            list.Add(SymbolDetailAction.DoorOpen);
            list.Add(SymbolDetailAction.DoorClose);
        }
        list.Add(SymbolDetailAction.ShowOnMap);
        return list;
    }

    /// <summary>액션 하나의 활성 여부와 비활성 사유.</summary>
    public static SymbolDetailActionState Evaluate(SymbolDetailAction action, in SymbolDetailContext ctx)
    {
        // 지도에서 보기 = 심볼만 있으면 되므로 어떤 조건에도 걸리지 않는다.
        if (action == SymbolDetailAction.ShowOnMap)
            return new(action, true, null);

        // 장비 연결이 없으면 장비를 대상으로 하는 모든 액션이 불가(장비 목록 페이지만 예외).
        if (!ctx.HasDevice && action != SymbolDetailAction.DevicePage)
            return new(action, false, "장비 미연결");

        switch (action)
        {
            case SymbolDetailAction.DevicePage:
            case SymbolDetailAction.DeviceDetail:
            case SymbolDetailAction.DeviceEdit:
                return ctx.WebServerEnabled
                    ? new(action, true, null)
                    : new(action, false, "웹서버 연동 꺼짐");

            case SymbolDetailAction.DetectionHistory:
                return ctx.CanViewEvents
                    ? new(action, true, null)
                    : new(action, false, "권한 없음(events:view)");

            case SymbolDetailAction.ControllerHome:
            case SymbolDetailAction.CameraHome:
                return ctx.HasNetworkEndpoint
                    ? new(action, true, null)
                    : new(action, false, "IP·포트 없음");

            case SymbolDetailAction.AimLocation:
                if (!ctx.IsPtzCamera) return new(action, false, "PTZ 카메라 아님");
                return new(action, true, null);

            case SymbolDetailAction.SoundPlay:
            case SymbolDetailAction.Tts:
            case SymbolDetailAction.BroadcastStop:
                return ctx.CanBroadcast
                    ? new(action, true, null)
                    : new(action, false, "권한 없음(broadcast:control)");

            case SymbolDetailAction.MicPtt:
                if (!ctx.CanBroadcast) return new(action, false, "권한 없음(broadcast:control)");
                // 서버 cmd(BROADCAST_MIC_START/STOP)가 확정되기 전엔 누를 수 없다 — 눌러도 아무 일도
                // 안 일어나는 버튼을 활성으로 두지 않는다(FR-23).
                return ctx.MicCommandAvailable
                    ? new(action, true, null)
                    : new(action, false, "마이크 방송 규격 미확정");

            case SymbolDetailAction.DoorOpen:
            case SymbolDetailAction.DoorClose:
                if (!ctx.CanControlDevice) return new(action, false, "권한 없음(devices:control)");
                if (ctx.DoorState == Door.DoorUiState.Pending) return new(action, false, "응답 대기 중");
                if (ctx.DoorState == Door.DoorUiState.Unknown) return new(action, false, "상태 미수신");
                // 이미 그 상태면 보낼 이유가 없다.
                var already = action == SymbolDetailAction.DoorOpen
                    ? ctx.DoorState == Door.DoorUiState.Open
                    : ctx.DoorState == Door.DoorUiState.Closed;
                return already ? new(action, false, "이미 그 상태") : new(action, true, null);

            default:
                return new(action, false, "미지원");
        }
    }

    /// <summary>이 타입의 액션 전부를 평가한다(화면 순서 유지).</summary>
    public static IReadOnlyList<SymbolDetailActionState> EvaluateAll(in SymbolDetailContext ctx)
    {
        var ctxCopy = ctx;
        return VisibleActions(ctx.DeviceType).Select(a => Evaluate(a, in ctxCopy)).ToList();
    }
}
