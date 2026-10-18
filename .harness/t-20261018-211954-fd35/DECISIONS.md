# DECISIONS — 선택 묶음 회전

## 1. 회전 중심을 무엇으로 잡을까
- options: 경계 상자 중심 / 무게중심(평균)
- chosen: 경계 상자 중심 — 멤버 위치(선/구역은 꼭짓점 전부)를 웹 메르카토르 평면에 놓고 그 상자의 가운데
- why: 화면에 보이는 "묶음 가운데" 와 맞고, 한쪽에 심볼이 몰려 있어도 중심이 쏠리지 않는다(오버레이 이미지에 맞출 때 예측 가능)
- effect: `GroupRotationSession` 생성자의 `GroupRotationMath.BoundsCenter(...)` 한 줄. 무게중심으로 바꾸려면 그 함수만 평균으로 교체

## 2. 회전 결과 저장 방식
- options: Draft/Temp-state 에 모았다가 [저장] / 기존 그룹 이동과 같이 확정 즉시 멤버별 DbUpdateProcess
- chosen: 기존 그룹 이동(`OnGroupMoveCompleted`)과 같은 경로 — 확정(놓음 · Enter) 즉시 멤버별 `DbUpdateProcess` + Undo 1단위(`그룹 회전 N개`)
- why: 목표가 "회전 결과가 서버에 반영되는 방식은 기존 이동과 같게" — 지금 지도 편집의 이동은 Draft 를 거치지 않고 바로 저장한다. 확정 전(드래그 중 · 키보드로 돌리는 중)은 미리보기만 하고 저장하지 않는다
- effect: `MapViewModel.OnGroupRotateCompleted`. Draft 경로가 생기면 이 메서드의 `DbUpdateProcess` 루프만 바꾸면 된다

## 3. 묶음 이동을 새로 만들까
- options: 회전 손잡이 체계로 이동 손잡이도 새로 / 기존 묶음 이동(선택한 것 아무거나 끌기 · 방향키) 재사용
- chosen: 기존 묶음 이동 재사용, 새 이동 손잡이 없음
- why: 묶음 이동은 이미 있고(드래그 + 방향키 + Undo + 저장) 목표가 "있으면 그 구조를 재사용"
- effect: 열린 키보드 회전이 있을 때 이동을 시작하면 회전을 먼저 확정한다(`MoveDragStarting`) — Undo 단위가 섞이지 않게

## 4. 키보드 폴백 키
- options: Alt+←/→(Key.System 함정) / Ctrl 조합(지도 회전 · 틸트가 점유) / `]` · `[`
- chosen: `]` = +1°(시계 방향) · `[` = −1°, Shift 를 함께 누르면 ±0.1°, Enter 확정 · Esc 원위치. 열린 회전 중 다른 키(조합키 단독 제외) · 선택 바꾸기 = 먼저 확정
- why: Ctrl+방향키는 지도 회전/틸트, 방향키는 묶음 이동이 쓰고 있고 `]`/`[` 는 비어 있다. 드래그의 Shift(15° 굵게)와 키보드 Shift(0.1° 가늘게)는 방향이 반대지만 "가늘게 맞추기" 가 키보드의 주 용도라 이렇게 둠
- effect: `MapViewModel.TryRouteGroupRotationKey` 와 `GroupRotationMath.KeyStepDegrees/FineKeyStepDegrees`. 각도 입력칸은 만들지 않음(목표가 "입력칸 또는 단축키")

## 5. Ctrl+A 대상
- options: 지도 위 편집 가능한 것 전부(이미지 포함) / 러버밴드와 같은 규칙(보이는 것 · 잠금 제외 · 오버레이 이미지 제외)
- chosen: 러버밴드와 같은 규칙 — 레이어가 켜져 있고 지금 줌에서 보이는 것, 잠금 · 오버레이 이미지 제외. 이미지는 Ctrl+클릭으로 더할 수 있다
- why: 사용자 목적이 "바뀐 오버레이 이미지에 마커들을 맞추기" — 이미지까지 같이 돌면 맞출 기준이 사라진다. 기존 Ctrl+A(AdornerManager 단일 경로, MultiSelectEnabled=false 로 사실상 꺼짐)는 그룹 선택에 이어지지 않아 VM 창 단위 키 처리기에서 그룹 선택으로 받았다
- effect: `MapViewModel.SelectAllForGroup` 의 Where 조건. 이미지를 넣으려면 `m is not GMapImageMarker` 를 빼고 키를 (true, Id) 로

## 6. 손잡이를 끄는 중 캡처를 잃었을 때
- options: 그때까지 돌린 것을 확정(기존 그룹 이동 방식) / 취소 · 원위치
- chosen: 취소 · 원위치(저장 없음). 놓음(MouseLeftButtonUp) = 확정, 데드존(8 DIU) 안에서 놓음 = 클릭(아무 일 없음)
- why: drag-first-ux 의 "LostMouseCapture = 취소 규정", 창 전환 같은 의도치 않은 끊김이 저장으로 이어지지 않게
- effect: `GroupSelectionAdorner.OnLostMouseCapture` 의 `FinishRotationDrag(commit: false)`

## 7. 손잡이 · 안내 모양
- options: 단일 편집처럼 초록 채운 원 / 새 어휘
- chosen: 묶음 위쪽 가운데(단일 회전점과 같은 거리)에 속 빈 원(Thumb, PrimaryBrush 테두리 · SurfaceBrush 속, 동적 리소스) + 점선 줄기(단일 편집 회전 연결선과 같은 Dot). 돌리는 중에는 중심 "+" 십자 · 중심→손잡이 점선 · 회전량 글(+12.0°) 칩
- why: 형태로 구분(속 빈 원 ≠ 단일의 채운 원), `{4,3}` · `{5,3}` 점선은 이미 배정된 어휘라 쓰지 않음, 토큰은 매번 재해석
- effect: `GroupSelectionAdorner.CreateRotateHandle` · `RenderRotationGuide`
