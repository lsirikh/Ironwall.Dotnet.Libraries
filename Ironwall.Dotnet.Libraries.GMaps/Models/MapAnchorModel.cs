using Ironwall.Dotnet.Libraries.Base.Models;
using Newtonsoft.Json;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 맵 앵커(사이트 고정) 설정 — 패닝 구역(BoundsOfMap)·최소줌 하한.
                  HomePosition과 동거하며 appsettings(AppSettings.MapAnchor)에 영속.
   Created By   : GHLee
   Created On   : 7/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
 ****************************************************************************/
public class MapAnchorModel : IMapAnchorModel
{
    #region - Ctors -
    /// <summary>기본 생성자 - Configuration Binding에서 사용.</summary>
    public MapAnchorModel()
    {
    }

    /// <summary>인터페이스 기반 복사 생성자.</summary>
    public MapAnchorModel(IMapAnchorModel source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        IsEnabled = source.IsEnabled;
        NorthWest = source.NorthWest;
        SouthEast = source.SouthEast;
        MinZoomFloor = source.MinZoomFloor;
        StrictContainment = source.StrictContainment;
    }
    #endregion
    #region - Properties -
    public bool IsEnabled { get; set; }
    public CoordinateModel? NorthWest { get; set; }
    public CoordinateModel? SouthEast { get; set; }
    public int MinZoomFloor { get; set; }
    public bool StrictContainment { get; set; }

    /// <summary>구역이 유효한지(활성 + 양 코너 존재).</summary>
    [JsonIgnore]
    public bool IsAvailable => IsEnabled && NorthWest != null && SouthEast != null;

    /// <summary>구역 중심 좌표(홈 자동세팅용). 미유효 시 null.</summary>
    [JsonIgnore]
    public CoordinateModel? Center => (NorthWest != null && SouthEast != null)
        ? new CoordinateModel(
            (NorthWest.Latitude + SouthEast.Latitude) / 2.0,
            (NorthWest.Longitude + SouthEast.Longitude) / 2.0,
            0.0)
        : null;
    #endregion
    #region - Overrides -
    public override string ToString()
        => $"Anchor[on={IsEnabled}, NW=({NorthWest?.Latitude},{NorthWest?.Longitude}), SE=({SouthEast?.Latitude},{SouthEast?.Longitude}), minZoom={MinZoomFloor}, strict={StrictContainment}]";
    #endregion
}
