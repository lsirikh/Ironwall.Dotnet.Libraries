# PLAN — SYNC_DETECTION DELETED 처리

## 목표
서버에서 탐지가 지워졌다는 SYNC_DETECTION{DELETED}(all.sync.detection)를 받으면 로그만 남기던 것을
→ 카드 닫기 · 큐 엔트리 해제(상단 신호등 노랑 건수 감소) · "서버에서 삭제됨" 토스트 잠깐 표시.

## 진행
- [x] `Events.Ui/Models/DetectionDeletedOnServerMessage.cs` — `DetectionDeletedOnServerMessage(EventId, EventType?)` · `EventDeletedOnServerNotice(Kind, EventId).Text`
- [x] `DetectionSyncNatsService` — DELETED 수신 시 큐 엔트리 유형을 찾아 메시지를 UI 스레드로 발행(큐에 없어도 발행 — 카드만 남은 경우 대비).
      진행 중 UPDATED 재조회 응답은 `_latestRequest` 항목 제거로 폐기. 발행 코드는 `PublishOnUiAsync` 로 공용화.
- [x] `EventCardListPanelViewModel` — `IHandle<DetectionDeletedOnServerMessage>`: 원격 조치보고와 같은 길(`CloseRemotely`)로 카드 · 엔트리 해제,
      실제로 닫았으면(CardClosed / QueueEntryCleared) `EventDeletedOnServerNotice` 발행.
- [x] `RemoteActionReportedMessage` 에 `DeletedOnServer` (기본 false) 추가 —
      조치보고 창: 보내는 중이어도 즉시 [확인] 끔 + "서버에서 삭제된 이벤트입니다…" 안내.
      대시보드: 서버 삭제는 '조치됨' 으로 표시하지 않음.
- [x] `MapViewModel.ServerDeletion.cs` — 알림을 받아 기존 지도 하단 토스트(5초)에 표시. 토스트 아이콘을 바인딩으로 바꿈(`CameraPopupToastIcon`, 기본 Cctv · 삭제는 DeleteOutline).
- [x] 시험 `Events.Ui/Tests/DetectionServerDeletionTests.cs` (서비스 4 · 패널/창 4)
- [ ] 전체 `dotnet test Ironwall.Dotnet.Libraries.sln` (하네스가 확인)

## 결정
- 토스트 자리: 새 UI 를 만들지 않고 지도 하단 기존 토스트(카메라 팝업 결과용) 재사용.
- 닫을 것이 없으면(이미 닫힘 · 모르는 탐지) 토스트를 띄우지 않는다.
- 탐지만 대상(장애 동일 번호 카드는 건드리지 않음 — 종류 + 번호 매칭).
- 서버 삭제도 `ActionReported` 이벤트를 발화 → 카메라 팝업 이벤트 창도 닫힌다(지워진 이벤트이므로 의도).

## 발견
- 새 파일 3개에 UTF-8 BOM 을 붙이려던 PowerShell 명령이 권한 거부됨(하위식 사용). 컴파일 · 시험엔 영향 없음.
