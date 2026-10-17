using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Monitoring.Models.Tests;

/// <summary>
/// 설정 <c>SensorPresets.Source</c> 읽기 · 저장소 고르기(sensor-mount-preset §5.3 · W1 "GOP API(읽기 전용)") — 순수 시험(지도 DB 접속 없음).
/// </summary>
public class SensorPresetSettingsTests
{
    [Fact, Trait("Scenario", "MNT-240")]
    public void should_default_to_gis_db_when_section_or_key_is_missing()
    {
        Assert.Equal(SensorPresetSource.GisDb, SensorPresetSettings.Parse(null).Source);
        Assert.Equal(SensorPresetSource.GisDb, SensorPresetSettings.Parse("{}").Source);
        Assert.Equal(SensorPresetSource.GisDb, SensorPresetSettings.Parse("{ \"SensorPresets\": {} }").Source);
        Assert.Null(SensorPresetSettings.Parse("{ \"SensorPresets\": { \"Source\": null } }").Error);
    }

    [Fact, Trait("Scenario", "MNT-241")]
    public void should_read_gop_api_from_root_or_app_settings_section_ignoring_case()
    {
        Assert.Equal(SensorPresetSource.GopApi, SensorPresetSettings.Parse("{ \"SensorPresets\": { \"Source\": \"GopApi\" } }").Source);
        Assert.Equal(SensorPresetSource.GopApi, SensorPresetSettings.Parse("{ \"AppSettings\": { \"sensorPresets\": { \"source\": \"gopapi\" } } }").Source);
        Assert.Equal(SensorPresetSource.GisDb, SensorPresetSettings.Parse("{ \"SensorPresets\": { \"Source\": \"GisDb\" } }").Source);
    }

    [Fact, Trait("Scenario", "MNT-242")]
    public void should_fall_back_to_gis_db_and_say_why_when_value_is_unknown_or_json_is_broken()
    {
        var unknown = SensorPresetSettings.Parse("{ \"SensorPresets\": { \"Source\": \"Cloud\" } }");
        Assert.Equal(SensorPresetSource.GisDb, unknown.Source);
        Assert.Contains("Cloud", unknown.Error);
        var broken = SensorPresetSettings.Parse("{ 깨진");
        Assert.Equal(SensorPresetSource.GisDb, broken.Source);
        Assert.NotNull(broken.Error);
        var missing = SensorPresetSettings.ReadFile(Path.Combine(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}.json"));
        Assert.Equal(SensorPresetSource.GisDb, missing.Source);
    }

    [Fact, Trait("Scenario", "MNT-243")]
    public void should_read_source_from_a_settings_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mnt-243-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ \"SensorPresets\": { \"Source\": \"GopApi\" } }");
            Assert.Equal((SensorPresetSource.GopApi, (string?)null), SensorPresetSettings.ReadFile(path));
        }
        finally { File.Delete(path); }
    }

    [Fact, Trait("Scenario", "MNT-244")]
    public async Task should_open_read_only_gop_api_store_that_shows_builtin_models_and_refuses_writes()
    {
        var gisDbCalls = 0;
        var repo = SensorPresetRepositoryFactory.Create(SensorPresetSource.GopApi, () => { gisDbCalls++; return new InMemorySensorPresetRepository(); });
        Assert.Equal(SensorPresetSource.GopApi, repo.Source);
        Assert.False(repo.CanWrite);
        Assert.Equal(0, gisDbCalls);                                                                    // GIS DB 는 만들지 않는다
        var models = await repo.GetModelsAsync();
        Assert.Equal(7, models.Count);                                                                  // 자리만인 GOP API 대신 회사 기본 7종
        Assert.NotEmpty(await repo.GetMountPresetsAsync(SensorPresetCatalog.MULTI));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.SaveModelAsync(models[0]));
        await Assert.ThrowsAsync<NotSupportedException>(() => repo.DeletePresetAsync(new PresetKey(PresetKind.Mount, 1)));
    }

    [Fact, Trait("Scenario", "MNT-245")]
    public void should_build_gis_db_store_only_when_source_is_gis_db()
    {
        var gisDb = new InMemorySensorPresetRepository();
        Assert.Same(gisDb, SensorPresetRepositoryFactory.Create(SensorPresetSource.GisDb, () => gisDb));
        Assert.IsType<InMemorySensorPresetRepository>(SensorPresetRepositoryFactory.Create(SensorPresetSource.Memory, () => throw new InvalidOperationException()));
        Assert.Throws<ArgumentNullException>(() => SensorPresetRepositoryFactory.Create(SensorPresetSource.GisDb, null!));
    }
}
