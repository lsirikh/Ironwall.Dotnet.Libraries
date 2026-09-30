namespace Ironwall.Dotnet.Libraries.CameraPopup;

/// <summary>팝업 호스트 상태(GIS 쪽 감시자가 보는 값).</summary>
public enum CameraPopupHostState
{
    /// <summary>아직 Start 하지 않음.</summary>
    NotStarted = 0,
    Starting = 1,
    /// <summary>연결 · 핸드셰이크 완료 — 명령이 호스트로 간다.</summary>
    Running = 2,
    /// <summary>죽음 · 무응답 감지 후 다시 띄우는 중. 명령은 기록됐다가 재시작 뒤 다시 보낸다.</summary>
    Restarting = 3,
    /// <summary>1분에 3번 넘게 죽어 재시작을 멈춤(FR-25). <c>Restart()</c> 로 다시 시작.</summary>
    Suspended = 4,
    /// <summary>호스트 실행 파일 없음 · 계약 판 불일치(FR-29). 팝업만 "사용할 수 없음".</summary>
    Unavailable = 5,
    Disposed = 6,
}
