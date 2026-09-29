using System.Windows.Automation;
using System.Windows.Automation.Peers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/// <summary>
/// 장비 심볼(2D · 3D · 폴백 공통)의 UIA peer — 헤디드 시험이 지도 상태를 단언하는 입구.
/// </summary>
/// <remarks>
/// <para>종전엔 마커 컨트롤(커스텀 <c>Control</c>)에 peer 가 없어, 지도 아래 항목들이 전부 같은 문자열로만 보였다
/// (ui-automation 규칙: "지도 심볼 개별 식별 불가"). 이제 식별자는 <c>GMaps.Symbol.{장비종류}.{장비Id}</c>,
/// 이름은 <c>제목 · 이벤트 … · 장비 … · 문 … · 부품 …</c>, ItemStatus 는 이벤트 한글이다.</para>
/// <para><b>성능</b>: WPF 는 UIA 클라이언트가 물을 때만 peer 를 만든다. 자식은 모으지 않는다(<see cref="GetChildrenCore"/> = null) —
/// 템플릿 안 수십 개 요소 × 수백 개 아이콘을 걷지 않는다. 이름 · 상태는 물을 때 계산한다(캐시 없음 = 항상 현재값).</para>
/// <para><c>x:Name</c> 은 쓰지 않는다(CM 바인딩 지시자). XAML 에서 <c>AutomationProperties.AutomationId</c>/<c>Name</c> 을 명시하면 그것이 이긴다.</para>
/// </remarks>
public sealed class PidsMarkerAutomationPeer : FrameworkElementAutomationPeer
{
    private readonly GMapMarkerPidsControl _owner;

    public PidsMarkerAutomationPeer(GMapMarkerPidsControl owner) : base(owner) => _owner = owner;

    protected override string GetAutomationIdCore()
    {
        var explicitId = AutomationProperties.GetAutomationId(_owner);
        if (!string.IsNullOrEmpty(explicitId)) return explicitId;
        var marker = _owner.Marker;
        return SymbolStatusText.AutomationId(_owner.DeviceType, marker?.LinkedDeviceId ?? 0, marker?.Id ?? 0);
    }

    protected override string GetNameCore()
    {
        var explicitName = AutomationProperties.GetName(_owner);
        if (!string.IsNullOrEmpty(explicitName)) return explicitName;
        return SymbolStatusText.AutomationName(_owner.MarkerTitle, _owner.EventStatus, _owner.MarkerState, _owner.DoorIndicator, _owner.ComponentSummary);
    }

    protected override string GetItemStatusCore() => SymbolStatusText.EventText(_owner.EventStatus);

    protected override string GetHelpTextCore() => _owner.SymbolToolTip ?? string.Empty;

    protected override string GetClassNameCore() => _owner.GetType().Name;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

    protected override string GetLocalizedControlTypeCore() => "지도 심볼";

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    protected override List<AutomationPeer>? GetChildrenCore() => null;
}
