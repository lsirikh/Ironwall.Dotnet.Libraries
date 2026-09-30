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
}
