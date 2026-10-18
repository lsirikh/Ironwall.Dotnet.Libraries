# DECISIONS — 묶음 회전 컨트롤러 판

## 1. 손잡이를 끌다가 놓으면 바로 저장할까
- options: 지금처럼 놓음 = 확정(저장) / 놓음 = 그 각에서 멈추고 판의 [확정] · Enter 로 저장
- chosen: 놓음 = 멈춤(세션 · 판 열린 채), 저장은 [확정] · Enter(또는 선택 바꾸기 · 다른 키 · 이동 시작 = 기존 규칙대로 먼저 확정)
- why: 사용자 요청이 "어디를 중심으로 몇 도 돌았는지 컨펌" — 놓자마자 저장되면 판이 뜨자마자 닫혀 확인할 수 없다. 키보드 회전과 같은 흐름이 된다
- effect: `GroupSelectionService.OnAdornerRotationDragFinished` 의 `if (commit) return;`. 예전 동작으로 돌리려면 그 줄을 `EndRotation(commit: true)` 로. 기존 시험 `GroupRotationScenarioTests` 의 손잡이 시험 1건은 이 뜻에 맞게 "놓음 → 아직 저장 없음 → [확정] → 저장" 으로 고쳤다(이름도 `..._save_only_after_confirm_...`)

## 2. 열린 세션에서 손잡이를 다시 끌면 / 끄는 중 캡처를 잃으면
- options: 매 끌기를 0° 에서 / 지금 각에서 이어서
- chosen: 지금 각에서 이어 돈다. 캡처를 잃으면 이 끌기만 되돌린다(이 끌기로 세션을 연 경우만 예전처럼 세션 전체 취소 · 원위치)
- why: 판에서 30° 넣고 손잡이로 조금 더 돌리는 흐름이 자연스럽고, drag-first-ux 의 "LostMouseCapture = 취소" 를 그 끌기 단위로 지킨다
- effect: `GroupSelectionService._dragBaseAngle · _dragOpenedSession`

## 3. 판 닫기(✕)
- options: 닫기 = 확정 / 닫기 = 취소
- chosen: 닫기 = 취소(원위치, 저장 없음) — 목표 명시. 선택이 바뀌거나 다른 키 · 이동 시작 · Undo 단추는 기존 DECISIONS(t-20261018-211954-fd35 #3 · #4)대로 먼저 확정
- why: 목표 2항. 판에서 명시적으로 닫는 것은 "그만두기" 로 읽힌다
- effect: `MapViewModel.GroupRotationPanel.CloseCommand = CancelGroupRotationCommand`

## 4. 판 모양 · 자리 — 어느 기존 창을 따를까
- options: 3D 만들기 판처럼 지도 가운데 고정(MapFloatingPanelStyle, 머리 띠 없음) / 사이트 고정 창처럼 OverlayWindow 패턴(Control + Themes 스타일 + PrimaryBrush 머리 띠 끌기 + ✕ + "?" 받침)
- chosen: OverlayWindow 패턴(`GroupRotationPanelControl` + `Themes/GroupRotationPanelStyle.xaml`, `PropertyPanelCanvas` 안 ContentPresenter, 처음 자리 Left 620 · Top 100)
- why: 사용자 추가 지시(머리 띠 제목 · 닫기 · "?" · 끌어 옮기기 구조를 기존 오버레이 창과 같게). 회전 판은 묶음 위에 떠 있어 가리면 옮길 수 있어야 한다. 지도 가운데 고정은 돌리는 묶음을 가린다
- effect: 자리 = `MapView.xaml` 의 ContentPresenter `Canvas.Left/Top`. 위치 기억은 하지 않음(다른 겹창처럼 실행 중에만)

## 5. 회전 중심 표시 형식
- options: 좌표 칩 컨트롤(MapCoordinateControl)을 판 안에 그대로 / WGS-84 는 입력칸, MGRS · UTM 은 칩과 같은 글꼴의 줄
- chosen: WGS-84 위도 · 경도 = 입력칸(칩과 같은 소수 8자리, 늘 보임 — 입력해야 하므로), MGRS · UTM 줄은 좌표 설정(IsShowMGRS · IsShowUTM)이 켜졌을 때만, 글은 칩과 같은 CoordinateSharp
- why: 칩 컨트롤은 자체가 MapFloatingPanelStyle(그림자 · 테두리) 판이라 판 안에 겹 판이 된다. 입력칸이 WGS-84 표시를 겸한다
- effect: `GroupRotationPanelStyle.xaml` 중심 구역. 확정 한 줄의 중심 글은 설정 순서(WGS-84 → MGRS → UTM) 첫 켜진 것(`GroupRotationPanelRules.CenterText`)

## 6. 「지도에서 고르기」 클릭을 어디서 받을까
- options: 지도 컨트롤에 새 독점 모드(IExclusiveInputModeSource 에 멤버 추가) / 그룹 어도너가 고르는 동안 지도 전체 눌림을 받기
- chosen: 그룹 어도너(HitTestCore 가 고르는 동안 전체 hit, PreviewMouseDown 에서 위경도로 알림). Esc = 고르기만 그만
- why: 공개 인터페이스 멤버 추가는 시험 가짜 구현을 깬다(공개 형식은 더하기만). 어도너는 AdornerDecorator 안(지도만 감쌈)이라 위에 뜬 판 · 도구줄 클릭은 그대로 판이 받는다. 선택된 심볼 위를 눌러도 중심으로 잡힌다
- effect: `GroupSelectionAdorner._pickingPivot` · `SetPivotPicking`

## 7. 판에 포커스가 있을 때 키
- options: 지도 키 처리 그대로(막대 ←/→ 가 묶음 이동 · "다른 키 = 확정" 으로 샘) / 판 안 포커스면 판 컨트롤이 키를 받고 Esc 만 회전 취소
- chosen: 판 안 포커스면 지도 키 처리기를 건너뛴다(Esc 제외). 입력칸은 원래대로(TextBox 포커스는 이미 건너뜀) + 입력칸 Enter = 값 넣기
- why: 막대를 키로 미세 조정하다 회전이 확정되거나 묶음이 움직이면 안 된다
- effect: `MapViewModel.OnMapPreviewKeyDownForGroup` 첫머리 판 포커스 검사, `GroupRotationPanelControl.OnPreviewKeyDown`

## 8. 「회전」 단추 자리
- options: 편집 도구줄 「선택」 묶음 / 지도 위 선택 칩(없음 — 새로 만들어야 함)
- chosen: 편집 도구줄 「선택」 묶음(선택 해제 · 삭제 옆), 2개 이상 고르면 켜짐(`StartGroupRotationCommand` CanExecute)
- why: 선택 개수 칩은 지금 없고(선택 수는 잠깐 뜨는 안내 띠뿐) 새 칩은 범위 밖. 선택 관련 명령이 모인 곳
- effect: `MapView.xaml` `Map.GroupRotation.Start`
