using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Api.Tests;
/****************************************************************************
   Purpose      : 이벤트 매핑 하위 쓰기 — 409 동시 쓰기 경합 문구(G01) · 카메라 프리셋 0 · 경광등 event_mapping_id 본문(G51)
   Created By   : Claude
   Created On   : 2026-10-21
   Company      : Sensorway Co., Ltd.

   Description  : G01 — 8.0.15 의 409 + details.retryable=true 는 "이미 연동됨" 이 아니라 "잠시 뒤 다시" 다(명세 §12.2).
                  G51 — 카메라 target/home_preset_id 0 은 실제 id 가 아니다(생략 = POST 없음 · PATCH/PUT 유지, 명세 §7.3).
                        경광등 event_mapping_id 는 경로가 정본 — PATCH 는 받지 않고(422 UNKNOWN_FIELD),
                        POST/PUT 은 경로와 달라야 422 IMMUTABLE_FIELD(8.0.15 감사 D04).
****************************************************************************/
[Trait("Category", "Contract")]
public class EventMappingConflictAndChildBodyTests
{
    private const string BaseUrl = "https://unit.test/api";

    private const string RetryableConflict =
        "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"동시에 처리 중인 다른 요청과 충돌해 이 요청을 처리하지 못했습니다\",\"details\":{\"retryable\":true}}}";

    private const string DuplicateConflict =
        "{\"success\":false,\"error\":{\"code\":\"CONFLICT\",\"message\":\"Camera 7 is already mapped to event mapping 3 (config id 12)\",\"details\":null}}";

    private static (EventApiService Service, EventCapturingApiService Http) Create()
    {
        var http = new EventCapturingApiService();
        var service = new EventApiService(new LogService(), http, new ApiSetupModel { Url = BaseUrl },
            new FixedEventContractProbe(EnumServerContract.V8_0));
        return (service, http);
    }

    #region - G01 409 재시도 -
    [Fact(DisplayName = "G01-1. 매핑 하위 쓰기 409 + retryable=true → 재시도 문구(카메라 · 스피커 · 경광등)")]
    public async Task G01_RetryableConflict_SaysRetry()
    {
        var (svc, http) = Create();
        http.ResponseStatus = HttpStatusCode.Conflict;
        http.ResponseJson = RetryableConflict;

        var camera = await svc.CreateMappingCameraAsync(3, new EventMappingCameraDto { CameraId = 7 });
        var speaker = await svc.PatchMappingSpeakerAsync(3, 4, new EventMappingSpeakerDto { SpeakerId = 6 });
        var lamp = await svc.UpdateMappingLampAsync(3, 5, new EventMappingLampDto { LampId = 8 });

        foreach (var response in new[] { camera.Error, speaker.Error, lamp.Error })
        {
            Assert.NotNull(response);
            Assert.Equal(ApiStatusHelper.RetryableConflictMessage, response!.Message);
            Assert.True(response.IsRetryable);                         // 원문 details 를 덮지 않는다
        }
    }

    [Fact(DisplayName = "G01-2. 매핑 하위 쓰기 409 + details=null → 기존 '이미 연동됨' 문구")]
    public async Task G01_DuplicateConflict_KeepsDuplicateText()
    {
        var (svc, http) = Create();
        http.ResponseStatus = HttpStatusCode.Conflict;
        http.ResponseJson = DuplicateConflict;

        var camera = await svc.CreateMappingCameraAsync(3, new EventMappingCameraDto { CameraId = 7 });

        Assert.Equal("이미 이 이벤트매핑에 연동된 카메라입니다. 새로 추가하는 대신 기존 구성을 편집하십시오.", camera.Error!.Message);
        Assert.Contains("already mapped", camera.Error.Details);
    }
    #endregion

    #region - G51 카메라 프리셋 0 -
    [Fact(DisplayName = "G51-1. 매핑 카메라 POST · PATCH · PUT — 프리셋 0 은 키째 뺀다")]
    public async Task G51_Camera_OmitsZeroPresets()
    {
        var (svc, http) = Create();
        var dto = new EventMappingCameraDto { CameraId = 7, TargetPresetId = 0, HomePresetId = 0, DelayTime = 3, Priority = 1 };

        await svc.CreateMappingCameraAsync(3, dto);
        Assert.Equal("POST", http.Method);
        Assert.False(http.Body!.ContainsKey("target_preset_id"));
        Assert.False(http.Body.ContainsKey("home_preset_id"));
        Assert.Equal(7, (int?)http.Body["camera_id"]);
        Assert.Equal(3, (int?)http.Body["delay_time"]);

        await svc.PatchMappingCameraAsync(3, 12, dto);
        Assert.Equal("PATCH", http.Method);
        Assert.False(http.Body!.ContainsKey("target_preset_id"));
        Assert.False(http.Body.ContainsKey("home_preset_id"));

        await svc.UpdateMappingCameraAsync(3, 12, dto);
        Assert.Equal("PUT", http.Method);
        Assert.Equal($"{BaseUrl}/integrations/event-mappings/3/cameras/12", http.Endpoint);
        Assert.False(http.Body!.ContainsKey("target_preset_id"));
        Assert.False(http.Body.ContainsKey("home_preset_id"));
        Assert.Equal(7, (int?)http.Body["camera_id"]);
    }

    [Fact(DisplayName = "G51-2. 매핑 카메라 — 실제 프리셋 id 는 그대로 싣는다")]
    public async Task G51_Camera_KeepsRealPresets()
    {
        var (svc, http) = Create();

        await svc.CreateMappingCameraAsync(3, new EventMappingCameraDto { CameraId = 7, TargetPresetId = 21, HomePresetId = 0 });

        Assert.Equal(21, (int?)http.Body!["target_preset_id"]);
        Assert.False(http.Body.ContainsKey("home_preset_id"));
    }
    #endregion

    #region - G51 경광등 event_mapping_id -
    [Fact(DisplayName = "G51-3. 매핑 경광등 PATCH — event_mapping_id 는 늘 뺀다")]
    public async Task G51_Lamp_PatchNeverSendsMappingId()
    {
        var (svc, http) = Create();

        await svc.PatchMappingLampAsync(3, 5, new EventMappingLampDto { EventMappingId = 3, LampId = 8 });

        Assert.Equal("PATCH", http.Method);
        Assert.False(http.Body!.ContainsKey("event_mapping_id"));
        Assert.Equal(8, (int?)http.Body["lamp_id"]);
    }

    [Theory(DisplayName = "G51-4. 매핑 경광등 POST · PUT — event_mapping_id 는 경로 값으로 맞춘다")]
    [InlineData(0)]
    [InlineData(9)]
    public async Task G51_Lamp_PostAndPutUsePathMappingId(int dtoMappingId)
    {
        var (svc, http) = Create();
        var dto = new EventMappingLampDto { EventMappingId = dtoMappingId, LampId = 8 };

        await svc.CreateMappingLampAsync(3, dto);
        Assert.Equal("POST", http.Method);
        Assert.Equal(3, (int?)http.Body!["event_mapping_id"]);

        await svc.UpdateMappingLampAsync(3, 5, dto);
        Assert.Equal("PUT", http.Method);
        Assert.Equal(3, (int?)http.Body!["event_mapping_id"]);
        Assert.Equal(8, (int?)http.Body["lamp_id"]);
        Assert.Equal(dtoMappingId, dto.EventMappingId);                // 호출자 DTO 는 건드리지 않는다
    }
    #endregion
}
