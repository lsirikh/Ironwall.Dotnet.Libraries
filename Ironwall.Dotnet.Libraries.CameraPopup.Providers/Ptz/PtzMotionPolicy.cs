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

    /// <summary>나눠 보내는 끌기(화각 상대 공간)에서 앞 이동이 멈췄는지 묻는 간격.</summary>
    public static readonly TimeSpan StepPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>앞 이동이 이 시간 안에 안 멈추면 다음 몫을 그냥 보낸다(카메라가 MOVING 을 계속 말하는 경우의 상한).</summary>
    public static readonly TimeSpan StepIdleTimeout = TimeSpan.FromSeconds(4);

    /// <summary>MoveStatus 를 안 주는 카메라에서 한 몫(반 화면)이 끝났다고 보는 어림 시간.</summary>
    public static readonly TimeSpan StepEstimate = TimeSpan.FromMilliseconds(900);

    /// <summary>
    /// 부른 쪽이 기다리기를 그만둔 ONVIF 호출이 이만큼 넘게 돌고 있으면 카메라가 응답하지 않는 것으로 본다 — 그 연결을 끊고
    /// (다음 명령이 새로 붙는다) 카메라별 줄을 바로 푼다. 끊지 않으면 WCF 송신 제한(8초)까지 줄이 막혀, 카메라가 돌아온 뒤에도
    /// 첫 명령 · 팝업 다시 열기가 그만큼 늦었다(2026-10-22 가짜 카메라 실측 6.5~7초). 정상 호출은 수 ms ~ 수백 ms.
    /// </summary>
    public static readonly TimeSpan StallThreshold = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 부른 쪽 제한 시간이 없는 배경 호출(위치 다시 읽기 · 이동 유지 재전송 · 나눠 보내는 끌기)의 제한 시간. 넘으면
    /// <see cref="StallThreshold"/> 규칙으로 연결을 끊는다 — 준비 직후 위치 읽기가 끊긴 카메라에 걸려 줄을 8초 쥐던 것.
    /// </summary>
    public static readonly TimeSpan BackgroundCallTimeout = TimeSpan.FromSeconds(2);
}
