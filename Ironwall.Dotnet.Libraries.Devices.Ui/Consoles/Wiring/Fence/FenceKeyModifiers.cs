using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>
/// 키를 누른 <b>순서와 무관한</b> 수정키 판정(펜스 보기 · 개념도) — 헤디드 r21: Alt 를 먼저 누르고 Shift 를 누른 Alt+Shift+→ 가 Alt+→(줄 안 한 칸)로
/// 처리됐다(Shift 를 먼저 누르면 다른 망으로 — 정상). Windows 의 Alt+Shift 입력 전환 단축키가 Shift 누름을 가로채 WPF 가 받은 수정키에서 Shift 가
/// 빠지는 것으로 본다(입력 언어는 바뀌지 않음 · 실기 재현으로 확인 대기). 그래서 키 이벤트의 수정키(<see cref="KeyboardDevice.Modifiers"/>) ·
/// 지금 수정키(<see cref="Keyboard.Modifiers"/>)를 합치고, Alt 가 눌린 키(<see cref="Key.System"/>)에서는 Shift 의 실제 상태(<c>GetAsyncKeyState</c>)도 본다.
/// <para><b>NumLock 가짜 Shift 떼기</b>(2026-10-13 실창 3회차 MNT-R28-216 · WIR-R27-135): 확장 키 표지 없이 들어온 화살표(숫자판 화살표 · FlaUI 처럼
/// 가상 키 코드로 넣은 화살표)는 NumLock 이 켜져 있으면 Windows 가 Shift 를 <b>잠깐 뗀 것처럼</b> Shift 떼기를 화살표 바로 앞에 끼워 넣는다 —
/// 그래서 화살표가 올 때 Shift 가 어느 수정키 상태에도 없다(Shift+글자는 정상 · Shift+화살표만 빠짐 · 실측). 그 Shift 떼기는 화살표와 <b>같은 시각</b>에 온다
/// (사람이 Shift 를 떼고 1 ms 안에 화살표를 누를 수는 없다) — <see cref="NoteKeyUp"/> 가 적어 둔 Shift 떼기 시각이 화살표 시각과 같으면 Shift 로 본다.</para>
/// </summary>
public static class FenceKeyModifiers
{
    private const int VK_SHIFT = 0x10;

    /// <summary>마지막 Shift 떼기 시각(<see cref="KeyEventArgs.Timestamp"/>) — 입력은 UI 스레드마다라 스레드마다 둔다.</summary>
    [ThreadStatic] private static int? _shiftUpAt;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    /// <summary>키 이벤트의 수정키 — 이벤트 장치 · 지금 장치를 합치고, Alt 키(<see cref="Key.System"/>)면 Shift 실제 상태를, 화살표면 가짜 Shift 떼기를 더한다.</summary>
    public static ModifierKeys Of(KeyEventArgs e)
    {
        var reported = (e?.KeyboardDevice?.Modifiers ?? ModifierKeys.None) | Keyboard.Modifiers;
        var asyncShift = e is { Key: Key.System } && (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        var fakeShift = e is not null && IsNumpadFakeShift(e.Key == Key.System ? e.SystemKey : e.Key, e.Timestamp, _shiftUpAt);
        return Combine(reported, asyncShift || fakeShift);
    }

    /// <summary>키 떼기를 본다 — Shift 떼기면 그 시각을 적는다(Alt 가 눌려 있으면 <see cref="Key.System"/> + <see cref="KeyEventArgs.SystemKey"/> 로 온다).</summary>
    public static void NoteKeyUp(KeyEventArgs e)
    {
        if ((e.Key == Key.System ? e.SystemKey : e.Key) is Key.LeftShift or Key.RightShift) _shiftUpAt = e.Timestamp;
    }

    /// <summary>
    /// 순수 판정(시험 대상) — 숫자판 자리의 이동 키(화살표 · Home · End · PageUp · PageDown · Insert · Delete)인데 바로 앞 Shift 떼기가 같은 시각(1 ms 안)이면
    /// Windows 가 NumLock 때문에 끼워 넣은 가짜 Shift 떼기다 → Shift 가 눌린 채다.
    /// <para>"같은 시각" 은 시계 한 눈금(<see cref="FAKE_SHIFT_WINDOW_MS"/>)까지 — 사건 시각(GetMessageTime)은 약 15.6 ms 눈금이라 두 사건이 눈금 경계를 걸치면
    /// 15~16 ms 로 벌어진다(실창 4회차 WIR-R27-135: Shift+Alt+↑ 는 놓치고 바로 뒤 Shift+Alt+↓ 는 잡았다 — 눈금 걸침 여부가 갈랐다).</para>
    /// </summary>
    public static bool IsNumpadFakeShift(Key key, int keyTimestamp, int? shiftUpTimestamp)
        => key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Insert or Key.Delete
           && shiftUpTimestamp is { } up && keyTimestamp - up is >= 0 and <= FAKE_SHIFT_WINDOW_MS;

    /// <summary>가짜 Shift 떼기로 보는 시각 차(ms) — 시계 한 눈금(약 15.6 ms). 사람이 Shift 를 떼고 이 안에 화살표를 누를 수는 없다.</summary>
    public const int FAKE_SHIFT_WINDOW_MS = 16;

    /// <summary>
    /// 순수 합치기(시험 대상) — WPF 가 알려 준 수정키에 Shift 의 실제 상태를 더한다. 실제로 눌려 있으면 알려 준 값에 Shift 가 없어도 Shift 다.
    /// </summary>
    public static ModifierKeys Combine(ModifierKeys reported, bool shiftActuallyDown)
        => shiftActuallyDown ? reported | ModifierKeys.Shift : reported;
}
