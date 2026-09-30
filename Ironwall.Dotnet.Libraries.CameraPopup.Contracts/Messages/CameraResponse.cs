using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// 호스트 → GIS: <see cref="CameraRequest"/> 의 결과. 실패해도 호스트는 산다 — 실패는 그 팝업 · 타일에만 보인다(FR-26).
/// GIS 는 호스트가 없거나 재시작돼 응답을 못 받으면 같은 모양의 실패 응답을 스스로 만든다
/// (<see cref="CameraErrorCodes.HostUnavailable"/> · <see cref="CameraErrorCodes.HostRestarted"/> · <see cref="CameraErrorCodes.Timeout"/>).
/// </summary>
public sealed class CameraResponse : IIpcMessage
{
    public string RequestId { get; init; } = string.Empty;
    public CameraRequestKind Kind { get; init; }
    public string CameraId { get; init; } = string.Empty;

    public bool Success { get; init; }

    /// <summary>실패 사유(<see cref="CameraErrorCodes"/>). 성공이면 null.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>진단용 짧은 설명(계정 · 주소는 넣지 않는다).</summary>
    public string? Message { get; init; }

    /// <summary>PreparePtz — PTZ 가능(ONVIF PTZ 클라이언트 + GetNode 좌표 공간).</summary>
    public bool PtzCapable { get; init; }

    /// <summary>PreparePtz · GetImaging — 영상 옵션(주야간 · 포커스) 가능.</summary>
    public bool ImagingCapable { get; init; }

    /// <summary>GetPresets — 성공이면 빈 목록일 수 있다(프리셋 없음), 실패면 null.</summary>
    public List<CameraPreset>? Presets { get; init; }

    /// <summary>GetImaging — "ON" · "OFF" · "AUTO".</summary>
    public string? IrCutFilter { get; init; }

    /// <summary>GetImaging — 오토포커스 여부.</summary>
    public bool AutoFocus { get; init; }

    /// <summary>GIS 쪽에서 실패 응답을 만든다(호스트 없음 · 시간 초과 등).</summary>
    public static CameraResponse Fail(CameraRequest request, string errorCode, string? message = null) => new()
    {
        RequestId = request.RequestId,
        Kind = request.Kind,
        CameraId = request.CameraId,
        Success = false,
        ErrorCode = errorCode,
        Message = message,
    };

    public override string ToString()
        => $"{Kind} #{RequestId} cam={CameraId} ok={Success}{(ErrorCode is null ? "" : " err=" + ErrorCode)}";
}

/// <summary>카메라(ONVIF)에 저장된 프리셋 하나. 이름은 비어 있을 수 있다(표시 쪽이 토큰으로 대신한다).</summary>
public sealed class CameraPreset
{
    public string Token { get; init; } = string.Empty;
    public string? Name { get; init; }
}
