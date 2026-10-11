using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// 속성창 실제 스타일을 Application 없이 읽는 도구(W2).
/// 저장소의 속성창 사전(BasePropertyStyle 등)은 Resources/Resources.xaml 을 병합하는데, 그 사전은 MDIX 스타일(MaterialDesignTextBlock 등)을
/// StaticResource 로 참조하고 MDIX 를 스스로 병합하지 않는다 — 앱에서는 App.xaml 의 MDIX 가 그 이름을 대 준다.
/// 각 사전을 Source 로 따로 읽으면 그 이름을 찾을 길이 없어 XamlParseException 이 난다(Application 을 만들면 다른 시험의 디스패처 경로가 바뀐다).
/// 그래서 저장소 XAML 원문을 <b>고치지 않고</b> 한 개 바깥 사전의 MergedDictionaries 안에 앱과 같은 순서
/// (MDIX 번들 → MD3 기본값 → 테마 토큰 Shared/Light → DesignTokens → Resources → BasePropertyStyle → 종류별 스타일)로 인라인해 한 번에 읽는다
/// — 앞 형제 사전의 키가 뒤 사전의 StaticResource 에 보인다. 바꾸는 것은 두 가지뿐: 파일끼리의 병합 블록(인라인 순서가 대신함)을 빼고,
/// 어셈블리 이름이 빠진 clr-namespace(같은 어셈블리 참조)에 GMaps.Ui 어셈블리 이름을 붙인다. 스타일 · 템플릿 · 묶음 · 트리거 · x:Name 은 그대로다.
/// </summary>
internal static class PropertyStyleLoader
{
    private const string UiAssembly = "Ironwall.Dotnet.Libraries.GMaps.Ui";

    private static string Inline(string relative)
    {
        var text = RepoFiles.Read(Path.Combine(UiAssembly, relative));
        text = Regex.Replace(text, @"^﻿?\s*<\?xml[^>]*\?>", string.Empty);
        text = Regex.Replace(text, @"<ResourceDictionary\.MergedDictionaries>.*?</ResourceDictionary\.MergedDictionaries>", string.Empty, RegexOptions.Singleline);
        text = Regex.Replace(text, @"clr-namespace:([A-Za-z0-9_.]+)""", $"clr-namespace:$1;assembly={UiAssembly}\"");
        return text.Trim().TrimStart('﻿');
    }

    /// <summary>종류별 속성창 스타일 파일(예: "GeometricPropertyStyle.xaml")까지 앱 순서로 읽은 사전.</summary>
    public static ResourceDictionary Load(string styleFile)
    {
        var files = new List<string> { "Themes/DesignTokens.xaml", "Resources/Resources.xaml", "Themes/BasePropertyStyle.xaml" };
        if (!styleFile.Equals("BasePropertyStyle.xaml", StringComparison.OrdinalIgnoreCase)) files.Add("Themes/" + styleFile);

        var sb = new StringBuilder();
        sb.Append("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:md=\"http://materialdesigninxaml.net/winfx/xaml/themes\">");
        sb.Append("<ResourceDictionary.MergedDictionaries>");
        sb.Append("<md:BundledTheme BaseTheme=\"Light\" PrimaryColor=\"LightBlue\" SecondaryColor=\"Cyan\" />");
        sb.Append("<ResourceDictionary Source=\"pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml\" />");
        sb.Append("<ResourceDictionary Source=\"pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Shared.xaml\" />");
        sb.Append("<ResourceDictionary Source=\"pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml\" />");
        foreach (var f in files) sb.Append(Inline(f));
        sb.Append("</ResourceDictionary.MergedDictionaries></ResourceDictionary>");
        return (ResourceDictionary)XamlReader.Parse(sb.ToString());
    }
}
