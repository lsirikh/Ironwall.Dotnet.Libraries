using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 키를 누른 <b>순서와 무관한</b> 수정키 판정(펜스 보기 · 개념도) — 헤디드 r21: Alt 를 먼저 누르고 Shift 를 누른 Alt+Shift+→ 가 Alt+→(줄 안 한 칸)로
/// 처리됐다(Shift 를 먼저 누르면 다른 망으로 — 정상). Windows 의 Alt+Shift 입력 전환 단축키가 Shift 누름을 가로채 WPF 가 받은 수정키에서 Shift 가
/// 빠지는 것으로 본다(입력 언어는 바뀌지 않음 · 실기 재현으로 확인 대기). 그래서 키 이벤트의 수정키(<see cref="KeyboardDevice.Modifiers"/>) ·
/// 지금 수정키(<see cref="Keyboard.Modifiers"/>)를 합치고, Alt 가 눌린 키(<see cref="Key.System"/>)에서는 Shift 의 실제 상태(<c>GetAsyncKeyState</c>)도 본다.
/// </summary>
public static class FenceKeyModifiers
{
    private const int VK_SHIFT = 0x10;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>키 이벤트의 수정키 — 이벤트 장치 · 지금 장치를 합치고, Alt 키(<see cref="Key.System"/>)면 Shift 실제 상태를 더한다.</summary>
    public static ModifierKeys Of(KeyEventArgs e)
    {
        var reported = (e?.KeyboardDevice?.Modifiers ?? ModifierKeys.None) | Keyboard.Modifiers;
        var asyncShift = e is { Key: Key.System } && (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        return Combine(reported, asyncShift);
    }

    /// <summary>
    /// 순수 합치기(시험 대상) — WPF 가 알려 준 수정키에 Shift 의 실제 상태를 더한다. 실제로 눌려 있으면 알려 준 값에 Shift 가 없어도 Shift 다.
    /// </summary>
    public static ModifierKeys Combine(ModifierKeys reported, bool shiftActuallyDown)
        => shiftActuallyDown ? reported | ModifierKeys.Shift : reported;
}
