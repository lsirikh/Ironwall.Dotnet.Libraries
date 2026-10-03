# PLAN — 맵핑에 카메라 프리셋 정보를 넣을 수 없는 문제

## 목표
이벤트 맵핑 워크벤치(Events.Ui/Consoles/Mapping)에서 카메라 배선 행에 타깃 · 홈 프리셋(과 지연)을 지정해 [적용]으로 서버에 보낼 수 있게 한다.

## 원인(증거)
- `MappingBoardRow.TargetPresetId/HomePresetId` 는 서버 응답(`FromDto`)에서만 채워진다 — 값을 쓰는 화면 · 뷰모델 코드가 0곳(grep `TargetPresetId =`).
- `MappingPresetInfo` · `MappingEligibility.PresetToRow` 는 정의만 있고 호출부 0 — 프리셋 후보를 받아 오는 경로가 없다.
- 보드 행 템플릿은 칩(`타깃 —`)을 읽기만 한다. 편집 칸 없음.
- 요청 빌더(`CreateCamera`/`PatchCamera`)와 커밋 계획은 이미 프리셋 id 를 싣는다 → 빠진 것은 입력 경로뿐.

## 결정
- 프리셋 후보는 서버 `GET {Url}/devices/cameras/{id}/presets`(`data.items[]`, CameraPresetDto 와 같은 모양) — `MappingPresetRefDto` 로 읽는다.
- `IMappingWorkbenchGateway` 는 넓히지 않는다(목·페이크 깨짐). 새 인터페이스 `IMappingPresetSource` 를 두고 `MappingWorkbenchGateway` 가 함께 구현. 뷰모델은 선택 인자로 받고, 없으면 게이트웨이를 `as` 로 시도한다(런처 · DI 변경 불필요).
- 편집 UI: 카메라 탭에서 보드 행을 **하나** 고르면 보드 아래에 "카메라 연동 설정" 칸(타깃 프리셋 콤보 · 홈 프리셋 콤보 · 지연 초). 값 변경 = Draft(행 Edited/Added) → 기존 [적용] 경로.
- 콤보 맨 위 "없음"(= null, 프리셋 해제). 현재 값이 목록에 없으면(못 불러옴) 현재 이름으로 자리 항목을 넣어 값이 소리 없이 지워지지 않게 한다.

## 진행
- [x] 원인 규명
- [x] IMappingPresetSource + 게이트웨이 구현
- [x] 뷰모델 편집 상태 · 프리셋 적재
- [x] 뷰 XAML 편집 칸
- [x] 헤드리스 시험(MappingPresetEditTests)
- [x] Events.Ui 매핑 시험(`--filter FullyQualifiedName~Mapping`) 197건 통과(새 8건 포함)
- [ ] 솔루션 전체 `dotnet test` — 1차 하네스: CameraPopup.Tests 1건 실패(2485 통과). 2차(내가 trx 로 재실행): CameraPopup 259/259 통과,
      대신 Utils.Tests `ConsoleChipGroupAutomationTests` 2건이 `TimeoutException: STA 스레드가 끝나지 않았다` 로 실패.
      두 프로젝트 모두 단독 실행에선 전부 통과(CameraPopup 259/259, ChipGroup 3/3) — 전체 병렬 부하에서만 나는 타이밍 실패이고,
      이 작업이 고친 Events.Ui · 매핑 코드와 의존 관계가 없다. 시험은 손대지 않는다(약화 금지).

## 남은 일 / 메모
- 실창(WPF) 확인은 안 했다: 카메라 탭에서 행 하나 선택 → 보드 아래 "… 연동 설정" 칸 → 콤보 · 지연 → [적용].
- 되돌리기 스택(보드 Undo)은 값 편집을 찍지 않는다 — [되돌리기](RevertAsync)는 서버 재조회라 값도 돌아간다.
