using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Tests.LiveApi;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Api.Tests;
/****************************************************************************
   Purpose      : 이벤트 라이브 시험의 선행 자료(장비 · 프리셋) 생성 · 삭제 도우미
   Created By   : Claude
   Created On   : 2026-10-12
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : 시험 서버는 시드 자료가 없다(INIT_SAMPLE_DATA=false) — 고정 id(장비 2 · 카메라 3 ·
                  스피커 6 · 경광등 7 · 그룹 1)에 기대면 400 DEVICE_NOT_FOUND / 404 로 깨진다.
                  그래서 시험이 필요한 장비를 직접 만들고 finally 에서 지운다.
                  Events.Api 에는 장비 서비스가 없으므로 공용 로그인 세션의 원시 IApiService(Bearer 부착)로
                  8.0.9 축 계약 본문을 그대로 보낸다. 이름은 E2E-EVT- 접두어 + Guid 라 재실행끼리 겹치지 않는다.
****************************************************************************/

/// <summary>라이브 서버에 시험용 장비 · 프리셋을 만들고 지운다. 쓰기 시험(<see cref="LiveApiWriteFactAttribute"/>)에서만 부른다.</summary>
internal static class LiveEventTestData
{
    public const string Controllers = "controllers";
    public const string Cameras = "cameras";
    public const string Speakers = "speakers";
    public const string Lamps = "lamps";

    private static string BaseUrl => LiveApiTarget.Url.TrimEnd('/');

    /// <summary>공용 로그인 세션의 원시 HTTP 서비스 — EventApiModule 이 <see cref="LiveApiSession.Name"/> 으로 Bearer 처리기와 함께 등록한다.</summary>
    private static Task<IApiService> HttpAsync() => LiveApiSession.ResolveAsync<IApiService>();

    /// <summary>재실행끼리 겹치지 않는 시험 자료 이름.</summary>
    public static string UniqueName(string kind) => $"E2E-EVT-{kind}-{Guid.NewGuid():N}";

    /// <summary>
    /// 제어기 — 이벤트(탐지 · 장애 · 연결)의 <c>device_id</c> 로 쓴다(서버는 장비 종류를 가리지 않는다 — detections.py:283-292).
    /// 필수: <c>type_controller</c> · <c>number_device</c> · <c>name_device</c> · <c>connection{ip_address, ip_port}</c>
    /// (서버 <c>app/schemas/device.py:536-557</c>).
    /// </summary>
    public static Task<int> CreateControllerAsync() => PostForIdAsync($"/devices/{Controllers}", new JObject
    {
        ["type_controller"] = "Controller",
        ["number_device"] = 9901,
        ["name_device"] = UniqueName("CTL"),
        ["connection"] = new JObject { ["ip_address"] = "192.0.2.11", ["ip_port"] = 8080 },
    }, "제어기");

    /// <summary>
    /// 카메라 — 필수: <c>type_camera</c>(FIXED|PTZ|SPEED_DOME) · <c>connection{ip_address, ip_port, protocol}</c>
    /// (<c>device.py:648-672</c> · <c>device_axes.py:1257-1261</c>).
    /// </summary>
    public static Task<int> CreateCameraAsync() => PostForIdAsync($"/devices/{Cameras}", new JObject
    {
        ["type_camera"] = "PTZ",
        ["number_device"] = 9902,
        ["name_device"] = UniqueName("CAM"),
        ["connection"] = new JObject { ["ip_address"] = "192.0.2.12", ["ip_port"] = 80, ["protocol"] = "ONVIF" },
    }, "카메라");

    /// <summary>스피커 — 필수는 번호 · 이름뿐(역할 · 형상은 기본값, 접속 축 선택 — <c>device.py:705-728</c>).</summary>
    public static Task<int> CreateSpeakerAsync() => PostForIdAsync($"/devices/{Speakers}", new JObject
    {
        ["number_device"] = 9903,
        ["name_device"] = UniqueName("SPK"),
    }, "스피커");

    /// <summary>경광등 — 접속 축 필수, <c>ip_port</c> 는 생략하면 80(<c>device.py:827-849</c> · <c>routers/lamps.py:51</c>).</summary>
    public static Task<int> CreateLampAsync() => PostForIdAsync($"/devices/{Lamps}", new JObject
    {
        ["number_device"] = 9904,
        ["name_device"] = UniqueName("LMP"),
        ["connection"] = new JObject { ["ip_address"] = "192.0.2.14" },
    }, "경광등");

    /// <summary>
    /// 카메라 프리셋 — <c>POST /devices/cameras/{id}/presets</c> <c>{preset_index, preset_name}</c>
    /// (<c>app/routers/camera_presets.py:208-280</c>, 같은 카메라에 같은 index 면 409).
    /// 매핑 카메라의 target/home 프리셋은 <b>그 카메라 소유</b>여야 한다(<c>event_mapping_cameras.py:63-84</c>) —
    /// 프리셋 0 이나 남의 프리셋에 기대지 않으려고 실제 프리셋을 만든다. 카메라를 지우면 함께 지워진다(cameras.py:343).
    /// </summary>
    public static Task<int> CreatePresetAsync(int cameraId, int presetIndex) => PostForIdAsync($"/devices/{Cameras}/{cameraId}/presets", new JObject
    {
        ["preset_index"] = presetIndex,
        ["preset_name"] = $"E2E-EVT-P{presetIndex}",
    }, $"카메라 {cameraId} 프리셋 {presetIndex}");

    /// <summary>
    /// 장비 삭제 — <c>DELETE /devices/{category}/{id}</c>. 제어기는 소속 센서, 카메라는 프리셋 · ROI 가 함께 지워진다.
    /// 응답 <c>data</c> 는 <c>null</c> 이라 상태 코드로만 판정한다. 0 이하 id(만들지 못한 장비)는 건너뛴다.
    /// </summary>
    public static async Task DeleteDeviceAsync(string category, int id)
    {
        if (id <= 0) return;
        var http = await HttpAsync();
        using var response = await http.DeleteRequestAsync($"{BaseUrl}/devices/{category}/{id}");
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"시험 장비 삭제 실패({category}/{id}) — HTTP {(int)response.StatusCode}: {text}");
    }

    private static async Task<int> PostForIdAsync(string path, JObject body, string what)
    {
        var http = await HttpAsync();
        using var response = await http.PostRequestAsync($"{BaseUrl}{path}", body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"시험 자료({what}) 생성 실패 — HTTP {(int)response.StatusCode}: {text}");

        // 봉투 {"success":true,"data":{"id":...}}
        var id = (int?)JObject.Parse(text)["data"]?["id"];
        Assert.True(id is > 0, $"시험 자료({what}) 생성 응답에 data.id 가 없습니다: {text}");
        return id!.Value;
    }
}
