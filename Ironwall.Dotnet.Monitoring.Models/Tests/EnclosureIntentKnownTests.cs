using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 함체 히터 · 팬의 "설정 없음" — <see cref="EnclosureDeviceModel.HeaterEnabledKnown"/> · <see cref="EnclosureDeviceModel.FanEnabledKnown"/>.
/// </summary>
/// <remarks>라이브 하네스 dl.4(2026-09-26): bool 하나로는 "설정 없음"을 못 적어 무변경 저장이 팬에 enabled:false 를 지어냈다.</remarks>
public class EnclosureIntentKnownTests
{
    [Fact]
    public void should_be_known_by_default_when_model_is_new()
    {
        var model = new EnclosureDeviceModel();

        Assert.True(model.HeaterEnabledKnown);
        Assert.True(model.FanEnabledKnown);
    }

    [Fact]
    public void should_become_known_when_value_is_set_even_to_false()
    {
        var model = new EnclosureDeviceModel { FanEnabledKnown = false };

        model.FanEnabled = false;

        Assert.True(model.FanEnabledKnown);
    }

    [Fact]
    public void should_keep_unknown_when_copied_from_a_model_without_intent()
    {
        var source = new EnclosureDeviceModel { HeaterEnabled = true, FanEnabled = false };
        source.FanEnabledKnown = false;

        var copy = new EnclosureDeviceModel(source);

        Assert.True(copy.HeaterEnabled);
        Assert.True(copy.HeaterEnabledKnown);
        Assert.False(copy.FanEnabledKnown);
    }

    [Fact]
    public void should_not_serialize_known_flags_when_model_is_written_as_json()
    {
        var json = JsonConvert.SerializeObject(new EnclosureDeviceModel { FanEnabledKnown = false });

        Assert.DoesNotContain("Known", json);
        Assert.Contains("\"fan_enabled\":false", json);
    }
}
