using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

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

    // ── 라벨 스타일 (FR-05·13 v2.5) — ISymbolModel과 동일 규약(EnumColorType, FillColor 파이프라인 재사용). ──
    /// <summary>라벨 글자색(기본 White).</summary>
    EnumColorType TitleColor { get; set; }
    /// <summary>라벨 배경(칩)색 — 렌더 시 α 0xCD 합성, Transparent=배경 없음. 기본 Black.</summary>
    EnumColorType TitleBackground { get; set; }
    /// <summary>라벨 폰트 패밀리 — 빈값=Segoe UI.</summary>
    string TitleFontFamily { get; set; }
    /// <summary>라벨 굵게.</summary>
    bool TitleBold { get; set; }
    /// <summary>라벨 이탤릭.</summary>
    bool TitleItalic { get; set; }
    /// <summary>라벨 최대 폭(px·DIP, 말줄임 지점) — WYSIWYG 폭 조절(FR-13).</summary>
    double TitleMaxWidth { get; set; }

    /// <summary>
    /// 런타임 Z-Order 홀더. MapLayers.ZOrder에서 주입받음 (DB 컬럼 없음).
    /// </summary>
    int ZOrder { get; set; }
}