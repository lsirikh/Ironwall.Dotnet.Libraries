namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 독점 입력 모드(심볼배치·타겟조준·홈배치·앵커그리기·측정·라인드로잉) 판정 — 어도너 눌림 투과 게이트
                  (WPF/GMap 무의존, 헤드리스 단위 테스트 가능). C12 / D-19 동일 계열.
   Note         : 맵 컨트롤 자신의 OnMouseLeftButtonDown(GMapCustomControl.cs)은 이 모드들을 base 전에 가로채지만,
                  라벨·편집핸들·그룹선택 어도너는 맵의 형제(AdornerLayer)라 어도너가 히트/캡처하면 눌림이 맵에
                  도달하지 못한다. 세 어도너는 HitTestCore·OnMouseLeftButtonDown 서두에서 반드시 이 게이트 하나로 판정한다.
                  목록은 GMapMarkerBaseControl.OnMouseLeftButtonDown 의 D-19 가드와 동일 + IsMeasuring
                  (맵 OnMouseLeftButtonDown 이 base 전 가로채는 모드는 전부 포함).
   Created On   : 2026-09-08 · Sensorway Co., Ltd.
****************************************************************************/

/// <summary>독점 입력 모드 플래그 공급자 — <c>GMapCustomControl</c> 이 구현한다(모두 기존 public 속성).</summary>
public interface IExclusiveInputModeSource
{
    bool IsSymbolPlacementMode { get; }
    bool IsTargetAimMode { get; }
    bool IsHomePlacementMode { get; }
    bool IsAnchorDrawMode { get; }
    bool IsMeasuring { get; }
    bool IsLineDrawing { get; }
}

/// <summary>
/// 어도너 눌림 투과 판정. <see cref="IsActive"/> 가 true 면 어도너는 히트테스트를 투과(null)하고
/// 눌림을 소비·캡처하지 않는다 — 눌림은 아래 맵/마커 본체로 내려가 맵의 base-전 가로채기 분기가 받는다.
/// </summary>
public static class ExclusiveInputModeGate
{
    /// <summary>독점 입력 모드 하나라도 활성이면 true. 공급자 null(맵 미연결)은 false(기존 동작 유지).</summary>
    public static bool IsActive(IExclusiveInputModeSource? map)
        => map != null
           && (map.IsSymbolPlacementMode || map.IsTargetAimMode
               || map.IsHomePlacementMode || map.IsAnchorDrawMode
               || map.IsMeasuring || map.IsLineDrawing);
}
