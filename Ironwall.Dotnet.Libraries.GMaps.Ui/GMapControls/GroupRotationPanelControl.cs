using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 묶음 회전 컨트롤러 판(2026-10-18) — 선택 묶음의 회전 중심 · 각도를 보이고 바꾸고 확정/취소한다.
/// MapAnchorPanelControl(OverlayWindow 패턴) 답습 — Control + Themes/GroupRotationPanelStyle.xaml(ControlTemplate)
/// + 머리 띠 끌기(공용 OverlayHeaderDrag) + DynamicResource 토큰(라이트/다크).
/// 폼은 컨트롤 DataContext(=MapViewModel)에 바인딩. 입력칸(UpdateSourceTrigger=LostFocus)은 {Enter} 로도 바로 넣는다.
/// </summary>
public class GroupRotationPanelControl : Control
{
    static GroupRotationPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GroupRotationPanelControl),
            new FrameworkPropertyMetadata(typeof(GroupRotationPanelControl)));
    }

    public GroupRotationPanelControl()
    {
        Width = 300;
        AutomationProperties.SetAutomationId(this, "Map.GroupRotation.Panel");
        AutomationProperties.SetName(this, "묶음 회전");
        Helpers.OverlayHeaderDrag.Attach(this, 38);   // 머리 띠(38)만 끌기
        PreviewKeyDown += OnPreviewKeyDown;
    }

    #region - Dependency Properties -
    /// <summary>머리 띠 닫기(✕) 커맨드 — 묶음 회전에서는 취소(원위치).</summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand),
            typeof(GroupRotationPanelControl), new PropertyMetadata(null));
    #endregion

    /// <summary>커스텀 Control 은 peer 가 없어 AutomationId 가 UIA 트리에 안 나온다 — 판 루트를 찾을 수 있게 peer 를 둔다(ui-automation 규칙).</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    /// <summary>입력칸 {Enter} = 그 칸 값을 바로 넣기(칸을 떠나지 않아도). 판 밖 키(]/[ · Enter 확정)는 지도 키 처리기가 받는다.</summary>
    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.OriginalSource is not TextBox box) return;
        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        box.SelectAll();
        e.Handled = true;
    }
}
