# PLAN — 원격지 설치 준비: 라이브러리 몫 (t-20261011-221423-6b8f)

## 목표
1. 옛 통합 시험(Devices.Api · Events.Api)을 HTTPS 로컬 서버 + 전용 주입 계정으로 돌릴 수 있게 하고, 환경이 없으면 **건너뜀**(실패 아님), 원격 운영 주소면 **쓰기 시험 즉시 건너뜀**.
2. HTTPS 인증서 신뢰 실패 · 연결 거부 · 시간 초과 · 이름 해석 실패를 구분해 결과 메시지/코드에 남기고, 로그인 실패 문구 · SSO 교환 결과까지 닿는지 헤드리스 시험.
3. Sounds 시험 4건의 순서 의존(공유 SetupModel) 격리 + 가짜 재생기로 같은 판정을 헤드리스로.
4. `dotnet test Ironwall.Dotnet.Libraries.sln` + 손댄 sln 밖 프로젝트 실패 0.

## 진행
- [x] 기준선 빌드(sln) 성공 확인
- [x] 1. 공용 라이브 도우미(`tests/Shared/LiveApi/*` 링크) + Devices.Api · Events.Api 시험 6파일 변환(하위 에이전트) + 안전 문 오프라인 시험
- [x] 2. `ApiTransportFailure` 분류 + `BuildExceptionResponse` 봉투 + 게이트웨이 · 로그인 문구 · 프로브 로그 + 시험(빨강 10 → 초록, 로그인 문구 빨강 6 → 초록)
- [x] 3. Sounds: 재생기 교체 자리(internal 생성자) + 시험별 격리 서비스 + 공용 판정 + 가짜 재생기 시험 5건(통과)
- [x] 4. 전체 시험 실행 · 결과 기록 (2026-10-11)
  - `dotnet test Ironwall.Dotnet.Libraries.sln` exit 0 — 실패 0(Accounts.Api 257 · Messages 421 · Utils.Tests 881 · Reports.Ui 365 · CameraPopup.Tests 286 · CameraPopup.Host.Tests 161 · Monitoring.Models 163 · Sso 91 · SystemResources 22 · Watchdog.Tests 4)
  - sln 밖: Devices.Api 통과 206 · 건너뜀 104 · 실패 0 / Events.Api 통과 5 · 건너뜀 44 · 실패 0 (라이브 환경 변수 비움 `-e IRONWALL_E2E_CRED_FILE= -e IRONWALL_LIVE_API_URL=`)
  - 계정 파일 있음 + 서버 안 닿음(`https://127.0.0.1:1/api`) → 라이브 시험 전부 "서버에 닿지 않습니다(127.0.0.1:1)" 로 건너뜀 확인
  - Accounts.Ui.Tests 406 통과 · Sounds 가짜 재생기 시험 5건 3회 반복 통과(실제 소리 시험은 실행 안 함) · Sounds.Ui 빌드 오류 0

## 결정
- **D1 동적 건너뜀 방식**: 저장소 관용구는 `Xunit.SkippableFact`(tools/dummy-cameras 시험)이다. 그러나 Devices.Api · Events.Api 는
  시험이 **출하 DLL 안에** 들어 있는 라이브러리라 새 패키지를 넣으면 GIS 설치본에 DLL 이 하나 더 따라간다
  (project_shipping_dlls_reference_xunit 의 교훈). 그래서 `FactAttribute` 파생(`LiveApiFact` · `LiveApiWriteFact`)이
  **실행 때마다** 환경을 보고 `Skip` 을 채우는 방식으로 같은 효과를 낸다(새 의존 0). xunit 2 는 발견 단계에서 특성 인스턴스의 `Skip` 을 읽는다.
- **D2 공유 소스**: 도우미는 `tests/Shared/LiveApi/` 에 두고 두 csproj 가 `<Compile Include … Link>` 로 링크(각 어셈블리에 internal 로 컴파일).
  도메인 모듈 등록만 어셈블리별 partial(`LiveApiSession.Devices.cs` · `LiveApiSession.Events.cs`).
- **D3 로그인 1회**: 서버가 단일 세션 축출(evict)이라 시험마다 로그인하면 병렬 실행에서 서로 쫓아낸다 → 어셈블리당 컨테이너 1개 · 로그인 1회 공유.
  로그인 실패는 기억해 두고 재시도하지 않는다(잠금 정책 보호). 세션은 앱처럼 서버 계약 프로브(openapi.json)를 먼저 확보한다.
- **D4 원격 안전 문**: 쓰기 시험은 주소 호스트가 `localhost` · loopback · RFC1918(10/8 · 172.16/12 · 192.168/16) **IP 리터럴**일 때만 허용.
  그 밖(원격 공인 IP · 임의 호스트 이름)은 네트워크에 닿기 **전에** 건너뜀(안전 문을 계정 · 닿음 확인보다 먼저 본다).
  서버 닿음 확인은 HTTP 가 아니라 TCP 연결만(2초) — 발견 단계에서 프로세스당 1회.
- **D5 연결 실패 분류**: 상위 `error.code` 는 종전과 같게 둔다(503 `SERVICE_UNAVAILABLE` · 504 `GATEWAY_TIMEOUT`) — 기존 분기(`IsServerUnavailable` · 편제 문구 등) 무회귀.
  세부 사유는 저장소의 안정 sub-code 자리 `error.details.error_code`(= `ApiError.DetailsErrorCode`)와 `error.details.reason`(SSO 교환 결과가 읽는 자리)에 싣는다:
  `CERT_UNTRUSTED` · `CONNECTION_REFUSED` · `CONNECT_TIMEOUT`(504) · `NAME_RESOLUTION_FAILED` · `CONNECTION_FAILED` · `REQUEST_TIMEOUT`(504).
  사람용 문구는 `error.message` 에 실어 `ApiErrorTextHelper` 등 기존 문구 경로가 그대로 쓴다. 서버 어휘 상수(`ApiErrorCodes`)에는 넣지 않았다(어휘 시험 19종 보존) — 클라 로컬 코드는 `Api.Services.ApiTransportFailure` 에.
  인증서 검증 우회는 넣지 않았고, 출하 라이브러리(`Ironwall.Dotnet.*`)에 우회 코드가 없음을 시험으로 고정(DEP-215, `tools\` 의 개발용 왕복 하네스는 설치본 밖이라 범위 밖).
- **D6 로그인 문구**: `ApiAccountGateway` 가 sub-code 가 연결 실패면 `AuthOutcome.ErrorCode` 를 sub-code 로 넘기고, `LoginPanelViewModel.FailMessage`(private → internal, 시험용 IVT)가
  `ApiTransportFailure.Describe` 문구를 보인다. 종전 상위 코드(UNAUTHORIZED · SERVICE_UNAVAILABLE · GATEWAY_TIMEOUT) 문구는 그대로.
- **D7 Sounds 격리 방식**: 공유 픽스처 SetupModel 을 시험마다 바꾸고 되돌리는 대신, 시험마다 **자기 SoundService · 자기 설정 · 자기 소리 목록**을 만든다(`IsolatedSoundService`).
  역할(탐지 · 장애 · 조치)마다 다른 파일을 쓴다 — 같은 파일이면 SoundModel 하나를 나눠 써 재생 상태 판정이 섞인다(실제 소리 시험은 파일 3개 이상 필요, 부족하면 명시적 실패 문구).
- **D8 재생기 교체 자리**: `SoundService` 에 internal 생성자(출력 장치 · 읽기기 팩터리)를 추가. Autofac 은 공개 생성자만 보므로 앱 배선 무변경. 읽기기 형식을 `AudioFileReader` → 기반형 `WaveStream` 으로 넓힘(동작 동일).

## 발견
- 연결 실패는 `ApiService.BuildExceptionResponse` 가 503 + `ReasonPhrase = ex.Message` 로 만들고 본문이 비어
  `ApiMessageHelper` 폴백이 "HTTP 503: An error occurred while sending the request." 로 뭉갰다. 로그도 바깥 메시지("see inner exception")뿐이었다 → 분류 코드 + 안쪽 메시지로 남김.
- Devices.Api · Events.Api · Sounds · Accounts.Ui(.Tests) 는 sln 밖 프로젝트(시험이 프로젝트 안 Tests 폴더).
- **Sounds 옛 시험 4건은 설정 공유만이 아니라 API 와도 어긋나 있었다**: `…SoundPlayAsync` 는 큐에 넣고 곧바로 끝나는데(큐 설계),
  옛 시험은 그 Task 를 재생 끝으로 보고 `await` 뒤 `IsPlaying == false` · `OperationCanceledException` 을 단언했다 → 설정과 무관하게도 깨질 수 있었다.
  또 안전 취소 시간 = 지속 시간이라 자동 멈춤과 경주했다. 새 판정(`SoundPlaybackScenarios`)은 재생 시작 · 스스로 멈춤(지속 시간 이상 · 안전 취소 전) · 완료 사건,
  수동 멈춤은 지속 시간이 지나도 계속 울림 → 취소 뒤 멈춤 · 완료 사건 없음, 동시 재생 방지는 두 번째 요청이 큐에서 대기 · 관찰 내내 동시 울림 0(가짜는 출력 장치 수준 최대 1)로 본다.
- 자동 멈춤은 지속 시간이 지난 뒤 **지금 도는 한 바퀴가 끝날 때** 멈춘다(긴 소리 파일이면 지속 시간보다 길게 울림) — 이번 범위 밖, 기록만.
- 셸 제약: `Remove-Item` 이 승인 필요로 거부돼, 건너뜀 확인용으로 만든 가짜 계정 파일 `.harness/t-20261011-221423-6b8f/dummy-cred.txt`(값은 가짜 문자열)를 지우지 못했다.

## 남은 일
- (사람) 전용 계정 파일을 주고 로컬 HTTPS 서버에 라이브 시험 실행:
  `dotnet test Ironwall.Dotnet.Libraries.Devices.Api/Ironwall.Dotnet.Libraries.Devices.Api.csproj -e IRONWALL_E2E_CRED_FILE=<파일> [-e IRONWALL_LIVE_API_URL=https://localhost:8000/api]` (Events.Api 도 같게)
- (사람) 소리가 나도 되는 PC 에서 Sounds 실제 재생 시험(C:\Sample 에 소리 파일 3개 이상): `dotnet test Ironwall.Dotnet.Libraries.Sounds/Ironwall.Dotnet.Libraries.Sounds.csproj --filter "FullyQualifiedName~SoundService_PlaybackTests"`

## 시나리오 id 표
분류: (가) 이 작업에서 헤드리스로 실행 · 통과 확인 / (나) 빌드 · 건너뜀 동작만 확인 — 라이브 서버 · 전용 계정이 있어야 실행(사람이 실행) / (다) 실제 소리가 나 이 작업에서 실행 금지 — 빌드만 확인(사람이 실행)

| id | 보증 | 시험 | 분류 |
|---|---|---|---|
| DEP-201 | 장비 CRUD · 목록 · 필터 · 프리셋/ROI/포인트 통합(HTTPS + 전용 계정, 쓰기는 사설망만) | Devices.Api `DeviceApiIntegrationTest.cs` 8클래스(쓰기 20 · 읽기 22) | (나) |
| DEP-202 | 서버 카테고리 · 인스턴스 · 메트릭 통합 | Devices.Api `ServerApiIntegrationTest.cs` 4클래스(쓰기 8 · 읽기 6) | (나) |
| DEP-203 | ServerApiService 카테고리 · 서버 · 메트릭 · 프록시 | Devices.Api `ServerApiServiceTests`(쓰기 15 · 읽기 5) | (나) |
| DEP-204 | DeviceApiService 조회 · 생성 · 수정 · 대량 생성(라이브) + 모듈 등록 · 설정 모델(오프라인 5) | Devices.Api `DeviceApiServiceTests` | 오프라인 5건 (가) · 라이브 23건 (나) |
| DEP-205 | 함체 메트릭 기록 · 이력 · 최신 · 삭제 | Devices.Api `EnclosureMetricsTests`(쓰기 4 · 읽기 1) | (나) |
| DEP-206 | 탐지 · 장애 · 연결 · 조치 · 관계 · 매핑 이벤트 통합 | Events.Api `EventApiIntegrationTest.cs` 6클래스(쓰기 12 · 읽기 8) | (나) |
| DEP-207 | EventApiService 조회 · 생성 · 수정 · 삭제(라이브) + 오프라인 5 | Events.Api `EventApiServiceTests` | 오프라인 5건 (가) · 라이브 24건 (나) |
| DEP-208 | 원격 쓰기 안전 문(loopback · RFC1918 만 쓰기, 원격 123.141.236.253 · 호스트 이름은 네트워크 전 차단) · 기본 주소 HTTPS · 설정에 계정 미탑재 | Devices.Api `LiveApiTargetGuardTests` | (가) |
| DEP-211 | 인증서 신뢰 실패 → 503 유지 + `CERT_UNTRUSTED` + "서버 인증서를 신뢰할 수 없습니다 … 신뢰 저장소" 문구(뭉갠 503 문구 아님) | Accounts.Api `ApiTransportFailureTests.should_tell_certificate_is_untrusted_when_tls_handshake_fails` | (가) |
| DEP-212 | 연결 거부 · 이름 해석(소켓 · HttpRequestError) · 연결 시간 초과(504) · 요청 시간 초과(504) · 분류 불가 · 인증서 콜백 거부 분류 + 상위 분기 무회귀 | Accounts.Api `ApiTransportFailureTests.should_classify_transport_failure_into_code_and_message`(7) | (가) |
| DEP-213 | 로그인 경로(ApiService → AccountApiService → ApiAccountGateway)가 `CERT_UNTRUSTED` · 인증서 문구를 AuthOutcome 에 싣는다 | Accounts.Api `ApiTransportFailureTests.should_carry_certificate_reason_into_login_outcome_when_login_cannot_reach_server` | (가) |
| DEP-214 | SSO 교환 결과에 인증서 문구 · `Reason=CERT_UNTRUSTED`, 재시도 불가 판정 종전대로 | Accounts.Api `ApiTransportFailureTests.should_carry_certificate_reason_into_sso_exchange_result` | (가) |
| DEP-215 | 출하 라이브러리에 인증서 검증 우회 코드 0 | Accounts.Api `ApiTransportFailureTests.should_not_contain_certificate_validation_bypass_anywhere` | (가) |
| DEP-216 | 로그인 화면 문구: 연결 실패 6종이 원인 + 다음 행동으로, "비밀번호" 오진 없음 · 종전 코드 문구 무회귀 | Accounts.Ui.Tests `LoginFailMessageTransportTests`(9) | (가) |
| DEP-221 | 탐지 자동 멈춤(실제 소리) — 시험 자기 설정 | Sounds `SoundService_PlaybackTests.Detection_Sound_Playback_AutoStop_Works` | (다) |
| DEP-222 | 장애 수동 멈춤(실제 소리) — 시험 자기 설정 | Sounds `…Malfunction_Sound_Playback_ManualStop_Works` | (다) |
| DEP-223 | 동시 재생 방지(실제 소리) — 시험 자기 설정 | Sounds `…Concurrent_Playback_Prevention_Works` | (다) |
| DEP-224 | 조치 보고 자동 멈춤(실제 소리) — 시험 자기 설정 | Sounds `…ActionReport_Sound_Playback_Works` | (다) |
| DEP-225 | DEP-221 과 같은 판정(가짜 재생기) | Sounds `SoundServiceFakePlayerTests.should_auto_stop_detection_after_duration_with_fake_player` | (가) |
| DEP-226 | DEP-222 와 같은 판정(가짜 재생기) | Sounds `…should_keep_malfunction_playing_until_cancelled_with_fake_player` | (가) |
| DEP-227 | DEP-223 과 같은 판정 + 출력 장치 수준 동시 울림 최대 1(가짜 재생기) | Sounds `…should_never_play_two_sounds_at_once_with_fake_player` | (가) |
| DEP-228 | DEP-224 와 같은 판정(가짜 재생기) | Sounds `…should_auto_stop_action_report_after_duration_with_fake_player` | (가) |
| DEP-229 | 격리 서비스끼리 설정 · 소리 목록을 나누지 않는다 | Sounds `…should_not_share_setup_between_isolated_services` | (가) |
