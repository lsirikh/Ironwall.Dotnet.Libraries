namespace Ironwall.Dotnet.Libraries.GMaps.Models;

public interface IGMapSetupModel
{
    HomePositionModel? HomePosition { get; set; }
    MapAnchorModel? MapAnchor { get; set; }
    /// <summary>지도 회전 상태 영속(나침반 ON/OFF + 회전각) — 사용자 요구 2026-07-28.</summary>
    MapRotationModel? MapRotation { get; set; }
    /// <summary>방위각 심볼(나침반 컨트롤) 위치+설정 영속 — GMap_Compass_Control FR-08.</summary>
    MapCompassModel? MapCompass { get; set; }
    /// <summary>강풍모드 인디케이터 위치+설정 — GMap_Map_Instruments FR-12.</summary>
    MapWindyIndicatorModel? MapWindyIndicator { get; set; }
    /// <summary>탐지·장애 인디케이터 위치+설정 — GMap_Map_Instruments FR-12.</summary>
    MapDetectionFaultModel? MapDetectionFault { get; set; }
    /// <summary>계기 보기(View) 메뉴 가시성 — GMap_Map_Instruments FR-16.</summary>
    MapInstrumentVisibilityModel? MapInstrumentVisibility { get; set; }
    string? MapMode { get; set; }
    string? MapName { get; set; }
    string? MapType { get; set; }
    /// <summary>기본맵(.mbtiles) 폴더 — MBTiles 사전정의 지도 스캔·로딩 위치 (MapData_Directory_Option).
    /// 기본맵은 수백 GB라 설치본에 포함할 수 없어 외부 폴더 지정이 필요하다.
    /// 비어있거나 폴더가 없으면 실행폴더\Datas 폴백(기존 동작 무회귀).
    /// 오버레이맵·오버레이 이미지는 이 설정과 무관(현행 유지).</summary>
    string? MapDataDirectory { get; set; }
}