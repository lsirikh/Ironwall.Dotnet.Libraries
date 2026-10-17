using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 관계도 캔버스의 UIA peer — 자식 = 노드 peer, Selection 패턴 (FR-40 · FR-41)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 관계도 캔버스 peer. 자식은 <b>노드 peer 만</b>이다 — 선 층(<c>DrawingVisual</c>)과 격자는 peer 가 없고,
/// 오버레이(HUD · 막대 · 확인)는 Phase 3 에서 자기 peer 로 더한다.
/// </summary>
/// <remarks>노드 목록은 캔버스가 넘긴다(<paramref name="nodes"/>) — 이 peer 는 캔버스 구현을 모른다.</remarks>
public sealed class UnitMapCanvasAutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
    private readonly Func<IEnumerable<UnitMapNode>> _nodes;
    private readonly Func<IEnumerable<UIElement>>? _extras;

    /// <param name="owner">캔버스.</param>
    /// <param name="nodes">지금 떠 있는 노드들(그림 순서).</param>
    /// <param name="extras">노드 뒤에 붙일 다른 자식(HUD 단추 등 — 없으면 <c>null</c>).</param>
    public UnitMapCanvasAutomationPeer(FrameworkElement owner, Func<IEnumerable<UnitMapNode>> nodes, Func<IEnumerable<UIElement>>? extras = null)
        : base(owner)
    {
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _extras = extras;
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

    protected override string GetClassNameCore() => Owner.GetType().Name;

    public override object? GetPattern(PatternInterface patternInterface)
        => patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var children = new List<AutomationPeer>();
        foreach (var node in _nodes())
            if (UIElementAutomationPeer.CreatePeerForElement(node) is { } peer) children.Add(peer);

        if (_extras is not null)
        {
            foreach (var element in _extras())
            {
                if (UIElementAutomationPeer.CreatePeerForElement(element) is { } peer) { children.Add(peer); continue; }
                // peer 가 없는 틀(Border · Grid)이면 그 안의 peer 들을 올린다.
                var inner = new FrameworkElementAutomationPeer((FrameworkElement)element).GetChildren();
                if (inner is not null) children.AddRange(inner);
            }
        }

        return children.Count > 0 ? children : null;
    }

    #region - ISelectionProvider -
    public bool CanSelectMultiple => false;

    public bool IsSelectionRequired => false;

    /// <summary>지금 고른 노드(하나 이하).</summary>
    public IEnumerable<UnitMapNode> SelectedNodes() => _nodes().Where(n => n.IsSelectedNode);

    public IRawElementProviderSimple[] GetSelection()
        => SelectedNodes()
                   .Select(n => UIElementAutomationPeer.CreatePeerForElement(n))
                   .Where(p => p is not null)
                   .Select(p => ProviderFromPeer(p!))
                   .Where(provider => provider is not null)        // 화면 원본이 없으면(헤드리스) 공급자가 없다
                   .ToArray();
    #endregion
}
