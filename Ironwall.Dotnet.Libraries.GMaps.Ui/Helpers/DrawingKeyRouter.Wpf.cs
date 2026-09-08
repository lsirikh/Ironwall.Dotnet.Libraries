using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/// <summary>
/// <see cref="DrawingKeyRouter"/> 의 WPF 어댑터 — <c>Key</c>/<c>ModifierKeys</c> 를 순수 열거형으로 사상한다.
/// 순수 판정(<c>DrawingKeyRouter.cs</c>)은 tests/GMaps.Ui.Tests 에 소스링크되므로 WPF 참조는 이 파일에만 둔다.
/// </summary>
public static partial class DrawingKeyRouter
{
    /// <summary>WPF 키 → 라우터 키. 목록 밖 키는 <see cref="DrawingKey.Other"/>(항상 PassThrough).</summary>
    public static DrawingKey FromWpfKey(Key key) => key switch
    {
        Key.Escape => DrawingKey.Escape,
        Key.Enter => DrawingKey.Enter,
        Key.Back => DrawingKey.Back,
        Key.Z => DrawingKey.Z,
        Key.Y => DrawingKey.Y,
        Key.Delete => DrawingKey.Delete,
        Key.Left => DrawingKey.Left,
        Key.Right => DrawingKey.Right,
        Key.Up => DrawingKey.Up,
        Key.Down => DrawingKey.Down,
        _ => DrawingKey.Other,
    };

    /// <summary>WPF 수정키 → 라우터 수정키(Alt/Control/Shift 비트 동일, Windows 키는 버린다).</summary>
    public static DrawingKeyModifiers FromWpfModifiers(ModifierKeys modifiers)
        => (DrawingKeyModifiers)((int)modifiers & (int)(DrawingKeyModifiers.Alt | DrawingKeyModifiers.Control | DrawingKeyModifiers.Shift));

    /// <summary>WPF 인자로 바로 판정 — 호출부 편의.</summary>
    public static DrawingKeyAction Route(Key key, ModifierKeys modifiers, bool isDrawing, bool isStrokePressed)
        => Route(FromWpfKey(key), FromWpfModifiers(modifiers), isDrawing, isStrokePressed);
}
