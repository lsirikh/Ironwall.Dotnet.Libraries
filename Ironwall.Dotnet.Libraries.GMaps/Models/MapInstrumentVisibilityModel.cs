namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 지도 계기(나침반·강풍·탐지장애·중앙십자선) 보기(View) 메뉴 가시성 영속
                  (GMap_Map_Instruments D-12/FR-16). 위치·설정과 분리된 교차 관심사.
   Note         : 나침반 가시성은 회전 기능(RotationFeature)과 무관하게 이 값이 최종 결정(FR-17).
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
 ****************************************************************************/
public class MapInstrumentVisibilityModel
{
    /// <summary>방위각(나침반) 표시(회전 기능 OFF여도 이 값이 마스터, FR-17).</summary>
    public bool Compass { get; set; } = true;

    /// <summary>강풍모드 인디케이터 표시.</summary>
    public bool Windy { get; set; } = true;

    /// <summary>탐지·장애 상태 인디케이터 표시.</summary>
    public bool DetectionFault { get; set; } = true;

    /// <summary>중앙 십자선(IsCenterCrosshairVisible) 표시.</summary>
    public bool Crosshair { get; set; }

    public override string ToString()
        => $"MapInstrumentVis[compass={Compass}, windy={Windy}, detFault={DetectionFault}, crosshair={Crosshair}]";
}
