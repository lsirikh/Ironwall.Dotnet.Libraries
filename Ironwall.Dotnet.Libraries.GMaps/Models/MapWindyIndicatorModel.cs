namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 강풍모드(WINDY) 인디케이터 계기 위치+설정 영속 (GMap_Map_Instruments FR-12).
   Note         : DisplayStyle는 JSON 가독성·오타 내성 위해 string(소비측 InstrumentMath 관대 파싱,
                  실패=IconLabel). 가시성(보기 메뉴)은 별도 MapInstrumentVisibilityModel(D-12).
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
 ****************************************************************************/
public class MapWindyIndicatorModel
{
    /// <summary>Canvas 화면 X(맵 팬/줌 불변). NaN=기본 도킹(좌상단, D-11).</summary>
    public double X { get; set; } = double.NaN;

    /// <summary>Canvas 화면 Y.</summary>
    public double Y { get; set; } = double.NaN;

    /// <summary>표시 형태: "IconLabel"(기본) / "IconOnly".</summary>
    public string DisplayStyle { get; set; } = "IconLabel";

    /// <summary>평상(wind0) 숨김 — ON이면 이상 풍속일 때만 노출.</summary>
    public bool HideOnNormal { get; set; }

    public override string ToString() => $"MapWindy[({X:F0},{Y:F0}), {DisplayStyle}, hideNormal={HideOnNormal}]";
}
