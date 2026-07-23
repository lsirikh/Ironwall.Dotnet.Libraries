using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
public interface ISymbolModel : IBaseModel
{
    int Pid { get; set; }
    string Title { get; set; }
    double TitleSize { get; set; }
    double Height { get; set; }
    double Width { get; set; }
    double Latitude { get; set; }
    double Longitude { get; set; }
    double Zoom { get; set; }
    float Altitude { get; set; }
    double Bearing { get; set; }
    bool ShowShape { get; set; }
    bool ShowTitle { get; set; }
    /// <summary>레이어 마스터 가시성(조건2) — 레이어 패널의 심볼 Visibility 체크. false면 심볼 전체(모양+Indicator+제목) 숨김.
    /// 속성창의 ShowShape(모양)·ShowTitle(제목)(조건3)과 독립하며 그 상위 게이트. 기본 true.</summary>
    bool Visible { get; set; }
    bool IsLocked { get; set; }
    EnumColorType FillColor { get; set; }
    EnumColorType StrokeColor { get; set; }
    double StrokeThickness { get; set; }
    EnumMarkerCategory Category { get; set; }
    EnumOperationState OperationState { get; set; }
    int ZOrder { get; set; }

    /// <summary>라벨(제목) 상대 오프셋 X(화면 픽셀, 아이콘 중심 기준). 0=기본위치. 라벨 분리·드래그 영속(Symbol_Label_Decouple FR-LB-01).</summary>
    double LabelOffsetX { get; set; }
    /// <summary>라벨(제목) 상대 오프셋 Y(화면 픽셀, 아이콘 중심 기준). 0=기본위치.</summary>
    double LabelOffsetY { get; set; }

    // ── 라벨 스타일 (Overlay_Title_ZoomStyle FR-05·13). 기본값=종전 하드코딩 렌더와 시각 동일(NFR-01). ──
    /// <summary>라벨 글자색 — packed ARGB int. 기본 0xF0F0F4F8(종전 하드코딩).</summary>
    int TitleColor { get; set; }
    /// <summary>라벨 배경(칩)색 — packed ARGB int, 0=완전투명. 기본 0xCD1C1E22(종전 하드코딩).</summary>
    int TitleBackground { get; set; }
    /// <summary>라벨 폰트 패밀리 — 빈값=Segoe UI(종전 하드코딩). 미설치 폰트는 렌더 시 폴백.</summary>
    string TitleFontFamily { get; set; }
    /// <summary>라벨 굵게.</summary>
    bool TitleBold { get; set; }
    /// <summary>라벨 이탤릭.</summary>
    bool TitleItalic { get; set; }
    /// <summary>라벨 최대 폭(px·DIP, 말줄임 지점). 기본 200(종전 하드코딩 MaxTextWidth). WYSIWYG 폭 조절(FR-13).</summary>
    double TitleMaxWidth { get; set; }
}