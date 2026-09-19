using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/// <summary>
/// 드롭존 겉모습 — 칩 · 레일 줄 · 빈 자리를 감싸면 끄는 동안 <b>형태</b>로 상태를 보인다:
/// 놓을 수 있음 = 파선 <c>{6,4}</c> 윤곽 / 놓을 수 없음 = 사선 해치 / 지금 그 위 = 굵은 윤곽.
/// </summary>
/// <remarks>
/// 색으로 구분하지 않는 이유: 라이트 테마에서 주색 · 선택색 · 포커스색이 같은 색이다.
/// 파선 <c>{4,3}</c> · <c>{5,3}</c> 은 그룹 선택 상자와 러버밴드가 이미 쓰고 있어 <c>{6,4}</c> 를 쓴다.
/// </remarks>
public class DropZoneChrome : ContentControl
{
    static DropZoneChrome()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DropZoneChrome), new FrameworkPropertyMetadata(typeof(DropZoneChrome)));
        FocusableProperty.OverrideMetadata(typeof(DropZoneChrome), new FrameworkPropertyMetadata(false));
    }

    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius), typeof(double), typeof(DropZoneChrome), new PropertyMetadata(4.0));
    public double CornerRadius { get => (double)GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
}
