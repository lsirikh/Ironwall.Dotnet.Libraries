using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

/// <summary>GIS → 호스트: 테마 전환. 열린 창 전부가 새 토큰으로 다시 그려진다(NFR-04).</summary>
public sealed class SetTheme : IIpcMessage
{
    /// <summary>"Light" · "Dark".</summary>
    public string Theme { get; init; } = "Dark";
}
