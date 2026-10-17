using System.Windows;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Views;

/****************************************************************************
   Purpose      : 커널 다이얼로그 틀의 '취소'(ESC · 머리 ✕)를 창의 옛 [취소] 버튼(x:Name="ClickCancel")으로 잇는다 (B3)
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 틀(<c>ConsoleDialogFrame</c>)의 <c>SecondaryInvoked</c>(ESC · 머리 ✕ · 틀 [취소])를 창 자신의 취소 버튼으로 넘긴다.
/// </summary>
/// <remarks>
/// <para>계정 셀프서비스 창은 확인 · 취소를 <b>x:Name 버튼</b>(Caliburn 바인딩 지시자 · UI 시험 앵커)으로 가진다 — 틀의 버튼을 쓰면
/// 그 이름이 사라진다. 그래서 틀 버튼은 비우고, 닫는 길(ESC · ✕)만 그 버튼 하나로 모은다(닫는 길이 하나라 두 번 닫히지 않는다).</para>
/// <para><b>꺼진 버튼은 누르지 않는다</b> — 강제 로그인(<c>CanClickCancel=false</c>)에서 ESC 가 로그인 창을 닫으면 안 된다.
/// 버튼의 Click 을 올리므로 Caliburn 의 이름 관례 · <c>Message.Attach</c> 어느 쪽으로 이어져 있어도 같은 동작이 불린다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public static class DialogCancelRoute
{
    /// <summary>취소 버튼을 누른 것과 같게 한다. 눌렀으면 <c>true</c>, 없거나 꺼져 있거나 숨어 있으면 <c>false</c>.</summary>
    public static bool Invoke(ButtonBase? cancel)
    {
        if (cancel is null || !cancel.IsEnabled || cancel.Visibility != Visibility.Visible) return false;
        cancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, cancel));
        return true;
    }
}
