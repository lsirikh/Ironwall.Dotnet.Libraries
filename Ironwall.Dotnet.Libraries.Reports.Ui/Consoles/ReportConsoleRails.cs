using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles;

/// <summary>
/// 보고서 콘솔의 레일 — 탭 3 이 레일 3 이 된다(설계 정본 window-layout-system-storyboard.html L1260-1262).
/// </summary>
/// <remarks>
/// 순서는 목업 그대로다: 생성 이력 · 새 보고서 · 템플릿. 키는 화면 설정(<c>ConsolePrefs</c>)과
/// 자동화 식별자에 쓰이므로 <b>바꾸면 사용자의 열 설정이 사라진다</b>.
/// </remarks>
public static class ReportConsoleRails
{
    /// <summary>생성 이력 — 목록 + 오른쪽 칸 미리보기.</summary>
    public const string List = "list";

    /// <summary>새 보고서 — T2 폼(WL L1531).</summary>
    public const string Create = "create";

    /// <summary>템플릿 — 오른쪽 칸 편집(WL L1282).</summary>
    public const string Template = "template";

    /// <summary>레일에 찍을 글자.</summary>
    public static string LabelOf(string key) => key switch
    {
        List => "생성 이력",
        Create => "새 보고서",
        Template => "템플릿",
        _ => key,
    };

    /// <summary>레일 아이콘 이름(MaterialDesign <c>PackIconKind</c>). 뷰가 토큰을 아이콘으로 바꾼다.</summary>
    public static string IconOf(string key) => key switch
    {
        List => "FileChartOutline",
        Create => "FilePlusOutline",
        Template => "FileCogOutline",
        _ => "CircleSmall",
    };

    /// <summary>상세 칸 머리에 쓰는 종류 이름 — "새 보고서 · 3개 선택" 같은 글이 여기서 만들어진다.</summary>
    public static string TypeNameOf(string key) => key switch
    {
        List => "보고서",
        Create => "보고서",
        Template => "템플릿",
        _ => key,
    };

    /// <summary>목업 순서 그대로.</summary>
    public static IReadOnlyList<string> Order { get; } = new[] { List, Create, Template };
}
