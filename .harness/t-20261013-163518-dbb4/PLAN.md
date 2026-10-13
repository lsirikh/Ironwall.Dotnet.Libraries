# PLAN — 운영 이벤트가 이벤트 패널 카드로 나온다

## 목표
OPERATION_EVENT 가 이벤트 패널(오른쪽 카드 목록)에 운영 카드로 뜬다. 결정 사항은 `docs/prds/operation-event-card-prd.md`.

## 진행
- [x] 코드 조사(패널 · 수신 서비스 · 카드 VM · 시험 장치 EventMapRig)
- [x] PRD 작성
- [x] OperationEventCardMessage · OperationEventCardViewModel · OperationEventCardView.xaml
- [x] 비지터 확장(virtual — 외부 파생 안 깨짐) · EventCardKind.Operation/ListKeyOf · 자동화 토막 "Operation"
- [x] 패널: IHandle · 중복 열쇠(KeyOf=ListKeyOf, FindCard 는 KeyOf 비교) · CloseOperationCard/OnButtonCloseCard · 전체 조치보고는 운영 카드를 치우기만
- [x] 수신 서비스: 새 생성자(ea, DeviceProvider) + 카드 메시지 발행, EventUiModule 배선
- [x] 헤드리스 시험 Tests/OperationEventCardScenarioTests.cs(OPC-001~015) + EventMapRig 연결
- [x] Events.Ui 전체 2078 통과 · Utils.Tests 882 통과 · sln 빌드 성공(CameraPopup 시험 통과 확인, 전체 요약은 출력 잘림)

## 남은 일
- 호스트(저장소 밖) 실창 시험 OPC-H01 — PRD §8 목록대로.

## 결정
- 운영 카드는 조치 대상 아님 · [닫기] 로만 치움 · 시간 만료 없음 · 상한 500 공유.
- 신호등 · 셸 배지 · 소리 없음(큐에 넣지 않음). 패널 "이벤트 수" 에는 포함.
- EventCardKind.Of(card) 는 그대로(탐지 · 장애만) — 원격 조치보고 · 가드가 운영 카드를 건드리지 않게. 패널 내부 열쇠(KeyOf)만 "operation" 을 안다.
- 기존 생성자 유지, 새 생성자 추가(공개 형식은 더하기만).

## 발견
- EventMapRig 의 PublishOperationAsync 는 body.id 가 늘 1 — 시험용 선택 인자 eventId 추가.
- Utils.Tests 계약 시험이 전 XAML 을 훑음 → 새 XAML 에 HintAssist.Hint · MaterialDesign 버튼 스타일 금지.
