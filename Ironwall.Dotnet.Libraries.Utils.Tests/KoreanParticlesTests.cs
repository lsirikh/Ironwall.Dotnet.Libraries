using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>조사 병기를 받침에 맞는 하나로 — "'감시탑'을(를)" · "새 그룹이(가)" 가 운영자 화면에 그대로 보이던 것(2026-09-27 실창).</summary>
public class KoreanParticlesTests
{
    [Theory]
    [InlineData("'감시탑'을(를) 저장했습니다.", "'감시탑'을 저장했습니다.")]
    [InlineData("'정문 스피커'을(를) 저장했습니다.", "'정문 스피커'를 저장했습니다.")]
    [InlineData("새 그룹이(가) 만들어집니다", "새 그룹이 만들어집니다")]
    [InlineData("새 장비이(가) 만들어집니다", "새 장비가 만들어집니다")]
    [InlineData("'1소초'은(는) 이미 최상위 부대입니다.", "'1소초'는 이미 최상위 부대입니다.")]
    [InlineData("'A'과(와) 'B'은(는) 인접합니다", "'A'와 'B'는 인접합니다")]
    public void should_pick_the_particle_that_fits_the_final_consonant_when_text_has_a_dual_particle(string input, string expected)
        => Assert.Equal(expected, KoreanParticles.Resolve(input));

    [Theory]
    [InlineData("'서울'(으)로 옮겼습니다.", "'서울'로 옮겼습니다.")]      // ㄹ 받침은 '로'
    [InlineData("'본부'(으)로 옮겼습니다.", "'본부'로 옮겼습니다.")]      // 받침 없음
    [InlineData("'1대대'(으)로 옮겼습니다.", "'1대대'로 옮겼습니다.")]
    [InlineData("'정문'(으)로 옮겼습니다.", "'정문'으로 옮겼습니다.")]    // ㄴ 받침
    public void should_choose_ro_or_euro_when_the_direction_particle_is_dual(string input, string expected)
        => Assert.Equal(expected, KoreanParticles.Resolve(input));

    [Theory]
    [InlineData("장비번호 3은(는) 이미 쓰고 있습니다", "장비번호 3은 이미 쓰고 있습니다")]   // 삼
    [InlineData("장비번호 2은(는) 이미 쓰고 있습니다", "장비번호 2는 이미 쓰고 있습니다")]   // 이
    [InlineData("'CAM-01'을(를) 추가했습니다", "'CAM-01'을 추가했습니다")]                 // 일
    [InlineData("'GATE-L'을(를) 추가했습니다", "'GATE-L'을 추가했습니다")]                 // 엘
    [InlineData("'PROXY'을(를) 추가했습니다", "'PROXY'를 추가했습니다")]                   // 와이
    public void should_read_digits_and_latin_letters_aloud_when_picking_the_particle(string input, string expected)
        => Assert.Equal(expected, KoreanParticles.Resolve(input));

    [Fact]
    public void should_leave_text_untouched_when_there_is_no_dual_particle()
    {
        const string text = "장비(카메라)를 골랐습니다 (3건)";

        Assert.Equal(text, KoreanParticles.Resolve(text));
    }

    [Fact]
    public void should_see_through_word_joiners_when_the_text_was_already_wrapped()
    {
        var joined = KoreanWordWrap.Join("'감시탑'을(를)");

        Assert.Equal("'감시탑'을", KoreanWordWrap.Strip(KoreanParticles.Resolve(joined)));
    }
}
