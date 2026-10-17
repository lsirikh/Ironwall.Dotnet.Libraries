using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Blocks;

/// <summary>
/// 팔레트의 <b>키보드 폴백</b>(FR-03) — 고른 칩에서 <c>Enter</c>(또는 <c>Space</c>)를 누르면 보드 끝에 더한다.
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — 드래그 전용 UI 는 내지 않는다. 키보드 길이 없으면 자동화가 좌표 클릭에 영구히 묶이고,
/// 좌표 드래그는 <c>DestructiveGuard</c>(AutomationId 기반)를 <b>구조적으로 우회</b>한다(드래그 규칙 §Must Never).
/// 회귀 단언은 이 경로로 잡는다 — UIA 에는 드래그 패턴 자체가 없다(.NET 8 WPF 에 <c>IDragProvider</c> 부재).</para>
/// <para>커널의 <c>ReorderKeyboardBehavior</c>(Alt+↑/↓)는 <b>같은 목록 안 이동</b>만 한다 — 목록을 건너
/// 더하는 길은 여기서 새로 낸다.</para>
/// <para><b>터널(<c>PreviewKeyDown</c>)</b> 에서 잡는다. 버블 <c>KeyDown</c> 은 목록 컨트롤이 먼저 먹는다(실측).
/// 실제로 <b>실행했을 때만</b> <c>Handled</c> 를 세운다 — 아무 일도 안 하고 이벤트를 삼키면
/// 기본 키 동작(선택 · 편집기 입력)이 조용히 죽는다.</para>
/// </remarks>
public sealed class PaletteAddKeyboardBehavior : Behavior<Selector>
{
    #region - Dependency properties -
    /// <summary>실행할 명령. 파라미터는 <b>그 항목</b>(팔레트의 유형 하나)이다.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(PaletteAddKeyboardBehavior), new PropertyMetadata(null));
    public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    #endregion

    #region - Pure decision (헤드리스로 단언하는 부분) -
    /// <summary>
    /// 이 키를 "보드에 더하기"로 볼 것인가. <b>순수 함수</b>다 — 시각트리 없이 단언한다.
    /// </summary>
    /// <param name="key">눌린 키.</param>
    /// <param name="isTextInputFocused">글 입력기(텍스트 상자 · 콤보 …)가 포커스를 쥐고 있는가. 쥐고 있으면 양보한다.</param>
    public static bool ShouldExecute(Key key, bool isTextInputFocused, ModifierKeys modifiers = ModifierKeys.None)
    {
        if (isTextInputFocused) return false;
        // Ctrl+Space · Shift+Space 는 목록의 여러 개 고르기다 — 가로채면 키보드로는 여러 개를 고를 수 없다.
        if (modifiers != ModifierKeys.None) return false;
        return key is Key.Enter or Key.Space;   // Key.Return 은 Key.Enter 와 같은 값이다
    }

    /// <summary>포커스를 쥔 요소가 글 입력기인가 — <c>Space</c> 는 글자이고 <c>Enter</c> 는 확정이라 양보한다.</summary>
    public static bool IsTextInputFocused(IInputElement? focused)
        => focused is TextBoxBase or PasswordBox or ComboBox or ComboBoxItem;
    #endregion

    #region - Processes -
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        base.OnDetaching();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (!ShouldExecute(e.Key, IsTextInputFocused(Keyboard.FocusedElement), Keyboard.Modifiers)) return;

        if (TryExecute(ResolveItem())) e.Handled = true;   // 실행했을 때만 삼킨다
    }

    /// <summary>
    /// 대상 항목을 정한다 — <b>포커스를 쥔 컨테이너</b>가 먼저고, 없으면 고른 항목이다.
    /// 인덱스 산술이나 <c>ContainerFromIndex</c> 는 쓰지 않는다(가상화에서 화면 밖은 <c>null</c>).
    /// </summary>
    private object? ResolveItem()
    {
        var list = AssociatedObject;
        if (list == null) return null;

        if (Keyboard.FocusedElement is DependencyObject focused)
        {
            var container = list.ContainerFromElement(focused);
            if (container != null)
            {
                var item = list.ItemContainerGenerator.ItemFromContainer(container);
                if (item != null && item != DependencyProperty.UnsetValue) return item;
            }
        }

        return list.SelectedItem;
    }

    /// <summary>명령을 실행한다. 대상 · 명령이 없거나 <c>CanExecute</c> 가 거절하면 아무 일도 안 한다.</summary>
    public bool TryExecute(object? item)
    {
        var command = Command;
        if (command == null || item == null) return false;
        if (!command.CanExecute(item)) return false;

        command.Execute(item);
        return true;
    }
    #endregion
}
