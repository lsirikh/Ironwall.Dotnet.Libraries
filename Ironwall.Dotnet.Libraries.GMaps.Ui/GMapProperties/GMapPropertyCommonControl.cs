using System;
using System.Windows;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties
{
    /// <summary>
    /// 멀티셀렉션(혼합 타입) 속성창 = "빈 상태" 안내 전용 — GMapPropertyBaseControl(추상)의 concrete 최소 구현.
    /// 서로 다른 타입(군대부호+제어기, 이미지+심볼 등)은 속성이 서로 달라 하나의 값으로 표현할 수 없으므로
    /// 편집 필드 없이 그 사실만 안내한다(사용자 피드백 — 오적용 원천 차단). 스타일=CommonPropertyStyle(자체 템플릿).
    /// 동종 타입 멀티셀렉션은 이 창이 아니라 해당 타입 패널이 뜬다(PropertyPanelFactory.CreateCommonPropertyPanel).
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
