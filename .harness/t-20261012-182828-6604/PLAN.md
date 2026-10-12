# PLAN — 탐지 신호값 실수(#4) 결함 수정

## 목표
탐지 `detail.signal` 이 실수(21.5 등)여도 목록 쪽 전체가 날아가지 않게 하고, 값을 반올림 없이 받아 정수는 "21", 실수는 "21.5" 로 보인다.
요청 본문에 되돌려 쓸 때 정수였던 값은 `21` 그대로(‘21.0’ 금지).

## 진행
- [x] DTO `DetectionDetailDto.Signal` → `double?` + 정수 모양 보존 변환기 `Messages/Helpers/WholeNumberJsonConverter.cs`
- [x] 같은 결함 `DetectedObjectDto.Bbox` → `List<double>?`(원소에 같은 변환기)
- [x] 모델 `IDetectionEventModel.Signal` / `DetectionEventModel.Signal` → `double?`, `DetectionObjectModel.Bbox` → `List<double>?`
- [x] Events.Ui 소비처: 카드(`DetectionEventCardViewModel`) · 그리드 행(`DetectionEventViewModel`) · 속성창(`DetectionSelectionViewModel` · `DetectionEditModel`) · 조치 창(`ActionSelectionViewModel.OriginSignalText`) · 탐지 이력(`SignalHistoryItemViewModel` · `MaxSignal`/`MaxSignalText`/`AvgSignalText`) · 차트(`SignalChartPoint.Signal` double · `NiceCeiling(double)` · 툴팁) · 패널 `MaxSignal` · 미니바 변환기(`SignalBarWidthConverter`/`SignalIsMaxConverter`)
- [x] 표시 규칙 한 곳: `Events.Ui/Helpers/SignalFormat.cs`("#,0.######" — 정수 "3,400", 실수 "21.5", 꼬리 0 없음)
- [x] 목록 한 건 형식 오류 → 그 항목만 빼고 나머지 싣기(`ApiMessageHelper.ToApiListResponseAsync`) + `ApiListResponse.SkippedItems` + `EventApiService.WarnSkipped` 경고 로그(어느 id · 어느 칸)
- [x] 시험: Messages `Tests/DetectionSignalNumberTests.cs`(sln 안) · Events.Ui `Tests/DetectionSignalFractionTests.cs`(sln 밖, 직접 실행)
- [x] 호스트(Dotnet.Monitoring.Solution) 영향 조사
- [x] 전체 검사 `dotnet test Ironwall.Dotnet.Libraries.sln` — 실패 0 · 통과 2684(기준: 실패 1 · 통과 2663, 이번엔 기존 실패 건도 통과)

## 결정
- 신호 형식은 `double?`. 쓰기는 `WholeNumberJsonConverter` 가 소수부 없는 값을 정수 토큰으로 쓴다 → 읽은 `21` 은 `21`, `21.5` 는 `21.5` 로 되돌아간다. (서버가 `21.0` 을 보냈다면 `21` 로 나간다 — 값은 같고 정수 기대 서버에 더 안전하다.)
- `NullValueHandling.Ignore` · `[JsonExtensionData]` 는 그대로(시험으로 고정).
- 모델(`Monitoring.Models`) 의 `[JsonProperty("signal")]` 에는 변환기를 달지 않았다 — Monitoring.Models 는 Messages 를 참조하지 않고, 모델 JSON 은 서버 본문으로 나가지 않는다(본문은 DTO 를 `ApiService` 가 직렬화).
- 속성창 신호 편집 칸은 `double?` 에 바로 묶으면(UpdateSourceTrigger=PropertyChanged) "21." 입력 순간 21 로 되돌려 써져 소수점을 못 친다 → 문자열 대리 속성 `SignalEditText` 를 두고 XAML 바인딩만 그쪽으로 옮겼다(x:Name 불변). 숫자가 아닌 글자는 값을 바꾸지 않는다.
- 미니바 변환기는 int · double 등 숫자면 다 받는다(호스트 미리보기가 아직 int 를 바인딩).
- 목록 내성: 통째 역직렬화가 `JsonException` 으로 실패했을 때만(예외 필터) 항목별로 다시 읽는다. 봉투가 깨졌거나 `data` 가 배열이 아니면 예전처럼 실패. 항목은 **텍스트로** 다시 읽는다 — `JToken.ToObject` 는 정수 칸의 실수를 예외 없이 반올림해 깨진 항목을 값만 바꿔 싣기 때문(시험 SIGNAL-FLOAT-010 이 잡았다). 날짜 문자열은 `DateParseHandling.None` 으로 보존. `Pagination` 은 서버 값 그대로.
- 경고 로그: Messages 는 로거가 없어 `SkippedItems`(+ `Trace.TraceWarning`)로 넘기고, 보고된 경로인 `EventApiService` 의 이벤트 목록 9곳(탐지 · 장애 · 연결 · 조치 ×4 · 탐지 로그 · 운영)에서 `_log.Warning` 으로 남긴다.
- 기존 시험 수정은 **형식 문자만**: `Bbox = new List<int> {…}` → `new List<double> {…}` (Messages `UnitTest.cs` 3곳, Events.Ui `DetectionSignalTests.cs` 4곳 · `EventEditSavePipelineTests.cs` 1곳). 값 · 단언은 그대로 — 공개 형식이 넓어져 컴파일만 맞춘 것. 기존 단언 `"\"signal\":85"` · `"\"bbox\":[10,20,30,40]"` 는 정수 모양 보존의 회귀 가드로 그대로 통과한다.

## 전수 대조 결과(서버 `C:\workspace_python\api-test-server\app\schemas` ↔ GIS DTO)
| 서버 스키마 | GIS DTO(수정 전) | 판정 |
|---|---|---|
| `event.py:316` `DetectionDetail.signal: Optional[float]` | `DetectionDetailDto.Signal: int?` | **같은 결함 → 고침(`double?`)** |
| `event.py:305-307` `DetectionDetailObject.bbox: List[float]`(정규화 비율 0~1 실수 실재) | `DetectedObjectDto.Bbox: List<int>?` | **같은 결함 → 고침(`List<double>?`)** |
| `event.py:302` `confidence: float` | `DetectedObjectDto.Confidence: double` | 정상 |
| `event.py:317-318` `frame_width/frame_height: Optional[int]` | `int?` | 정상 |
| `event.py:324` `inference_ms: Optional[int]` | `int?` | 정상 |
| `event.py:335-338` `MalfunctionDetail.first_start…second_end: Optional[int]` | `MalfunctionDetailDto int?` ×4 | 정상 |
| 이벤트 봉투 `id`/`device_id`/`unit_id`/`from_event_id: int` (탐지 · 장애 · 연결 · 운영 · 조치) | `int`/`int?` | 정상 |
| `device.py:123-129` 위치 lat/long/altitude/heading `Optional[float]` | `GeolocationDto double/double?` | 정상(형식) — Latitude/Longitude 가 non-null double 이라 null 처리 위험은 별건 |
| `device.py:1405-1461` 함체 계측 temperature/humidity/current/voltage `Optional[float]` · vibration/ups_battery_level `Optional[int]` | `EnclosureMetricDto` string?(의도된 설계) · `int?` | 정상(문자열은 숫자 토큰을 받는다) |
| `device_axes.py` max_detection_range float · channel/connection/schema_version int | `double?` · `int?` | 정상 |
| `server.py:600-608` 서버 계측 ×9 float · `:636-637` threshold value/threshold float | `double?` · `double` | 정상 |
| `camera_preset.py` ROI x/y/resolution float | `double` | 정상 |
| `unit.py:263-268` dx/dy float | `double` | 정상 |
| `tracking.py:39-45` lat/long/distance_m/confidence/speed_mps float | `double`/`double?` | 정상 |
| 이벤트 통계(EventTrend · ControllerStats · CameraStats · DeviceEventStats · Summary · DailyAverages · ActiveDevices) 약 35칸 | `int` 건수 / `double` 평균 | 정상 |
| (규격 없음) `TrackingTargetDto.Bbox: List<int>`(NATS 추적 메시지) | — | 서버 REST 스키마에 대응 없음, 이번 범위 밖(손대지 않음) |
| (레거시) `Api.Messages\Events\*` · `Api.Messages\Devices\*` | 6.3 평면 모양 | 현행 스키마 대응 없음 — signal 칸 없음 |

## 발견
- 2026-10-12 작업 중 C: 드라이브가 한동안 0바이트(100%)라 파일 저장이 ENOSPC 로 실패했다가 다시 풀렸다(원인은 이 작업 밖 — 지울 권한 없는 파일).
- `ApiMessageHelper` 의 항목별 재읽기를 `JToken.ToObject` 로 하면 정수 칸의 실수가 조용히 반올림된다(NATS 경로의 21.5→22 와 같은 원인). 텍스트 재역직렬화로 해결.
- 호스트(`C:\workspace_app\Dotnet.Monitoring.Solution`) 에서 신호를 int 로 쓰는 곳: `tools\shell-surface-preview\HostDialogSnapshots.cs`
  - `:129` 자체 행 `int? Signal` · `:154` `int MaxSignal` — 우리 XAML 미니바 변환기에 바인딩된다 → 변환기가 int 도 받게 해 무회귀.
  - `:181/:195` `new SignalChartPoint(time, s.Signal(int), …)` — 생성자 인자가 `double` 이 됐지만 int→double 암시 변환이라 **컴파일 무회귀**.
  - 그 밖에 호스트에서 `IDetectionEventModel.Signal` · `DetectionDetailDto.Signal` · `Bbox` 를 int 로 쓰는 곳은 없다(grep).
- `Devices.Api\Helpers\ResponseHelper.ToApiListResponseAsync` 는 별도 구현이라 항목 내성이 없다 — 장비 목록 DTO 에는 이번 대조에서 같은 결함이 없어 손대지 않음.

## 시험(빨강 → 초록)
- Messages(sln 안) `DetectionSignalNumberTests` 20건(Theory 포함): 실수 신호 한 건 섞인 쪽 전부 읽힘(21 / 21.0 / 21.5 / 32.2) · 단건 · NATS `ToObject` 반올림 없음 · 쓰기 정수 모양(21 · 0 · 3400 / 21.5 · 32.2) · detail 읽기→쓰기 원문 동일(업체 키 포함) · null 키 생략 · Replace 본문 · bbox 비율/픽셀 · 깨진 항목만 빼고 id/칸 보고 · 봉투 깨짐은 예전처럼 실패.
  - 수정 전 코드에선 SIGNAL-FLOAT-001(21.0/21.5/32.2) · 002 · 008 이 `INTERNAL_ERROR`/예외, 003 이 22, 004/005/007 의 실수 케이스는 `int?` 라 값 자체를 담을 수 없어 빨강(대화형 세션 재현 증거와 같은 시나리오). 이 세션에서 수정 전 코드로 되돌려 실행하지는 않았다.
- Events.Ui(sln 밖) `DetectionSignalFractionTests` · `DetectionSignalFractionRowTests`: 표시 문구 · DTO→모델 반올림 없음 · 모델→Replace 본문 정수/실수 모양 · bbox 왕복 · 미니바 변환기(double · int) · 탐지 이력 그리드/차트/최대/평균 · 그리드 행/속성창 문구 · "21." 입력 유지 · 숫자 아닌 입력 무변경.
- 실행: `dotnet test Ironwall.Dotnet.Libraries.Messages/...csproj` 448/448 통과. `dotnet test Ironwall.Dotnet.Libraries.Events.Ui/...csproj --filter …` 신호 관련 76/76 · 인접 헤드리스 213/213 통과(창 띄우는 시험 클래스는 제외).

## 남은 일
- 없음.
