using System.Collections.ObjectModel;
using System.Windows;
using Ironwall.Dotnet.Libraries.Theme.Services;
using MaterialDesignThemes.Wpf;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// TEST-12 — 토글 스모크 (라이브러리 한정, NFR-02).
/// Theme 어셈블리 단독 호스트에서 <see cref="IThemeService.Toggle"/> 라운드트립 시
/// 토큰 dict 교체(누수 없음)·<see cref="IThemeService.ThemeChanged"/> 시퀀스를 검증한다.
/// 마커 포함 토글(실제 BAML·Application)은 범위 밖 — V-05/Phase 6.
/// 가짜 토큰 팩토리 + 독립 MergedDictionaries 주입으로 WPF Application/BAML 없이 검증.
/// </summary>
public class ThemeToggleSmokeTests
{
    private const string TagKey = "__themeTag";

    private static (ThemeService svc, Collection<ResourceDictionary> dicts) CreateSut()
    {
        var dicts = new Collection<ResourceDictionary>();
        var svc = new ThemeService(
            store: null,
            mergedDictionaries: dicts,
            tokenDictionaryFactory: theme => new ResourceDictionary { [TagKey] = theme });
        return (svc, dicts);
    }

    private static BaseTheme TagOf(ResourceDictionary d) => (BaseTheme)d[TagKey]!;

    [Fact]
    public void should_round_trip_to_light_when_toggle_twice()
    {
        // Arrange
        var (svc, dicts) = CreateSut();
        svc.InitializeFromSettings();   // Light

        // Act — Light → Dark → Light
        svc.Toggle();
        svc.Toggle();

        // Assert — 출발 테마로 복귀 + 단일 토큰 dict 유지
        Assert.Equal(BaseTheme.Light, svc.Current);
        Assert.Single(dicts);
        Assert.Equal(BaseTheme.Light, TagOf(dicts[0]));
    }

    [Fact]
    public void should_fire_themechanged_for_each_toggle_in_sequence()
    {
        // Arrange
        var (svc, _) = CreateSut();
        svc.InitializeFromSettings();   // Light
        var raised = new List<BaseTheme>();
        svc.ThemeChanged += (_, t) => raised.Add(t);

        // Act — 3회 연속 토글
        svc.Toggle();   // → Dark
        svc.Toggle();   // → Light
        svc.Toggle();   // → Dark

        // Assert — 토글마다 1회씩, 올바른 순서로 발화
        Assert.Equal(new[] { BaseTheme.Dark, BaseTheme.Light, BaseTheme.Dark }, raised);
        Assert.Equal(BaseTheme.Dark, svc.Current);
    }

    [Fact]
    public void should_keep_single_token_dict_across_many_toggles()
    {
        // Arrange
        var (svc, dicts) = CreateSut();
        svc.InitializeFromSettings();

        // Act — 6회 토글(누수 시 dict 누적)
        for (var i = 0; i < 6; i++)
            svc.Toggle();

        // Assert — Add-new→Remove-old 불변식: 항상 정확히 1개
        Assert.Single(dicts);
        Assert.Equal(svc.Current, TagOf(dicts[0]));   // dict 태그 = 현재 테마와 일치
        Assert.Equal(BaseTheme.Light, svc.Current);   // 짝수 회 토글 → Light 복귀
    }
}
