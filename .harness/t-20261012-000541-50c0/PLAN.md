# PLAN — 라이브 통합 시험(DEP-201~208) 실패 정리 (t-20261012-000541-50c0)

## 목표
로컬 GOP 시험 서버(https://localhost:8000/api, 8.0.9)에 붙인 Devices.Api(48 실패) · Events.Api(15 실패) 라이브 시험을
실패 0 으로. 실패마다 (다) 제품 결함 → 라이브러리 수정 / (라) 시험 낡음 → 시험 수정(단언 약화 금지) / 서버 결함 → Skip + 근거.
마무리 조건: `dotnet test Ironwall.Dotnet.Libraries.sln` 실패 0 + 두 프로젝트 라이브 실행 실패 0.

## 이 세션의 제약(중요)
- 이 세션에서는 다음이 **권한 거부**됐다(무인 실행이라 승인 불가):
  - PowerShell `$env:IRONWALL_E2E_CRED_FILE=...; $env:IRONWALL_LIVE_API_URL=...` (환경변수 설정) → 라이브 시험 직접 실행 불가
  - 작업 밖 경로 TRX 기록 폴더(scratchpad/dep/live1) 읽기 → 실제 실패 메시지 열람 불가
  - `curl https://localhost:8000/openapi.json` → 서버 openapi 직접 조회 불가
- 우회하지 않는다(다른 셸 · 래퍼 · runsettings 로 환경변수 주입 금지).
- 대신 **서버 소스**(`C:\workspace_python\api-test-server`, `app/main.py` 판본 8.0.9 = 시험 서버와 같은 판)를 읽을 수 있어서,
  시험 → 서비스 요청(경로 · 질의 · 본문 JSON) → 서버 라우터/스키마 → 응답 → DTO 역직렬화 → 단언을 정적으로 추적해 원인을 가렸다
  (분석 에이전트 5개 병렬, 예측 실패 합계 Devices.Api 46~47 · Events.Api 15 — 보고된 48 · 15 와 맞는다).
- 라이브 재실행(검증)은 이 세션에서 못 한다 → 최종 보고에 명시. 재실행 명령:
  ```
  $env:IRONWALL_E2E_CRED_FILE='<injector-cred.txt 경로>'; $env:IRONWALL_LIVE_API_URL='https://localhost:8000/api'
  dotnet test Ironwall.Dotnet.Libraries.Devices.Api/Ironwall.Dotnet.Libraries.Devices.Api.csproj
  dotnet test Ironwall.Dotnet.Libraries.Events.Api/Ironwall.Dotnet.Libraries.Events.Api.csproj
  ```

## 진행
- [x] 1. 시험 파일별 정적 추적(분석 에이전트 5개) — 실패 예측 · 원인 · 분류 (아래 표)
- [x] 2. (다) 제품 결함 수정 + 헤드리스 시험(빨강→초록) — 서버 결함으로 판정된 것은 0건(Skip 없음)
  - [x] L1 카메라 `/settings` 3종 7.0+ 선차단(`ENDPOINT_REMOVED`) — DeviceApiService
  - [x] L2 카테고리 쓰기 `type_server`→`category_server`(ServerApiService.CategoryAxisBody) + 응답 `category_server` 역투영(CategoryDto.CategoryServer)
  - [x] L3 서버 계측 POST 본문 정리 — 7.0+ 에서 서버가 받는 12키(계측 10 + detail + observed_at)만, collected_at→observed_at
        (ServerApiService.MetricAxisBody, 서버 schemas/server.py:585-617). 헤드리스 `Tests/ServerApiServiceWriteBodyTests.cs` 17건(L2 포함)
  - [x] L4 함체 계측 삭제 `data:null` → 삭제 수를 message 에서 합성 — DeviceApiService.DeleteEnclosureMetricsAsync + ParseDeletedCount
  - [x] L5 옛 질의 파라미터(축 계약): include_sensors/include_controller → `include=` 토큰(AddRelationInclude), type_device→type_sensor,
        mode→protocol, category→type_camera, speaker_type→speaker_role(RenamedFilter), door_status → 선차단(ENDPOINT_UNAVAILABLE) — DeviceApiService
  - [x] 헤드리스: `Tests/DeviceLegacyQueryContractTests.cs` 21건(L1 · L2 역투영 · L4 · L5, 판본 8.0/6.3 쌍) — 수정 전 코드라면 옛 키를 보내 실패하는 단언
- [x] 3a. 장비 시험 갱신: DeviceApiIntegrationTest D16 · D21 · D23 · D42, UnitTest.cs(DEP-204 전체 라이브 · DEP-205 함체 계측) —
      `Tests/LiveDeviceKit.cs`(자기 장비 생성) · finally 삭제 · `if (Success)` 무검증 제거. Devices.Api 헤드리스 통과 227 · 건너뜀 104
- [x] 3b. 서버 시험 갱신(ServerApiIntegrationTest S01–S11 · ServerApiServiceTests 전체) — `Tests/ServerTestKit.cs`:
      서버 등록 · 수정은 IServerAxisApiService(평면 쓰기 AXIS_SHAPE_REQUIRED 는 회귀로 계속 단언), 상태는 ReportServerStatusAsync,
      계측 삭제는 olderThanDays, 고정 서버 id 1 제거(자기 카테고리 · 서버 생성 → finally 서버 → 카테고리 삭제),
      카테고리 유형은 시드에 없는 16종을 두 컬렉션에 겹치지 않게 배분(통합: ETC · MONITORING · LOG · BACKUP · PUSH / 서비스: 나머지 11),
      잔재 정리는 자기 접두어(MIG_ · LIVE-SRV-) 행만. 5.1 은 옛 proxy-settings ENDPOINT_REMOVED + PROXY 서버 server_config.modes 확인.
      Devices.Api 헤드리스 통과 244 · 건너뜀 104 · 실패 0.
- [x] 3c. 이벤트 시험 갱신 — `Tests/LiveEventTestData.cs`(원시 IApiService 로 8.0.9 본문의 제어기 · 카메라 · 프리셋 · 스피커 · 경광등 생성/삭제,
      `E2E-EVT-` + Guid). EventApiIntegrationTest: 픽스처가 제어기 1대를 만들고 끝에 지움(장비 id 2 제거), E10 · E11 정리 순서(조치 → 원본) 보장,
      E13 장비그룹 1 제거 + PATCH/PUT 값 단언 추가, E16~E20 자기 카메라 + 실제 프리셋(target/home) · 자기 스피커 · 경광등.
      UnitTest.cs(DEP-207): 고정 id · `if (Success)` 무검증 제거, 단건 조회는 자기 자료라 LiveApiWriteFact 로, 삭제는 Success · 200 · 재조회 실패로 판정,
      "05-1"/"08-1" 은 요청 필드가 아닌 action_reported 대신 result/reason 수정. Events.Api 헤드리스 통과 16 · 건너뜀 44 · 실패 0.
- [x] 4. `dotnet test Ironwall.Dotnet.Libraries.sln` 실패 0 확인(10개 어셈블리, 종료 코드 0) · Devices.Api 통과 244/건너뜀 104 ·
      Events.Api 통과 16/건너뜀 44 · Messages 421 통과.
- [ ] 5. 라이브 재실행(두 프로젝트, 위 명령) — 이 세션에서는 환경변수 설정 거부로 못 함. 다음 세션 몫.
  - [x] L6 이벤트 매핑 쓰기 본문을 서버 5키로만(id · cameras · speakers · lamps · created_at 제거) — EventApiService.BuildEventMappingWriteBody
  - [x] L7 매핑 스피커 `file_group_id: 0`(=없음) 을 싣지 않기 — EventApiService.BuildMappingSpeakerWriteBody(생성 · 부분 수정 · 전체 수정)
  - [x] 헤드리스: `Events.Api/Tests/EventMappingWriteBodyTests.cs` 11건(대조군 2건이 DTO 통째 직렬화의 금지 키 · file_group_id 0 을 보여 줌,
        수정을 임시로 되돌리면 8건 빨강 → 복원 후 초록 확인), 대역 `EventApiCaptureDoubles.cs`
- [ ] 3. (라) 시험 갱신(자료 자가 생성 · finally 삭제)
- [ ] 4. 헤드리스: Devices.Api · Events.Api csproj 각각 `dotnet test` 초록 + sln 초록

## 실패 표
분류: (다)=제품 결함→라이브러리 수정, (라)=시험 낡음→시험 수정, 서버=서버 결함(현재 0건).
근거 파일은 서버 `C:\workspace_python\api-test-server\app\...`.

### Devices.Api — DeviceApiIntegrationTest.cs (DEP-201)
| 시험 | 증상(예측) | 원인 | 분류 | 조치 | 빨강→초록 |
|---|---|---|---|---|---|
| D04 GetController_IncludeSensors | `?include_sensors=true` 422 REMOVED_FIELD | 7.0+ 는 `include=sensors`(controllers.py:183,188 · device_axes_io.py:519-523 · _legacy.py:82) | 다 | L5 | 헤드리스(질의 캡처) + 라이브 |
| D08 GetSensors_IncludeController | `include_controller` 422 | sensors.py:152,169 | 다 | L5 | 〃 |
| D09 GetSensors_TypeFilter | `type_device` 422 | sensors.py:148,163 — 새 키 type_sensor | 다 | L5 | 〃 |
| D13 GetCameras_ModeFilter | `mode` 422 | cameras.py:125,140 — 새 키 protocol | 다 | L5 | 〃 |
| D14 GetCameras_CategoryFilter | `category` 422 | cameras.py:124,138 — 새 키 type_camera | 다 | L5 | 〃 |
| D19 GetSpeakers_TypeFilter | `speaker_type` 422 | speakers.py:136,145 — 새 키 speaker_role | 다 | L5 | 〃 |
| D23 GetEnclosures_DoorStatusFilter | `door_status` 422 | enclosures.py:126,138 — 목록 대체 필터 없음(by-component 로 이동) | 다+라 | L5(선차단) + 시험을 by-component 로 | 〃 |
| D16 CameraSetting_GetAndPatch | GET settings 410 | camera_settings.py:82-134 묘비 | 라(+다 L1) | `/config` PATCH 로 재작성 + 옛 경로 ENDPOINT_REMOVED 단언 | 〃 |
| D42 CameraSetting_PUT | GET settings 410 | 〃 | 라(+다 L1) | `/config` PUT 으로 재작성 | 〃 |
| D21 Enclosure_CRUD | DoorStatus "CLOSED" 기대 → null | 7.0 에서 door_status 제거, 문 위치 초기값 없음(device.py:760-768) | 라 | 생성 직후 문 상태 null(모름) + 임계치 역투영 단언 | 라이브 |
| 나머지 D01–D03 · D05–D07 · D10–D12 · D15 · D17–D18 · D20 · D22 · D24–D41 | 통과 예측 | — | — | — | — |

### Devices.Api — ServerApiIntegrationTest.cs (DEP-202) · ServerApiServiceTests.cs (DEP-203)
| 시험 | 증상(예측) | 원인 | 분류 | 조치 |
|---|---|---|---|---|
| S01 · A59.2~A59.7 카테고리 쓰기 | "요청 검증에 실패했습니다" 422 REMOVED_FIELD `type_server` | server.py:69 · 518-570 (moved_to=category_server) | 다 | L2 |
| S04 · S09~S11 · A60.2~A60.7 서버 등록/수정 | `AXIS_SHAPE_REQUIRED`(네트워크 전 차단) | 7.0+ 평면 ServerDto 쓰기 금지(설계) — IServerAxisApiService 로 | 라 | 시험을 축 통로로 재작성 |
| S09 · A61.1 · A61.5 서버 계측 기록 | 422 UNKNOWN_FIELD server_id · created_at · threshold_exceeded | ServerMetricsCreate extra=forbid(server.py:591) | 다 | L3 |
| S09 · A61.4 · A61.5 계측 삭제 | `INVALID_ARGUMENT`(before_date 오버로드) | 서버 계약은 older_than_days 만(server_metrics.py:301) | 라 | olderThanDays 로 |
| A61.3 최신 계측(서버 1) | 시드 서버 1 에 계측이 없으면 latest null | 고정 id 의존 | 라 | 자기 서버 만들고 계측 기록 후 조회 |
| 5.1 GetProxySettings(1) | ENDPOINT_REMOVED | 7.0+ proxy-settings 묘비 → server_config.modes | 라 | 옛 경로 ENDPOINT_REMOVED 단언 + 축 통로 modes 확인 |
| S12 EnclosureMetrics_Lifecycle | `delResp.Data!.DeletedCount` NRE | 삭제 응답 data:null, 수는 message(enclosure_metrics.py:362-366) | 다 | L4 |
| 시험 간 유형 충돌(ETC · LOG · BACKUP) | 409 (L2 이후 드러남) | category_server UNIQUE, 두 컬렉션 병렬 | 라 | 유형 분리 + 잔재 정리 |

### Devices.Api — UnitTest.cs DeviceApiServiceTests (DEP-204) · EnclosureMetricsTests (DEP-205)
| 시험 | 증상(예측) | 원인 | 분류 | 조치 |
|---|---|---|---|---|
| 07 · 11 · 12 · 13 · 18 · 19 · 22 | 404 NOT_FOUND | 고정 id 1(제어기 · 센서 · 카메라) — 장비 id 는 7종 공유 번호표, 센서 1 은 불가능 | 라 | 시험이 장비를 만들고 finally 삭제 |
| 09-1 | include_controller 422 | L5 와 같음 | 다 | L5 |
| 24 BulkCreateCameras | 422(type_camera 누락) | FISHEYES · THERMAL · NONE 은 8.0 어휘 아님(FIXED·PTZ·SPEED_DOME) | 라 | 어휘 교체 + 만든 카메라 삭제 |
| A62.1~A62.5 | 404 | 함체 3 고정 | 라 | 시험이 함체 생성 · 삭제 |
| A62.4 | Data null | L4 와 같음 | 다 | L4 |
| 05 · 08 · 10 · 10-1 · 14 · 16 · 20 (통과지만 무검증) · 06 · 17 · 21 · 23 (자료 누적) | — | `if (Success)` 무검증 · 남의 장비 삭제 · 만든 장비 미삭제 | 라 | 자기 자료 + 강한 단언 + finally 삭제 |

### Events.Api — EventApiIntegrationTest.cs (DEP-206) · UnitTest.cs (DEP-207)
| 시험 | 증상(예측) | 원인 | 분류 | 조치 |
|---|---|---|---|---|
| E13 · E16~E20 매핑 생성 | 422 UNKNOWN_FIELD id · cameras · speakers · lamps | EventMappingCreate extra=forbid, 5키만(integration.py:110 · 117-128) | 다 | L6 + 시험의 DeviceGroupId=1(없을 수 있음) 제거/자기 그룹 |
| E17 매핑 스피커 | (L6 뒤) 404 FileGroup 0 | `file_group_id: 0` 을 실제 id 로 해석 | 다 | L7 + 자기 스피커 |
| E16 · E19 매핑 카메라 | (L6 뒤) 404 preset 0 · 카메라 3 없음 | 고정 id + preset 0 | 라 | 자기 카메라 · 프리셋 생성(실 id 사용) — 카메라 DTO 자체는 다른 작업(ca07) 몫이라 손대지 않음 |
| E18 매핑 경광등 | (L6 뒤) 경광등 7 없음 | 고정 id | 라 | 자기 경광등 |
| E01 · E04 · E06 · E08 · E10 · E11 · U05 · U08 · U10 | 400 → DEVICE_NOT_FOUND | device_id=2 고정(서버 INIT_SAMPLE_DATA=false) | 라 | 픽스처가 제어기를 만들고 지움 |
| U 03~12-3 나머지(통과지만 고정 id · if(Success) 무검증) | — | 고정 id 의존 | 라 | 자기 이벤트로 강한 단언 |

## 결정
- D1. 라이브 실행이 막혀 서버 소스 정적 추적으로 원인 규명(위 제약 참조).
- D2. 다른 작업(t-20261011-230657-ca07: 6.3 매핑 카메라 DTO · gates 404)과의 경계: **그 결함 자체**(EventMappingCameraDto 의
  preset 0 · camera 응답 모양, 통문 API 404)는 손대지 않는다. 같은 파일(DeviceApiService.cs · EventApiService.cs)의
  **다른 메서드**는 이 작업의 결함이라 고친다(그렇지 않으면 Devices.Api 결함 대부분을 못 고친다). E16 · E19 는 시험이
  실제 프리셋 id 를 만들어 써서 카메라 DTO 를 고치지 않고 통과하게 한다.
- D3. 라이브러리 수정은 축 계약(7.0+)에서만 동작을 바꾸고 6.3 본문 · 질의는 바이트 그대로 둔다(운영 6.3.2 무회귀).
- D4. 시험이 만드는 자료는 이름에 접두어(`LIVE-` · `MIG_` · `E2E-`)를 붙이고 finally 에서 지운다(장비 삭제는 하위 센서 · 프리셋 연쇄 삭제).

## 발견(이번 실패와 무관한 잠재 결함 — 범위 밖, 기록만)
- 부분 DTO 로 장비 PATCH 하면 값형 `number_device:0` · `is_enable:false` 가 늘 실린다(BaseDeviceDto 값형) — 이름만 바꿔도 번호 0 · 비활성.
- 프리셋 · ROI PATCH 도 값형 기본값(preset_index 0 · touring_time 10 · resolution 0)이 실려 덮어쓴다(DeviceApiService PatchPreset/PatchRoi).
- `ReplacePointsAsync` 응답 모양 `{roi_id, points, total}` 을 `{items, total}` 로 읽어 Items 가 비어 있다.
- `EnclosureMetricSaveResponseDto.threshold_exceeded` 를 봉투 최상위에서 읽는데 8.0.9 는 `data.threshold_exceeded[]`(direction · severity).
- 탐지 detail `signal` · `bbox` 를 정수로 받는데 8.0.9 는 실수 허용 — 실수 값이 오면 목록 역직렬화 실패 가능.
- 탐지 · 장애 이벤트 부분 수정에서 `Result`/`Reason` 을 비워 두면 `""` 가 실려 422 EMPTY_STRING(값형 기본값 문자열).
- 매핑 스피커 DTO `RepeatCount` 기본 0 은 서버 `ge=1` 에 걸려 422 — 호출부가 명시해야 한다.
- 매핑 카메라 목록 응답 `camera:{id, category_device}` 를 `CameraId` 로 읽지 못해 0 — 다른 작업(ca07: 6.3 매핑 카메라 DTO) 몫, 손대지 않음.
- Devices.Api · Events.Api 는 `Ironwall.Dotnet.Libraries.sln` 에 **들어 있지 않다** → 하네스 검사(sln)는 이 두 프로젝트를 돌리지 않는다.
  기준선: Devices.Api 통과 206 · 건너뜀 104, Events.Api 통과 5 · 건너뜀 44. sln 실패 0.

## 라이브 재실행 때 확인할 위험(정적 추적으로 못 닫은 것)
- 빨강→초록의 **라이브 쪽 초록은 아직 확인되지 않았다**(환경변수 설정 거부). 헤드리스 초록만 확인.
- 전용 계정 권한: `servers:control`(서버 계측 POST · 상태 보고), `devices:edit/delete`(장비 생성 · 삭제), `devices:edit`(/config).
  없으면 해당 단계가 403 — 서버 결함이 아니라 계정 설정 문제다.
- 전역 정비 창(억제 스케줄)이 켜져 있으면 이벤트 생성이 202 + data 없음(detections.py:310-313) → 생성 단언 실패.
- 5.1(프록시 설정): 시드 PROXY 카테고리가 지워진 서버면 실패. 남이 만든 같은 유형 카테고리가 있으면 서버 시험이 409 로 멈춘다(의도).
- DEP-201 D23 의 by-component 조회는 문 부품 관측이 없으면 빈 목록(통과) — 값 단언은 Assert.All.

## 남은 일
- 위 진행 2~4.
- 라이브 재실행은 환경변수 설정 권한이 있는 세션에서(위 명령).
- **이전(수정 전) 라이브 실행이 서버에 남긴 자료** — 옛 시험은 만든 자료를 지우지 않았다(예: DEP-204 06 "제어기_1",
  21/23 "제어기_01~03" + 센서 240개 "펜스센서_…", 17/24 "테스트_카메라" · "IP카메라_…", 서버 카테고리 · 서버 "테스트_…",
  이벤트 · 조치). 이 세션은 서버에 붙을 수 없어 지우지 못했다. 이름으로 골라 지우는 것은 실사용 자료를 지울 위험이 있어
  자동화하지 않았다 — 라이브 권한이 있는 세션에서 이름 · 생성 시각을 확인하고 지울 것. 새 시험은 `LIVE-`·`MIG_`·`E2E-` 접두어 +
  finally 삭제라 더 남기지 않는다.
