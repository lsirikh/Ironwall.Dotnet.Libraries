using System.Collections.ObjectModel;
using System.Windows;
using Ironwall.Dotnet.Libraries.Theme.Services;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// TEST-07 — ThemeService 스왑 로직 단위 테스트.
/// 가짜 토큰 팩토리 + 독립 MergedDictionaries 주입으로 WPF Application/BAML 없이 검증.
/// </summary>
public class ThemeServiceTests
{
    private const string TagKey = "__themeTag";

    // 각 테마를 식별 가능한 fake dict로 생성(pack URI BAML 로딩 회피).
    private static (ThemeService svc, Collection<ResourceDictionary> dicts) CreateSut(IThemeSettingsStore? store = null)
    {
        var dicts = new Collection<ResourceDictionary>();
        var svc = new ThemeService(
            store: store,
            mergedDictionaries: dicts,
            tokenDictionaryFactory: theme => new ResourceDictionary { [TagKey] = theme });
        return (svc, dicts);
    }

    private static BaseTheme TagOf(ResourceDictionary d) => (BaseTheme)d[TagKey]!;

    [Fact]
    public void should_default_light_when_initialize()
    {
        // Arrange
        var (svc, dicts) = CreateSut();

        // Act
        svc.InitializeFromSettings();

        // Assert
        Assert.Equal(BaseTheme.Light, svc.Current);
        Assert.Single(dicts);
        Assert.Equal(BaseTheme.Light, TagOf(dicts[0]));
    }

    [Fact]
    public void should_swap_token_dictionary_when_apply_dark()
    {
        // Arrange
        var (svc, dicts) = CreateSut();
        svc.InitializeFromSettings();   // Light

        // Act
        svc.ApplyTheme(BaseTheme.Dark);

        // Assert
        Assert.Equal(BaseTheme.Dark, svc.Current);
        Assert.Single(dicts);                       // Add-then-Remove → 여전히 1개
        Assert.Equal(BaseTheme.Dark, TagOf(dicts[0]));
    }

    [Fact]
    public void should_raise_themechanged_when_toggle()
    {
        // Arrange
        var (svc, _) = CreateSut();
        svc.InitializeFromSettings();   // Light
        BaseTheme? raised = null;
        svc.ThemeChanged += (_, t) => raised = t;

        // Act
        svc.Toggle();                   // → Dark

        // Assert
        Assert.Equal(BaseTheme.Dark, raised);
        Assert.Equal(BaseTheme.Dark, svc.Current);
    }

    [Fact]
    public void should_keep_one_token_dict_when_repeated_apply()
    {
        // Arrange
        var (svc, dicts) = CreateSut();
        svc.InitializeFromSettings();

        // Act — Light → Dark → Light → Dark 반복 스왑
        svc.ApplyTheme(BaseTheme.Dark);
        svc.ApplyTheme(BaseTheme.Light);
        svc.ApplyTheme(BaseTheme.Dark);

        // Assert — 누수 없이 정확히 1개 유지
        Assert.Single(dicts);
        Assert.Equal(BaseTheme.Dark, TagOf(dicts[0]));
    }

    [Fact]
    public void should_ignore_when_apply_same_theme_twice()
    {
        // Arrange
        var (svc, _) = CreateSut();
        svc.InitializeFromSettings();   // Light
        var count = 0;
        svc.ThemeChanged += (_, _) => count++;

        // Act — 동일 테마 재적용(R-17)
        svc.ApplyTheme(BaseTheme.Light);

        // Assert — 중복 full-invalidation 차단 → 이벤트 미발화
        Assert.Equal(0, count);
    }

    [Fact]
    public void should_persist_when_apply_theme()
    {
        // Arrange
        var store = new FakeStore();
        var (svc, _) = CreateSut(store);

        // Act
        svc.ApplyTheme(BaseTheme.Dark);

        // Assert
        Assert.Equal(BaseTheme.Dark, store.Saved);
    }

    [Fact]
    public void should_restore_saved_theme_when_initialize()
    {
        // Arrange
        var store = new FakeStore { Saved = BaseTheme.Dark };
        var (svc, dicts) = CreateSut(store);

        // Act
        svc.InitializeFromSettings();

        // Assert
        Assert.Equal(BaseTheme.Dark, svc.Current);
        Assert.Equal(BaseTheme.Dark, TagOf(dicts[0]));
    }

    private sealed class FakeStore : IThemeSettingsStore
    {
        public BaseTheme? Saved { get; set; }
        public BaseTheme? Load() => Saved;
        public void Save(BaseTheme theme) => Saved = theme;
    }
}
