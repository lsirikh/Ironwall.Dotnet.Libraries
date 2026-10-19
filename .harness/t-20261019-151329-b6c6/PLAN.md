# 장비 1개 = 마커 1개 (연결 장비 재할당 확인) — PLAN

## 목표
속성 판 「연결디바이스」에서 다른 마커(A)에 이미 연결된 장비를 마커 B 에 고르면 확인 팝업 →
예: A 연결 비움 + B 연결(둘 다 DB 저장, 실패 시 둘 다 원상태) · Undo 1단위 / 아니오: B 원래 값(콤보 포함).
기존 중복 자료는 불러올 때 깨지지 않게(첫 마커 유효) + 로그 경고 + 속성 판 표시. 콤보 항목에 '· 정문1 에 할당됨'.

## 근거("모델" = 연결 장비)
- ModelVariant 는 모양 프리셋 3종(고정형/돔형/PTZ) — 여러 마커가 같은 값을 갖는 게 정상이고 "널로" 비울 대상이 아님.
- 연결 장비는 이벤트 조회표(SymbolLifecycleCoordinator `_devices[(Id, DeviceType)]`)가 장비당 심볼 1개만 가짐 →
  중복이면 마지막에 등록된 마커만 색칠 · 나머지는 이벤트를 못 받음. 부팅 재바인딩(InitializeDeviceSymbolIntegration)은
  FirstOrDefault 로 첫 마커만 LinkedDevice 를 다시 붙여, 두 번째 마커는 LinkedDeviceId 만 있고 객체가 없어 카메라 팝업 ·
  현재위치 적용 · 문 제어가 죽음. 3D 창(Map3DWindows: FirstOrDefault(LinkedDeviceId==CameraId)) · 스피커 등도 첫 마커만 고름.

## LinkedDevice 를 바꾸는 경로(전수)
| 경로 | 처리 |
|---|---|
| 속성 판 콤보(단일) → GMapPropertyPidsControl.OnLinkedDeviceChanged → MapViewModel.OnMarkerPropertyChanged | 충돌 검사 · 확인 팝업(이번 작업) |
| 다중 선택 속성 판 | 이미 막힘(IsGroupMode return + 콤보 비활성 · 툴팁 "그룹 편집에서는 디바이스 연결을 변경할 수 없습니다") — 시험으로 고정 |
| 복사/붙여넣기 · 복제(ApplyCopyTransform) | 이미 LinkedDeviceId=0 — 시험으로 고정 |
| 「현재위치 적용」 | 연결을 바꾸지 않음(장비 좌표 저장만) |
| 부팅 재바인딩 InitializeDeviceSymbolIntegration · MarkerFactory | 첫 마커만 유효(기존 동작) + 중복 경고 로그(이번 작업) |
| Undo/Redo(PropertyChangeCommand) | LinkedDevice 를 replay 가능 속성에 추가 + 조회표 재등록 |
| 새 심볼 생성(LinkedDeviceId=0) | 해당 없음 |

## 진행
- [x] 코드 조사 · 경로 전수
- [x] VM: 충돌 검사 → 즉시 되돌림 → 확인 팝업 → 확인 콜백에서 저장 두 건 · 원복 · Undo 배치 · 조회표 재등록
- [x] Undo: UndoableCommandBase 에 LinkedDevice 추가, ApplyMarkerUpdateAsync 에서 조회표 재등록
- [x] 패널: DeviceAssignmentNotes(콤보 항목 글) · LinkedDeviceSharedWarning(중복 경고) DP + XAML + 변환기(Utils/DeviceAssignmentNoteConverter.cs)
- [x] 부팅 중복 경고 로그(InitializeDeviceSymbolIntegration "[장비 중복 연결]")
- [x] 시험(tests/GMaps.MapEdit.Tests/DeviceReassignScenarioTests.cs, 16건) — 확인 분기를 끄면 12건 실패 · 켜면 16건 통과 확인
- [x] dotnet test Ironwall.Dotnet.Libraries.sln 통과 · GMaps.MapEdit(405) · PropertyPanel(314) · Ui(1501) 통과

## 남은 일 / 메모
- 확인 팝업 인프라는 취소 콜백이 없음 → 고르는 즉시 B 를 원래 값으로 되돌리고 "확인" 때만 적용(DECISIONS 2).
- 중복 경고 DP 이름은 `...Note` 로 끝나면 HelpContractTests(설명형 문구 금지)가 잡는다 → 상태 경고라 `LinkedDeviceSharedWarning`.
- 참고: tests/GMaps.MapAux.Tests 의 OverlayHelpPlaybackScenarioTests 1건은 `MapAnchorPanelControl._isDragging` 필드 부재로 실패 — 이 작업과 무관한 기존 상태(동시 작업 영역).
