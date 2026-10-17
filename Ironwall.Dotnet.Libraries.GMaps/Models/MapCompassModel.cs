namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 방위각 심볼(나침반) 컨트롤 상태 영속(GMap_Compass_Control FR-08).
                  드래그 위치 + 우클릭 메뉴 설정을 AppSettings.MapCompass 한 모델로 저장.
   Note         : Size/Variant는 JSON 가독성·오타 내성 위해 string — 소비측(CompassMath)이
                  관대 파싱(실패=기본 M/Rose, 부팅 크래시 금지 NFR-04).
   Created On   : 2026-07-30 · Sensorway Co., Ltd.
 ****************************************************************************/
public class MapCompassModel
{
    /// <summary>Canvas 화면 X(맵 팬/줌 불변). NaN/미보존=기본 우상단 도킹(D-04).</summary>
    public double X { get; set; } = double.NaN;

    /// <summary>Canvas 화면 Y.</summary>
    public double Y { get; set; } = double.NaN;

    /// <summary>다이얼 크기: "S"(64) / "M"(96, 기본) / "L"(128).</summary>
    public string Size { get; set; } = "M";

    /// <summary>스타일 변형: "Rose"(택티컬 로즈, 기본) / "Ring"(미니멀 링).</summary>
    public string Variant { get; set; } = "Rose";

    /// <summary>각도 리드아웃 필 표시 여부.</summary>
    public bool ShowReadout { get; set; } = true;

    public override string ToString() => $"MapCompass[({X:F0},{Y:F0}), {Size}/{Variant}, readout={ShowReadout}]";
}
