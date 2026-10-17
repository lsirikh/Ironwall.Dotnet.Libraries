using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>
/// GIS → 호스트: 수동 포커스 누름 · 뗌(보내고 잊기, FR-28). 포커스 모터는 PTZ 와 다른 경로라
/// 합치기 키도 따로다(<c>focus:{카메라}</c>) — 팝업 닫기의 PTZ 정지 · 포커스 정지가 서로를 지우지 않게.
/// </summary>
public sealed class PtzFocusCommand : IIpcMessage
{
    public string CameraId { get; init; } = string.Empty;

    /// <summary>+1 원경(far) · -1 근경(near) · 0 정지.</summary>
    public int Direction { get; init; }

    /// <summary>호스트가 이 카메라를 아직 준비하지 않았을 때(재시작 직후 등) 쓸 접속 정보.</summary>
    public VideoProviderInfo? Provider { get; init; }
}
