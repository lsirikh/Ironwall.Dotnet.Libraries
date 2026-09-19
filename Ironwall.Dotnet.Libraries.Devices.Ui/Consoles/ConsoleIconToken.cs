namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;

/// <summary>
/// 레일 항목의 아이콘 — <b>이름만</b> 쥔다. 싱글턴 뷰모델이 시각 요소(PackIcon)를 쥐면 뷰가 새로 만들어질 때 옛 트리에 묶인 채 남는다.
/// 맨 문자열로 두지 않는 까닭: 화면이 문자열 전체에 암시적 템플릿을 걸어야 해서, 접힌 레일의 말풍선(라벨 문자열)까지 아이콘으로 그려진다.
/// </summary>
/// <param name="Kind">MaterialDesign PackIconKind 이름.</param>
public sealed record ConsoleIconToken(string Kind)
{
    public override string ToString() => Kind;
}
