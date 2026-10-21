# DECISIONS — t-20261021-113151-39a2 (L2)

## 1. Events.Ui 매핑 시험 파일을 어디에 둘까
- options: (a) 기존 `Events.Ui/Tests/` (b) 소유 경로 안 `Events.Ui/Consoles/Mapping/Tests/`
- chosen: (b)
- why: `Events.Ui/Tests/**` 는 동시에 도는 L3 작업 소유다. Events.Ui 는 프로젝트 안 모든 .cs 를 컴파일하므로 (b) 에 둬도 같은 시험 실행에 잡힌다. Messages DTO 시험도 같은 이유로 `Devices.Api/Tests/` 에 뒀다(`Messages/Tests/**` 는 L1 소유).
- effect: 시험 파일 위치만 다르다. 나중에 `Events.Ui/Tests/` 로 옮기면 된다.

## 2. G30 CategoryDetailDto.Servers 를 참조형으로 바꿀까
- options: (a) `Servers` 형식을 `{Id, CategoryServer}` 로 교체 (b) `Servers`(List<ServerDto>) 는 두고 참조형 `ServerReferences` 를 덧붙임
- chosen: (b)
- why: 기존 `Messages/Tests/UnitTest.cs:4044`(L1 소유)가 `Servers` 의 Name · IpAddress 를 쓴다. 바꾸면 남의 시험이 컴파일되지 않는다. 같은 JSON 을 둘 다 읽는다.
- effect: 그 시험을 참조형 기대로 고친 뒤 `Servers` 를 없애면 (a) 가 된다.

## 3. G36 방송 음량 기본값
- options: 100 · 스피커별 기본 음량 · 0
- chosen: 100(설정 `AppSettings.Broadcast.DefaultVolume` 이 없을 때. 범위 밖은 0~100 으로 자름, 0 허용)
- why: 명세상 칸을 빼면 매니저가 100 으로 채운다 → 지금 동작이 바뀌지 않는다. 지시가 재생 창만 말해 TTS 창에는 칸 없이 설정 기본값만 싣는다.
- effect: 설정 값 하나로 바꾼다. 명세 D-2 가 '스피커 기본 음량' 으로 정해지면 그때 바꾼다.

## 4. 새 인터페이스 멤버의 기본 구현 (IDeviceApiService · IDeviceProviderService · IBroadcastControlService)
- options: (a) 멤버만 추가(모든 구현이 따라야 함) (b) 인터페이스 기본 구현('지원하지 않음' 또는 기존 메서드로 넘김)
- chosen: (b)
- why: 기존 시험 가짜 구현 · 호스트 구현이 깨지지 않는다.
- effect: 가짜를 쓰는 시험에서는 그 멤버가 실패를 돌려준다. 필요하면 가짜에 구현을 더한다.

## 5. G18 부품 상태 이름 — 카탈로그와 내장 사전 중 무엇을 먼저 볼까
- options: (a) 지시대로 카탈로그 라벨 먼저 (b) 내장 사전 먼저, 사전에 없는 상태만 카탈로그 라벨
- chosen: (b)
- why: 화면 말투 정본이 내장 사전이다. 카탈로그를 먼저 보면 이미 쓰는 이름이 서버 표현으로 바뀐다(예: RUNNING "구동 중" → "동작 중"). 19종은 모두 내장 사전에 있어, 카탈로그는 앞으로 생길 새 상태에만 쓰인다. ★지시와 다르다.
- effect: `ComponentDisplay.StateName` 에서 두 조회 순서를 바꾸면 (a).

## 6. G18 ActiveStates(가동 칸으로 칠할 상태)에 넣을 것
- options: LINK_UP · CHARGING · DISCHARGING 중 무엇을 넣을까
- chosen: LINK_UP · CHARGING 은 넣고 DISCHARGING 은 뺌
- why: 방전 중은 외부 전원 없이 축전지로 버티는 상태라 가동으로 칠하면 이상이 가려진다.
- effect: `ComponentDisplay.ActiveStates` 목록 한 줄.

## 7. G32 함체째 부대 이동 — 어디에, 실패하면 어떻게
- options: 적용 범위 (a) 한 대 선택 (b) 여러 대 선택까지 / 복원 (a) device_ids 만 복원 (b) 옮긴 장비를 원래 부대로 먼저 되돌린 뒤 device_ids 복원
- chosen: 범위 (a) · 복원 (b)
- why: 여러 대면 장비마다 함체 역조회를 불러야 한다. 장비가 새 부대에 남은 채로 목록만 다시 넣으면 서버가 422 로 거절한다. 4단계는 8.0.6 이상에서만, 6.3 은 지금 경로 그대로.
- effect: 여러 대를 고르면 종전 경로이고, 함체가 얽혀 있으면 서버 422 가 일반 실패 문구로 보인다. `EnclosureUnitMover` 를 여러 대 경로에서 부르면 넓힐 수 있다.

## 8. G17 저장 경고를 붙인 곳
- options: 지시 4곳만 / 모든 쓰기 경로
- chosen: 지시 4곳 — 축 저장(DeviceAxisWriter) · 임계값 창 · config 경로 · 장비 등록(POST: 스피커 SERVER_ID_MISSING · 함체 UNMATCHED_THRESHOLD)
- why: 지시 범위. 수정(PUT/PATCH) 패널 · 결선 적용 경로에는 붙이지 않았다.
- effect: 다른 경로는 `SaveWarningNotice` 한 줄을 부르면 된다.

## 9. G25 콤보에서 보내는 음원 그룹 번호
- options: 목록 행 `id` / 서버 안 `group_id`
- chosen: `id`
- why: 명세의 BROADCAST_PLAY `file_group_id` 가 '파일 그룹 ID' 이고 매니저가 그 값으로 캐시에서 찾는다.
- effect: 매니저가 `group_id` 를 기대하면 `BroadcastFileGroupChoice.From` 한 곳만 바꾼다. 매핑 스피커 행도 같은 `id` 를 쓴다.

## 10. G24 재연결 때 다시 채우기
- options: NATS 재연결 사건 구독 / 기동 1회 + 공개 메서드
- chosen: 기동 1회 + 공개 메서드 `RefreshFromServerAsync`
- why: `INatsService` 의 재연결 사건은 동시에 도는 L3 작업이 만드는 중이라 아직 없다.
- effect: 재연결 사건이 생기면 그 메서드를 부르는 한 줄을 붙인다(후속).

## 11. G37 LRF 표식 — 추적 종료(lost/idle) 때 지울까
- options: (a) 카메라 종료와 함께 지움(전) (b) `lrf-` 표식은 남기고 TTL 스윕에 맡김
- chosen: (b)
- why: 명세가 'GIS 결정 사항' 으로 둔 것이다. 운용자가 잰 표적이 AI 추적 종료와 함께 사라지는 것은 의도에 맞지 않고, 되돌리기 쉽다.
- effect: `TrackingOverlayManager.ExpireCameraAsync` 의 접두사 검사를 빼면 예전 동작.

## 12. G25 매핑 화면에 음원 목록을 잇는 길
- options: 모듈 등록 변경 / 런처 생성자 선택 인자 / 게이트웨이에서 직접 호출
- chosen: `MappingWorkbenchLauncher` 끝에 `IDeviceApiService? deviceApi = null`
- why: `EventUiModule` 은 소유 밖이고, `IDeviceApiService` 는 이미 이름 없이 등록돼 Autofac 이 넣어 준다. 목록을 못 받을 때만 번호 칸을 보인다.
- effect: 장비 서비스가 없으면 번호 입력으로 동작한다.
