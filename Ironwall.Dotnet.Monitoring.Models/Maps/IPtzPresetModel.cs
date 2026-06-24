using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

/// <summary>
/// 카메라 PTZ 프리셋 로컬 DB 영속 모델(CameraPopup_PTZ_Control).
/// pan/tilt/zoom 좌표 + space URI 보존(저장↔이동 round-trip, 미결②).
/// (CameraId, PresetName) 유니크 — 동일 이름 덮어쓰기. 카메라당 IsHome 정확히 1개.
/// </summary>
public interface IPtzPresetModel : IBaseModel
{
    /// <summary>카메라 장비 Id(= PidsSymbol.LinkedDeviceId).</summary>
    int CameraId { get; set; }
    /// <summary>프리셋 이름(카메라 내 유니크).</summary>
    string PresetName { get; set; }
    /// <summary>Home 프리셋 여부(카메라당 1개).</summary>
    bool IsHome { get; set; }
    double Pan { get; set; }
    double Tilt { get; set; }
    double Zoom { get; set; }
    /// <summary>AbsolutePanTiltPositionSpace URI(이동 시 동일 URI 사용).</summary>
    string? PanTiltSpace { get; set; }
    /// <summary>AbsoluteZoomPositionSpace URI.</summary>
    string? ZoomSpace { get; set; }
    DateTime? UpdatedAt { get; set; }
}
