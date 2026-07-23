using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Symbols;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/22/2025 6:09:29 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 지도의 GMap 심볼용 기본 모델
/// </summary>
public class SymbolModel : BaseModel, ISymbolModel
{
    #region - Ctors -
    public SymbolModel()
    {
        // 기본값 설정
        Id = 0;
        Pid = 0;
        Title = "Unknown";
        TitleSize = 15;
        OperationState = EnumOperationState.NONE;
        Latitude = 0.0;       // 순수 double 타입
        Longitude = 0.0;      // 순수 double 타입
        Altitude = 0;
        Width = 30;
        Height = 30;
        Bearing = 0;
        Category = EnumMarkerCategory.BASIC_SHAPES;
        ShowShape = true;
    }

    public SymbolModel(string title, double latitude, double longitude, double zoom)
    {
        Title = title;
        Latitude = latitude;
        Longitude = longitude;
        Zoom = zoom;

        // 기본값
        Pid = 0;
        OperationState = EnumOperationState.NONE;
        Altitude = 0;
        Width = 30;
        Height = 30;
        Bearing = 0;
        Category = EnumMarkerCategory.BASIC_SHAPES;
        ShowShape = true;
    }
    #endregion

    #region - 기본 식별 속성 -
    public int Pid { get; set; }
    public string Title { get; set; }
    public double TitleSize { get; set; }
    #endregion

    #region - 타입 및 상태 속성 -
    public EnumOperationState OperationState { get; set; }
    #endregion

    #region - 위치 및 방향 속성 -
    /// <summary>
    /// 위도 (편의 속성)
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// 경도 (편의 속성)
    /// </summary>
    public double Longitude { get; set; }
    /// <summary>
    /// 심볼 생성 유효 줌
    /// </summary>
    public double Zoom { get; set; }

    public float Altitude { get; set; }
    public double Bearing { get; set; }
    #endregion

    #region - 시각적 표현 속성 -
    public double Width { get; set; }
    public double Height { get; set; }
    public EnumMarkerCategory Category { get; set; }
    public bool ShowShape { get; set; }
    public bool ShowTitle { get; set; }
    /// <summary>레이어 마스터 가시성(조건2) — 레이어 패널 체크. false=심볼 전체(모양+Indicator+제목) 숨김.
    /// 속성창 ShowShape/ShowTitle(조건3)과 독립·상위 게이트. 기본 true(전 생성자·전 파생 impl 자동 커버).</summary>
    public bool Visible { get; set; } = true;
    /// <summary>잠금 상태 — true면 맵에서 클릭/선택 불가(레이어 패널에서만 토글). 기본 false.</summary>
    public bool IsLocked { get; set; }
    public EnumColorType FillColor { get; set; } = EnumColorType.Blue;
    public EnumColorType StrokeColor { get; set; } = EnumColorType.White;
    public double StrokeThickness { get; set; } = 1.0;
    #endregion

    #region - 레이어 순서 -
    public int ZOrder { get; set; } = 10;
    #endregion

    #region - 라벨 오프셋 (Symbol_Label_Decouple) -
    /// <summary>라벨 상대 오프셋 X(화면 픽셀, 아이콘 중심 기준). 0=기본위치.</summary>
    public double LabelOffsetX { get; set; }
    /// <summary>라벨 상대 오프셋 Y(화면 픽셀, 아이콘 중심 기준). 0=기본위치.</summary>
    public double LabelOffsetY { get; set; }
    #endregion

    #region - 라벨 스타일 (Overlay_Title_ZoomStyle FR-05·13) -
    // 기본값=종전 LabelAdorner 하드코딩과 시각 동일(NFR-01 무변화 업그레이드). DB DEFAULT와 삼위일치.
    public int TitleColor { get; set; } = unchecked((int)0xF0F0F4F8);
    public int TitleBackground { get; set; } = unchecked((int)0xCD1C1E22);
    public string TitleFontFamily { get; set; } = string.Empty;   // 빈값=Segoe UI
    public bool TitleBold { get; set; }
    public bool TitleItalic { get; set; }
    public double TitleMaxWidth { get; set; } = 200.0;
    #endregion

}