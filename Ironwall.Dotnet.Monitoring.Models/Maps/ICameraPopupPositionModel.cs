using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Maps;

/// <summary>
/// 카메라별 RTSP 팝업의 마지막 위치(Geo 앵커) 영속 모델.
/// CameraId(=장비 Id) 단독 키, 다중 클라이언트 공유(DB).
/// </summary>
public interface ICameraPopupPositionModel : IBaseModel
{
    /// <summary>카메라 장비 Id (= PidsSymbol.LinkedDeviceId). 테이블 PK.</summary>
    int CameraId { get; set; }
    /// <summary>팝업 좌상단 코너의 위경도(앵커) — 위도.</summary>
    double Latitude { get; set; }
    /// <summary>팝업 좌상단 코너의 위경도(앵커) — 경도.</summary>
    double Longitude { get; set; }
    /// <summary>마지막 갱신 시각(last-write-wins).</summary>
    DateTime? UpdatedAt { get; set; }
}
