# PLAN — 카메라 팝업 PTZ: 끌기 → 화살표(놓는 순간 한 번)

## 목표
영상 위 프레스 → 시작점부터 현재 마우스까지 반투명 화살표 → 놓는 순간 방향 · 길이(화면 비율)로 PTZ 상대 이동 1건.
누르는 동안 명령 0건 · 데드존(8 DIU) 클릭 0건 · Esc/포커스 잃음/창 닫힘 취소 0건.

## 조사 결과(현 상태)
- 기존(7ba25e42)도 이미 "떼는 순간 한 번"이었다: `PtzDragGesture`(Press/Move/Finish, 데드존 8, 단일 Finish) ·
  `TileView`(호스트 타일) · `CameraStreamPopupControl`(GIS 지도 팝업)이 같은 판정을 쓴다. 누르는 동안 명령 없음 · Esc · 캡처 잃음 · 창 비활성 취소도 이미 있다.
- 바뀌는 것은 두 가지:
  1. **그림**: 끈 선 + 과녁(떼면 화면 중심에 올 점) → 시작점→현재점 **화살표**(몸통 + 화살촉, 어두운 테두리 위 밝은 선, 전체 반투명).
  2. **길이 기준**: 옛 기준 m = 끈 길이 ÷ (가로/2), 길이 1 에서 자름(가로 1/2 = 한 폭) →
     새 기준 = 가로 성분 ÷ 폭, 세로 성분 ÷ 높이(각각 ±1 에서 자름). 화면 끝까지(폭 전체) = 화면 한 폭(수평 화각 1개),
     세로 높이 전체 = 수직 화각 1개.

## 결정
- 길이 기준 변경은 `PtzDragGesture.WidthVector` 한 곳에서 한다(모든 방식 — 화각 상대 `FovSteps` · 일반 상대 · 절대 · 시간 제한 연속 — 이 이걸 거친다).
  새 식: (x, y) = (clamp(viewX), clamp(viewY) ÷ 가로세로비) — 가로 폭 단위. 세로를 폭 단위로 바꾸면 `FovSteps` 의 틸트 = −2·viewY(높이 전체 = 2 = 수직 화각 1개)가 되어
  "세로 성분은 높이" 와 같은 말이고, 각도/픽셀도 가로와 같다(광학적으로 동일). 공개 형식 · 서명은 그대로(더하기만 규칙).
- 프로토콜(PtzDragVector · DragMoveAsync 의 viewX/viewY/aspect)은 그대로 = 영상 상자 비율. 호스트 · 제공자 서명 변경 없음.
- 화살촉 도형은 기존 x:Name `DragTarget` 을 재사용한다(x:Name 추가/삭제 금지). 화살 각도 · 몸통 끝 계산은 순수 함수
  `PtzDragGesture.Arrow(...)` 로 더해 헤드리스로 시험한다.
- 끈 크기 글자: "0.5화면"(가로 · 세로 성분 중 큰 쪽 기준 아님 — 화면 비율 벡터 길이). 최대 표시는 없앤다(대각선은 1 을 넘을 수 있음).
  → 단순화: AmountText(m) = m 을 소수 1자리 + "화면".
- `TargetPoint` 는 쓰는 곳이 없어지지만 공개 API 라 남긴다(새 기준으로 계산 — 시험도 새 기준).
- GIS 지도 팝업도 같은 화살표로 바꾼다(코드로 만드는 도형 — 영상 표면은 테마와 무관한 흑/백 고정 기존 방식 유지 + 반투명).

## 기존 시험 기대 변경(약화 아님 — 근거)
- 기준이 "가로 1/2 = 한 폭" → "가로 전체 = 한 폭"으로 사용자가 바꿨다(2026-10-16). 같은 끌기의 이동량이 정확히 절반이 된다.
  값 단언은 새 기준으로 다시 계산해 **같은 정밀도로** 유지한다(예: viewX 0.5 → 화각 0.5개, viewX 1 → 1개).
- 대각선 자르기(길이 1)는 성분별 ±1 자르기로 바뀐다.

## 호스트 실창 시험(다른 저장소 — 여기서 안 고침)
`Dotnet.Monitoring.Solution.UiTests` CameraPopupE2ETests.DragPtz.cs 에서 고칠 것:
- 이동량 기대: 끈 길이 ÷ 영상 폭(가로 1/2 아님). 예: 영상 폭의 1/4 끌기 → 화각 0.25개(옛 0.5개). 화각 상대 공간이면 RelativeMove x = 0.5(옛 1.0).
- 폭 전체를 끌면 RelativeMove 2건 대신 x = 1 씩 2번(= 2 = 한 폭) — 옛 "가로 1/2 = 한 폭" 시나리오는 끌기 거리를 2배로.
- 끄는 동안 PTZ 명령 0건 · 놓으면 1건(현 단언 유지).
- 표시 단언: 과녁(`DragTarget` 원+십자) → 화살촉(같은 AutomationId 없음, 형태만) · 글자 "N폭"/"1폭 · 최대" → "N화면"(예 "0.5화면").
- `tools/dummy-cameras/tests/DragPtzHostTests.cs`(이 저장소, 솔루션 밖 · 실카메라 농장)는 viewX 를 직접 넣으므로 수치 기대만 위와 같이 바뀐다 — 솔루션 시험 대상 아님.

## 추가 결정
- GIS 지도 팝업 화살표 색은 그 팝업의 영상 위 배지와 같은 고정값(#AA000000 테두리 + 흰 선)을 그대로 쓴다 — 영상 표면은 테마와 무관하고
  같은 팝업의 배지 · '이동 중' 칩이 이미 이 값이다(라이트/다크 모두 영상 위에서 보임). 호스트 타일은 기존 토큰
  `CameraPopup.VideoChipBrush`/`VideoTextBrush` 를 DynamicResource 로(매번 재해석).
- 반투명 = 겹(Canvas) 전체 Opacity 0.8(겹친 몸통 2겹이 서로 비치지 않게 묶음 단위).
- 포커스 잃음 취소: 타일 · GIS 둘 다 `IsKeyboardFocusWithinChanged(false)` 에서 `FinishDrag(commit:false)` 를 더했다(기존엔 키 누름만 정리).
- 시간 제한 연속(ContinuousPulse)은 대각선 크기가 1 을 넘을 수 있어 시간을 PulseMaxMs 에서 자른다.

## 빨강 → 초록 증거
- 빨강(구현 전): `dotnet test …CameraPopup.Host.Tests --filter TilePtzInteractionTests` → 실패 2 / 통과 28
  (`should_draw_arrow_…`: "화살표는 반투명이어야 한다", `should_show_arrow_amount_…`: 기대 "0.5화면" 실제 "1폭 · 최대").
  CameraPopup.Tests 는 `PtzDragGesture.Arrow` 가 없어 컴파일 실패(빨강).
- 초록: 아래 진행 참조.

## 진행
- [x] PtzDragGesture: WidthVector 새 기준 · DragMagnitude · AmountText("N화면") · Arrow 순수 함수 + PtzArrow 형식(추가)
- [x] PtzDragMath 주석 갱신 · ContinuousPulse 시간 자르기
- [x] TileView.xaml/.cs: 화살표(x:Name 그대로 — DragTarget 을 화살촉으로) · 포커스 잃음 취소
- [x] CameraStreamPopupControl: 화살표 · 포커스 잃음 취소
- [x] 시험 갱신 · 추가: PTZ-DRAG-101~115(수학 · 화살표), 146(타일 형태 · 놓기 전 0건 · 놓으면 1건), 147(GIS 형태 · 0건/1건)
- [ ] dotnet test(CameraPopup.Host.Tests · CameraPopup.Tests(RealCamera 제외) · GMaps.Ui 제스처 시험 · 솔루션)
