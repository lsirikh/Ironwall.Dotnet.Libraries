using ScenarioShared;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Xunit;

namespace GMaps.MapNav.Tests;

/// <summary>공용 도구 자체 확인 — 생성자 없는 MapViewModel 이 STA 에서 만들어지는가(시나리오 표지 없음: 카탈로그 항목이 아니다).</summary>
public class HarnessSmokeTests
{
    [Fact]
    public void should_create_uninitialized_map_view_model_on_sta() => Scenario.Sta(() =>
    {
        var vm = Scenario.Uninit<MapViewModel>();
        Assert.NotNull(vm);
    });
}
