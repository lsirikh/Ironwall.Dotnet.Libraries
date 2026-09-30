using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// GIS → 호스트: 결과가 필요한 카메라 조작(FR-17 · T-02). 호스트는 같은 <see cref="RequestId"/> 로
/// <see cref="CameraResponse"/> 를 돌려준다. GIS 는 응답을 비동기로 기다리고(UI · NATS 줄을 막지 않는다),
/// 제한 시간이 지나거나 호스트가 재시작되면 실패로 끝낸다. 호스트는 요청마다 <see cref="TimeoutMs"/> 를 지킨다(FR-26).
/// </summary>
public sealed class CameraRequest : IIpcMessage
{
    public string RequestId { get; init; } = string.Empty;
    public CameraRequestKind Kind { get; init; }
    public string CameraId { get; init; } = string.Empty;

    /// <summary>제공자 · 접속 정보(계정 포함 — 같은 사용자 전용 파이프로만 오간다, ToString 은 가린다).</summary>
    public VideoProviderInfo Provider { get; init; } = new();

    /// <summary>GotoPreset · RemovePreset 의 프리셋 토큰.</summary>
    public string? PresetToken { get; init; }

    /// <summary>SetPreset 의 새 이름.</summary>
    public string? PresetName { get; init; }

    /// <summary>SetIrCutFilter 값("ON" 주간 · "OFF" 야간 · "AUTO").</summary>
    public string? IrCutFilter { get; init; }

    /// <summary>SetAutoFocus 값.</summary>
    public bool AutoFocus { get; init; }

    /// <summary>호스트 쪽 제한 시간(ms). 0 이하이면 호스트 기본값.</summary>
    public int TimeoutMs { get; init; }

    public override string ToString() => $"{Kind} #{RequestId} cam={CameraId} {Provider}";
}
