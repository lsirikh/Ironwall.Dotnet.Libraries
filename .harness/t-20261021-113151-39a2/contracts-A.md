# 작업자 A 계약 (공개 시그니처) — 컴파일 확인됨

## G25 음원 그룹 목록
`IDeviceApiService` (네임스페이스 `Ironwall.Dotnet.Libraries.Devices.Api.Services`):

```csharp
Task<ApiListResponse<FileGroupDto>> GetFileGroupsAsync(
    int? serverId = null, int page = 1, int limit = 100, CancellationToken token = default);
```
- `GET /api/file-groups?page=&limit=&server_id=` · limit 은 1..100 으로 자름 · page<1 은 1.
- 인터페이스 기본 구현 = `NOT_SUPPORTED` 오류(시험 대역 안 깨짐).
- 페이지: `resp.Pagination` (`Page·Limit·Total·TotalPages`), `resp.HasMorePages`.
- 403/404 구분: `resp.StatusCode` (403 = 권한 없음 `files:view`, 404 = 없음). `resp.Error?.Code` 는 서버 코드.

`FileGroupDto` (`Ironwall.Dotnet.Libraries.Messages.Dto.Devices`):
- `int Id` · `int ServerId`(server_id) · `int GroupId`(group_id) · `string Name`(JSON 키 **group_name**) · `List<string> Files`(files — 원소가 객체여도 name/file_name/filename/path 로 읽음) · `string? Description`(값 있을 때만) · `CreatedAt`/`UpdatedAt`.

```csharp
var r = await deviceApi.GetFileGroupsAsync(serverId: speakerServerId);
if (!r.Success) { if (r.StatusCode == 403) /* 권한 없음 */; else if (r.StatusCode == 404) /* 없음 */; }
foreach (var g in r.Data ?? new()) Console.WriteLine($"{g.GroupId} {g.Name} {g.Files.Count}");
```

## G22 함체 안 장비
`EnclosureDeviceDto` 추가:
- `List<EnclosureContentDto>? Contents` — 읽기 전용(`contents`, `?include=contents` 일 때만 옴, 기본 응답엔 null). 직렬화 안 됨.
- `List<int>? DeviceIds` — 쓰기(`device_ids`), **통째 교체**. `null` = 키 생략(유지), 빈 목록 = 모두 빼기.
- `bool UseEnclosureContents` — [JsonIgnore]. `DeviceApiService` 의 Create/Patch/Update 가 서버 판본 **≥ 8.0.6** 일 때 켬. 꺼져 있으면 `device_ids` 는 안 실림.
- `EnclosureContentDto { int Id; string? CategoryDevice; string? NameDevice; }` (id · category_device · name_device).

읽기: `GetEnclosureByIdAsync(id, include: "contents")` → `dto.Contents`.
쓰기: `PatchEnclosureAsync(id, new EnclosureDeviceDto { ..., DeviceIds = new() { 101, 102 } })`.
422(device_ids 사유)는 `resp.Success=false`, `resp.StatusCode=422`, `resp.Error.Message`/`resp.Error.Details` 에 서버 문구 그대로.

역조회:
```csharp
Task<ApiListResponse<EnclosureDeviceDto>> GetEnclosuresAsync(int deviceId, CancellationToken token = default);
```
- `GET /api/devices/enclosures?device_id={id}` → 0 또는 1건.
- 서버 < 8.0.6(판본 못 읽음 포함)이면 HTTP 없이 `Error.Code = "ENDPOINT_UNAVAILABLE"`.
- 인터페이스 기본 구현 = `NOT_SUPPORTED`.
- ⚠ 기존 `GetEnclosuresAsync(string? doorStatus = null, ...)` 와 겹(오버로드). `GetEnclosuresAsync(5)` → 역조회, `GetEnclosuresAsync()` → 기존 목록.

## G17 저장 경고 해석기
`Ironwall.Dotnet.Libraries.Devices.Api.Helpers.SaveWarnings` (정적):

```csharp
public sealed record Notice(string Code, string? Field, string? Metric, string Text);
static IReadOnlyList<Notice> Collect(IEnumerable<ResponseWarningDto>? warnings, IReadOnlyDictionary<string,string>? extraLabels = null);
static IReadOnlyList<Notice> Collect<T>(ApiResponse<T>? response, IReadOnlyDictionary<string,string>? extraLabels = null);
static IReadOnlyList<Notice> Collect<T>(ApiListResponse<T>? response, IReadOnlyDictionary<string,string>? extraLabels = null);
static IReadOnlyList<Notice> CollectFromJson(string? envelopeJson, IReadOnlyDictionary<string,string>? extraLabels = null);
static string? ToText(IReadOnlyList<Notice> notices);   // 없으면 null
static string MetricLabel(string? metric, IReadOnlyDictionary<string,string>? extraLabels = null); // 모르면 "측정 항목"
static IReadOnlyDictionary<string,string> MetricLabels;   // temperature→온도 등
```
- 모으는 코드: `UNMATCHED_THRESHOLD`(여러 건은 한 Notice 로 묶음) · `SERVER_ID_MISSING`. 나머지/모르는 코드는 무시.
- 문구 예: `저장했지만 이 장비에 없는 측정 항목이라 경보가 동작하지 않습니다(온도, 습도)` /
  `저장했지만 방송 서버가 정해지지 않았습니다. 방송 서버를 등록한 뒤 이 스피커에 지정해 주십시오.`
- `ApiResponse<T>.Warnings` / `ApiListResponse<T>.Warnings` 는 이미 있다(Messages/Defines/Apis) — 장비 쓰기 · `PatchDeviceAxesAsync` · `Patch/UpdateDeviceConfigAsync` 응답의 `Warnings` 를 그대로 넘기면 된다.

```csharp
var resp = await deviceApi.PatchDeviceConfigAsync("enclosures", id, cfg);
var text = SaveWarnings.ToText(SaveWarnings.Collect(resp));
if (text != null) /* 토스트/알림으로 text 표시 */;
```

## G24 전 함체 최신 계측 (총괄 추가 요청)
```csharp
Task<ApiListResponse<EnclosureLatestMetricsDto>> GetAllEnclosureLatestMetricsAsync(CancellationToken token = default);
```
- `GET /api/enclosure-metrics` (명세 §5.5.13). 봉투 `pagination: null` → `resp.Pagination == null`(무시).
- 404/403 구분: `resp.StatusCode`(404 = 경로 없는 서버, 403 = 권한 없음 `devices:view`). `resp.Error?.Code` 는 서버 코드("NOT_FOUND" 등).
- 인터페이스 기본 구현 = `NOT_SUPPORTED` 오류.

`EnclosureLatestMetricsDto` (`Messages.Dto.Devices`): `int EnclosureId`(enclosure_id) · `string? EnclosureName`(enclosure_name) · `EnclosureMetricDto? LatestMetrics`(latest_metrics, 계측 없으면 null).

`EnclosureMetricDto` 읽기 확인: `Id`(id) · `ObservedAt`(observed_at) · `CreatedAt`(created_at) · **새로** `MeasuredAt`(measured_at — NATS 쪽 키, 읽기 전용·직렬화 안 함). `ObservedAtEffective` = observed_at → measured_at → created_at 순.
수치: `TemperatureValue` · `HumidityValue` · `CurrentValue` · `VoltageValue`(double?), `Vibration` · `UpsBatteryLevel`(int?), `UpsCharging`(bool?).

```csharp
var r = await deviceApi.GetAllEnclosureLatestMetricsAsync();
if (!r.Success && r.StatusCode == 404) { /* 이 서버엔 없음 → 함체별 /metrics/latest 로 폴백 */ }
foreach (var row in r.Data ?? new()) { var m = row.LatestMetrics; /* null = 계측 없음 */ }
```

## 그 밖의 공개 추가 (참고)
- G21 `GetPresetsAsync(cameraId, includeRois)` — 시그니처 그대로, 이제 `limit=100` 으로 끝 쪽까지 모아 `Data.Items` 에 전부(라이브 12/12 확인).
- G20 `RoiDto.IsExclusion` (bool?, `is_exclusion`, null 이면 안 실림).
- G23 `EnclosureMetricSaveResponseDto.EffectiveThresholdExceeded` (data.threshold_exceeded 우선 → 6.3 최상위) · `.Warnings`; `ThresholdExceededItemDto.Direction` · `.Severity` · `.EffectiveDirection`; `EnclosureMetricDto.ThresholdExceeded`(읽기 전용).
- G26 `ReplacePointsAsync` 반환형 그대로(`ApiResponse<PointListDataDto>`), `Data` 실제 형은 `PointReplaceResultDto`(`RoiId` · `Points`=`Items` · `Total`).
- G29 `EnumServerStatus.UNKNOWN` (= 3, 끝에 추가).
- G30 `CategoryDetailDto.ServerReferences` (`IReadOnlyList<ServerReferenceDto{Id, CategoryServer}>`), `Servers`(List<ServerDto>) 는 호환용으로 남김.
- G36 `BroadcastPlayBodyDto.Volume` · `TtsBodyDto.Volume` (int, 기본 100, 0~100 으로 자름, 늘 직렬화). 상수 `VolumeMin/VolumeMax/VolumeDefault`.
