using System;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;

/// <summary>
/// 연속 이동(ContinuousMove) 안전 제한 · 유지 재전송 주기(PRD camera-popup-modes FR-22).
/// 카메라는 <see cref="MoveTimeout"/> 뒤 스스로 멈춘다 — GIS · 네트워크가 죽어 정지가 안 가도 최대 2초만 돈다(이전 PT10S).
/// 누르고 있는 동안은 <see cref="KeepAliveInterval"/> 마다 같은 속도를 다시 보내 2초 제한에 걸려 멈추지 않게 한다.
/// </summary>
public static class PtzMotionPolicy
{
    /// <summary>ContinuousMove Timeout(ISO 8601).</summary>
    public const string MoveTimeout = "PT2S";

    /// <summary>제한 시간(<see cref="MoveTimeout"/>)과 같은 값 — 시험 · 계산용.</summary>
    public static readonly TimeSpan MoveTimeoutSpan = TimeSpan.FromSeconds(2);

    /// <summary>누르는 동안 이동 재전송 주기 — 제한 시간의 절반(한 번 늦거나 잃어도 끊기지 않게).</summary>
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// 한 번 누름으로 이동을 유지하는 최대 시간. 뗌(정지)이 끝내 오지 않는 경우(GIS 화면 멈춤 · 뗌 이벤트 유실)에도
    /// 재전송이 여기서 끊기고, 카메라는 <see cref="MoveTimeout"/> 안에 스스로 멈춘다. 정상 조작은 이보다 훨씬 짧다.
    /// </summary>
    public static readonly TimeSpan MaxHoldDuration = TimeSpan.FromSeconds(60);

    /// <summary>정지 · 이동 뒤 위치(줌) 다시 읽기까지 기다리는 시간 — 드래그 경로 밖(배경)에서 읽는다.</summary>
    public static readonly TimeSpan StatusRefreshDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>카메라가 아직 움직이는 중이면 이 간격으로 다시 읽는다(최대 <see cref="StatusRefreshMaxAttempts"/> 번).</summary>
    public static readonly TimeSpan StatusRefreshRetry = TimeSpan.FromMilliseconds(700);

    public const int StatusRefreshMaxAttempts = 8;

    /// <summary>
    /// 기억한 위치 · 줌을 믿는 시간. 이보다 오래됐으면 드래그를 <b>보낸 뒤</b> 배경에서 다시 읽는다 — 다른 자리(NVR · 다른 GIS)가
    /// 줌을 바꿨어도 다음 드래그부터는 맞는 화각을 쓴다. 드래그를 보내기 전에는 절대 읽지 않는다(지연 없음).
    /// </summary>
    public static readonly TimeSpan StatusMaxAge = TimeSpan.FromSeconds(3);
}
