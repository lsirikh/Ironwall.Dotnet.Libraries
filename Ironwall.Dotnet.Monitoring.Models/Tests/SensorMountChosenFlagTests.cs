using Ironwall.Dotnet.Monitoring.Models.Fences;
using Newtonsoft.Json;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 센서 자리의 "프리셋을 골라 세움" 표지(<see cref="SensorMountSpec.MountPresetChosen"/> · 2026-10-13 실창 회귀) — 더하기만 · 없으면 싣지 않는다 ·
/// 옛 문서(표지 없음)와 이관만 된 자리는 "고르지 않음"(9자리 계산이 정본).
/// </summary>
public class SensorMountChosenFlagTests
{
    [Fact, Trait("Scenario", "SC-FEN-024")]
    public void should_not_write_the_chosen_flag_unless_set_and_read_old_documents_as_not_chosen()
    {
        var migrated = new SensorMountSpec(2, FenceMountSpot.PostTop) { ModelId = 5, MountPresetId = 12 };
        var json = JsonConvert.SerializeObject(migrated);
        Assert.DoesNotContain("mount_preset_chosen", json);                                           // 옛 읽는 쪽 영향 없음
        Assert.True(migrated.HasMountPreset);
        Assert.False(migrated.IsPresetChosen);

        var old = JsonConvert.DeserializeObject<SensorMountSpec>("{\"panel\":2,\"spot\":0,\"model_id\":5,\"mount_preset_id\":12}")!;
        Assert.Null(old.MountPresetChosen);
        Assert.False(old.IsPresetChosen);

        var chosen = migrated with { MountPresetChosen = true };
        var round = JsonConvert.DeserializeObject<SensorMountSpec>(JsonConvert.SerializeObject(chosen))!;
        Assert.True(round.IsPresetChosen);
        Assert.Equal(chosen, round);
        Assert.False((chosen with { MountPresetId = null }).IsPresetChosen);                            // 프리셋 없이 표지만이면 고른 것이 아니다
    }
}
