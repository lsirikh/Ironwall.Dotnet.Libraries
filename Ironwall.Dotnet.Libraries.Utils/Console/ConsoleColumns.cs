using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// "열" 메뉴 — 목록은 <b>기본 열</b>만 보이고 나머지는 상세 칸이 맡는다. 창을 넓혔을 때 전체 열을 되살리거나,
/// 열 하나하나를 끄고 켤 수 있다. 선택은 <see cref="ConsolePrefs"/> 가 콘솔 키별로 기억한다.
/// (설계 정본 window-layout-system-storyboard.html L956-976 · L-D2)
/// </summary>
/// <remarks>
/// 열 재정렬 · 정렬은 제공하지 않는다 — 화면 순서와 저장 순서가 어긋나면 의미와 자동화 단언이 같이 무너진다.
/// </remarks>
public static class ConsoleColumns
{
    /// <summary>열의 식별 키. 키가 없는 열(핸들 열 등)은 늘 보이고 개수에도 세지 않는다.</summary>
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(ConsoleColumns), new PropertyMetadata(null));
    public static string? GetKey(DependencyObject column) => (string?)column.GetValue(KeyProperty);
    public static void SetKey(DependencyObject column, string? value) => column.SetValue(KeyProperty, value);

    /// <summary>기본 열인가. 거짓이면 "전체 열"을 켰을 때만 보인다.</summary>
    public static readonly DependencyProperty IsDefaultProperty = DependencyProperty.RegisterAttached(
        "IsDefault", typeof(bool), typeof(ConsoleColumns), new PropertyMetadata(true));
    public static bool GetIsDefault(DependencyObject column) => (bool)column.GetValue(IsDefaultProperty);
    public static void SetIsDefault(DependencyObject column, bool value) => column.SetValue(IsDefaultProperty, value);

    /// <summary>
    /// 그 열을 서버 계약이 지원하는가 — 거짓이면 어떤 설정으로도 보이지 않고 개수에도 세지 않는다
    /// (운영 6.3 에 붙었을 때 v7+ 전용 열을 <b>비활성이 아니라 감춘다</b>).
    /// </summary>
    public static readonly DependencyProperty IsSupportedProperty = DependencyProperty.RegisterAttached(
        "IsSupported", typeof(bool), typeof(ConsoleColumns), new PropertyMetadata(true));
    public static bool GetIsSupported(DependencyObject column) => (bool)column.GetValue(IsSupportedProperty);
    public static void SetIsSupported(DependencyObject column, bool value) => column.SetValue(IsSupportedProperty, value);

    /// <summary>한 열이 보여야 하는가 — 순수 판정.</summary>
    public static bool ShouldShow(bool isSupported, bool isDefault, bool showAll, bool isHiddenByUser)
        => isSupported && (isDefault || showAll) && !isHiddenByUser;

    /// <summary>설정을 열들에 적용하고 툴바에 찍을 글자("열 6/9")를 돌려준다.</summary>
    public static string Apply(IEnumerable<DataGridColumn> columns, bool showAll, ICollection<string>? hiddenKeys)
    {
        int shown = 0, total = 0;
        foreach (var column in columns)
        {
            var key = GetKey(column);
            if (string.IsNullOrEmpty(key)) continue;

            var supported = GetIsSupported(column);
            var visible = ShouldShow(supported, GetIsDefault(column), showAll, hiddenKeys?.Contains(key!) == true);
            column.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

            if (!supported) continue;
            total++;
            if (visible) shown++;
        }
        return $"열 {shown}/{total}";
    }

    /// <summary>"열" 메뉴에 올릴 항목 — (키, 머리글, 지금 보이는가, 기본 열인가). 지원하지 않는 열은 빠진다.</summary>
    public static IReadOnlyList<(string Key, string Header, bool IsVisible, bool IsDefault)> Describe(IEnumerable<DataGridColumn> columns)
        => columns
            .Where(c => !string.IsNullOrEmpty(GetKey(c)) && GetIsSupported(c))
            .Select(c => (GetKey(c)!, c.Header?.ToString() ?? GetKey(c)!, c.Visibility == Visibility.Visible, GetIsDefault(c)))
            .ToList();

    /// <summary>
    /// 사용자가 메뉴에서 한 열을 껐다/켰다. 기본이 아닌 열을 켜려면 "숨김 목록에서 빼는 것"으로는 부족하다 —
    /// 그래서 그때는 전체 열을 켜고 나머지 비기본 열을 숨김 목록에 넣는다.
    /// </summary>
    public static void Toggle(IEnumerable<DataGridColumn> columns, ConsolePrefEntry prefs, string key)
    {
        var all = columns.Where(c => !string.IsNullOrEmpty(GetKey(c)) && GetIsSupported(c)).ToList();
        var column = all.FirstOrDefault(c => GetKey(c) == key);
        if (column == null) return;

        var visible = ShouldShow(true, GetIsDefault(column), prefs.ShowAllColumns, prefs.HiddenColumns.Contains(key));
        if (visible)
        {
            if (!prefs.HiddenColumns.Contains(key)) prefs.HiddenColumns.Add(key);
            return;
        }

        prefs.HiddenColumns.Remove(key);
        if (GetIsDefault(column) || prefs.ShowAllColumns) return;

        // 비기본 열 하나만 켠다: 전체 열을 켜되, 지금 안 보이던 다른 비기본 열은 숨김으로 돌린다.
        prefs.ShowAllColumns = true;
        foreach (var other in all.Where(c => !GetIsDefault(c) && GetKey(c) != key))
            if (!prefs.HiddenColumns.Contains(GetKey(other)!)) prefs.HiddenColumns.Add(GetKey(other)!);
    }
}
