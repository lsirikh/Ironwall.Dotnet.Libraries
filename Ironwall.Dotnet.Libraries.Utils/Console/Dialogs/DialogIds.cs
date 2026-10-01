namespace Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;

/// <summary>
/// 다이얼로그 자동화 식별자 — <c>Dialog.{Key}.{부품}</c> 한 규칙으로 찍는다.
/// </summary>
/// <remarks>
/// <para>규칙(ui-automation.md): 식별자는 <c>AutomationProperties.AutomationId</c> 하나뿐이고 <c>x:Name</c> 은 건드리지 않는다
/// — 이 저장소에서 <c>x:Name</c> 은 Caliburn 바인딩 지시자다.</para>
/// <para>peer 가 없는 요소(<c>TextBlock</c> · <c>Border</c> · <c>StackPanel</c> · <c>ContentControl</c>)에는 붙이지 않는다.
/// 그래서 제목 · 안내 글은 peer 를 실제로 만드는 <see cref="ConsoleDialogText"/> 가 이고, 뿌리는 <see cref="ConsoleDialogFrame"/> 자신의 peer 다.</para>
/// </remarks>
public static class DialogIds
{
    public const string Prefix = "Dialog";

    public const string RootPart = "Root";
    public const string TitlePart = "Title";
    public const string ClosePart = "Close";
    public const string PrimaryPart = "Primary";
    public const string SecondaryPart = "Secondary";
    public const string MessagePart = "Message";
    public const string MovePart = "Move";

    /// <summary><paramref name="key"/> 가 비어 있으면 <c>null</c> — 식별자를 붙이지 않는다(가짜 전역 이름을 만들지 않는다).</summary>
    public static string? For(string? key, string part)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (string.IsNullOrWhiteSpace(part)) return null;
        return $"{Prefix}.{key.Trim()}.{part.Trim()}";
    }

    public static string? Root(string? key) => For(key, RootPart);
    public static string? Title(string? key) => For(key, TitlePart);
    public static string? Close(string? key) => For(key, ClosePart);
    public static string? Primary(string? key) => For(key, PrimaryPart);
    public static string? Secondary(string? key) => For(key, SecondaryPart);
    public static string? Message(string? key) => For(key, MessagePart);

    /// <summary>머리의 옮기기 손잡이(<c>Thumb</c> — peer 가 실재한다). 셸 안 카드에서만 보인다.</summary>
    public static string? Move(string? key) => For(key, MovePart);
}
