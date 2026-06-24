using System.Collections.ObjectModel;
using System.Windows;
using ControlzEx.Theming;
using MaterialDesignThemes.Wpf;

namespace Ironwall.Dotnet.Libraries.Theme.Services;

/// <summary>
/// <see cref="IThemeService"/> 구현 (IMPL-07). PRD §3.4 순서 준수:
/// (1) MergedDictionaries Add-new → Remove-old (key-absent 윈도우 방지)
/// (2) PaletteHelper.SetTheme (MaterialDesign 컨트롤)
/// (3) ThemeManager.ChangeTheme (MahApps chrome)
/// (4) ThemeChanged 발화 (SkiaSharp 차트 등 비-WPF 경로 재색칠)
/// <para>1~3은 단일 Dispatcher 패스. MD/MahApps 중복 키 호출 금지(double full-invalidation 방지, R-17).</para>
/// <para>스레드: UI 스레드 가정. 비-UI 호출 시 Dispatcher로 1회 마샬링. 상태 스왑은 <c>_gate</c> 직렬화,
/// 이벤트/엔진 동기화는 락 밖(재진입 데드락 방지).</para>
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string LightTokensUri =
        "pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml";
    private const string DarkTokensUri =
        "pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml";

    private readonly object _gate = new();
    private readonly IThemeSettingsStore? _store;
    private readonly Collection<ResourceDictionary>? _mergedDictionaries;
    private readonly Func<BaseTheme, ResourceDictionary> _tokenFactory;

    /// <summary>현재 리소스 트리에 얹힌 토큰 오버라이드 딕셔너리(Light/Dark). 적용 전 null.</summary>
    private ResourceDictionary? _appliedTokens;

    /// <param name="store">테마 영속화 seam(선택). null이면 InitializeFromSettings는 Light 기본.</param>
    /// <param name="mergedDictionaries">
    /// 스왑 대상 MergedDictionaries. 기본 = <see cref="Application"/>.Current.Resources.MergedDictionaries.
    /// 단위 테스트는 독립 컬렉션을 주입해 Application 없이 스왑 로직을 검증한다.
    /// </param>
    /// <param name="tokenDictionaryFactory">
    /// 테마별 토큰 딕셔너리 생성기(선택). 기본 = pack URI(Tokens.Light/Dark.xaml) 로더.
    /// 단위 테스트는 가짜 팩토리를 주입해 BAML 로딩 없이 스왑 카운트/누수를 검증한다.
    /// </param>
    public ThemeService(IThemeSettingsStore? store = null,
                        Collection<ResourceDictionary>? mergedDictionaries = null,
                        Func<BaseTheme, ResourceDictionary>? tokenDictionaryFactory = null)
    {
        _store = store;
        _mergedDictionaries = mergedDictionaries
            ?? Application.Current?.Resources?.MergedDictionaries;
        _tokenFactory = tokenDictionaryFactory ?? DefaultTokenFactory;
    }

    /// <summary>프로덕션 기본 팩토리 — pack URI로 컴파일된 토큰 딕셔너리(BAML)를 로드.</summary>
    private static ResourceDictionary DefaultTokenFactory(BaseTheme theme)
        => new()
        {
            Source = new Uri(theme == BaseTheme.Dark ? DarkTokensUri : LightTokensUri, UriKind.Absolute)
        };

    public BaseTheme Current { get; private set; } = BaseTheme.Light;

    public event EventHandler<BaseTheme>? ThemeChanged;

    public void InitializeFromSettings()
        => ApplyThemeInternal(_store?.Load() ?? BaseTheme.Light, force: true);

    public void ApplyTheme(BaseTheme theme)
        => ApplyThemeInternal(theme, force: false);

    public void Toggle()
        => ApplyTheme(Current == BaseTheme.Dark ? BaseTheme.Light : BaseTheme.Dark);

    private void ApplyThemeInternal(BaseTheme theme, bool force)
    {
        if (theme == BaseTheme.Inherit)
            theme = BaseTheme.Light;

        // 비-UI 스레드 → Dispatcher 단일 마샬링.
        var app = Application.Current;
        if (app != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(() => ApplyThemeInternal(theme, force));
            return;
        }

        lock (_gate)
        {
            // R-17: 동일 테마 재적용 차단(중복 full-invalidation 방지).
            if (!force && _appliedTokens != null && theme == Current)
                return;

            SwapTokenDictionary(theme);   // (1)
            Current = theme;
        }

        // (2)(3) 엔진 동기화 + (4) 이벤트/영속화는 락 밖, 동일 UI 스레드.
        SyncMaterialDesignAndMahApps(app, theme);
        ThemeChanged?.Invoke(this, theme);
        _store?.Save(theme);
    }

    /// <summary>(1) Add-new → Remove-old. 항상 정확히 1개의 토큰 오버라이드만 유지(누수 없음).</summary>
    private void SwapTokenDictionary(BaseTheme theme)
    {
        var dicts = _mergedDictionaries;
        if (dicts == null)
            return;   // 리소스 타깃 없음(헤드리스) — 엔진 동기화만.

        var newDict = _tokenFactory(theme);

        dicts.Add(newDict);                 // Add-new 먼저
        if (_appliedTokens != null)
            dicts.Remove(_appliedTokens);   // Remove-old 나중
        _appliedTokens = newDict;
    }

    /// <summary>(2)(3) MaterialDesign(PaletteHelper) + MahApps(ThemeManager) 단일 패스 동기화.</summary>
    private static void SyncMaterialDesignAndMahApps(Application? app, BaseTheme theme)
    {
        if (app == null)
            return;   // 헤드리스(단위 테스트) — WPF 엔진 미가동.

        // (2) MaterialDesign 컨트롤 — 베이스 + 스토리보드 Modern Operations primary/secondary 정렬(FR-02).
        //     앱 전역 MD 컨트롤이 LightBlue/Cyan(App.xaml 기본) 대신 선택 디자인(navy/teal)로 리컬러된다.
        var paletteHelper = new PaletteHelper();
        // 네임스페이스 Ironwall...Theme 와 MD Theme 클래스 충돌 → 정규화. SetBaseTheme(Theme, BaseTheme).
        var mdTheme = (MaterialDesignThemes.Wpf.Theme)paletteHelper.GetTheme();
        mdTheme.SetBaseTheme(theme);
        if (theme == BaseTheme.Dark)
        {
            mdTheme.SetPrimaryColor(System.Windows.Media.Color.FromRgb(0x4A, 0x90, 0xE2));   // storyboard Dark --primary
            mdTheme.SetSecondaryColor(System.Windows.Media.Color.FromRgb(0x2B, 0xC4, 0xD4)); // storyboard Dark --accent
        }
        else
        {
            mdTheme.SetPrimaryColor(System.Windows.Media.Color.FromRgb(0x2C, 0x5F, 0x9E));   // storyboard Light --primary
            mdTheme.SetSecondaryColor(System.Windows.Media.Color.FromRgb(0x0E, 0x8C, 0x9B)); // storyboard Light --accent
        }
        paletteHelper.SetTheme(mdTheme);

        // (3) MahApps chrome
        ThemeManager.Current.ChangeTheme(app, theme == BaseTheme.Dark ? "Dark.Blue" : "Light.Blue");
    }
}
