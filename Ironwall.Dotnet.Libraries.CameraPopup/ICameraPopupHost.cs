using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Frames;

namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>
/// GIS 가 카메라 팝업 호스트를 쓰는 유일한 창구(FR-24~29).
/// <b>모든 멤버는 기다리지 않고 예외를 던지지 않는다</b> — UI 스레드 · NATS 처리 줄에서 그대로 불러도 된다.
/// 호스트가 죽어 있거나 재시작 중이어도 명령은 기록되고, 호스트가 살아나면 다시 보내진다.
/// </summary>
public interface ICameraPopupHost : IDisposable
{
    CameraPopupHostState State { get; }

    /// <summary>마지막 상태 사유(안내 문구용).</summary>
    string? StateReason { get; }

    /// <summary>상태 변화(배경 스레드에서).</summary>
    event EventHandler<CameraPopupHostStateChangedEventArgs>? StateChanged;

    /// <summary>호스트 상태 메시지(배경 스레드에서, 받은 순서대로).</summary>
    event EventHandler<CameraPopupStatusEventArgs>? StatusReceived;

    /// <summary>호스트를 띄운다(한 번). 실행 파일이 없으면 <see cref="CameraPopupHostState.Unavailable"/>.</summary>
    void Start();

    /// <summary>Suspended · Unavailable 에서 다시 시작(지도 하단 [다시 시작]). 재시작 예산을 비운다.</summary>
    void Restart();

    /// <summary>지도 오버레이 영상. 크기 오류 · 공유 메모리 실패면 null.</summary>
    IFrameSource? OpenOverlay(OverlayStreamRequest request);

    void CloseOverlay(string streamId);

    void OpenEventWindow(OpenEventWindow request);

    /// <summary>이벤트 창 닫기. 키는 <see cref="Contracts.Protocol.EventKeys.Build"/>. 복원 목록에서도 뺀다.</summary>
    void CloseEventWindow(string eventKey, Contracts.Protocol.EventWindowCloseReason reason, bool returnHome);

    /// <summary>열려 있는 이벤트 창을 앞으로(같은 이벤트 재수신 등). 모르는 키면 무시.</summary>
    void BringEventWindowToFront(string eventKey);

    /// <summary>호스트 창 테마("Light" · "Dark"). 기억했다가 재시작 복원 때도 먼저 보낸다.</summary>
    void SetTheme(string theme);

    /// <summary>PTZ. 같은 카메라의 밀린 명령은 최신 것 하나로 합쳐진다. 호스트가 없으면 버린다(순간 조작이므로 복원 안 함).</summary>
    void SendPtz(PtzCommand command);
}
