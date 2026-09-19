namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles;

/// <summary>
/// 레일 아이콘 <b>이름</b>만 쥔 토큰. 싱글턴 뷰모델이 <c>PackIcon</c> 요소를 쥐면 뷰가 새로 만들어질 때 옛 시각 트리에 묶인다 —
/// 그래서 뷰모델은 이름만 주고 그림은 뷰의 <c>DataTemplate</c> 이 그린다.
/// </summary>
/// <remarks>맨 문자열을 쓰지 않는 까닭: 접힌 레일의 말풍선(라벨 문자열)까지 아이콘 템플릿에 걸린다(장비 콘솔 실측).</remarks>
public sealed record EventConsoleIcon(string Kind)
{
    public override string ToString() => Kind;
}
