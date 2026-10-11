# PLAN — t-20261011-230657-ca07 · 원격 6.3.x 대비 라이브러리 결함(F1 · F2 · 전수 대조)

## 목표
원격지 GOP API 6.3.x(지난 실측 6.3.2)에서 GIS 가 조용히 깨지는 라이브러리 결함을 고친다.
호스트 작업(t-20261011-221423-4e9d)이 6.3.0 스펙 대역으로 찾아 보고만 한 F1 · F2 + 같은 종류 전수 대조.
각 건 빨강 → 초록 기록, 시험에 `[Trait("Scenario","DEP-NNN")]`(DEP-301 부터).

## 근거 자료(읽기 전용)
- **6.3.0 서버 소스**: `C:\workspace_python\api-test-server\offline-installer\dist\bundle\payload\app` (main.py:417 `version="6.3.0"`).
  api-test-server 저장소의 `git log/show` 는 이 세션에서 거부돼(허용 목록 밖) 대신 이 6.3.0 번들 소스를 정본으로 썼다.
- 6.3.0 openapi: `Dotnet.Monitoring.Solution.Tests\Deployment\Fixtures\openapi-6.3.0.json`(호스트 저장소, 읽기만). 매핑 하위 목록은 `ApiSingleResponse[dict]` 라 모양은 소스로 확인.
- 6.3.1 · 6.3.2 변경: api-test-server `CHANGELOG.md` 1275-1388 (6.3.1 = v65·v66·v67·v69 마이그 + 억제 신설 · server_metrics tz 수정, 6.3.2 = 억제 4건).
- 호스트 시험 `OldServerCompatScenarioTests.cs`(손대지 않음)의 6.3.0 매핑 응답 본문을 라이브러리 시험에 그대로 옮겼다.

## 진행 (빨강 → 초록)
| DEP | 결함 | 빨강(수정 전 실패 한 줄) | 초록 |
|---|---|---|---|
| DEP-301 | F2 매핑 카메라 중첩 `camera{id}` 를 못 읽음 → 탐지 창 카메라 0개 | `Expected: [5, 6] Actual: []` (OldServerMappingCameraCompatTests) | ✅ 4/4 |
| DEP-302 | F2 6.3 응답의 `target_preset.preset_index` 버림 → 서버 설정 프리셋 이동이 조용히 사라짐 | `Sequence contains no matching element` (카메라 0개라 프리셋 행 없음) | ✅ |
| DEP-303 | F2 카메라 id 없는 행을 말없이 버림 → 경고 로그 1줄 | `Expected: [5] Actual: []` | ✅ |
| DEP-304 | F2 `EventMappingCameraDto` 두 모양 읽기 · 스칼라 우선 · 요청 본문 불변 | `Expected: 5 Actual: 0` (OldServerMappingDtoShapeTests) | ✅ 7/7 |
| DEP-305 | F2 같은 결함: 스피커 `speaker{id}`·`file_group{id}`, 경광등 `lamp{id}`·`event_mapping{id}` | `Expected: (12, 3, 3) Actual: (0, 0, 3)` · `Expected: (21, 7) Actual: (0, 0)` | ✅ |
| DEP-306 | F1 통문 404/405 를 오류 로그로 → 정보 1줄 '이 판본 미지원', 적재 계속(500 은 종전대로 오류) | `Assert.DoesNotContain() … "Failed to fetch gates at page 1: HTTP 404…"` | ✅ 3/3 |
| DEP-307 | 세션 정책: 6.3.0~6.3.2 는 동시 로그인 5키가 없음 → 화면이 "여러 곳 동시 로그인 허용"을 서버 값처럼 보이고 저장은 말없이 버려짐 | `Assert.False() Expected: False Actual: True` (IsConcurrencySupported) · PUT 키에 `login_anomaly_event_enabled, max_concurrent_sessions …` | ✅ 3/3 |
| DEP-308 | 세션 목록: 6.3 은 pagination 없음 → 100건에서 무한 스크롤이 조용히 멈춤 | `Assert.True() Expected: True Actual: False` (HasMorePages at 100) | ✅ 2/2 |
| DEP-309 | 서버 임계: 6.3 `PATCH /servers/{id}` 는 `threshold_config` 통째 교체 → 한 칸만 고쳐도 나머지 임계가 200 과 함께 지워짐 | 본문 `{"threshold_config":{"cpu":{"warning":61.0}}}` (나머지 7값 없음) | ✅ 4/4 |
| DEP-310 | 서버 계측: 6.3 `threshold_exceeded` dict(서버 판정)를 버림 → 임계 초과 배지가 조용히 0개 | `Expected: ["cpu","ram"] Actual: []` | ✅ 4/4 |

- [x] F2 매핑 카메라 · 스피커 · 경광등 DTO 중첩 읽기 (Messages `EventMapping{Camera,Speaker,Lamp}Dto`)
- [x] F2 `EventMappingCameraCache` 옛 경로: 카메라 id 없는 행 경고 + 6.3 프리셋 번호 전달
- [x] F1 부팅 적재 통문 404/405 → 정보 1줄 (`DeviceProviderService.FetchGatesAsync`)
- [x] 항목 3 감사 — 하위 감사 4건(장비 프리셋·ROI / 계정 / 이벤트 / 추적·보고서·서버) 결과 반영 → DEP-307~310
- [x] 항목 3 주 감사(장비 목록 8경로 · 그룹 · 단건 CRUD) — 6.3.0 모양 결함 0건(아래 '문제 없음')
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 실패 0(종료 코드 0) — Messages 428 · Utils.Tests 881 · Reports.Ui 365 · Accounts.Api 257 ·
      CameraPopup 286 · CameraPopup.Host 161 · Monitoring.Models 163 · Sso 91 · SystemResources 22 · Watchdog 4
- [x] 솔루션 실행에 안 잡히는 손댄 프로젝트 개별 실행 — Devices.Ui 4282 · Events.Ui 2043 · Devices.Api 206(건너뜀 104 기존) · Events.Api 5(건너뜀 44 기존) ·
      **sln 밖** Accounts.Ui.Tests 411 — 전부 실패 0

## 결정
- (F2) 응답 전용 중첩 속성은 **쓰기 전용(setter-only) private 속성**으로 받는다 → Newtonsoft 가 직렬화하지 않으므로
  같은 DTO 를 쓰는 생성 · 수정 요청 본문(`camera_id` 등 스칼라)은 한 글자도 바뀌지 않는다(`ShouldSerialize` 전역 함정 회피).
  최상위 스칼라가 오면 그것이 우선, 없을 때만 중첩 id 로 채운다(`[OnDeserialized]`). 기존 `MappingDeviceRefDto` · `MappingPresetRefDto` ·
  `MappingFileGroupRefDto` · `MappingParentRefDto` 재사용.
- (F2) 6.3 옛 경로도 `target_preset.preset_index` 를 탐지 창에 넘긴다 — 종전 주석 "옛 응답엔 프리셋 번호가 없다"는 6.3.0 소스와 다르다
  (event_mapping_cameras.py:89-101 `_build_preset_nested`). 감시금지구역 표시는 6.3.0 프리셋에 없어 false 유지. 프리셋 이동은 호스트 ONVIF 경로라 서버 판본 무관.
- (F1) 판본으로 아예 안 부르는 방식은 **쓰지 않는다**. 근거: `ServerContractProbe` 는 6.x 전부를 `V6_3` 하나로 묶고
  (`Contract` 로는 6.3.0 과 통문이 있는 6.3.x 를 못 가른다), 통문이 생긴 정확한 패치 판본이 확정돼 있지 않다
  (호스트 기록 "6.3.2 뒤 · 6.3.16 무렵", api-test-server CHANGELOG 6.3.0~6.3.2 에 통문 없음, 라이브러리 `GateDeviceApiTests` 주석은 "6.3 부터"). `RawVersion` 문자열 비교로 끊으면
  통문이 있는 6.3.x 에서 통문 전체가 조용히 사라지는 더 나쁜 결함이 된다. 그래서 늘 한 번 부르되 404/405 만 '이 판본 미지원' 정보 1줄로 넘긴다.
  다른 실패(500 · 422 · 타임아웃)는 종전처럼 오류 로그.
- (DEP-307) 동시 로그인 키가 응답에 **하나도 없으면** 미지원으로 본다(일부만 빠진 새 판은 종전 기본값 폴백). 미지원이면 6.3.0 실제 동작(auth.py:572-591, 같은 계정 세션 전부 종료)대로
  `evict_all` 을 보이고 절을 잠근다(XAML `IsEnabled=CanEditConcurrency`, 첫 칸 `LockReason`), 저장 본문에서 5키를 뺀다. 시험용 주입점 `ApiOverride`(internal).
- (DEP-308) pagination 이 없으면 "쪽이 가득 찼으면(100) 다음 쪽이 있다"로 판단, 전체 수를 모르는 동안 `"N건 이상"` 표시.
- (DEP-309) 6.3 본문만 기준선(`ServerWriteIntent.BaselineThresholds` = 저장 직전 GET 의 임계) 위에 덮는다. 7.0+ 축 본문은 RFC 7396 병합이라 종전대로 손댄 값만.
- (DEP-310) 기존 주석은 "6.3 dict 는 배지 없음이 옳다"였으나 그 근거(우리 임계로 판정 금지)는 dict 에 맞지 않는다 — dict 도 서버의 `_check_thresholds` 판정이다.
  `threshold` 숫자가 없는 항목은 판정이 아니라 건너뛴다(기존 시험 `{cpu:true}` 무배지 그대로 통과).

## 발견 · 확인 못 한 것(나)
- (나) **이벤트 목록 4종 날짜 필터 500** — 6.3.0 은 `events.created_at` 이 naive `DateTime` 이고 목록 라우터가 aware 날짜를 그대로 비교한다
  (detections.py:204,241-246 등, 서버 자체 주석 event_statistics.py:38-46 "asyncpg 500"). GIS 는 규약상 늘 `+09:00` 을 보낸다.
  그러나 6.3.1(2026-07-31)의 v66 마이그레이션이 전 naive 컬럼을 timestamptz 로 바꿨고(`docs/coordinations/gis/GOP_Server_API_datetime_unification_GIS_NOTIFY.md`
  "배포 완료, main / release/v6.3", CHANGELOG 6.3.1 마이그 v65~v69) 원격은 6.3.2 라 **원격에는 해당 없음으로 판단**, 고치지 않았다(aware 전송은 프로젝트 규약).
  원격 6.3.2 에서 `GET /api/events/detections?start_date=…%2B09:00` 한 번으로 확인 권장(이 작업은 원격 요청 금지).
- (나) 억제(정비 창) API 7종은 6.3.0 에 없음(6.3.1 신설) — 원격 6.3.2 에는 있다. 6.3.0 전용 문제라 손대지 않음.
- (나) `action_reported` 필터 소문자 전송(6.3.0 은 `"True"/"False"` 문자열 비교) — 비시험 호출부 0 → 영향 없음, 손대지 않음.
- (나) 프리셋 · ROI 목록 limit 미전송 시 서버 기본 10건 — 6.3 에서 프리셋 편집 경로는 7.0+ 게이트로 막혀 있음. `PUT /rois/{id}/points` 응답 `points` vs DTO `items`,
  `DELETE enclosure metrics` 개수 유실 — 비시험 호출부 0.
- (나) 서버 계측 이력 `collected_at` 이 비면 "보고 없음" — 운영 매니저가 `collected_at` 을 싣는지 소스로 확인 불가.
- (나) 계정 목록 `GET /users` 6.3.0 은 응답 변환 못 한 행을 건너뛰어(users.py:123-134) 가득 찬 쪽이 99건으로 오면 전량 순회가 일찍 멈출 수 있음 — DB 상태 의존, 확정 불가.
- (나) 감사 로그 날짜 조건 · 보고서 날짜(`yyyy-MM-dd`) — 드라이버 · Pydantic 동작 의존, 실측 필요(실패해도 오류 팝업이 뜨는 쪽).
- (참고 · 판본 무관 · 범위 밖) `SensorWriteGateway.ListByControllerAsync` 가 limit 500 을 1쪽만 부르고 라이브러리 `ClampLimit` 가 100 으로 접는다 →
  제어기당 센서가 100개를 넘으면 '만든 뒤 id 를 못 받았을 때 번호로 되찾기'(C7)가 101번째부터 못 찾는다. 6.3 차이가 아니라 손대지 않음 — 후속 작업 후보.
- 6.3.0 매핑 카메라 응답에 `target_preset.preset_index` 가 있다(위 결정).

## 대조했고 문제 없음(6.3.0 소스 기준)
- 장비 목록: controllers(`include_sensors`) · sensors · cameras(`urls` · `hardware_spec`) · speakers(`server{}` 중첩을 먼저 읽음) · enclosures · lamps · groups(+단건 · 배정/해제) — 봉투 · 키 · 열거값 일치, 6.3 에 7.0+ 쿼리 안 보냄.
- 이벤트: 탐지 · 장애 · 연결 · 조치 목록/단건 응답, 조치 생성 본문, 원본별 조치, 통계 4종, 매핑 목록(`status`·`device_group_id`).
- 계정: 로그인 · refresh · 로그아웃 · `/auth/me` · `/auth/me/permissions` · 사용자/그룹/부여 CRUD · 감사 로그(pagination 있음) · SSO 교환 404 → 조용한 폴백.
- 프리셋/ROI/점 단건 · 카메라 설정 · 함체 계측 · 위치/하드웨어 PATCH(응답 `Success` 만 사용) · 축 계열은 6.3 에서 호출 전 차단.
- 추적 점(`cursor.next_cursor`) · 보고서 구성/템플릿/생성/미리보기/PDF · 서버 목록/단건/분류/계측 이력/프록시 설정 · 조치보고 문구 404 → 기본 문구.

## 호스트(Dotnet.Monitoring.Solution)에 넘길 것 — 이 저장소에서 손대지 않음
- DEP-044 `should_return_the_mapped_camera_from_a_6_3_mapping_response` 의 `[Ignore]` 는 이 수정이 v2.6 에 들어간 뒤 호스트가 걷는다.
- DEP-042 는 "통문 404 는 오류 로그로만 남는다"(`log.ErrorMessages` 에 gate)를 단언한다 — F1 수정 뒤에는 오류가 아니라 **정보** 로그라 호스트 단언을 갱신해야 한다.

- DEP-307 의 세션 정책 화면은 6.3 원격에서 '동시 로그인' 절이 잠기고 사유 한 줄이 보인다 — 헤디드 확인은 호스트 쪽 몫(창 띄우는 시험 금지).

## 남은 일
- 없음(이 작업 범위). 위 (나) 항목은 원격 6.3.2 실측 또는 후속 작업 후보.
