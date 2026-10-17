using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.EventWindows;

/****************************************************************************
   Purpose      : GIS 쪽 이벤트 창 관리자 창구 (PRD camera-popup-modes FR-09 · 11 · 12 · 15)
   Created By   : Claude (T-04/T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 이벤트 1건 = 창 1개(키 = <see cref="EventKeys.Build"/>)를 관리한다. <b>모든 멤버는 기다리지 않고 예외를 던지지 않는다</b>
/// — 명령은 호스트 감시자에 보내고 잊는다(FR-27/28). UI 스레드 · NATS 처리 줄 어디서 불러도 된다.
/// </summary>
public interface IEventWindowManager
{
    /// <summary>
    /// 창을 연다. 같은 키가 열려 있으면 앞으로만(FR-09). 한도(설정 1~10)를 넘으면 고정 안 된 가장 오래된 창을 닫고(사유 Evicted) 연다(FR-11).
    /// 위치는 대상 모니터 작업영역 안 계단 자리(FR-12).
    /// </summary>
    EventWindowOpenResult Open(EventWindowRequest request);

    /// <summary>열려 있으면 앞으로. 열려 있었는지.</summary>
    bool BringToFront(EventWindowKind kind, string eventId);

    /// <summary>
    /// 그 이벤트의 조치보고(로컬 · 원격)가 왔다 — "조치보고로 닫기"가 켜져 있고 📌 고정이 아니면 닫는다(FR-15).
    /// 닫기 명령을 보냈는지.
    /// </summary>
    bool CloseForActionReport(EventWindowKind kind, string eventId);

    bool IsOpen(string eventKey);

    /// <summary>지금 설정(정규화, 읽기 실패면 사용 안 함). 탐지 트리거가 모드 · 탐지/장애 켜짐 · 창당 카메라 수를 여기서 읽는다.</summary>
    Ironwall.Dotnet.Libraries.Streaming.Base.CameraPopup.CameraPopupSettings CurrentSettings { get; }

    /// <summary>관리자가 알고 있는 열린 창 수.</summary>
    int OpenCount { get; }

    /// <summary>사람에게 알릴 한 줄(모니터 없음 · 화면 밖 당김 · 전부 고정). 배경 스레드에서 올 수 있다.</summary>
    event EventHandler<string>? NoticeRaised;
}
