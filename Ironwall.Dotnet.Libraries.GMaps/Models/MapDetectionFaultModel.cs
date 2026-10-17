namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 탐지·장애 상태 인디케이터 계기 위치+설정 영속 (GMap_Map_Instruments FR-12).
   Note         : Orientation은 string 관대 파싱(실패=Vertical). 가시성(보기 메뉴)은 별도
                  MapInstrumentVisibilityModel(D-12). 카운트 자체는 영속 대상 아님(런타임 EQM 소스, D-01).
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
 ****************************************************************************/
public class MapDetectionFaultModel
{
    /// <summary>Canvas 화면 X. NaN=기본 도킹(좌측, 강풍 아래, D-11).</summary>
    public double X { get; set; } = double.NaN;

    /// <summary>Canvas 화면 Y.</summary>
    public double Y { get; set; } = double.NaN;

    /// <summary>배치: "Vertical"(기본) / "Horizontal".</summary>
    public string Orientation { get; set; } = "Vertical";

    /// <summary>0건 pill 숨김(각 종류 0이면 해당 pill 숨김).</summary>
    public bool HideOnZero { get; set; }

    public override string ToString() => $"MapDetFault[({X:F0},{Y:F0}), {Orientation}, hideZero={HideOnZero}]";
}
