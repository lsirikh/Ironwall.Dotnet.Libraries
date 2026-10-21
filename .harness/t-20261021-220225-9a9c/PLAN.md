# PLAN — GOP 8.0.15 L1 결함 묶음 빨강 → 초록 증명 (t-20261021-220225-9a9c)

## 목표
L1(REST 공통 · 계정 · 보고서 · 추적) 수정은 thin 작업 t-20261021-183650-ba72 가 v2.6 에 병합했다(88e12b1a). 그 작업은 시험 결과를 보지 못하고 끝났다.
이 작업은 **코드를 새로 만들지 않고**, 묶음마다 "제품 수정만 되돌림 → 시험 실패(빨강) 확인 → 복구 → 같은 시험 통과(초록)" 를 실제로 돌려 증명한다.

## 진행
- [x] 88e12b1a 의 제품 파일 34개 diff 를 읽고 묶음별 수정 위치를 정리(`git diff 88e12b1a^1 88e12b1a`)
- [x] 묶음별 시험 위치 확인 — 대부분 이미 있음. 약한 곳 1곳(G02 앱 로그 배선) 시험 추가
- [x] 기존 시험에 `[Trait("Scenario","GOP815-Gnn")]` 꼬리표 64줄 추가(본문 · 이름 그대로)
- [x] 기준선 빌드 `dotnet build Ironwall.Dotnet.Libraries.sln` → 오류 0
- [x] 기준선(초록) — 11개 프로젝트 `--filter Scenario~GOP815` 전부 통과
- [x] 빨강 회차 18회(묶음별, 한 번에 한 묶음만 되돌림) — 결과표 참조
- [x] 회차마다 복구 → `cmp` 백업 일치 + `git diff --quiet 88e12b1a -- <제품 34파일>` 확인
- [x] 복구 상태 초록 재확인(묶음별 꼬리표 시험)
- [x] 라이브 읽기 확인(로컬 8.0.11, 쓰기 없음)
- [x] 전수 시험(sln, Sounds · Libraries.Db 제외) + sln 밖 MapAux 전체

## 새로 오는 사람이 알아야 할 것
- 시험은 대부분 **제품 프로젝트 안 `Tests/` 폴더**에 있다(제품 csproj 가 xunit 을 참조). 예외: `Accounts.Ui.Tests`(별도 프로젝트), `tests/GMaps.MapAux.Tests`(**sln 밖**).
- 묶음별 시험은 꼬리표로 다시 돌릴 수 있다: `dotnet test <프로젝트.csproj> --filter "Scenario=GOP815-G05"`.
- 시험 위치(묶음 → 파일):
  - G01: `Messages/Tests/ApiRetryableAndRequestIdTests.cs`(핵심) · `Accounts.Ui.Tests/RetryableConflictMessageTests.cs` · `Devices.Api/Tests/ServerAxisRetryAndListViewTests.cs` · `Devices.Ui/Tests/ServerConsoleRetryableConflictTests.cs` · `Events.Api/Tests/EventMappingConflictAndChildBodyTests.cs` · `Reports.Api/Tests/ActionReportTemplateRetryableConflictTests.cs` · `Reports.Ui/Tests/ReportTemplateGop8015Tests.cs`(ActionReportTemplateRetryableConflictUiTests)
  - G02: `Messages/Tests/ApiRetryableAndRequestIdTests.cs` · **새** `Accounts.Api/Tests/BearerAuthFailureLogWiringTests.cs`
  - G03: `Messages/Tests/ApiErrorCodesVocabularyTests.cs`(22종 — 명세 §12.2 :15970 「닫힌 22종」, v8.0.6 에서 INVALID_TOKEN · TOKEN_REPLAYED · UNKNOWN_CLIENT 추가)
  - G04: `Accounts.Api/Tests/SsoExchangeTests.cs`
  - G05 · G06 · G07 · G08: `Accounts.Ui.Tests/` 의 PermissionMatrixAvailableModulesTests · Scenarios/AccountConsoleScenarioTests(Theory 행) · LoginFailMessageTransportTests · AccountSyncNatsServiceTests
  - G09 · G10 · G11: `Reports.Ui/Tests/ReportTemplateGop8015Tests.cs`
  - G12 · G13: `GMaps.Ui/Tests/TrackingFetchOutcomeTests.cs` · `tests/GMaps.MapAux.Tests/TrackingPlaybackScenarioTests.cs`
  - G28: `Devices.Api/Tests/ServerAxisRetryAndListViewTests.cs`
  - G42: `Events.Api/Tests/ActionEventsFromEventQueryTests.cs` · `Events.Ui/Tests/EventConsoleCompletenessTests.cs`
  - G51: `Events.Api/Tests/EventMappingConflictAndChildBodyTests.cs`
- L1 지시문의 G04 · G05 · G06 · G07 · G51 도 묶음 목록에 있어 함께 증명했다("지시문에 있는 전부").

## 되돌림 정의(빨강 때만 잠시 적용, 모두 복구함)
| 회차 | 되돌린 곳 |
|---|---|
| G01core | `Messages/Defines/Apis/ApiError.cs` `IsRetryable` → `false` |
| G01use | 소비처 6곳 + 보고서 콘솔의 retryable 분기 앞에 `false &&`: ServerConsoleService.Plain · ServerAxisApiService(등록 · 수정 2곳) · ActionReportTemplateApiService · UserSessionPanelViewModel.ExplainFailure · PermissionMatrixPanelViewModel.Explain · ActionReportTemplateConsoleViewModel.MapError · EventApiService.MapMappingDuplicate |
| G02helper | `ApiMessageHelper.LogFailure` 의 sink 를 null 로 · `StampRequestId` 의 X-Request-ID 헤더 폴백 끔 |
| G02wire | `BearerAuthHandler` 생성자의 `FailureLog ??=` 배선 끔 |
| G03 | `ApiErrorCodes` 의 INVALID_TOKEN · TOKEN_REPLAYED · UNKNOWN_CLIENT 상수 삭제 + `AccountApiService` 리터럴 복원 |
| G04 | `BearerAuthHandler.IsAuthEndpoint` · `IsLoginOrRefresh` 에서 `/auth/sso-exchange` 삭제 |
| G05 | `BuildMergedModules` 가 available_modules 무시 · 422 MISSING_FIELD 재시도 끔 |
| G06 | `LogoutReasonConverter` 새 6행 삭제 |
| G07 | `LoginPanelViewModel` FORBIDDEN-서버문구 분기 삭제 |
| G08 | `AccountSyncNatsService.OnNatsAsync` 즉시 반환 + `AccountUiModule` 등록 블록 `if (false)` |
| G09 | `ReportConsoleViewModel.DetailCanApply` · 꼬리말, `ReportTemplateEditViewModel` 저장 전 막기 · 422 components 문구를 옛 코드로(옛 `comps.Count == 0` 줄 복원) |
| G10 | `ReportCreateViewModel` template_id 422 분기 끔 |
| G11 | `TemplateComponentBoard.IsDropped` → `false` · 고아 안내 끔 |
| G12 | `TrackingApiService` HTTP 실패 종류 분류 끔(옛 `break` 만) · `PlaybackViewModel` 알림 무시 |
| G13 | `GetHealthAsync` 503 → Unknown · `PlaybackViewModel` 사전 점검 끔 |
| G28 | 서버 목록 쿼리를 옛 `view=full` 로 |
| G42 | `GetActionEventsAsync` 의 from_event_id · user 쿼리 끔 · `EventActionHistory` · `EventDetailViewModel` 에서 Connection 삭제 |
| G51 | 매핑 카메라 · 경광등 쓰기에 본문 빌더 대신 DTO 그대로 |

## 실행한 명령
- 기준선 빌드: `dotnet build Ironwall.Dotnet.Libraries.sln -nodeReuse:false -v:q -clp:ErrorsOnly` → `빌드했습니다. 오류 0개` (5분 15초)
- 묶음별 시험: `dotnet test <csproj> -nodeReuse:false --filter "Scenario=GOP815-Gnn" --logger "console;verbosity=normal"` (스크립트 `$TEMP/w/rt.sh`, 되돌림 `$TEMP/w/revert.py`, 복구 `$TEMP/w/restore.sh`, 회차 묶음 `$TEMP/w/red.sh` — TEMP 는 작업 끝에 지워진다. 위 표가 그 내용이다)

- G12 · G13 보강 회차 되돌림(첫 회차가 덜 되돌렸음 — DECISIONS 7):
  - G12full: `TrackingApiService` 결과 종류를 늘 Success 로(옛 동작 = 분류 없음) · `TrackPointApiReader` 알림 두 곳 null · `PlaybackViewModel` 알림 무시
  - G13full: `GetHealthAsync` 503 → Unknown · `TrackPointApiReader.CheckAvailabilityAsync` 문구 → null · `PlaybackViewModel` 사전 점검 끔

## 기준선(복구 상태) — `--filter "Scenario~GOP815"`
Messages 9/9 · Accounts.Api 2/2 · Accounts.Ui.Tests 33/33 · Devices.Api 7/7 · Devices.Ui 5/5 · Events.Api 10/10 · Events.Ui 1/1 · Reports.Api 3/3 · Reports.Ui 12/12 · GMaps.Ui 15/15 · MapAux 4/4 = **101건 전부 통과**.
빨강 회차가 다 끝난 뒤 같은 명령으로 다시 돌린 초록 재확인도 같은 숫자로 전부 통과(실패 0).

## 빨강 회차 요약(되돌린 상태, `--filter "Scenario=GOP815-Gnn"`)
빨강 회차에서 통과한 시험은 모두 **대조군**(옛 판 · 다른 코드 · 빈 값처럼 고치기 전후 같아야 하는 행)임을 이름 · 인자로 확인했다 — 예: G01 의 retryable=False 행, G05 의 6.3 스냅샷, G06 의 기존 사유 행, G28 의 6.3 서버, G42 의 옛 오버로드.
| 회차 | 프로젝트: 실패/총 |
|---|---|
| G01core | Messages 2/6 |
| G01use | Accounts.Ui.Tests 2/4 · Devices.Api 3/4 · Devices.Ui 3/5 · Events.Api 1/2 · Reports.Api 2/3 · Reports.Ui 1/2 |
| G02helper | Messages 2/2 · Accounts.Api 1/1 |
| G02wire | Accounts.Api 1/1 |
| G03 | Messages 1/1 |
| G04 | Accounts.Api 1/1 (3초 걸려 끝남 — 데드락을 시험이 3초 취소로 끊음) |
| G05 | Accounts.Ui.Tests 3/4 |
| G06 | Accounts.Ui.Tests 6/12 (새 6행 전부 실패, 기존 6행 통과) |
| G07 | Accounts.Ui.Tests 1/3 |
| G08 | Accounts.Ui.Tests 6/10 |
| G09 | Reports.Ui 3/4 |
| G10 | Reports.Ui 1/2 |
| G11 | Reports.Ui 2/4 |
| G12(첫 회차) | GMaps.Ui 1/8 · MapAux 3/3 — 덜 되돌림, 보강 회차로 대체 |
| G12full | GMaps.Ui 6/8 · MapAux 3/3 |
| G13(첫 회차) | GMaps.Ui 1/7 · MapAux 1/1 — 덜 되돌림, 보강 회차로 대체 |
| G13full | GMaps.Ui 2/7 · MapAux 1/1 |
| G28 | Devices.Api 2/3 |
| G42 | Events.Api 2/3 · Events.Ui 1/1 |
| G51 | Events.Api 5/5 |
매 회차 뒤 `restored: product files == 88e12b1a` 확인(20회 모두).

## 라이브 읽기 확인(로컬 https://localhost:8000/api, 쓰기 없음)
- `GET /tracking/health`(무인증) → `200 {"status":"ok","tracking_count":0}` — G13 의 503 분기는 로컬에서 재현할 수 없어 가짜 응답 시험으로 판정.
- 전용 계정(IRONWALL_E2E_CRED_FILE 파일, 내용 출력 안 함)으로 로그인 → `GET /auth/me/permissions` 200, `available_modules` **16개**(devices · events · reports · cameras · users · user_groups · audit_logs · servers · map · broadcast · setup_system · setup_feature · action_report_templates · integrations · files · units) → 로그아웃 200. 만든 자료 없음.
- 판본은 API 로 확인하지 못했다(`/info` 404, `/health` 는 판본 없음) — 지시문의 8.0.11 을 따른다.
- G42 6.3.2 지원: 서버 저장소 태그 `v6.3.2:app/routers/actions.py:256-257` 에 `user` · `from_event_id` 쿼리가 있다 → 판본 게이트 불필요(이전 작업의 판단과 같음).

## 전수 시험(복구 상태, 최종)
⚠ **발견**: `dotnet test Ironwall.Dotnet.Libraries.sln` 은 11개 어셈블리만 시험한다(Messages · SystemResources · Watchdog.Tests · Monitoring.Models · Accounts.Api · Reports.Api · CameraPopup.Host.Tests · Reports.Ui · Sso · Utils.Tests · CameraPopup.Tests, Tracking.Api 는 "일치하는 테스트 없음"). Accounts.Ui.Tests · Devices.Api · Devices.Ui · Events.Api · Events.Ui · GMaps.Ui 의 시험은 sln 경로로 돌지 않는다(이 작업 전부터 그랬다 — 하네스 검사도 이것들을 안 본다). 그래서 이 6개는 프로젝트별로 따로 전부 돌렸다.

1. `dotnet test Ironwall.Dotnet.Libraries.sln -nodeReuse:false --filter "FullyQualifiedName!~Ironwall.Dotnet.Libraries.Sounds.Tests&FullyQualifiedName!~Ironwall.Dotnet.Libraries.Db.Tests"` (단독 실행) → 종료 코드 1
   - 통과: Messages 504/504 · SystemResources 22/22 · Watchdog.Tests 15/15 · Monitoring.Models 250/250 · Accounts.Api 261/261 · Reports.Api 3/3 · Sso 106/106 · Reports.Ui 377/377 · CameraPopup.Host.Tests 197/197 · CameraPopup.Tests 334/334
   - **실패: Utils.Tests 6/886** — 전부 `CaptureDragLifecycleTests`(실제 `Mouse.Captured` 를 보는 드래그 시험). 단독 재실행(`--filter FullyQualifiedName~CaptureDragLifecycleTests`)에서도 6/14 실패. 이 작업은 Utils · 그 의존 코드를 건드리지 않았다(제품 파일 변경 0, 바뀐 시험 파일은 다른 어셈블리) → **이 작업과 무관한 기존 · 환경 실패**(사용자 RDP 세션이 활성인 채 실제 마우스 상태를 읽는 것으로 보임 — t-20261021-183628-7cd9 의 Shift 흔들림과 같은 종류로 추정, 원인 미확정). 고치지 않았다(범위 밖).
   - 첫 sln 실행(MapAux 와 동시)에서는 CameraPopup.Tests 1건(`…within_budget…` 1072ms > 50ms 예산)이 실패했으나 단독 실행에서 334/334 통과 — 동시 빌드 부하 탓.
2. sln 밖 · 꼬리표를 붙인 프로젝트 전체(순서대로 하나씩):
   - `tests/GMaps.MapAux.Tests` → **122/122 통과**(종료 코드 0). (첫 시도는 sln 빌드와 동시에 돌려 Events.Ui `.g.cs` CS2001 빌드 충돌 — 시험 실패 아님, 단독 재실행으로 대체)
   - Accounts.Ui.Tests **469/469** · Devices.Api **301 통과 / 108 건너뜀**(라이브 시험 — 자격 환경변수 없음) · Events.Api **37 통과 / 44 건너뜀**(같은 이유)
   - Devices.Ui 4534/4551(17 실패: `UnitConsoleNonModalTests` 13 · `UnitConsoleTreeSelectionReloadTests` 4, STA 창 시험 30초 · 1분 시간 초과) → 두 클래스 **단독 재실행 19/19 · 5/5 통과**
   - Events.Ui 2220/2222(2 실패: `SuppressionRecurrenceRadioTests`(UI 자동화) · `MappingCameraVideoTests`(영상)) → 두 클래스 **단독 재실행 45/45 통과**
   - GMaps.Ui 735/736(1 실패: `CameraPopupPtzSpeedMotionTests`) → **단독 재실행 3/3 통과**
   - 위 흔들린 시험들은 모두 이 작업이 바꾸지 않은 파일이다. 전체 실행 중 부하(다른 세션 testhost 3개 동작 중)에서만 실패했다.
3. 최종 상태 확인: `git diff --name-only HEAD` = 시험 파일 17개뿐(꼬리표 · BOM), 새 파일은 `Accounts.Api/Tests/BearerAuthFailureLogWiringTests.cs` 하나. **제품 파일 변경 0**(`git diff --quiet HEAD -- <제품 34파일>` 통과) — 되돌림이 남지 않았다.

## 정리(찌꺼기)
- 라이브: 로그인 세션 1개를 열고 로그아웃으로 닫음. 서버에 만든 자료 없음.
- 작업 폴더 안 임시 파일(`revert_tmp.py` · `revert_add_tmp.py`)은 TEMP 로 옮기거나 지움. 스크립트 · 로그 · 백업은 `$TEMP/w` · `$TEMP/backup`(하네스가 지움).
- 빌드 · 시험은 모두 `-nodeReuse:false` 로 돌려 MSBuild 노드를 남기지 않음. 중간에 끊은 빌드 · 시험 없음.

## 남은 일
- (범위 밖, 보고만) Utils.Tests `CaptureDragLifecycleTests` 6건 — 실제 마우스 상태 의존으로 보임, 숨은 데스크톱 전환 등 후속 작업 필요.
- (범위 밖, 보고만) sln 시험이 Accounts.Ui.Tests · Devices.* · Events.* · GMaps.Ui 를 돌리지 않는다 — 하네스 검사가 이 프로젝트들의 회귀를 못 본다.

## 결과표(묶음 · 시험 · 빨강 한 줄 · 초록 · 판정)
초록 = 복구 뒤 같은 꼬리표 시험 재실행(묶음 시험 101건 전부 통과) + 위 전수 실행.
| 묶음 | 시험(꼬리표 `GOP815-Gnn`) | 빨강 한 줄(되돌린 상태의 실제 실패) | 초록 | 판정 |
|---|---|---|---|---|
| G01 핵심 | Messages `ApiRetryableAndRequestIdTests.should_treat_only_409_with_retryable_as_retryable_conflict` 외 1 | `Assert.True() Failure / Expected: True / Actual: False` | 통과 | 증명됨 |
| G01 소비처 | Accounts.Ui `RetryableConflictMessageTests` 2 · Devices.Api `ServerAxisRetryAndListViewTests` 3 · Devices.Ui `ServerConsoleRetryableConflictTests` 3 · Events.Api `G01_RetryableConflict_SaysRetry` · Reports.Api `ActionReportTemplateRetryableConflictTests` 2 · Reports.Ui `should_pick_the_409_words_by_the_retryable_flag` | 예: `Expected: "다른 작업과 동시에 처리돼 저장하지 못했습니다. 잠시 뒤 다시 시도하세요." / Actual: "같은 이름의 권한 그룹이 이미 있습니다. 다른 이름을 입력해 주세요."` (6곳 모두 각자의 옛 고정 문구로 실패) | 통과 | 증명됨(6곳 + 보고서 콘솔) |
| G02 줄 내용 | Messages `should_log_meta_request_id_on_failure_envelope` · `should_fall_back_to_x_request_id_header…` | `Assert.Contains() Failure: Filter not matched in collection / Collection: []` | 통과 | 증명됨 |
| G02 앱 로그 배선 | **새** Accounts.Api `BearerAuthFailureLogWiringTests.should_write_the_failure_line_with_request_id_to_the_app_log_when_the_handler_is_built_with_a_logger` | `Assert.Contains() Failure: Filter not matched in collection / Collection: []` | 통과 | 증명됨 |
| G03 | Messages `should_declare_exactly_the_22_server_codes_when_spec_is_v8_0_6` (근거: 명세 §12.2 :15970 「닫힌 22종」) | `Collections differ / Expected: [··· "INTERNAL_ERROR", "INVALID_TOKEN", "METHOD_NOT_ALLOWED" ···] / Actual: [··· "INTERNAL_ERROR", "METHOD_NOT_ALLOWED" ···]` | 통과 | 증명됨 |
| G04 | Accounts.Api `should_return_transient_at_once_when_exchange_inside_reauth_gets_token_replayed` | `교환 요청이 전역 락 재진입으로 멈췄다(데드락)` (3초) | 통과 | 증명됨(Authorization 미부착 단언 포함) |
| G05 | Accounts.Ui `PermissionMatrixAvailableModulesTests` 3/4 | `should_not_retry_more_than_once…: Expected: 2 / Actual: 1` | 통과 | 증명됨(6.3 스냅샷 행은 대조군) |
| G06 | Accounts.Ui `should_show_the_logout_reason_in_korean…` 새 6행 | `Expected: 로그아웃 / Actual: 알 수 없음` (MANUAL) | 통과 | 증명됨 |
| G07 | Accounts.Ui `should_show_the_server_reason_for_forbidden_login_when_it_is_given` | `Expected: "비활성화된 계정입니다" / Actual: "계정이 잠겼거나 비활성 상태입니다. …"` | 통과 | 증명됨 |
| G08 | Accounts.Ui `AccountSyncNatsServiceTests` 6/10 (등록 시험 포함) | `should_skip_what_the_operator_may_not_view: Assert.Single() Failure: The collection was empty` | 통과 | 증명됨 |
| G09 | Reports.Ui `ReportTemplateGop8015Tests` 3/4 | `should_keep_apply_off_and_send_nothing…: Assert.False() Failure / Expected: False / Actual: True` | 통과 | 증명됨(이름만 수정 행은 대조군) |
| G10 | Reports.Ui `should_say_the_template_has_no_component_when_custom_generation_is_refused_on_template_id` | `Expected: "이 템플릿에 켜진 구성 요소가 없습니다. …" / Actual: "보고서 생성을 요청하지 못했습니다. …"` | 통과 | 증명됨 |
| G11 | Reports.Ui `should_drop_withdrawn…` · `should_tell_how_many_withdrawn…` | `Expected: "더 이상 쓰지 않는 항목 1개는 저장할 때 빠집니다." / Actual: ""` | 통과 | 증명됨(카탈로그 실패 2행은 대조군) |
| G12 | GMaps.Ui `TrackingFetchOutcomeTests` 403 · 2쪽째 500 · 400 cursor · 판독기 알림 3행 + MapAux 3 | 403: `Expected: Forbidden / Actual: Success`; 500·400: `Expected: Failed / Actual: Success`; 화면: `String: "2점 · 2건 로드" / Expected end: "— 일부만 불러왔습니다"` | 통과 | 증명됨(보강 회차 G12full) |
| G13 | GMaps.Ui `should_classify_tracking_health_by_status`(503) · `should_block_playback_only_when_tracking_store_is_unavailable`(Unavailable) + MapAux 1 | `Expected: Unavailable / Actual: Unknown`; 화면: `Expected: "추적 이력 저장소를 쓸 수 없습니다" / Actual: "해당 기간 데이터 없음"` | 통과 | 증명됨(보강 회차 G13full; 로컬 health 는 200 이라 503 은 가짜 응답으로만) |
| G28 | Devices.Api `should_list_servers_with_basic_view_and_server_config_on_the_axis_contract` | `Expected: "basic" / Actual: "full"` | 통과 | 증명됨(credentials.user_name 은 목록이 쓰지 않음 — 상세가 단건 view=full 로 읽음, 코드 주석에 사유) |
| G42 | Events.Api `should_send_from_event_id_and_user_when_given` · Events.Ui `should_fill_connection_event_actions_from_the_common_list_filtered_by_origin` | `KeyNotFoundException : The given key 'from_event_id' was not present`; `Expected: Loaded / Actual: NotRequested` | 통과 | 증명됨(6.3.2 지원은 서버 태그 소스로 확인) |
| G51 | Events.Api `G51_*` 4개(5행) | `G51-1. 매핑 카메라 POST · PATCH · PUT — 프리셋 0 은 키째 뺀다: Assert.False() Failure / Expected: False / Actual: True` | 통과 | 증명됨 |

'증명 못 함' 묶음: 없음. 단, 전수 실행에서 이 작업과 무관한 기존 · 환경 실패(Utils.Tests 6건)가 남아 sln 종료 코드는 1 이다.
