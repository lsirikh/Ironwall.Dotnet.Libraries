using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 지도 조립 카드(L3, component-display-unify FR-05) — <c>Themes/ComponentCardStyle.xaml</c> 템플릿.
/// DataContext = <see cref="ComponentCardViewModel"/>. 어도너(<c>ComponentCardAdorner</c>)가 아이콘 옆에 띄운다.
/// </summary>
/// <remarks>
/// 이 컨트롤은 끌어 옮기지 않는다(맵 위 새 드래그 금지 — drag-first-ux.md). 카드 위 마우스 누름은 카드가 소비해
/// 아래 지도로 새지 않는다(카드 안을 눌렀는데 지도가 팬 · 빈 곳 클릭으로 카드를 닫는 일이 없게).
/// </remarks>
public class ComponentCardControl : Control
{
    static ComponentCardControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ComponentCardControl),
            new FrameworkPropertyMetadata(typeof(ComponentCardControl)));
    }

    /// <summary>Esc — 카드에 포커스가 있을 때도 닫힌다(지도에 포커스가 있을 때는 서비스가 받는다).</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && DataContext is ComponentCardViewModel vm)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>카드 빈 곳 누름은 여기서 끝낸다 — 단추는 자기 클릭을 먼저 처리한다(버블 단계라 가로채지 않는다).</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);
}
