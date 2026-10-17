namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>프리셋 하나(우클릭 "프리셋 이동 ▸" 목록).</summary>
internal sealed record TilePreset(string Token, string? Name)
{
    public string Display => string.IsNullOrWhiteSpace(Name) ? Token : Name!;
}
