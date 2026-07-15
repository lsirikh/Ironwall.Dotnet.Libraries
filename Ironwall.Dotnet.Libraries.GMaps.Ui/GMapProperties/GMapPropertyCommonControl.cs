using System;
using System.Windows;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties
{
    /// <summary>
    /// 멀티셀렉션(혼합 타입) 공통 속성창 — GMapPropertyBaseControl(추상)의 concrete 최소 구현.
    /// 타입 특화 속성 없이 공통 속성(이름/제목크기/색/선두께/크기/회전/표시/최소줌/Z순서)만 노출
    /// (HasSpecificProperties=false → 특화 영역 숨김). 스타일=CommonPropertyStyle(BasePropertyStyle 상속).
    /// 기능 ②(멀티셀렉트 공통속성): 서로 다른 타입을 함께 선택하면 이 기본 속성창을 띄운다.
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
