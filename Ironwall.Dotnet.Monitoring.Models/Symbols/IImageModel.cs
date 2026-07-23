using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;

public interface IImageModel : IBaseModel
{
    float Altitude { get; set; }
    double Bottom { get; set; }
    string? CoordinateSystem { get; set; }
    string FilePath { get; set; }
    bool HasGeoReference { get; set; }
    double Height { get; set; }
    double Latitude { get; set; }
    double Left { get; set; }
    double Longitude { get; set; }
    double Opacity { get; set; }
    double Right { get; set; }
    double Rotation { get; set; }
    string? Title { get; set; }
    double Top { get; set; }
    bool Visibility { get; set; }
    /// <summary>잠금 상태 — true면 맵에서 클릭/선택 불가(레이어 패널에서만 토글). 기본 false.</summary>
    bool IsLocked { get; set; }
    double Width { get; set; }

    /// <summary>
    /// 이미지가 표시되는 최소 줌 레벨.
    /// 지도 줌이 이 값 이상일 때만 이미지가 표시됨.
    /// </summary>
    double Zoom { get; set; }

    /// <summary>라벨 글자 크기(pt). 기존 마커 인메모리 기본 11 유지 (Overlay_Title_ZoomStyle FR-10).</summary>
    double TitleSize { get; set; }
    /// <summary>라벨(제목) 표시 여부 — 이전엔 비영속 인메모리 필드였음 (FR-10).</summary>
    bool ShowTitle { get; set; }
    /// <summary>라벨 오프셋 U — 시각 footprint 하프익스텐트(가로) 대비 비율. px 아님 (FR-02 정규화 도메인).</summary>
    double LabelOffsetU { get; set; }
    /// <summary>라벨 오프셋 V — 하프익스텐트(세로) 대비 비율 (FR-02).</summary>
    double LabelOffsetV { get; set; }

    /// <summary>
    /// 런타임 Z-Order 홀더. MapLayers.ZOrder에서 주입받음 (DB 컬럼 없음).
    /// </summary>
    int ZOrder { get; set; }
}