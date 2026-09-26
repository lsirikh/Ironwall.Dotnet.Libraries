using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 프리셋 저장소 — 씨앗 · 깨진 파일 · 더 새 판 · 원자적 쓰기 · 내보내기/가져오기(FR-10).
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// <see cref="DevicePresetStore"/> · <see cref="DevicePresetSanitizer"/> · <see cref="DevicePresetSeeds"/> 검증.
/// </summary>
/// <remarks>
/// 테스트마다 <b>새 임시 폴더</b>를 쓰고 끝나면 지운다 — 저장소는 파일이 정본이라 한 폴더를 나눠 쓰면
/// 서로의 씨앗 · 손상 파일을 본다. 시계는 주입한다(<c>DateTime.Now</c> 를 로직이 읽지 않는다).
/// </remarks>
public class DevicePresetStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private DateTimeOffset _now = new(2026, 9, 19, 22, 15, 0, TimeSpan.FromHours(9));

    public DevicePresetStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ironwall-preset-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "device-assembly-presets.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* 임시 폴더는 OS 가 치운다 */ }
        catch (UnauthorizedAccessException) { }
        GC.SuppressFinalize(this);
    }

    private DevicePresetStore NewStore(string? path = null) => new(path ?? _path, () => _now);

    private static DevicePreset BuildPreset(string name, EnumDeviceCategory category, string? id = null) => new()
    {
        Id = id ?? DevicePresetStore.NewId(),
        Name = name,
        Category = category,
        Components = new List<ComponentDefinitionModel> { new() { Key = "door", Type = "DOOR_SENSOR" } },
    };

    private static string EmptyEnvelope()
        => "{ \"schema\": 1, \"saved_at\": \"2026-09-19T22:15:00.0000000+09:00\", \"presets\": [] }";

    #region 씨앗

    [Fact]
    public void should_write_five_seeds_when_file_is_missing()
    {
        var store = NewStore();
        store.Load();

        Assert.Equal(PresetStoreState.Ready, store.State);
        Assert.Null(store.StateMessage);
        Assert.Equal(5, store.Presets.Count);
        Assert.True(File.Exists(_path));
        Assert.All(store.Presets, p => Assert.True(p.IsSeed));

        // 목업의 다섯 — 이름 · 카테고리 · 부품 수
        Assert.Equal(7, store.Presets.Single(p => p.Name == "표준 옥외 함체").Components.Count);
        Assert.Equal(17, store.Presets.Single(p => p.Name == "IO 제어기 16채널").Components.Count);
        Assert.Equal(5, store.Presets.Single(p => p.Name == "슬라이딩 통문").Components.Count);
        Assert.Equal(5, store.Presets.Single(p => p.Name == "PTZ 카메라 (한랭지)").Components.Count);
        Assert.Equal(2, store.Presets.Single(p => p.Name == "펜스 진동 센서").Components.Count);

        Assert.Equal(EnumDeviceCategory.Gate, store.Presets.Single(p => p.Name == "슬라이딩 통문").Category);
        Assert.Equal("actuator", store.Presets.Single(p => p.Name == "슬라이딩 통문").Components[0].Key);
    }

    [Fact]
    public void should_not_duplicate_seeds_when_loaded_twice()
    {
        var first = NewStore();
        first.Load();

        var second = NewStore();
        second.Load();

        Assert.Equal(5, second.Presets.Count);
        Assert.Equal(5, second.Presets.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(PresetStoreState.Ready, second.State);
    }

    [Fact]
    public void should_keep_seed_deleted_when_reloaded()
    {
        var store = NewStore();
        store.Load();
        var deleted = store.Presets.Single(p => p.Name == "펜스 진동 센서").Id;
        Assert.True(store.Delete(deleted).IsSuccess);

        var reopened = NewStore();
        reopened.Load();

        // 씨앗 심기는 '파일이 없거나 깨졌을 때'만 — 지운 씨앗이 되살아나면 지우는 뜻이 없다.
        Assert.Equal(4, reopened.Presets.Count);
        Assert.Null(reopened.Find(deleted));
        Assert.DoesNotContain(reopened.Presets, p => p.Name == "펜스 진동 센서");
    }

    [Fact]
    public void should_keep_fixed_ids_when_seeds_are_read_twice()
    {
        var first = DevicePresetSeeds.All.Select(p => p.Id).ToArray();
        var second = DevicePresetSeeds.All.Select(p => p.Id).ToArray();

        Assert.Equal(first, second);
        Assert.Contains(DevicePresetSeeds.EnclosureStandardId, first);
        Assert.NotSame(DevicePresetSeeds.All, DevicePresetSeeds.All);   // 매번 새 벌 — JObject 를 돌려 쓰지 않는다
    }

    #endregion

    #region 깨진 파일

    [Fact]
    public void should_move_corrupt_file_aside_and_recover_when_content_is_not_json()
    {
        var broken = "{ this is not json ";
        File.WriteAllText(_path, broken);
        var before = File.ReadAllBytes(_path);

        var store = NewStore();
        store.Load();

        var moved = Directory.GetFiles(_dir, "*.corrupt-*");
        Assert.Single(moved);
        Assert.Equal(before, File.ReadAllBytes(moved[0]));                      // 한 글자도 잃지 않았다
        Assert.Equal(PresetStoreState.RecoveredFromCorruption, store.State);
        Assert.Contains("corrupt-20260919-221500", store.StateMessage);
        Assert.Equal(5, store.Presets.Count);
        Assert.False(store.IsReadOnly);
    }

    [Fact]
    public void should_treat_file_as_corrupt_when_envelope_has_no_schema()
    {
        File.WriteAllText(_path, "{ \"presets\": [] }");

        var store = NewStore();
        store.Load();

        Assert.Equal(PresetStoreState.RecoveredFromCorruption, store.State);
        Assert.Single(Directory.GetFiles(_dir, "*.corrupt-*"));
        Assert.Equal(5, store.Presets.Count);
    }

    #endregion

    #region 더 새 판

    [Fact]
    public void should_open_read_only_when_schema_is_newer()
    {
        WriteNewerSchemaFile();
        var before = File.ReadAllBytes(_path);

        var store = NewStore();
        store.Load();

        Assert.Equal(PresetStoreState.ReadOnlyNewerSchema, store.State);
        Assert.True(store.IsReadOnly);
        Assert.Contains("새 버전", store.StateMessage);
        Assert.DoesNotContain("schema", store.StateMessage);
        Assert.Single(store.Presets);

        var exported = Path.Combine(_dir, "exported.json");
        Assert.False(store.Save(BuildPreset("새 프리셋", EnumDeviceCategory.Camera)).IsSuccess);
        Assert.False(store.Rename(store.Presets[0].Id, "다른 이름").IsSuccess);
        Assert.False(store.Duplicate(store.Presets[0].Id, out var copy).IsSuccess);
        Assert.Null(copy);
        Assert.False(store.Delete(store.Presets[0].Id).IsSuccess);

        Assert.True(store.Export(exported).IsSuccess);                 // 내보내기만은 된다 — 건져 낼 통로
        Assert.False(store.Import(exported).IsSuccess);

        Assert.Equal(before, File.ReadAllBytes(_path));                // 파일을 한 바이트도 건드리지 않았다
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
    }

    private void WriteNewerSchemaFile()
    {
        File.WriteAllText(_path, """
            {
              "schema": 3,
              "saved_at": "2026-09-19T22:15:00.0000000+09:00",
              "presets": [
                { "id": "future-1", "name": "다음 판 프리셋", "category": "Camera",
                  "components": [ { "key": "ptz", "type": "PTZ_UNIT" } ],
                  "brand_new_field": { "unknown": true } }
              ]
            }
            """);
    }

    #endregion

    #region 왕복

    [Fact]
    public void should_round_trip_every_member_when_saved_and_reloaded()
    {
        var store = NewStore();
        store.Load();

        var spec = JObject.Parse("""
            { "nested": { "list": [ 1, 2, { "deep": "값" } ], "empty": null }, "text": "2026-01-01" }
            """);
        var overrides = JObject.Parse("""
            { "door": { "enabled": false, "tuning": { "sensitivity": 3 } } }
            """);
        var preset = new DevicePreset
        {
            Id = DevicePresetStore.NewId(),
            Name = "왕복 시험 함체",
            Category = EnumDeviceCategory.Enclosure,
            TypeAxisCode = "Outdoor",
            ExtraAxisCode = "BROADCAST",
            Description = "설명 한 줄",
            Manufacturer = "제조사",
            Model = "모델",
            Firmware = "1.2.3",
            HardwareRev = "revB",
            MaxDetectionRange = 12.5,
            OnvifVersion = "2.6",
            Components = new List<ComponentDefinitionModel>
            {
                new()
                {
                    Key = "door", Type = "door_sensor", Label = "전면 도어", Channel = 4,
                    Position = "문틀", InService = false, Manufacturer = "부품사", Model = "부품모델",
                    Firmware = "0.9", HardwareRev = "revA", Spec = spec,
                },
            },
            Thresholds = JObject.Parse("""{ "temperature": { "high": 45, "low": -10 } }"""),
            Modes = JObject.Parse("""{ "camera_mode": "AUTO", "is_record": true }"""),
            ComponentOverrides = overrides,
        };

        Assert.True(store.Save(preset).IsSuccess);

        var reopened = NewStore();
        reopened.Load();
        var loaded = reopened.Find(preset.Id);

        Assert.NotNull(loaded);
        Assert.Equal(preset.Name, loaded!.Name);
        Assert.Equal(preset.Category, loaded.Category);
        Assert.Equal(preset.TypeAxisCode, loaded.TypeAxisCode);
        Assert.Equal(preset.ExtraAxisCode, loaded.ExtraAxisCode);
        Assert.Equal(preset.Description, loaded.Description);
        Assert.Equal(preset.Manufacturer, loaded.Manufacturer);
        Assert.Equal(preset.Model, loaded.Model);
        Assert.Equal(preset.Firmware, loaded.Firmware);
        Assert.Equal(preset.HardwareRev, loaded.HardwareRev);
        Assert.Equal(preset.MaxDetectionRange, loaded.MaxDetectionRange);
        Assert.Equal(preset.OnvifVersion, loaded.OnvifVersion);
        Assert.False(loaded.IsSeed);
        Assert.Equal(_now, loaded.UpdatedAt);

        var component = Assert.Single(loaded.Components);
        Assert.Equal("door", component.Key);
        Assert.Equal("DOOR_SENSOR", component.Type);                       // 씻으며 대문자로 맞춘다
        Assert.Equal("전면 도어", component.Label);
        Assert.Equal(4, component.Channel);
        Assert.Equal("문틀", component.Position);
        Assert.False(component.InService);
        Assert.Equal("부품사", component.Manufacturer);
        Assert.Equal("부품모델", component.Model);
        Assert.Equal("0.9", component.Firmware);
        Assert.Equal("revA", component.HardwareRev);
        Assert.True(JToken.DeepEquals(spec, component.Spec), "spec 이 글자 그대로 돌아오지 않았다");

        Assert.True(JToken.DeepEquals(preset.Thresholds, loaded.Thresholds));
        Assert.True(JToken.DeepEquals(preset.Modes, loaded.Modes));
        Assert.True(JToken.DeepEquals(overrides, loaded.ComponentOverrides));
        Assert.False(loaded.ComponentOverrides!["door"]!["enabled"]!.Value<bool>());  // enabled:false 가 살아 있다
    }

    /// <summary>
    /// 카메라 씨앗의 <c>camera_mode</c> 는 영상 모드 어휘여야 한다 — "AUTO" 는 서버 8.0.2 가 422 로 거절했다
    /// (라이브 하네스 asm.R5a: 허용 NORMAL · STABILIZATION · BLC · NIGHT_ENHANCE).
    /// </summary>
    [Fact]
    public void should_seed_the_camera_with_a_camera_mode_the_server_accepts()
    {
        var camera = DevicePresetSeeds.All.Single(p => p.Id == DevicePresetSeeds.CameraPtzColdId);

        Assert.Contains((string?)camera.Modes!["camera_mode"], new[] { "NORMAL", "STABILIZATION", "BLC", "NIGHT_ENHANCE" });
        Assert.Equal("AUTO", (string?)camera.Modes!["day_night_mode"]);   // 주야간 모드는 AUTO 를 받는다
    }

    /// <summary>이미 파일에 쓰인 옛 씨앗(손대지 않은 것)은 읽을 때 지금 판의 씨앗으로 바뀐다.</summary>
    [Fact]
    public void should_refresh_an_untouched_seed_when_the_file_still_holds_the_old_seed_value()
    {
        NewStore().Load();
        RewriteCameraMode("AUTO");                  // 고치기 전 판이 심어 둔 파일

        var reopened = NewStore();
        reopened.Load();

        var camera = reopened.Presets.Single(p => p.Id == DevicePresetSeeds.CameraPtzColdId);
        Assert.Equal("NORMAL", (string?)camera.Modes!["camera_mode"]);
        Assert.True(camera.IsSeed);
    }

    /// <summary>사용자의 손이 닿은 씨앗(이름 바꾸기 → 시각이 바뀜)은 건드리지 않는다.</summary>
    [Fact]
    public void should_leave_a_seed_alone_when_the_user_has_touched_it()
    {
        var store = NewStore();
        store.Load();
        Assert.True(store.Rename(DevicePresetSeeds.CameraPtzColdId, "우리 현장 PTZ").IsSuccess);
        RewriteCameraMode("BLC");

        var reopened = NewStore();
        reopened.Load();

        var camera = reopened.Presets.Single(p => p.Id == DevicePresetSeeds.CameraPtzColdId);
        Assert.Equal("BLC", (string?)camera.Modes!["camera_mode"]);
        Assert.Equal("우리 현장 PTZ", camera.Name);
    }

    private void RewriteCameraMode(string value)
    {
        var file = JObject.Parse(File.ReadAllText(_path));
        var entry = ((JArray)file["presets"]!).Single(p => (string?)p["id"] == DevicePresetSeeds.CameraPtzColdId);
        entry["modes"]!["camera_mode"] = value;
        File.WriteAllText(_path, file.ToString());
    }

    [Fact]
    public void should_round_trip_seed_thresholds_when_value_is_null()
    {
        var store = NewStore();
        store.Load();

        var reopened = NewStore();
        reopened.Load();

        var seed = reopened.Presets.Single(p => p.Id == DevicePresetSeeds.EnclosureStandardId);
        Assert.Equal(JTokenType.Null, seed.Thresholds!["humidity"]!["low"]!.Type);
    }

    #endregion

    #region 씻기 · 금지 사실

    [Fact]
    public void should_strip_device_only_facts_when_sanitized()
    {
        var preset = new DevicePreset
        {
            Id = "x",
            Name = "  앞뒤 공백  ",
            Category = EnumDeviceCategory.Enclosure,
            Components = new List<ComponentDefinitionModel>
            {
                new()
                {
                    Key = "door", Type = "door_sensor", Serial = "SN-001",
                    InstalledAt = "2026-01-01", ReplacedAt = "2026-02-02",
                },
            },
            ComponentOverrides = JObject.Parse("""{ "door": { "enabled": true }, "ghost": { "enabled": true } }"""),
        };

        var clean = DevicePresetSanitizer.Sanitize(preset);

        Assert.Equal("앞뒤 공백", clean.Name);
        var component = Assert.Single(clean.Components);
        Assert.Null(component.Serial);
        Assert.Null(component.InstalledAt);
        Assert.Null(component.ReplacedAt);
        Assert.Equal("DOOR_SENSOR", component.Type);
        Assert.NotNull(clean.ComponentOverrides!["door"]);
        Assert.Null(clean.ComponentOverrides!["ghost"]);                 // 주인 없는 재정의는 버린다

        Assert.Equal("SN-001", preset.Components[0].Serial);             // 원본은 그대로
    }

    [Fact]
    public void should_return_empty_components_when_collection_is_null()
    {
        var preset = new DevicePreset
        {
            Id = "x", Name = "빈 것", Category = EnumDeviceCategory.Etc,
            Components = null!, ComponentOverrides = null, Thresholds = null, Modes = null,
        };

        var clean = DevicePresetSanitizer.Sanitize(preset);

        Assert.Empty(clean.Components);
        Assert.Empty(DevicePresetSanitizer.FindForbiddenFacts(clean));
    }

    [Fact]
    public void should_flag_forbidden_facts_when_spec_or_override_carries_them()
    {
        var preset = new DevicePreset
        {
            Id = "x",
            Name = "금지 사실",
            Category = EnumDeviceCategory.Enclosure,
            Components = new List<ComponentDefinitionModel>
            {
                new()
                {
                    Key = "heater", Type = "HEATER",
                    Spec = JObject.Parse("""{ "states": [ "ON", "OFF" ], "watt": 30 }"""),
                },
            },
            ComponentOverrides = JObject.Parse("""
                { "heater": { "enabled": false, "commands": [ "ON" ], "setpoint": 5 } }
                """),
        };

        var found = DevicePresetSanitizer.FindForbiddenFacts(preset);

        Assert.Contains("components[0].spec.states", found);
        Assert.Contains("component_overrides.heater.commands", found);
        Assert.DoesNotContain(found, f => f.EndsWith(".enabled", StringComparison.Ordinal));  // 재정의의 enabled 는 정본
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void should_report_clean_when_preset_has_no_forbidden_facts()
    {
        Assert.All(DevicePresetSeeds.All, seed => Assert.Empty(DevicePresetSanitizer.FindForbiddenFacts(seed)));
    }

    #endregion

    #region 이름 규칙

    [Fact]
    public void should_reject_duplicate_name_when_same_category()
    {
        var store = NewStore();
        store.Load();
        Assert.True(store.Save(BuildPreset("같은 이름", EnumDeviceCategory.Camera)).IsSuccess);

        var result = store.Save(BuildPreset("같은 이름", EnumDeviceCategory.Camera));

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Single(store.Presets, p => p.Name == "같은 이름");
    }

    [Fact]
    public void should_allow_same_name_when_category_differs()
    {
        var store = NewStore();
        store.Load();

        Assert.True(store.Save(BuildPreset("같은 이름", EnumDeviceCategory.Camera)).IsSuccess);
        Assert.True(store.Save(BuildPreset("같은 이름", EnumDeviceCategory.Sensor)).IsSuccess);

        Assert.Equal(2, store.Presets.Count(p => p.Name == "같은 이름"));
    }

    [Fact]
    public void should_replace_in_place_when_same_id_is_saved_again()
    {
        var store = NewStore();
        store.Load();
        var preset = BuildPreset("고칠 프리셋", EnumDeviceCategory.Camera);
        Assert.True(store.Save(preset).IsSuccess);

        Assert.True(store.Save(preset with { Description = "고쳤다" }).IsSuccess);

        Assert.Single(store.Presets, p => p.Id == preset.Id);
        Assert.Equal("고쳤다", store.Find(preset.Id)!.Description);
    }

    [Fact]
    public void should_reject_save_when_name_is_blank_or_too_long()
    {
        var store = NewStore();
        store.Load();

        Assert.False(store.Save(BuildPreset("   ", EnumDeviceCategory.Camera)).IsSuccess);
        Assert.False(store.Save(BuildPreset(new string('가', DevicePresetStore.MaxNameLength + 1), EnumDeviceCategory.Camera)).IsSuccess);
        Assert.True(store.Save(BuildPreset(new string('가', DevicePresetStore.MaxNameLength), EnumDeviceCategory.Camera)).IsSuccess);
    }

    [Fact]
    public void should_rename_when_new_name_is_free()
    {
        var store = NewStore();
        store.Load();
        var id = store.Presets.Single(p => p.Name == "펜스 진동 센서").Id;

        Assert.True(store.Rename(id, " 바뀐 이름 ").IsSuccess);
        Assert.Equal("바뀐 이름", store.Find(id)!.Name);

        Assert.False(store.Rename(id, "  ").IsSuccess);
        Assert.False(store.Rename("없는-id", "무엇이든").IsSuccess);
    }

    [Fact]
    public void should_copy_with_new_id_and_suffix_when_duplicated()
    {
        var store = NewStore();
        store.Load();
        var source = store.Presets.Single(p => p.Name == "표준 옥외 함체");

        Assert.True(store.Duplicate(source.Id, out var first).IsSuccess);
        Assert.True(store.Duplicate(source.Id, out var second).IsSuccess);

        Assert.NotNull(first);
        Assert.NotEqual(source.Id, first!.Id);
        Assert.Equal("표준 옥외 함체 복사본", first.Name);
        Assert.Equal("표준 옥외 함체 복사본 2", second!.Name);
        Assert.False(first.IsSeed);
        Assert.Equal(source.Components.Count, first.Components.Count);
        Assert.Equal(_now, first.UpdatedAt);
    }

    [Fact]
    public void should_remove_from_file_when_deleted()
    {
        var store = NewStore();
        store.Load();
        var id = store.Presets[0].Id;

        Assert.True(store.Delete(id).IsSuccess);
        Assert.False(store.Delete(id).IsSuccess);

        var reopened = NewStore();
        reopened.Load();
        Assert.Equal(4, reopened.Presets.Count);
    }

    #endregion

    #region 차례 · 조회

    [Fact]
    public void should_order_by_name_within_category_when_listed()
    {
        var store = NewStore();
        store.Load();
        Assert.True(store.Save(BuildPreset("가 카메라", EnumDeviceCategory.Camera)).IsSuccess);
        Assert.True(store.Save(BuildPreset("하 카메라", EnumDeviceCategory.Camera)).IsSuccess);

        var cameras = store.ForCategory(EnumDeviceCategory.Camera);

        Assert.Equal(3, cameras.Count);
        Assert.Equal(cameras.Select(p => p.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase), cameras.Select(p => p.Name));

        var categories = store.Presets.Select(p => (int)p.Category).ToList();
        Assert.Equal(categories.OrderBy(c => c), categories);
        Assert.Empty(store.ForCategory(EnumDeviceCategory.Lamp));
        Assert.Null(store.Find("없는-id"));
    }

    #endregion

    #region 내보내기 · 가져오기

    [Fact]
    public void should_import_with_new_ids_and_clash_suffix_when_exported_file_is_read()
    {
        var source = NewStore();
        source.Load();
        var exported = Path.Combine(_dir, "exported.json");
        var export = source.Export(exported);
        Assert.True(export.IsSuccess);
        Assert.Contains("5", export.Message);

        var otherDir = Path.Combine(_dir, "other");
        Directory.CreateDirectory(otherDir);
        var otherPath = Path.Combine(otherDir, "device-assembly-presets.json");
        File.WriteAllText(otherPath, EmptyEnvelope());

        var target = NewStore(otherPath);
        target.Load();
        Assert.Empty(target.Presets);

        var first = target.Import(exported);

        Assert.True(first.IsSuccess);
        Assert.Equal(5, target.Presets.Count);
        Assert.Equal(source.Presets.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal),
                     target.Presets.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(target.Presets, p => Assert.DoesNotContain(p.Id, source.Presets.Select(s => s.Id)));
        Assert.All(target.Presets, p => Assert.False(p.IsSeed));

        var imported = target.Presets.Single(p => p.Name == "표준 옥외 함체");
        var original = source.Presets.Single(p => p.Name == "표준 옥외 함체");
        Assert.Equal(original.Components.Count, imported.Components.Count);
        Assert.True(JToken.DeepEquals(original.ComponentOverrides, imported.ComponentOverrides));

        var second = target.Import(exported);

        Assert.True(second.IsSuccess);
        Assert.Equal(10, target.Presets.Count);
        Assert.Contains("(2)", second.Message);
        Assert.Contains(target.Presets, p => p.Name == "표준 옥외 함체 (2)");
    }

    [Fact]
    public void should_refuse_import_without_touching_store_when_file_is_corrupt_or_newer()
    {
        var store = NewStore();
        store.Load();
        var before = File.ReadAllBytes(_path);

        var corrupt = Path.Combine(_dir, "corrupt.json");
        File.WriteAllText(corrupt, "not json at all");
        var newer = Path.Combine(_dir, "newer.json");
        File.WriteAllText(newer, "{ \"schema\": 9, \"presets\": [] }");

        Assert.False(store.Import(corrupt).IsSuccess);
        Assert.False(store.Import(newer).IsSuccess);
        Assert.False(store.Import(Path.Combine(_dir, "없는파일.json")).IsSuccess);

        Assert.Equal(5, store.Presets.Count);
        Assert.Equal(before, File.ReadAllBytes(_path));
        Assert.Equal(PresetStoreState.Ready, store.State);
        Assert.Empty(Directory.GetFiles(_dir, "*.corrupt-*"));           // 가져오기 실패는 남의 파일을 옮기지 않는다
    }

    [Fact]
    public void should_export_only_chosen_when_ids_are_given()
    {
        var store = NewStore();
        store.Load();
        var chosen = store.Presets.Single(p => p.Name == "슬라이딩 통문").Id;
        var exported = Path.Combine(_dir, "one.json");

        Assert.True(store.Export(exported, new[] { chosen, "없는-id" }).IsSuccess);

        var otherPath = Path.Combine(_dir, "other", "presets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(otherPath)!);
        File.WriteAllText(otherPath, EmptyEnvelope());
        var target = NewStore(otherPath);
        target.Load();
        Assert.True(target.Import(exported).IsSuccess);

        var only = Assert.Single(target.Presets);
        Assert.Equal("슬라이딩 통문", only.Name);
    }

    #endregion

    #region 파일 다루기

    [Fact]
    public void should_skip_entry_when_category_is_unknown()
    {
        File.WriteAllText(_path, """
            {
              "schema": 1,
              "saved_at": "2026-09-19T22:15:00.0000000+09:00",
              "presets": [
                { "id": "a", "name": "아는 것", "category": "Camera", "components": [] },
                { "id": "b", "name": "모르는 것", "category": "Teleporter", "components": [] },
                { "id": "c", "name": "숫자로 적은 것", "category": "3", "components": [] }
              ]
            }
            """);

        var store = NewStore();
        store.Load();

        Assert.Equal(PresetStoreState.Ready, store.State);
        var kept = Assert.Single(store.Presets);
        Assert.Equal("아는 것", kept.Name);
        Assert.Contains("2", store.StateMessage);
        Assert.Contains("건너뛰었습니다", store.StateMessage);
    }

    /// <summary>
    /// ★ 건너뛴 줄은 <b>다음 저장에서도 파일에 남아 있어야 한다</b>.
    /// </summary>
    /// <remarks>
    /// 종전에는 읽을 때 버리고 쓸 때 안 써서, 사용자가 프리셋 하나를 저장하는 순간
    /// 모르는 카테고리의 줄이 <b>영구히 사라졌다</b>. 그 줄은 더 새 판에서 멀쩡한 프리셋이고,
    /// "이번 판이 못 읽는다"가 지울 이유가 되지 않는다.
    /// </remarks>
    [Fact]
    public void should_keep_the_unknown_entry_in_the_file_when_another_preset_is_saved()
    {
        var unknown = JObject.Parse("""
            {
              "id": "b",
              "name": "모르는 것",
              "category": "Teleporter",
              "type_axis": "Warp",
              "components": [ { "key": "coil", "type": "WARP_COIL" } ],
              "미래의칸": { "값": 1 }
            }
            """);

        File.WriteAllText(_path, $$"""
            {
              "schema": 1,
              "saved_at": "2026-09-19T22:15:00.0000000+09:00",
              "presets": [
                { "id": "a", "name": "아는 것", "category": "Camera", "components": [] },
                {{unknown.ToString()}}
              ]
            }
            """);

        var store = NewStore();
        store.Load();
        Assert.Contains("건너뛰었습니다", store.StateMessage);

        Assert.True(store.Save(BuildPreset("새로 만든 것", EnumDeviceCategory.Lamp)).IsSuccess);

        var written = (JArray)JObject.Parse(File.ReadAllText(_path))["presets"]!;
        var survivor = written.OfType<JObject>().Single(e => (string?)e["id"] == "b");
        Assert.True(JToken.DeepEquals(unknown, survivor));   // 모르는 칸까지 글자 그대로

        // 아는 줄도 그대로 있고, 새로 저장한 줄이 더해졌다.
        Assert.Equal(3, written.Count);
        Assert.Contains(written.OfType<JObject>(), e => (string?)e["name"] == "새로 만든 것");
    }

    /// <summary>지우기 · 이름 바꾸기도 같은 통로(<c>Persist</c>)를 지난다 — 한 번 더 못 박는다.</summary>
    [Fact]
    public void should_keep_the_unknown_entry_in_the_file_when_a_preset_is_renamed_or_deleted()
    {
        File.WriteAllText(_path, """
            {
              "schema": 1,
              "saved_at": "2026-09-19T22:15:00.0000000+09:00",
              "presets": [
                { "id": "a", "name": "아는 것", "category": "Camera", "components": [] },
                { "id": "b", "name": "모르는 것", "category": "Teleporter", "components": [] }
              ]
            }
            """);

        var store = NewStore();
        store.Load();

        Assert.True(store.Rename("a", "이름 바꾼 것").IsSuccess);
        Assert.Contains("\"id\": \"b\"", File.ReadAllText(_path));

        Assert.True(store.Delete("a").IsSuccess);
        var written = (JArray)JObject.Parse(File.ReadAllText(_path))["presets"]!;
        Assert.Equal("b", (string?)Assert.IsType<JObject>(Assert.Single(written))["id"]);
    }

    /// <summary>전부 내보낼 때도 같이 나간다 — 더 새 판 · 손상 때 건져 낼 유일한 통로라서다.</summary>
    [Fact]
    public void should_carry_the_unknown_entry_when_everything_is_exported()
    {
        File.WriteAllText(_path, """
            {
              "schema": 1,
              "saved_at": "2026-09-19T22:15:00.0000000+09:00",
              "presets": [
                { "id": "a", "name": "아는 것", "category": "Camera", "components": [] },
                { "id": "b", "name": "모르는 것", "category": "Teleporter", "components": [] }
              ]
            }
            """);

        var store = NewStore();
        store.Load();

        var target = Path.Combine(_dir, "내보낸 것.json");
        Assert.True(store.Export(target).IsSuccess);

        var written = (JArray)JObject.Parse(File.ReadAllText(target))["presets"]!;
        Assert.Contains(written.OfType<JObject>(), e => (string?)e["id"] == "b");

        // 고른 것만 내보낼 때는 고른 것만이다.
        var only = Path.Combine(_dir, "고른 것.json");
        Assert.True(store.Export(only, new[] { store.Presets[0].Id }).IsSuccess);
        var chosen = (JArray)JObject.Parse(File.ReadAllText(only))["presets"]!;
        Assert.DoesNotContain(chosen.OfType<JObject>(), e => (string?)e["id"] == "b");
    }

    [Fact]
    public void should_leave_no_temp_file_when_save_succeeds()
    {
        var store = NewStore();
        store.Load();

        Assert.True(store.Save(BuildPreset("임시 파일 확인", EnumDeviceCategory.Camera)).IsSuccess);

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
        Assert.Single(Directory.GetFiles(_dir, "*.json"));
    }

    [Fact]
    public void should_create_directory_when_it_does_not_exist()
    {
        var nested = Path.Combine(_dir, "없던", "폴더", "device-assembly-presets.json");
        var store = NewStore(nested);

        store.Load();

        Assert.Equal(PresetStoreState.Ready, store.State);
        Assert.True(File.Exists(nested));
        Assert.Equal(5, store.Presets.Count);
    }

    [Fact]
    public void should_use_injected_clock_when_saving()
    {
        var store = NewStore();
        store.Load();
        _now = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.FromHours(9));

        var preset = BuildPreset("시계 시험", EnumDeviceCategory.Camera);
        Assert.True(store.Save(preset).IsSuccess);

        Assert.Equal(_now, store.Find(preset.Id)!.UpdatedAt);
        Assert.Contains("2030-01-02T03:04:05", File.ReadAllText(_path));
    }

    [Fact]
    public void should_raise_changed_once_per_successful_mutation()
    {
        var store = NewStore();
        store.Load();
        var count = 0;
        store.Changed += (_, _) => count++;

        Assert.True(store.Save(BuildPreset("알림 시험", EnumDeviceCategory.Camera)).IsSuccess);
        Assert.Equal(1, count);

        Assert.False(store.Save(BuildPreset("알림 시험", EnumDeviceCategory.Camera)).IsSuccess);   // 이름 충돌
        Assert.False(store.Delete("없는-id").IsSuccess);
        Assert.False(store.Rename("없는-id", "무엇이든").IsSuccess);
        Assert.Equal(1, count);

        var id = store.Presets.Single(p => p.Name == "알림 시험").Id;
        Assert.True(store.Rename(id, "알림 시험 2").IsSuccess);
        Assert.True(store.Duplicate(id, out _).IsSuccess);
        Assert.True(store.Delete(id).IsSuccess);
        Assert.Equal(4, count);
    }

    [Fact]
    public void should_throw_when_path_is_blank()
    {
        Assert.Throws<ArgumentException>(() => new DevicePresetStore("   "));
    }

    [Fact]
    public void should_give_unique_ids_when_new_id_is_called()
    {
        var ids = Enumerable.Range(0, 50).Select(_ => DevicePresetStore.NewId()).ToList();

        Assert.Equal(50, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.Equal(32, id.Length));
    }

    [Fact]
    public void should_default_to_local_app_data_when_default_path_is_used()
    {
        var path = DevicePresetStore.DefaultPath;

        Assert.EndsWith(Path.Combine("Ironwall", "device-assembly-presets.json"), path);
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), path);
    }

    #endregion
}
