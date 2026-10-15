# PLAN — 이슈 #7 G7-4 제어 명령 공통 선택 칸(requested_by · requested_client_id)

## 목표
GIS 가 보내는 제어 명령 body(방송 BROADCAST_* · TTS · 경광등 LAMP_* · PTZ/카메라 제어 · §8.3 설정 · 문 개폐 GATE_DOOR_SET/ENCLOSURE_DOOR_SET)에
`requested_by`(로그인 login_id) · `requested_client_id`(통제DB 세션 client_id = X-Client-Id = `ClientIdentity.Current`)를 선택 칸으로 싣는다.
값이 없으면 직렬화에서 뺀다. 다른 키는 그대로. 근거: 브로커 연동설계 v2.0.12 §7 머리(첨부).

## 진행
- [x] `Messages/Dto/Brokers/ControlCommandBodyDto.cs` 신설(두 칸, `NullValueHandling.Ignore`)
- [x] 기반 상속 22종: BroadcastPlay/Stop/Test/Mic · Tts · LampClear/Off/ColorSet/BuzzerSet/ColorTest/BuzzerTest · PtzControl · §8.3(CameraModeSet · DayNightSet · FanSet · HeadlightSet · HeaterSet · PaletteSet · PowerSet · TrackingSet · WeatherModeSet · WiperSet)
- [x] `DoorSetBodyDto` · `CameraAimLocationBodyDto` 에 `requested_client_id` 직접 추가(이미 requested_by 를 가진 DTO)
- [x] `GMaps.Ui/Services/Brokers/ControlCommandRequester.cs` — LoginId(IPermissionService) · ClientId()(ClientIdentity.Current) · Stamp
- [x] BroadcastControlService · LampControlService: 선택 생성자 인자 `IPermissionService? permission = null`(Autofac 기본값 처리) 로 Stamp
- [x] DoorControlService · CameraAimControlService: requested_client_id 채움
- [x] MapViewModel.CurrentRequestedBy: 표시이름 → login_id(IPermissionService.LoginId → JWT sub 폴백), 없으면 null(Windows 사용자명 폴백 제거)
- [x] 시험: `Messages/Tests/ControlCommandRequesterFieldsTests.cs`(DTO 전 종류 직렬화) · `GMaps.Ui/Tests/ControlCommandRequesterTests.cs`(서비스 경로)
- [x] 기존 시험 기대값 갱신: `tests/GMaps.MapEdit.Tests` 문 개폐 requested_by "시험관리자"(표시이름) → login_id
- [x] 부분 시험 통과: Messages(83건, 새 DTO 직렬화 포함) · GMaps.Ui 제어 명령 관련(64건) · MapEdit 시험 프로젝트 빌드(창을 띄울 수 있어 실행 안 함, sln 밖)
- [x] `dotnet test Ironwall.Dotnet.Libraries.sln` 전체 통과(실패 0) · GMaps.Ui(sln 밖) 제어 명령 시험 73건 통과
- [x] DECISIONS.md 작성
- 새 파일 4개는 UTF-8 BOM 으로 저장(프로젝트 규칙)

## 결정
- requested_by = login_id. 기존 회전요청 · 문 개폐 · 카메라 팝업은 표시이름(user.name)을 우선했으나 목표가 "운영자 계정(login_id) · 같은 값"이라 login_id 로 바꿨다. 이 때문에 MapEdit 시나리오 시험의 기대값("시험관리자")이 명세상 틀린 값이 되어 login_id 로 고쳤다(시험 약화 아님).
- 미로그인이면 requested_by 를 싣지 않는다(Windows 사용자명은 운영자 계정이 아님). 단 PTZ_AIM_LOCATION 은 기존 계약대로 빈 문자열을 싣는다(기존 시험 `should_default_requested_by_to_empty_when_null` 유지).
- requested_client_id 는 `ClientIdentity.Current`(= ApiService 가 X-Client-Id 로 보내는 값, 호출 순서 안전). SSO OIDC client_id(gis-monitoring) 미사용.
- 카메라 팝업(CAMERA_POPUP_OPEN, §11.5.9)은 requested_by 값만 login_id 로 맞추고 requested_client_id 는 추가하지 않았다 — 같은 값이 이미 `target_client_id` 로 실리고, 목표의 명령 목록에 팝업은 "requested_by 정렬" 대상으로만 나온다. 기존 키 목록 시험도 그대로 유지.
- MODE_CHANGE · WINDY(PidsProxy)는 목표 목록에 없어 제외. 보고 · 상태(PTZ_STATUS 등)는 명세상 비대상.
- 기반 클래스 속성은 Newtonsoft 가 파생 속성 뒤에 직렬화 → 기존 키 순서 불변(시험으로 고정).

## 남은 일
- 시험 실행 결과 확인 후 보고.
