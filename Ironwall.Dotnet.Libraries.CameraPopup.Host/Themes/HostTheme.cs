using System.Windows;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Themes;

/// <summary>
/// 호스트 창 테마(NFR-04). 라이브러리 토큰(Ironwall.Dotnet.Libraries.Theme 의 Tokens.Light/Dark — 무의존 leaf 사전)을
/// 그대로 Application 자원에 병합하고, 전환은 토큰 사전만 바꿔 끼운다 — 모든 창 요소가 DynamicResource 라 매번 재해석된다.
/// 헤드리스 호스트는 창을 만들지 않으므로 첫 창을 만들 때 게으르게 올린다. UI 스레드 전용.
/// </summary>
internal static class HostTheme
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    private const string ThemeAssembly = "pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/";
    private const string HostAssembly = "pack://application:,,,/Ironwall.CameraPopupHost;component/Themes/";

    private static ResourceDictionary? _tokens;
    private static string _current = Dark;

    public static string Current => _current;

    /// <summary>"light" · "Light" → Light, 그 밖(비었거나 모름)은 Dark.</summary>
    public static string Normalize(string? theme)
        => string.Equals(theme?.Trim(), Light, StringComparison.OrdinalIgnoreCase) ? Light : Dark;

    /// <summary>자원이 없으면 올린다(공유 토큰 → 테마 토큰 → 호스트 스타일).</summary>
    public static void EnsureLoaded(Application app)
    {
        if (_tokens is not null) return;
        var merged = app.Resources.MergedDictionaries;
        merged.Add(new ResourceDictionary { Source = new Uri(ThemeAssembly + "Tokens.Shared.xaml", UriKind.Absolute) });
        _tokens = LoadTokens(_current);
        merged.Add(_tokens);
        merged.Add(new ResourceDictionary { Source = new Uri(HostAssembly + "HostStyles.xaml", UriKind.Absolute) });
    }

    /// <summary>테마 전환. 아직 창이 없으면 이름만 기억한다.</summary>
    public static void Apply(Application? app, string? theme)
    {
        var name = Normalize(theme);
        if (name == _current && _tokens is not null) return;
        _current = name;
        if (app is null || _tokens is null) return;
        var merged = app.Resources.MergedDictionaries;
        int index = merged.IndexOf(_tokens);
        var next = LoadTokens(name);
        if (index >= 0) merged[index] = next;
        else merged.Add(next);
        _tokens = next;
    }

    private static ResourceDictionary LoadTokens(string name)
        => new() { Source = new Uri(ThemeAssembly + $"Tokens.{name}.xaml", UriKind.Absolute) };
}
