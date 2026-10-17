using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 노드의 끄는 동안 대상 표시 — 보일 때만 실체화한다 (FR-30 · NFR-01)
   Created By   : GHLee
   Created On   : 9/29/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 끄는 동안의 대상 표시(막힘 해치 · 후보 파선 · 머묾 굵은 윤곽 · 인접 연결 표지)를 담는 자리. 모양은 스타일(<c>UnitMapStyles.xaml</c>)의
/// 템플릿이 그대로 가진다 — 옛 노드 템플릿에 직접 있던 요소들을 이 템플릿으로 옮겼을 뿐 값은 같다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>(2026-09-29 NFR-01 분해): 이 요소들(사각형 3 · 캔버스 1 · 원 3, 트리거 바인딩 4, 해치 <c>DrawingBrush</c> 하나)은 끄는 동안에만 보이는데
/// 노드 200개 모두가 템플릿을 입을 때마다 만들어졌다. <c>DropState</c> 가 무언가를 보이는 값일 때만 <see cref="UIElement.Visibility"/> 가
/// <c>Visible</c> 이 되고, 접힌(<c>Collapsed</c>) 컨트롤은 재지 않으므로 템플릿도 그때 처음 입는다.</para>
/// <para>자동화 peer 는 만들지 않는다 — 옛 요소들(사각형 · 원)도 peer 가 없었다. 노드 peer 의 자식 목록이 그대로다.</para>
/// </remarks>
public sealed class UnitMapNodeDropDecor : Control
{
    static UnitMapNodeDropDecor()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(UnitMapNodeDropDecor), new FrameworkPropertyMetadata(typeof(UnitMapNodeDropDecor)));
        FocusableProperty.OverrideMetadata(typeof(UnitMapNodeDropDecor), new FrameworkPropertyMetadata(false));
        IsHitTestVisibleProperty.OverrideMetadata(typeof(UnitMapNodeDropDecor), new UIPropertyMetadata(false));
    }

    public static readonly DependencyProperty DropStateProperty = DependencyProperty.Register(
        nameof(DropState), typeof(UnitMapNodeDropState), typeof(UnitMapNodeDropDecor), new PropertyMetadata(UnitMapNodeDropState.None));

    /// <summary>노드의 대상 표시 상태(템플릿 바인딩) — 템플릿 안 표시 요소들의 트리거가 이것을 읽는다.</summary>
    public UnitMapNodeDropState DropState { get => (UnitMapNodeDropState)GetValue(DropStateProperty); set => SetValue(DropStateProperty, value); }

    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}
