using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Devices;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// N-11 FR-12 — 장비의 소속 부대만 바꾸는 PATCH 본문은 <b>받은 값을 하나도 되돌리지 않는다</b>.
/// </summary>
/// <remarks>
/// <para><c>PATCH</c> 는 RFC 7396 병합이라 본문의 <c>null</c> 은 <b>그 키의 삭제</b>다.
/// 축 모드에서 조건 없이 직렬화되는 키(스피커·경광등 <c>description</c> · <c>connection</c> · <c>device_config</c>)를
/// 안 채우면 소속을 바꾸는 한 번의 조작이 설명과 접속 설정을 조용히 지운다.
/// 이 판정을 사람 눈에 맡기지 않도록 <b>일곱 카테고리를 전수로</b> 잠근다
/// (선례: <c>PresetRegisterTests.should_never_null_or_reset_a_fetched_value_in_the_apply_body</c>).</para>
/// </remarks>
[Collection("CaliburnIoC")]   // 컬렉션 수를 늘리면 정적 IoC 를 바꾸는 이웃과 겹칠 확률이 올라간다 — 같이 직렬화한다.
public class UnitAssignRequestTests
{
    private const int TARGET_UNIT = 42;

    private static string Wire(object dto) => JsonConvert.SerializeObject(dto, PresetRequestBuilder.WireSettings);

    #region - 전수 감사 -
    [Theory]
    [InlineData(EnumDeviceCategory.Controller)]
    [InlineData(EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceCategory.Camera)]
    [InlineData(EnumDeviceCategory.Speaker)]
    [InlineData(EnumDeviceCategory.Enclosure)]
    [InlineData(EnumDeviceCategory.Lamp)]
    [InlineData(EnumDeviceCategory.Gate)]
    public void should_never_null_or_reset_a_fetched_value_in_the_assign_body(EnumDeviceCategory category)
    {
        var fetched = Filled(category);

        var body = JObject.Parse(Wire(UnitAssignRequestBuilder.Build(fetched, TARGET_UNIT)));

        // (a) 본문에 null 이 하나도 없다 — null 하나가 곧 "우리가 베끼지 않은 칸" 이다.
        foreach (var property in body.Descendants().OfType<JProperty>())
            Assert.True(property.Value.Type != JTokenType.Null, $"{category}: {property.Path} 가 null 로 나간다");

        // (b) 실린 키는 전부 "아무것도 바꾸지 않는 본문" 과 같다 — unit_id 하나만 우리 것이다.
        fetched.UseAxisWrite = true;
        var unchanged = JObject.Parse(Wire(fetched));

        foreach (var property in body.Properties())
        {
            if (property.Name == "unit_id") continue;
            Assert.True(JToken.DeepEquals(property.Value, unchanged[property.Name]),
                        $"{category}.{property.Name}: {property.Value} ≠ {unchanged[property.Name]}");
        }

        Assert.Equal(TARGET_UNIT, (int)body["unit_id"]!);
    }

    [Theory]
    [InlineData(EnumDeviceCategory.Controller)]
    [InlineData(EnumDeviceCategory.Sensor)]
    [InlineData(EnumDeviceCategory.Camera)]
    [InlineData(EnumDeviceCategory.Speaker)]
    [InlineData(EnumDeviceCategory.Enclosure)]
    [InlineData(EnumDeviceCategory.Lamp)]
    [InlineData(EnumDeviceCategory.Gate)]
    public void should_not_send_hardware_spec_when_only_the_unit_changes(EnumDeviceCategory category)
    {
        // 축이라도 components 는 배열이라 통째 교체다 — 소속을 바꾸는 길에 부품이 실리면 안 된다.
        var body = JObject.Parse(Wire(UnitAssignRequestBuilder.Build(Filled(category), TARGET_UNIT)));

        Assert.DoesNotContain("hardware_spec", body.Properties().Select(p => p.Name));
        Assert.DoesNotContain("geolocation", body.Properties().Select(p => p.Name));
        Assert.DoesNotContain("group_ids", body.Properties().Select(p => p.Name));
        Assert.DoesNotContain("id", body.Properties().Select(p => p.Name));
    }

    [Fact]
    public void should_keep_the_speaker_description_when_moving_it_to_another_unit()
    {
        var body = JObject.Parse(Wire(UnitAssignRequestBuilder.Build(Filled(EnumDeviceCategory.Speaker), TARGET_UNIT)));

        Assert.Equal("북측 9구간 방송", (string?)body["description"]);
        Assert.Equal("ADMIN", (string?)body["speaker_role"] ?? (string?)body["speaker_type"]);
    }

    [Fact]
    public void should_keep_the_gate_link_info_when_moving_it_to_another_unit()
    {
        var body = JObject.Parse(Wire(UnitAssignRequestBuilder.Build(Filled(EnumDeviceCategory.Gate), TARGET_UNIT)));

        Assert.NotNull(body["connection"]);
    }
    #endregion

    #region - 경계 · 부정 -
    [Fact]
    public void should_blank_the_type_axis_so_the_device_kind_is_never_overwritten()
    {
        var body = JObject.Parse(Wire(UnitAssignRequestBuilder.Build(Filled(EnumDeviceCategory.Camera), TARGET_UNIT)));

        Assert.DoesNotContain("type_device", body.Properties().Select(p => p.Name));
        Assert.DoesNotContain("type_camera", body.Properties().Select(p => p.Name));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void should_throw_when_the_unit_id_is_not_positive(int unitId)
        => Assert.Throws<ArgumentOutOfRangeException>(() => UnitAssignRequestBuilder.Build(Filled(EnumDeviceCategory.Lamp), unitId));

    [Fact]
    public void should_throw_when_the_dto_is_null()
        => Assert.Throws<ArgumentNullException>(() => UnitAssignRequestBuilder.Build(null!, TARGET_UNIT));

    [Fact]
    public void should_refuse_a_device_kind_it_does_not_know()
        => Assert.Throws<ArgumentException>(() => UnitAssignRequestBuilder.Build(new BaseDeviceDto(), TARGET_UNIT));

    [Fact]
    public void should_return_the_same_dto_kind_so_the_category_patch_accepts_it()
    {
        Assert.IsType<CameraDeviceDto>(UnitAssignRequestBuilder.Build(Filled(EnumDeviceCategory.Camera), TARGET_UNIT));
        Assert.IsType<GateDeviceDto>(UnitAssignRequestBuilder.Build(Filled(EnumDeviceCategory.Gate), TARGET_UNIT));
    }
    #endregion

    #region - Fixture -
    /// <summary>모든 스칼라를 기본값이 아닌 값으로 채운다 — 본문에 남은 null 하나하나가 베끼지 않은 칸의 증거다.</summary>
    private static BaseDeviceDto Filled(EnumDeviceCategory category)
    {
        BaseDeviceDto dto = category switch
        {
            EnumDeviceCategory.Controller => new ControllerDeviceDto
            {
                TypeDevice = "IoController",
                IpAddress = "10.0.0.11",
                IpPort = 5001,
            },
            EnumDeviceCategory.Sensor => new SensorDeviceDto
            {
                TypeDevice = "Multi",
                ControllerId = 12,
            },
            EnumDeviceCategory.Camera => new CameraDeviceDto
            {
                TypeDevice = "Camera",
                IpAddress = "10.0.0.12",
                IpPort = 80,
                UserName = "admin",
                UserPassword = "cam-pw",
                RtspUri = "rtsp://10.0.0.12/1",
                RtspPort = 554,
                Mode = "ONVIF",
                Category = "PTZ",
                IsRecord = true,
                Urls = CameraUrls(),
                DeviceConfigModes = new JObject { ["day_night_mode"] = "AUTO" },
            },
            EnumDeviceCategory.Speaker => new SpeakerDeviceDto
            {
                TypeDevice = "Speaker",
                SpeakerType = "ADMIN",
                Description = "북측 9구간 방송",
                ServerId = 3,
                TypeSpeaker = "Horn",
            },
            EnumDeviceCategory.Enclosure => new EnclosureDeviceDto
            {
                TypeDevice = "Enclosure",
                TypeEnclosure = "Outdoor",
                HeaterEnabled = true,
                FanEnabled = true,
                ThresholdConfig = new JObject { ["temp_high"] = 45 },
            },
            EnumDeviceCategory.Lamp => new LampDeviceDto
            {
                TypeDevice = "Lamp",
                TypeLamp = "Strobe",
                IpAddress = "10.0.0.13",
                IpPort = 4001,
                UserName = "op",
                UserPassword = "lamp-pw",
                Description = "북측 9구간 경광등",
            },
            EnumDeviceCategory.Gate => new GateDeviceDto
            {
                TypeDevice = "Gate",
                TypeGate = "Sliding",
                Urls = new JObject { ["homepage"] = "http://10.0.0.14/" },
                LinkInfo = new JObject { ["type"] = "RS485", ["channel"] = 2, ["parent_device_id"] = 9 },
            },
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        dto.Id = 41;
        dto.NumberDevice = 5;
        dto.NameDevice = "DEV-5";
        dto.Status = "ACTIVATED";
        dto.IsEnable = true;
        dto.Version = "8.0.1";
        dto.UnitId = 7;                 // 지금은 7 부대 소속 — 42 로 옮긴다
        return dto;
    }

    private static CameraUrlsDto CameraUrls() => new()
    {
        Homepage = new CameraHomepageDto { Url = "http://10.0.0.12/" },
        Onvif = new CameraOnvifDto { DeviceService = "http://10.0.0.12/onvif/device_service" },
        Streams = new CameraStreamsDto
        {
            Rtsp = new CameraRtspDto { Main = "rtsp://10.0.0.12/1", Sub = "rtsp://10.0.0.12/2" },
            Webrtc = new CameraWebrtcDto { Main = "webrtc://10.0.0.12/1" },
        },
        Snapshot = new CameraSnapshotDto { Ch1 = "http://10.0.0.12/snap1" },
    };
    #endregion
}
