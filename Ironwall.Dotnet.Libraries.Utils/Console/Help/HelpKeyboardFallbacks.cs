using System.Windows;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 섹션 아래 끌기 면들의 키보드 대체 경로를 모은다 — 섹션 "?" 의 "키보드로" 묶음에 저절로 들어간다(help-callout PRD FR-06).
/// </summary>
/// <remarks>
/// <para>끌기 면마다 <see cref="CaptureDragBehavior.KeyboardFallback"/> 에 이미 적어 둔 글을 그대로 쓴다 — 같은 말을 설명 목록에 다시 적지 않는다.</para>
/// <para>보이는(<see cref="Visibility.Visible"/>) 가지만 걷는다. 안쪽에 제 "?" 를 가진 다른 도움말 범위가 있으면 그 가지는 그쪽 몫이라 건너뛴다.
/// 같은 글은 한 번만.</para>
/// </remarks>
public static class HelpKeyboardFallbacks
{
    /// <summary>"키보드로" 묶음의 소제목.</summary>
    public const string Heading = "키보드로";

    /// <summary><paramref name="scope"/> 아래 끌기 면의 키보드 대체 경로(나타난 순서, 중복 없음).</summary>
    public static IReadOnlyList<string> Collect(DependencyObject? scope)
    {
        var found = new List<string>();
        if (scope is null) return found;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        Walk(scope, scope, found, seen);
        return found;
    }

    /// <summary>모은 글을 <paramref name="entry"/> 의 "키보드로" 묶음에 더한 항목(모은 것이 없으면 그대로).</summary>
    public static HelpEntry AppendTo(HelpEntry entry, DependencyObject? scope)
        => entry.Merge(Heading, Collect(scope));

    private static void Walk(DependencyObject node, DependencyObject scope, List<string> found, HashSet<string> seen)
    {
        if (node is UIElement { Visibility: not Visibility.Visible }) return;
        if (!ReferenceEquals(node, scope) && HelpTip.GetScopeTip(node) is { } other && other.IsAvailable) return;

        if (CaptureDragBehavior.GetPublishedKeyboardFallback(node) is { Length: > 0 } text && seen.Add(text.Trim()))
            found.Add(text.Trim());

        if (node is not Visual and not System.Windows.Media.Media3D.Visual3D) return;
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++) Walk(VisualTreeHelper.GetChild(node, i), scope, found, seen);
    }
}
