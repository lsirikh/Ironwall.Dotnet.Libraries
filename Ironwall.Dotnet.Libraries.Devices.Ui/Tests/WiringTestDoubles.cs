using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 시험이 함께 쓰는 가짜들 — 한 자리에 모아 계약이 늘어날 때 한 번만 고친다.
/// </summary>
internal static class WiringDoubles
{
    public static DeviceQueryPolicy AxisPolicy() => new(new WiringFixedProbe(EnumServerContract.V8_0));
    public static DeviceQueryPolicy LegacyPolicy() => new(new WiringFixedProbe(EnumServerContract.V6_3));

    /// <summary>센서 <paramref name="count"/> 대 · 1차 선 1번부터 차례로 꽂힌 보드.</summary>
    public static WiringBoard Board(int count, int placedOnFirst = 0, int? channelOffset = 1)
    {
        var board = new WiringBoard();
        board.Load(Enumerable.Range(0, count).Select(i => (
            Id: 101 + i,
            Channel: channelOffset is null ? (int?)null : (int?)(i + channelOffset.Value),
            Facts: Facts(1101 + i, i + 1),
            Placement: i < placedOnFirst ? new WiringPlacement(1, i + 1) : null,
            Issue: (string?)null,
            Groups: (IReadOnlyList<int>?)null)));
        return board;
    }

    public static SensorFacts Facts(int number, int index)
        => new(number, $"북측 {index}구간 펜스", "Fence", "북측 7구간");

    /// <summary>서버가 돌려줄 센서 DTO — 모든 칸이 채워져 있다(본문 감사의 기준).</summary>
    public static SensorDeviceDto ServerSensor(int id, int number, int index, WiringPlacement? placement)
        => new()
        {
            Id = id,
            NumberDevice = number,
            NameDevice = $"북측 {index}구간 펜스",
            TypeDevice = "Fence",
            Status = "ACTIVATED",
            IsEnable = true,
            ControllerId = 10,
            Version = "1.4.2",
            GroupIds = new List<int> { 7 },
            Geolocation = new GeolocationDto { Location = "북측 7구간", Latitude = 37.5, Longitude = 127.5, Altitude = 12.5, Heading = 90 },
            HardwareSpec = new HardwareSpecDto
            {
                Manufacturer = "Sensorway",
                Model = "FS-200",
                Firmware = "1.4.2",
                DeviceId = "FS200-0041",
                Hardware = "B",
                MacAddress = "AA:BB:CC:00:02:01",
                MaxDetectionRange = 120,
                Schema = 1,
                Spec = Spec(placement),
                Components = new List<ComponentDefinitionDto> { new() { Key = "vib", Type = "VIBRATION_SENSOR" } },
            },
        };

    /// <summary>벤더 키가 이미 들어 있는 spec — 우리 키만 얹는다(보존 시험의 재료).</summary>
    public static JObject Spec(WiringPlacement? placement)
    {
        var spec = JObject.Parse("""{"detection_range":120}""");
        return placement is null ? spec : WiringSpec.Apply(spec, placement);
    }
}

internal sealed class WiringFixedProbe : IServerContractProbe
{
    public WiringFixedProbe(EnumServerContract contract) => Contract = contract;
    public EnumServerContract Contract { get; }
    public string? RawVersion => Contract.ToString();
    public bool IsResolved => true;
    public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
    public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
}

/// <summary>보낸 DTO 를 그대로 붙잡는 가짜 통로 — 본문 감사가 이것을 직렬화한다.</summary>
internal sealed class WiringFakeGateway : ISensorWriteGateway
{
    public Dictionary<int, SensorDeviceDto> Fetched { get; } = new();
    public List<(int Id, SensorDeviceDto Dto)> Patched { get; } = new();
    public List<SensorDeviceDto> Created { get; } = new();
    public List<(int GroupId, bool Add, IReadOnlyList<int> DeviceIds)> GroupCalls { get; } = new();
    public HashSet<int> FetchFails { get; } = new();
    public HashSet<int> PatchFails { get; } = new();
    public HashSet<int> GroupFails { get; } = new();

    /// <summary>만들기 응답에서 id 를 비워 돌려준다(C7).</summary>
    public bool CreateWithoutId { get; set; }

    /// <summary><see cref="ListByControllerAsync"/> 가 돌려줄 목록(비면 실패로 흉내 낸다).</summary>
    public List<SensorDeviceDto> ListResult { get; } = new();

    public int GetCount { get; private set; }
    public int ListCount { get; private set; }
    public int PatchCount => Patched.Count;
    public int CreateCount => Created.Count;

    public Task<ApiResponse<SensorDeviceDto>> GetAsync(int id, CancellationToken token = default)
    {
        GetCount++;
        if (FetchFails.Contains(id) || !Fetched.TryGetValue(id, out var dto))
            return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("NOT_FOUND", "없는 장비"));
        return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateSuccess(dto));
    }

    public Task<ApiListResponse<SensorDeviceDto>> ListByControllerAsync(int controllerId, CancellationToken token = default)
    {
        ListCount++;
        return Task.FromResult(ApiListResponse<SensorDeviceDto>.CreateSuccess(ListResult.ToList()));
    }

    public Task<ApiResponse<SensorDeviceDto>> CreateAsync(SensorDeviceDto dto, CancellationToken token = default)
    {
        Created.Add(dto);
        var id = CreateWithoutId ? 0 : 900 + Created.Count;
        return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateSuccess(new SensorDeviceDto { Id = id }));
    }

    public Task<ApiResponse<SensorDeviceDto>> PatchAsync(int id, SensorDeviceDto dto, CancellationToken token = default)
    {
        Patched.Add((id, dto));
        if (PatchFails.Contains(id))
            return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateError("CONSTRAINT", "저장 실패"));
        return Task.FromResult(ApiResponse<SensorDeviceDto>.CreateSuccess(dto));
    }

    public Task<ApiResponse<DeviceGroupAssignResultDto>> AssignToGroupAsync(int groupId, IReadOnlyList<int> deviceIds, CancellationToken token = default)
    {
        GroupCalls.Add((groupId, true, deviceIds.ToList()));
        if (GroupFails.Contains(groupId))
            return Task.FromResult(ApiResponse<DeviceGroupAssignResultDto>.CreateError("CONSTRAINT", "그룹 실패"));
        return Task.FromResult(ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
            new DeviceGroupAssignResultDto { GroupId = groupId, AssignedDeviceIds = deviceIds.ToList() }));
    }

    public Task<ApiResponse<DeviceGroupBulkRemoveResultDto>> RemoveFromGroupAsync(int groupId, IReadOnlyList<int> deviceIds, CancellationToken token = default)
    {
        GroupCalls.Add((groupId, false, deviceIds.ToList()));
        if (GroupFails.Contains(groupId))
            return Task.FromResult(ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateError("CONSTRAINT", "그룹 실패"));
        return Task.FromResult(ApiResponse<DeviceGroupBulkRemoveResultDto>.CreateSuccess(
            new DeviceGroupBulkRemoveResultDto { GroupId = groupId, RemovedDeviceIds = deviceIds.ToList() }));
    }
}

/// <summary>펜스 구성 로컬 저장소의 가짜(fence-wiring-editor FR-11) — 저장한 문서를 붙잡고, 다음 결과를 정할 수 있다.</summary>
internal sealed class FakeFenceStore : Ironwall.Dotnet.Monitoring.Models.Fences.IFenceLayoutStore
{
    public Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutDocument? Stored { get; set; }
    public List<Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutDocument> Saved { get; } = new();
    public Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutSaveStatus Next { get; set; } = Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutSaveStatus.Saved;

    public Task<Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutDocument?> LoadAsync(int controllerId, CancellationToken token = default)
        => Task.FromResult(Stored);

    public Task<Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutSaveResult> SaveAsync(Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutDocument document, CancellationToken token = default)
    {
        Saved.Add(document);
        return Task.FromResult(Next == Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutSaveStatus.Saved
            ? new Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutSaveResult(Next, document.Revision + 1, "펜스 구성을 이 PC 에 저장했습니다.")
            : new Ironwall.Dotnet.Monitoring.Models.Fences.FenceLayoutSaveResult(Next, document.Revision, "다른 GIS 가 먼저 저장했습니다."));
    }
}

/// <summary>ping 가짜 — 정한 표본을 차례로 돌려준다(다 쓰면 10ms 성공).</summary>
internal sealed class FakePing : Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals.IPingProbe
{
    public Queue<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals.PingSample> Next { get; } = new();
    public List<string> Hosts { get; } = new();

    public Task<Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals.PingSample> SendAsync(string host, TimeSpan timeout, CancellationToken token = default)
    {
        Hosts.Add(host);
        return Task.FromResult(Next.Count > 0 ? Next.Dequeue() : new Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals.PingSample(true, 10));
    }
}

/// <summary>사람에게 묻는 자리의 가짜.</summary>
internal sealed class WiringFakeDialogs : IWiringDialogs
{
    public bool Confirm { get; set; }
    public string? TextAnswer { get; set; }
    public MakeSensorsResult? MakeResult { get; set; }
    public bool PasteAccepted { get; set; }
    public string? Clipboard { get; set; }

    public int ConfirmCount { get; private set; }
    public PasteReport? LastReport { get; private set; }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        ConfirmCount++;
        Confirms.Add((title, message));
        return Task.FromResult(Confirm);
    }

    public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult(TextAnswer);

    public Task<MakeSensorsResult?> AskMakeSensorsAsync(IReadOnlyList<string> types, string defaultType, string defaultZone,
                                                        IReadOnlyCollection<int> existingNumbers, int suggestedStart)
        => Task.FromResult(MakeResult);

    public Task<bool> ShowPasteReportAsync(PasteReport report)
    {
        LastReport = report;
        return Task.FromResult(PasteAccepted && report.HasRows);
    }

    public void RememberPasteContext(IReadOnlyCollection<int> existingNumbers, string defaultType, string defaultZone) { }

    public string? ReadClipboardText() => Clipboard;

    /// <summary>마지막으로 보인 "바뀌는 번호 표"(fence-wiring-editor FR-11) · 경고.</summary>
    public IReadOnlyList<Ironwall.Dotnet.Monitoring.Models.Fences.NumberChange>? LastNumberChanges { get; private set; }
    public string? LastNumberWarning { get; private set; }

    /// <summary>마지막 확인 제목 · 문장.</summary>
    public List<(string Title, string Message)> Confirms { get; } = new();

    public Task<bool> ConfirmNumberChangesAsync(string title, IReadOnlyList<Ironwall.Dotnet.Monitoring.Models.Fences.NumberChange> changes, string warning, string details)
    {
        LastNumberChanges = changes;
        LastNumberWarning = warning;
        return ConfirmAsync(title, details);
    }
}

/// <summary>
/// <b>제품이 실제로 쓰는 길</b>로 본문을 붙잡는다 — 가짜는 HTTP 층(<see cref="IApiService"/>)뿐이고,
/// 그 위의 <see cref="DeviceApiService"/> 는 진짜다(<c>ShapeWrite</c> · <c>LinkVersionToFirmware</c> ·
/// 같은 직렬화 설정 · 같은 엔드포인트 계산을 전부 지난다).
/// </summary>
internal sealed class CapturingHttp : IApiService
{
    public List<(string Method, string Endpoint, JObject? Body)> Calls { get; } = new();

    // IApiService 의 나머지 표면 — 이 시험에서는 쓰이지 않는다.
    public string Url { get; set; } = "https://example.test";
    public string UserId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public void Initialize() { }
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;

    /// <summary>단건 조회가 돌려줄 봉투(JSON) — 없으면 빈 성공 응답.</summary>
    public string? GetResponseJson { get; set; }

    /// <summary>엔드포인트마다 다른 봉투(JSON)를 돌려줄 때 — <c>null</c> 을 돌려주면 <see cref="GetResponseJson"/> 로 간다.</summary>
    public Func<string, string?>? GetResponseFor { get; set; }

    public JObject? LastBody => Calls.Count == 0 ? null : Calls[^1].Body;

    public Task<HttpResponseMessage> GetRequestAsync(string endpoint, Dictionary<string, string>? parameters = null)
    {
        Calls.Add(("GET", endpoint, null));
        return Task.FromResult(Ok(GetResponseFor?.Invoke(endpoint) ?? GetResponseJson ?? """{"success":true,"data":null,"meta":{}}"""));
    }

    public Task<HttpResponseMessage> PostRequestAsync<T>(string endpoint, T body) => Record("POST", endpoint, body);
    public Task<HttpResponseMessage> PatchRequestAsync<T>(string endpoint, T body) => Record("PATCH", endpoint, body);
    public Task<HttpResponseMessage> PutRequestAsync<T>(string endpoint, T body) => Record("PUT", endpoint, body);
    public Task<HttpResponseMessage> DeleteRequestAsync<T>(string endpoint, T body) => Record("DELETE", endpoint, body);

    public Task<HttpResponseMessage> DeleteRequestAsync(string endpoint)
    {
        Calls.Add(("DELETE", endpoint, null));
        return Task.FromResult(Ok("""{"success":true,"data":null,"meta":{}}"""));
    }

    public Task<HttpResponseMessage> PostFormDataRequestAsync(string endpoint, MultipartFormDataContent content)
    {
        Calls.Add(("POST-FORM", endpoint, null));
        return Task.FromResult(Ok("""{"success":true,"data":null,"meta":{}}"""));
    }

    private Task<HttpResponseMessage> Record<T>(string method, string endpoint, T body)
    {
        // 제품과 같은 직렬화 — ShouldSerialize* 가 그대로 반영된다.
        var json = body is null ? null : JObject.Parse(JsonConvert.SerializeObject(body));
        Calls.Add((method, endpoint, json));
        return Task.FromResult(Ok("""{"success":true,"data":null,"meta":{}}"""));
    }

    private static HttpResponseMessage Ok(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>이 HTTP 가짜 위에 얹은 <b>진짜</b> 장비 API 서비스.</summary>
    public IDeviceApiService Real(EnumServerContract contract = EnumServerContract.V8_0)
        => new DeviceApiService(null, this, new ApiSetupModel { Url = "https://example.test" }, new WiringFixedProbe(contract));
}
