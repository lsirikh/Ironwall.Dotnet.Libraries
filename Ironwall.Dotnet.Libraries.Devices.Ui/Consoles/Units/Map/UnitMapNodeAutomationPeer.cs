using System;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 관계도 노드의 UIA peer — Thumb peer + SelectionItem(좌표 없이 고르기) (FR-40 · FR-41)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 관계도 노드 peer. <c>SelectionItem.Select()</c> 는 좌표 없이 <see cref="UnitMapNode.SelectRequestedEvent"/> 를 올린다 —
/// 자동화가 좌표 클릭에 묶이지 않게(DF "드래그 전용 UI 금지").
/// </summary>
/// <remarks>
/// <para>.NET 8 WPF 에는 UIA 드래그 패턴 타입이 없다(DF) — 끌기는 자동화로 단언하지 않고, 회귀는 키보드 폴백과
/// 이 peer 의 <c>Name</c> · <c>ItemStatus</c> 로 잡는다. 두 문자열은 캔버스가 순수 함수(<c>UnitMapText</c>)로 만들어 노드에 싣는다.</para>
/// <para>선택은 하나다(트리와 같은 <c>SelectedRow</c> — FR-02). 여럿 고르기는 <see cref="InvalidOperationException"/>.</para>
/// </remarks>
public sealed class UnitMapNodeAutomationPeer : ThumbAutomationPeer, ISelectionItemProvider
{
    public UnitMapNodeAutomationPeer(UnitMapNode owner) : base(owner) { }

    private UnitMapNode Node => (UnitMapNode)Owner;

    public override object? GetPattern(PatternInterface patternInterface)
        => patternInterface == PatternInterface.SelectionItem ? this : base.GetPattern(patternInterface);

    protected override string GetNameCore()
    {
        var name = Node.PeerName;
        return string.IsNullOrEmpty(name) ? base.GetNameCore() : name;
    }

    protected override string GetItemStatusCore()
    {
        var status = Node.PeerStatus;
        return string.IsNullOrEmpty(status) ? base.GetItemStatusCore() : status;
    }

    protected override string GetClassNameCore() => nameof(UnitMapNode);

    #region - ISelectionItemProvider -
    public bool IsSelected => Node.IsSelectedNode;

    public IRawElementProviderSimple? SelectionContainer
        => GetParent() is AutomationPeer parent ? ProviderFromPeer(parent) : null;

    public void Select() => Node.RequestSelect();

    public void AddToSelection() => throw new InvalidOperationException("관계도는 한 번에 한 부대만 고를 수 있습니다.");

    public void RemoveFromSelection()
    {
        if (Node.IsSelectedNode) throw new InvalidOperationException("선택을 비우려면 다른 부대를 고르거나 빈 곳을 누르세요.");
    }
    #endregion

    /// <summary>노드의 선택이 바뀌었다 — 듣는 쪽이 있을 때만 알린다.</summary>
    internal void RaiseSelectionChanged(bool oldValue, bool newValue)
    {
        if (!ListenerExists(AutomationEvents.PropertyChanged) && !ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected)) return;

        RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, oldValue, newValue);
        if (newValue) RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }
}
