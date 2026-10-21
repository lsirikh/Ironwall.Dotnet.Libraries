using Ironwall.Dotnet.Monitoring.Models.Components;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 부품 상태 어휘 v8.0.8 확장 9종(GOP API 8.0.15 대조 G18) — 차단기 TRIPPED · 충전 제어기 CHARGING 등이 "알 수 없음" 으로 보이던 것.
/// </summary>
/// <remarks>정본: <c>GOP_Restful_Api_연동설계.md</c> 부품 카탈로그 표 "새 상태 9".</remarks>
public class ComponentState808Tests
{
    [Theory]
    [InlineData("CLEAR", "감지 없음")]
    [InlineData("DETECTED", "감지")]
    [InlineData("TRIPPED", "차단됨")]
    [InlineData("CHARGING", "충전 중")]
    [InlineData("DISCHARGING", "방전 중")]
    [InlineData("LINK_UP", "연결됨")]
    [InlineData("LINK_DOWN", "끊김")]
    [InlineData("RAISED", "올라감")]
    [InlineData("LOWERED", "내려감")]
    [InlineData("tripped", "차단됨")]
    public void should_name_v808_component_states_in_korean(string code, string korean)
        => Assert.Equal(korean, ComponentDisplay.StateName(code));

    [Fact]
    public void should_not_show_unknown_when_breaker_reports_tripped()
    {
        Assert.DoesNotContain(ComponentDisplay.UnknownText, ComponentDisplay.StateName("TRIPPED"));
        Assert.True(ComponentDisplay.IsKnownState("TRIPPED"));
    }

    [Theory]
    [InlineData("LINK_UP", true)]      // 링크가 살아 있다 = 제 일을 하는 중(ON 과 같은 뜻)
    [InlineData("CHARGING", true)]     // 충전 중 = 구동 중
    [InlineData("LINK_DOWN", false)]
    [InlineData("DISCHARGING", false)] // 축전지로 버티는 중 — 가동 칸이 아니다(갈림길: 보고서 참조)
    [InlineData("CLEAR", false)]
    [InlineData("DETECTED", false)]
    [InlineData("TRIPPED", false)]
    [InlineData("RAISED", false)]
    [InlineData("LOWERED", false)]
    public void should_classify_v808_active_states(string state, bool active)
        => Assert.Equal(active, ComponentDisplay.IsActiveState(state));

    [Fact]
    public void should_use_catalog_label_when_state_is_outside_builtin_dictionary()
    {
        var catalog = new FakeStateLabels(new Dictionary<string, string> { ["FLASHING"] = "깜빡임" });

        Assert.Equal("깜빡임", ComponentDisplay.StateName("FLASHING", catalog));
    }

    [Fact]
    public void should_keep_console_wording_when_catalog_has_different_label_for_known_state()
    {
        // 서버 카탈로그는 RUNNING="동작 중" — 화면 말투 정본(구동 중)이 이긴다(스토리보드 §A).
        var catalog = new FakeStateLabels(new Dictionary<string, string> { ["RUNNING"] = "동작 중" });

        Assert.Equal("구동 중", ComponentDisplay.StateName("RUNNING", catalog));
    }

    [Fact]
    public void should_fall_back_to_unknown_with_raw_code_when_catalog_also_does_not_know()
    {
        Assert.Equal("알 수 없음 (MELTING)", ComponentDisplay.StateName("MELTING", new FakeStateLabels(new())));
        Assert.Equal("알 수 없음 (MELTING)", ComponentDisplay.StateName("MELTING", new ThrowingStateLabels()));
    }

    private sealed class FakeStateLabels : IComponentStateLabels
    {
        private readonly Dictionary<string, string> _labels;
        public FakeStateLabels(Dictionary<string, string> labels) => _labels = labels;
        public string? StateLabel(string? state) => state != null && _labels.TryGetValue(state, out var l) ? l : null;
    }

    private sealed class ThrowingStateLabels : IComponentStateLabels
    {
        public string? StateLabel(string? state) => throw new InvalidOperationException("카탈로그 고장");
    }
}
