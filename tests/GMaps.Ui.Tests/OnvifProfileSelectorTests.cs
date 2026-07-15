using System.Collections.Generic;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;
using Xunit;
using static Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz.OnvifProfileSelector;

namespace GMaps.Ui.Tests;

/// <summary>
/// ONVIF 재생 프로파일 선택(<see cref="OnvifProfileSelector"/>) 단위 테스트 — 실코드 소스링크.
/// 규칙(OQ-01 확정): 해상도 최소(preferSub) → 동률 비오디오 우선 → 원 순서. (FR-03)
/// </summary>
public class OnvifProfileSelectorTests
{
    /// <summary>VER-01 실측 픽스처 — 카메라 66(192.168.202.66)의 8개 프로파일 그대로.</summary>
    private static List<ProfileInfo> Cam66Profiles() => new()
    {
        new("0_PROFILE_WITH_AUDIO", 1920, 1080, true),
        new("1_PROFILE_WITH_AUDIO", 720, 480, true),
        new("2_PROFILE", 1920, 1080, false),
        new("3_PROFILE", 720, 480, false),
        new("4_PROFILE_WITH_AUDIO", 1024, 768, true),
        new("5_PROFILE_WITH_AUDIO", 720, 480, true),
        new("6_PROFILE", 1024, 768, false),
        new("7_PROFILE", 720, 480, false),
    };

    [Fact]
    public void should_select_min_resolution_non_audio_when_prefer_sub()
    {
        // 720x480 4개 동률(1,3,5,7) → 비오디오(3,7) → 원 순서 첫 번째 = 3_PROFILE(video1s)
        Assert.Equal("3_PROFILE", Select(Cam66Profiles(), preferSub: true));
    }

    [Fact]
    public void should_select_max_resolution_non_audio_when_prefer_main()
    {
        // 1920x1080 동률(0,2) → 비오디오 = 2_PROFILE(video1)
        Assert.Equal("2_PROFILE", Select(Cam66Profiles(), preferSub: false));
    }

    [Fact]
    public void should_prefer_non_audio_when_resolution_ties()
    {
        var profiles = new List<ProfileInfo>
        {
            new("A_AUDIO", 704, 480, true),
            new("B_VIDEO", 704, 480, false),
        };
        Assert.Equal("B_VIDEO", Select(profiles, preferSub: true));
    }

    [Fact]
    public void should_select_second_when_no_resolution_info()
    {
        // 해상도 정보 전무 — 관례 폴백(1=메인, 2=서브)
        var profiles = new List<ProfileInfo> { new("MAIN", 0, 0, false), new("SUB", 0, 0, false) };
        Assert.Equal("SUB", Select(profiles, preferSub: true));
        Assert.Equal("MAIN", Select(profiles, preferSub: false));
    }

    [Fact]
    public void should_select_first_when_single_profile()
    {
        var profiles = new List<ProfileInfo> { new("ONLY", 0, 0, false) };
        Assert.Equal("ONLY", Select(profiles, preferSub: true));
    }

    [Fact]
    public void should_return_null_when_empty()
    {
        Assert.Null(Select(new List<ProfileInfo>(), preferSub: true));
        Assert.Null(Select(null!, preferSub: true));
    }

    [Fact]
    public void should_ignore_profiles_without_resolution_when_mixed()
    {
        // 해상도 있는 것만 정렬 대상 — 미상 프로파일은 후보에서 제외(결정성)
        var profiles = new List<ProfileInfo>
        {
            new("UNKNOWN", 0, 0, false),
            new("BIG", 1920, 1080, false),
            new("SMALL", 640, 360, false),
        };
        Assert.Equal("SMALL", Select(profiles, preferSub: true));
        Assert.Equal("BIG", Select(profiles, preferSub: false));
    }
}
