# PLAN — 맵핑 결함 G8 · G9 고치기 ("무시하지마")

## 목표
- G8: 실행 중(탐지 → 이벤트 창) 매핑 카메라의 타깃 프리셋이 감시금지구역(`is_restricted_zone`)이어도 그대로 창에 띄우고 그 프리셋으로 PTZ 를 돌렸다. → 제외한다.
- G9: 서버 NATS `SYNC_PRESET`(프리셋 추가 · 수정 · 삭제, 감시금지구역 변경 포함)을 GIS 가 아무도 받지 않았다. → 받는다.

## 원인(증거)
- `EventMappingCameraCache.LoadMappingCamerasAsync` 가 `TargetPreset.IsRestrictedZone` 을 `MappingCameraEntry` 로 옮기지 않음 → `EventWindowPlanning.Order/Select/BuildCamera` 어디에도 판정 없음.
  워크벤치 경고(MappingWarnings:107)는 "일반 팝업·녹화에서 제외됩니다" 라고 약속하고 있음.
- `SYNC_PRESET` 은 `EnumGopCommand` 에 이름만 있고 처리기 0곳(CHANGELOG 818~820 "enum 인식만"). 매핑 카메라 캐시는 프리셋 번호 · 이름 · 금지 여부를 들고 있어 프리셋이 바뀌어도 SYNC_EVENT_MAPPING 이 오기 전엔 옛 값으로 창을 연다.

## 결정
- G8: 타깃 프리셋이 감시금지구역인 배선은 이벤트 창 후보에서 뺀다(`EventWindowPlanning.Order`). 같은 카메라에 금지 아닌 다른 배선이 있으면 그 배선으로 뜬다. 6.3 옛 API 는 플래그가 없어 영향 없음. 뺀 건수는 로그.
- G9: 라이브러리 자가필터 서비스 `PresetSyncNatsService`(Events.Ui/Services, `EventSuppressionSyncNatsService` 와 같은 모양 · 같은 파서 `NatsEnvelopeItems`) →
  `CameraPresetsChangedMessage(Action, ResourceId, CameraId)` 를 EA 로 발행. 메인 솔루션 변경 불필요.
  - `EventWindowTrigger` 가 받아 매핑 카메라 캐시를 비운다(다음 탐지가 새 프리셋 · 금지 여부로 읽음).
  - 열린 매핑 워크벤치는 프리셋 후보 캐시를 버리고(다음 행 선택 때 다시 받음) 바깥 변경 신호를 한 번 준다.
- DI: `EventUiModule` 에 `.As<IService>()` 로 등록(StopAsync 로 구독 해제) + 빌드 콜백에서 StartService.

## 진행
- [x] 원인 규명
- [x] G8 구현(Entry 플래그 · 캐시 · Order · 로그)
- [x] G9 구현(메시지 · 서비스 · 트리거 · 워크벤치 · DI)
- [x] 헤드리스 시험 `MappingRuntimePresetDefectTests` 17건 — Events.Ui 전체 1749건 통과
- [x] CHANGELOG 기록
- [x] 솔루션 `dotnet test` 종료 코드 0 — 시험 프로젝트 10개 모두 통과(Events.Ui 는 솔루션 시험 목록에 없어 따로 돌림: 1749/1749)

## 발견
- 시험 가짜 `CountingGateway.ListCamerasAsync` 가 TargetPreset/HomePreset 을 복사하지 않아 워크벤치 시험이 실패 → 가짜에 두 필드 복사 추가(기존 시험 약화 아님).
- Write 도구로 만든 새 .cs 는 BOM 이 없다 → 첫 줄 Edit 로 U+FEFF 를 넣어 해결(확인: ef bb bf).

## 남은 일 / 메모
- 실창 · 실 NATS 확인은 안 함.
