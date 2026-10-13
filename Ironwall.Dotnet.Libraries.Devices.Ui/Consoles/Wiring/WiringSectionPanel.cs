using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 속성 칸의 절(예: "설치 위치") — 자동화(UIA)에 <b>묶음(Group)</b> 으로 나온다. 그냥 <see cref="StackPanel"/> 은 peer 가 없어
/// AutomationId 를 달아도 트리에 뜨지 않았다(2026-10-13 실창 4회차 MNT-R28-400 "설치 위치 절 없음" — 절 안 칸 · 알림 글도 못 찾음).
/// </summary>
public class WiringSectionPanel : StackPanel
{
    protected override AutomationPeer OnCreateAutomationPeer() => new WiringSectionPanelAutomationPeer(this);

    private sealed class WiringSectionPanelAutomationPeer : FrameworkElementAutomationPeer
    {
        public WiringSectionPanelAutomationPeer(WiringSectionPanel owner) : base(owner) { }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
        protected override string GetClassNameCore() => nameof(WiringSectionPanel);
        protected override bool IsControlElementCore() => true;
    }
}
