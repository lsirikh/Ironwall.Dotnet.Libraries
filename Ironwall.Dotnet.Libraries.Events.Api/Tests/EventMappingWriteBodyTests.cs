using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Api.Tests;
/****************************************************************************
   Purpose      : 이벤트 매핑 쓰기 본문 계약 시험 (L6 · L7) — 네트워크 없음
   Created By   : Claude
   Created On   : 2026-10-12
   Department   : SW Team
   Company      : Sensorway Co., Ltd.

   Description  : EventApiService 가 서버(8.0.9)로 보내는 본문의 키 집합을 가로채 검사한다.
                  L6 — 매핑 본체 POST · PATCH · PUT 은 5키만(서버 extra="forbid", integration.py:103-157)
                  L7 — 매핑 스피커 file_group_id 0(= 음원 없음)은 싣지 않는다(event_mapping_speakers.py:277-286)
****************************************************************************/

[Trait("Category", "Contract")]
public class EventMappingWriteBodyTests
{
    private const string BaseUrl = "https://unit.test/api";

    /// <summary>서버 <c>EventMappingCreate</c>/<c>EventMappingUpdate</c> 가 받는 키 전부(integration.py:124-128 · 153-157).</summary>
    private static readonly HashSet<string> MappingWriteKeys = new()
    {
        "name_event", "device_group_id", "category_event_mapping", "description", "status",
    };

    private static (EventApiService Service, EventCapturingApiService Http) Create(EnumServerContract? contract)
    {
        var http = new EventCapturingApiService();
        var probe = contract.HasValue ? new FixedEventContractProbe(contract.Value) : null;
        var service = new EventApiService(new LogService(), http, new ApiSetupModel { Url = BaseUrl }, probe);
        return (service, http);
    }

    private static HashSet<string> Keys(JObject? body) => body!.Properties().Select(p => p.Name).ToHashSet();

    #region - L6 이벤트 매핑 본체 -
    /// <summary>
    /// 결함 근거(대조군) — DTO 를 통째로 직렬화하면 서버가 모르는 키가 실린다.
    /// 종전 서비스는 이 본문을 그대로 보냈다 → 8.0.9 에서 422 UNKNOWN_FIELD.
    /// </summary>
    [Fact(DisplayName = "L6-0. EventMappingDto 통째 직렬화에는 서버가 모르는 키가 있다(대조군)")]
    public void L6_RawDto_CarriesForbiddenKeys()
    {
        var raw = JObject.FromObject(new EventMappingDto
        {
            NameEvent = "m", CategoryEventMapping = "FENCE_SENSOR_ONLY", Status = true,
        });

        var unknown = Keys(raw).Except(MappingWriteKeys).ToList();
        Assert.Contains("id", unknown);
        Assert.Contains("cameras", unknown);
        Assert.Contains("speakers", unknown);
        Assert.Contains("lamps", unknown);
        Assert.Contains("created_at", unknown);
    }

    [Theory(DisplayName = "L6-1. 매핑 생성(POST) 본문은 5키만 — 판본 무관")]
    [InlineData(null)]
    [InlineData(EnumServerContract.V6_3)]
    [InlineData(EnumServerContract.V8_0)]
    public async Task L6_CreateEventMapping_SendsOnlyServerKeys(EnumServerContract? contract)
    {
        var (svc, http) = Create(contract);
        var dto = new EventMappingDto
        {
            Id = 77,                                     // 응답 전용 — 실리면 안 된다
            NameEvent = "E2E 매핑",
            DeviceGroupId = 3,
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = "설명",
            Status = true,
            Cameras = new List<EventMappingCameraDto>(),  // 자식 컬렉션 — 스키마에 없는 키
        };

        await svc.CreateEventMappingAsync(dto);

        Assert.Equal("POST", http.Method);
        Assert.Equal($"{BaseUrl}/integrations/event-mappings", http.Endpoint);
        Assert.True(MappingWriteKeys.SetEquals(Keys(http.Body)), $"본문 키: {string.Join(",", Keys(http.Body))}");
        Assert.Equal("E2E 매핑", (string?)http.Body!["name_event"]);
        Assert.Equal(3, (int?)http.Body["device_group_id"]);
        Assert.Equal("FENCE_SENSOR_ONLY", (string?)http.Body["category_event_mapping"]);
        Assert.Equal("설명", (string?)http.Body["description"]);
        Assert.True((bool?)http.Body["status"]);
    }

    [Fact(DisplayName = "L6-2. 매핑 생성 — null 선택 필드는 키째 빠진다(그룹 미지정)")]
    public async Task L6_CreateEventMapping_OmitsNullOptionalKeys()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.CreateEventMappingAsync(new EventMappingDto
        {
            NameEvent = "그룹 없음", CategoryEventMapping = "FENCE_SENSOR_ONLY", Status = true,
        });

        var keys = Keys(http.Body);
        Assert.True(keys.SetEquals(new[] { "name_event", "category_event_mapping", "status" }),
            $"본문 키: {string.Join(",", keys)}");
    }

    [Fact(DisplayName = "L6-3. 매핑 부분 수정(PATCH) — 준 키만 보낸다")]
    public async Task L6_PatchEventMapping_SendsOnlyGivenKeys()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.PatchEventMappingAsync(12, new EventMappingDto { Description = "수정" });

        Assert.Equal("PATCH", http.Method);
        Assert.Equal($"{BaseUrl}/integrations/event-mappings/12", http.Endpoint);
        Assert.True(Keys(http.Body).SetEquals(new[] { "description" }), $"본문 키: {string.Join(",", Keys(http.Body))}");
        Assert.Equal("수정", (string?)http.Body!["description"]);
    }

    [Fact(DisplayName = "L6-4. 매핑 전체 수정(PUT) — 5키만, 자식 컬렉션 · id · created_at 없음")]
    public async Task L6_UpdateEventMapping_SendsOnlyServerKeys()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.UpdateEventMappingAsync(12, new EventMappingDto
        {
            Id = 12,
            NameEvent = "교체",
            CategoryEventMapping = "FENCE_SENSOR_ONLY",
            Description = "전체 교체",
            Status = false,
            Speakers = new List<EventMappingSpeakerDto>(),
            Lamps = new List<EventMappingLampDto>(),
        });

        Assert.Equal("PUT", http.Method);
        Assert.Equal($"{BaseUrl}/integrations/event-mappings/12", http.Endpoint);
        var keys = Keys(http.Body);
        Assert.True(keys.SetEquals(new[] { "name_event", "category_event_mapping", "description", "status" }),
            $"본문 키: {string.Join(",", keys)}");
        Assert.False((bool?)http.Body!["status"]);
    }
    #endregion

    #region - L7 매핑 스피커 -
    [Fact(DisplayName = "L7-0. EventMappingSpeakerDto 통째 직렬화는 file_group_id:0 을 싣는다(대조군)")]
    public void L7_RawDto_CarriesZeroFileGroup()
    {
        var raw = JObject.FromObject(new EventMappingSpeakerDto { SpeakerId = 6, RepeatCount = 1 });
        Assert.Equal(0, (int?)raw["file_group_id"]);
    }

    [Fact(DisplayName = "L7-1. 매핑 스피커 생성 — file_group_id 0 은 싣지 않는다")]
    public async Task L7_CreateMappingSpeaker_OmitsZeroFileGroup()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.CreateMappingSpeakerAsync(5, new EventMappingSpeakerDto
        {
            SpeakerId = 6, FileGroupId = 0, RepeatCount = 3, Priority = 1,
        });

        Assert.Equal("POST", http.Method);
        Assert.Equal($"{BaseUrl}/integrations/event-mappings/5/speakers", http.Endpoint);
        var keys = Keys(http.Body);
        Assert.True(keys.SetEquals(new[] { "speaker_id", "repeat_count", "is_enable", "priority" }),
            $"본문 키: {string.Join(",", keys)}");
        Assert.Equal(6, (int?)http.Body!["speaker_id"]);
        Assert.Equal(3, (int?)http.Body["repeat_count"]);
        Assert.True((bool?)http.Body["is_enable"]);
        Assert.Equal(1, (int?)http.Body["priority"]);
    }

    [Fact(DisplayName = "L7-2. 매핑 스피커 생성 — 실제 음원 그룹 id 는 그대로 싣는다")]
    public async Task L7_CreateMappingSpeaker_KeepsRealFileGroup()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);

        await svc.CreateMappingSpeakerAsync(5, new EventMappingSpeakerDto
        {
            SpeakerId = 6, FileGroupId = 9, RepeatCount = 2, Priority = 0,
        });

        Assert.Equal(9, (int?)http.Body!["file_group_id"]);
        Assert.Equal(0, (int?)http.Body["priority"]);
    }

    [Fact(DisplayName = "L7-3. 매핑 스피커 PATCH · PUT — file_group_id 0 은 싣지 않는다(= 현재 값 유지)")]
    public async Task L7_PatchAndUpdateMappingSpeaker_OmitZeroFileGroup()
    {
        var (svc, http) = Create(EnumServerContract.V8_0);
        var dto = new EventMappingSpeakerDto { SpeakerId = 6, FileGroupId = 0, RepeatCount = 2, Priority = 1 };

        await svc.PatchMappingSpeakerAsync(5, 40, dto);
        Assert.Equal("PATCH", http.Method);
        Assert.Equal($"{BaseUrl}/integrations/event-mappings/5/speakers/40", http.Endpoint);
        Assert.False(http.Body!.ContainsKey("file_group_id"));

        await svc.UpdateMappingSpeakerAsync(5, 40, dto);
        Assert.Equal("PUT", http.Method);
        Assert.Equal($"{BaseUrl}/integrations/event-mappings/5/speakers/40", http.Endpoint);
        Assert.False(http.Body!.ContainsKey("file_group_id"));
        Assert.Equal(6, (int?)http.Body["speaker_id"]);
        Assert.Equal(2, (int?)http.Body["repeat_count"]);
    }
    #endregion
}
