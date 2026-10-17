using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 맵 앵커(사이트 고정) 설정 인터페이스 — 패닝 구역·최소줌 제한
   Created By   : GHLee
   Created On   : 7/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
 ****************************************************************************/
public interface IMapAnchorModel
{
    /// <summary>앵커(사이트 고정) 활성 여부.</summary>
    bool IsEnabled { get; set; }
    /// <summary>앵커 구역 북서(좌상) 좌표.</summary>
    CoordinateModel? NorthWest { get; set; }
    /// <summary>앵커 구역 남동(우하) 좌표.</summary>
    CoordinateModel? SouthEast { get; set; }
    /// <summary>최소 줌 하한(이보다 축소 불가). 0이면 미설정.</summary>
    int MinZoomFloor { get; set; }
    /// <summary>엄격 컨테인먼트(뷰포트 inset로 화면밖 완전차단) 여부.</summary>
    bool StrictContainment { get; set; }
    /// <summary>[Rotation V-06 옵션C] 앵커 활성 중 회전 허용 — false(기본)=현행(활성 시 정북 강제+회전 잠금),
    /// true=회전 유지 잠금(가두기 inset은 회전 화면의 외접 bbox 기준 — FR-08 ViewArea가 공급).</summary>
    bool AllowRotation { get; set; }
}
