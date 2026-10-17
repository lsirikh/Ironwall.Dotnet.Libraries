using System;
using System.Windows;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties
{
    /// <summary>
    /// 멀티셀렉션(혼합 타입) 공통 속성창 — GMapPropertyBaseControl(추상)의 concrete 최소 구현.
    /// 공통 속성 편집 필드(BasePropertyStyle)를 그대로 쓰되, 그룹 Pending 모드(EnterGroupMode)가
    /// "전원 동일=값 / 서로 다름=빈칸(Pending)"으로 채운다(확정 스펙 — VS 속성그리드 방식).
    /// 값 입력 시 MapViewModel이 선택된 전 마커에 일괄 적용(캐시+DB)+배치 Undo.
    /// 동종 타입 멀티셀렉션은 이 창이 아니라 해당 타입 패널(같은 Pending 모드)이 뜬다.
    /// </summary>
    public class GMapPropertyCommonControl : GMapPropertyBaseControl
    {
        static GMapPropertyCommonControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyCommonControl),
                new FrameworkPropertyMetadata(typeof(GMapPropertyCommonControl)));
        }

        // 타입 특화 속성 없음 — 공통 속성(base DP)만 사용.
        protected override void SetupSpecificBindings() { }
        protected override void ClearSpecificBindings() { }
        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker) { }
        protected override void UpdateSpecificProperties() { }
        public override Type GetSupportedMarkerType() => typeof(IEditableMarker);
    }
}
