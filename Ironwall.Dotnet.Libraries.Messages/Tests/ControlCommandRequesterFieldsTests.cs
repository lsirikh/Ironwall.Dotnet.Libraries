using System;
using System.Collections.Generic;
using System.Linq;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Messages.Tests;

/// <summary>
/// 브로커 연동설계 v2.0.12 §7 머리 "제어 명령 공통 선택 칸" — 방송 · TTS · 경광등 · PTZ · §8.3 설정 · 문 개폐 ·
/// PTZ_AIM_LOCATION body 가 <c>requested_by</c> · <c>requested_client_id</c> 를 싣고, 값이 없으면 빼며, 다른 키는 그대로인지 본다.
/// </summary>
public class ControlCommandRequesterFieldsTests
{
    private const string LoginId = "op01";
    private const string ClientId = "gis-7f3a9c2e41b6";

    /// <summary>공통 기반을 상속한 제어 명령 body 전부(명령 · 원래 키 하나).</summary>
    public static IEnumerable<object[]> Commands() => new List<object[]>
    {
        new object[] { "BROADCAST_PLAY", new BroadcastPlayBodyDto { SpeakerIds = { 1 }, FileGroupId = 3 }, "file_group_id" },
        new object[] { "BROADCAST_STOP", new BroadcastStopBodyDto { SpeakerIds = { 1 } }, "speaker_ids" },
        new object[] { "BROADCAST_TEST", new BroadcastTestBodyDto { SpeakerIds = { 1 }, DurationSec = 5 }, "duration_sec" },
        new object[] { "BROADCAST_MIC_START", new BroadcastMicBodyDto { SpeakerIds = { 1 } }, "speaker_ids" },
        new object[] { "TTS", new TtsBodyDto { SpeakerIds = { 1 }, Message = "경고" }, "message" },
        new object[] { "LAMP_CLEAR", new LampClearBodyDto { LampIds = new List<int> { 1 } }, "lamp_ids" },
        new object[] { "LAMP_OFF", new LampOffBodyDto { LampIds = { 1 } }, "lamp_ids" },
        new object[] { "LAMP_COLOR_SET", new LampColorSetBodyDto { LampIds = { 1 } }, "color" },
        new object[] { "LAMP_BUZZER_SET", new LampBuzzerSetBodyDto { LampIds = { 1 } }, "buzzer" },
        new object[] { "LAMP_COLOR_TEST", new LampColorTestBodyDto { LampIds = { 1 }, Color = "GREEN" }, "color" },
        new object[] { "LAMP_BUZZER_TEST", new LampBuzzerTestBodyDto { LampIds = { 1 }, Buzzer = "BEEP" }, "buzzer" },
        new object[] { "PTZ_CONTROL", new PtzControlBodyDto { CameraId = 9, Preset = 2 }, "preset" },
        new object[] { "CAMERA_MODE_SET", new CameraModeSetBodyDto { CameraId = 9, CameraMode = "DAY" }, "camera_mode" },
        new object[] { "DAY_NIGHT_SET", new DayNightSetBodyDto { CameraId = 9, DayNightMode = "AUTO" }, "day_night_mode" },
        new object[] { "FAN_SET", new FanSetBodyDto { CameraId = 9, Fan = "on" }, "fan" },
        new object[] { "HEADLIGHT_SET", new HeadlightSetBodyDto { CameraId = 9, Headlight = "on" }, "headlight" },
        new object[] { "HEATER_SET", new HeaterSetBodyDto { CameraId = 9, Heater = "on" }, "heater" },
        new object[] { "PALETTE_SET", new PaletteSetBodyDto { CameraId = 9, Palette = "WHITE_HOT" }, "palette" },
        new object[] { "POWER_SET", new PowerSetBodyDto { CameraId = 9, Power = "on" }, "power" },
        new object[] { "TRACKING_SET", new TrackingSetBodyDto { CameraId = 9, Tracking = "on" }, "tracking" },
        new object[] { "WEATHER_MODE_SET", new WeatherModeSetBodyDto { CameraId = 9, WeatherMode = "FOG" }, "weather_mode" },
        new object[] { "WIPER_SET", new WiperSetBodyDto { CameraId = 9, Wiper = "on" }, "wiper" },
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void should_carry_requested_by_and_client_id_when_values_given(string cmd, ControlCommandBodyDto body, string ownKey)
    {
        var before = JObject.FromObject(body).Properties().Select(p => p.Name).ToList();
        body.RequestedBy = LoginId;
        body.RequestedClientId = ClientId;

        var b = JObject.Parse(JsonConvert.SerializeObject(body));

        Assert.Equal(LoginId, (string?)b["requested_by"]);
        Assert.Equal(ClientId, (string?)b["requested_client_id"]);
        Assert.NotNull(b.Property(ownKey));
        // 받는 쪽 계약 — 원래 키는 순서까지 그대로, 두 칸만 뒤에 붙는다.
        Assert.True(before.Concat(new[] { "requested_by", "requested_client_id" }).SequenceEqual(b.Properties().Select(p => p.Name)), cmd);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public void should_omit_both_fields_when_values_missing(string cmd, ControlCommandBodyDto body, string ownKey)
    {
        var json = JsonConvert.SerializeObject(body);

        Assert.DoesNotContain("requested_by", json);
        Assert.DoesNotContain("requested_client_id", json);
        Assert.True(json.Contains($"\"{ownKey}\""), cmd);
    }

    [Fact]
    public void should_carry_requested_client_id_next_to_requested_by_when_door_command_serialized()
    {
        var b = JObject.FromObject(new DoorSetBodyDto
        {
            DeviceId = 77, Command = DoorSetBodyDto.Open, DeviceDescription = "[gate] (id: 77)",
            RequestedBy = LoginId, RequestedClientId = ClientId, RequestedAt = "2026-10-15T10:00:00.000+09:00",
        });

        Assert.Equal(LoginId, (string?)b["requested_by"]);
        Assert.Equal(ClientId, (string?)b["requested_client_id"]);
        Assert.Equal(new[] { "device_id", "command", "device_description", "requested_by", "requested_client_id", "requested_at" },
                     b.Properties().Select(p => p.Name));
    }

    [Fact]
    public void should_omit_requested_client_id_when_door_command_has_no_terminal()
    {
        var b = JObject.FromObject(new DoorSetBodyDto { DeviceId = 77, RequestedAt = "2026-10-15T10:00:00.000+09:00" });

        Assert.Null(b.Property("requested_by"));
        Assert.Null(b.Property("requested_client_id"));
    }

    [Fact]
    public void should_carry_requested_client_id_when_aim_location_serialized()
    {
        var b = JObject.FromObject(new CameraAimLocationBodyDto { CameraId = 5, RequestedBy = LoginId, RequestedClientId = ClientId });

        Assert.Equal(LoginId, (string?)b["requested_by"]);
        Assert.Equal(ClientId, (string?)b["requested_client_id"]);
    }

    [Fact]
    public void should_omit_requested_client_id_but_keep_requested_by_when_aim_location_has_no_terminal()
    {
        var b = JObject.FromObject(new CameraAimLocationBodyDto { CameraId = 5, RequestedBy = string.Empty });

        Assert.Null(b.Property("requested_client_id"));
        Assert.NotNull(b.Property("requested_by"));   // 기존 계약 그대로(빈 문자열도 싣는다)
    }
}
